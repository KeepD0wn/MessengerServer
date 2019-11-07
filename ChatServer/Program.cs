using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using System.Reflection;

namespace ChatServer
{
    class Program
    {                
        public static List<string[]> data = new List<string[]>();
        public static List<ClientClass> clients = new List<ClientClass>();

        static void Main(string[] args)
        {
            const int port = 12000;
            TcpListener server = null;

            try
            {
                server = new TcpListener(IPAddress.Any, port);
                server.Start();
                Console.WriteLine("Server started");

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient(); //ждём клиента
                    ClientClass newClient = new ClientClass(client);
                    clients.Add(newClient);
                    Console.WriteLine("New client");                     
                    Thread clientThread = new Thread(new ThreadStart(newClient.Connect));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (server != null)
                    server.Stop();
            }
            
        }
    }
}