using System;
using System.Text.Json;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using LibData;
using Microsoft.Extensions.Configuration;

namespace LibServerSolution
{
    public struct Setting
    {
        public int ServerPortNumber { get; set; }
        public string ServerIPAddress { get; set; }
        public int BookHelperPortNumber { get; set; }
        public string BookHelperIPAddress { get; set; }
        public int ServerListeningQueue { get; set; }
    }


    abstract class AbsSequentialServer
    {
        protected Setting settings;

        /// <summary>
        /// Report method can be used to print message to console in standaard formaat. 
        /// It is not mandatory to use it, but highly recommended.
        /// </summary>
        /// <param name="type">For example: [Exception], [Error], [Info] etc</param>
        /// <param name="msg"> In case of [Exception] the message of the exection can be passed. Same is valud for other types</param>

        protected void report(string type, string msg)
        {
            // Console.Clear();
            Console.Out.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>");
            if (!String.IsNullOrEmpty(msg))
            {
                msg = msg.Replace(@"\u0022", " ");
            }

            Console.Out.WriteLine("[Server] {0} : {1}", type, msg);
        }

        /// <summary>
        /// This methid loads required settings.
        /// </summary>
        protected void GetConfigurationValue()
        {
            settings = new Setting();
            try
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                IConfiguration Config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetFullPath(Path.Combine(path, @"../../../../")))
                    .AddJsonFile("appsettings.json")
                    .Build();

