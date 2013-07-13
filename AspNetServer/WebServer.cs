using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AspNetServer
{
    class WebServer
    {
        private Socket _serverSocket;

        private SimpleHost _host;

        public int Port { get; private set; }

        public bool IsRuning { get; set; }

        public WebServer(SimpleHost host, int port)
        {
            _host = host;
            Port = port;
        }

        public void Start()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.ExclusiveAddressUse = true;
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
            _serverSocket.Listen(1000);
            IsRuning = true;

            Console.WriteLine("Serving HTTP on 0.0.0.0 port " + Port + " ...");

            new Thread(OnStart).Start();
        }

        private void OnStart(object state)
        {
            while (IsRuning)
            {
                try
                {
                    Socket socket = _serverSocket.Accept();
                    //AcceptSocket(socket);
                    ThreadPool.QueueUserWorkItem(AcceptSocket, socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Thread.Sleep(100);
                }
            }
        }

        private void AcceptSocket(object state)
        {
            if (IsRuning)
            {
                Socket socket = state as Socket;
                HttpProcessor processor = new HttpProcessor(_host, socket);
                processor.ProcessRequest();
            }
        }

        public void Stop()
        {
            IsRuning = false;
            if (_serverSocket != null)
                _serverSocket.Close();
        }
    }
}