                settings.ServerIPAddress = Config.GetSection("ServerIPAddress").Value;
                settings.ServerPortNumber = Int32.Parse(Config.GetSection("ServerPortNumber").Value);
                settings.BookHelperIPAddress = Config.GetSection("BookHelperIPAddress").Value;
                settings.BookHelperPortNumber = Int32.Parse(Config.GetSection("BookHelperPortNumber").Value);
                settings.ServerListeningQueue = Int32.Parse(Config.GetSection("ServerListeningQueue").Value);
                // Console.WriteLine( settings.ServerIPAddress, settings.ServerPortNumber );
            }
            catch (Exception e) { report("[Exception]", e.Message); }
        }

       
        protected abstract void createSocketAndConnectHelpers();

        public abstract void handelListening();

        protected abstract Message processMessage(Message message);
    
        protected abstract Message requestDataFromHelpers(string msg);


    }

    class SequentialServer : AbsSequentialServer
    {
        // check all the required parameters for the server. How are they initialized? 
        Socket serverSocket;
        IPEndPoint listeningPoint;
        Socket bookHelperSocket;

        Socket notAcceptedserverSocket;

        public SequentialServer() : base()
        {
            GetConfigurationValue();
        }
        
        /// <summary>
        /// Connect socket settings and connec
        /// </summary>
        protected override void createSocketAndConnectHelpers()
        {
            // todo: To meet the assignment requirement, finish the implementation of this method.
            // Extra Note: If failed to connect to helper. Server should retry 3 times.
            // After the 3d attempt the server starts anyway and listen to incoming messages to clients
           
            try
            {
                IPAddress iPAddress = IPAddress.Parse(settings.ServerIPAddress);
                listeningPoint = new IPEndPoint(iPAddress, settings.ServerPortNumber);
                notAcceptedserverSocket = new Socket(AddressFamily.InterNetwork,
                                    SocketType.Stream, ProtocolType.Tcp);
                notAcceptedserverSocket.Bind(listeningPoint);
                notAcceptedserverSocket.Listen(settings.ServerListeningQueue);

                IPAddress ipAddress = IPAddress.Parse(settings.BookHelperIPAddress);
                IPEndPoint serverEndPoint = new IPEndPoint(ipAddress, settings.BookHelperPortNumber);
                bookHelperSocket = new Socket(AddressFamily.InterNetwork,
                                     SocketType.Stream, ProtocolType.Tcp);
                
                try{
                bookHelperSocket.Connect(serverEndPoint);
                }
                catch
                {
                    for (int x = 1; x <= 3; x++){
                        delay();
                        try{
                        bookHelperSocket.Connect(serverEndPoint);
                        break;
                        }
                        catch{}
                    }
                    Console.WriteLine("Unable to connect to BookHelper.");
                }
                serverSocket = notAcceptedserverSocket.Accept();
            }
            catch
            {
                Console.WriteLine("Error in setting up server");
            }

        }

        /// <summary>
        /// This method starts the socketserver after initializion and listents to incoming connections. 
        /// It tries to connect to the book helpers. If it failes to connect to the helper. Server should retry 3 times. 
        /// After the 3d attempt the server starts any way. It listen to clients and waits for incoming messages from clients
        /// </summary>
        public override void handelListening()
        {
            createSocketAndConnectHelpers();
            //todo: To meet the assignment requirement, finish the implementation of this method.
            void newClient(){
                try
                {
                    byte[] buffer = new byte[1000];
                    void waitForReceive(){
                        Console.WriteLine("Waiting for messages from client...");
                        int b = serverSocket.Receive(buffer);
                        Console.WriteLine("Message received from client.");
                        string data = Encoding.ASCII.GetString(buffer, 0, b);
                        Message ClientRecieved = JsonSerializer.Deserialize<Message>(data);

                        string jsonString = JsonSerializer.Serialize(processMessage(ClientRecieved));
                        byte[] msg = Encoding.ASCII.GetBytes(jsonString);
                        serverSocket.Send(msg);
                        Console.WriteLine("Message send to client.");
                        waitForReceive();
                    }
                    waitForReceive();
                }

                catch {
                    serverSocket = null;
                    Console.WriteLine("Client disconnected.");
                    Console.WriteLine("Waiting for new client...\n");
                    serverSocket = notAcceptedserverSocket.Accept();
                    Console.WriteLine("New client connected.");                  
                    newClient();
                }
            }
            newClient();
        }

        /// <summary>
        /// Process the message of the client. Depending on the logic and type and content values in a message it may call 
        /// additional methods such as requestDataFromHelpers().
        /// </summary>
        /// <param name="message"></param>
        protected override Message processMessage(Message message)
        {
            Message pmReply = new Message();
            
            if (message.Type == MessageType.Hello){
                pmReply.Type = MessageType.Welcome;
                pmReply.Content = "Welcome";
            }

            else if (message.Type == MessageType.BookInquiry){
                pmReply = requestDataFromHelpers(message.Content);
            }


            return pmReply;
            
        }

        /// <summary>
        /// When data is processed by the server, it may decide to send a message to a book helper to request more data. 
        /// </summary>
        /// <param name="content">Content may contain a different values depending on the message type. For example "a book title"</param>
        /// <returns>Message</returns>
        protected override Message requestDataFromHelpers(string content)
        {
            Message HelperReply = new Message();
            //todo: To meet the assignment requirement, finish the implementation of this method .

            try
            {
                Message messageToHelper = new Message();
                messageToHelper.Type = MessageType.BookInquiry;
                messageToHelper.Content = content;

                string jsonString = JsonSerializer.Serialize(messageToHelper);
                byte[] msg = Encoding.ASCII.GetBytes(jsonString);
                bookHelperSocket.Send(msg);
                Console.WriteLine($"Message with content: {messageToHelper.Content} send to BookHelper.");

                byte[] buffer = new byte[1000];
                int b = bookHelperSocket.Receive(buffer);
                Console.WriteLine("BookReply received from BookHelper.");
                string data = Encoding.ASCII.GetString(buffer, 0, b);
                Message helperReceived = JsonSerializer.Deserialize<Message>(data);
                HelperReply = helperReceived;
                
            }
            catch { 
                Console.WriteLine("Can't communicate with BooksHelper server.");
            }

            return HelperReply;

        }

        public void delay()
        {
            int m = 3;
            for (int i = 0; i <= m; i++)
            {
                Console.Out.Write("{0} .. ", i);
                Thread.Sleep(200);
            }
            Console.WriteLine("\n");
            //report("round:","next to start");
        }

    }
}

