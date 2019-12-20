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
        public static List<ClientClass> clients = new List<ClientClass>();
        static SqlConnection connect = new SqlConnection("Server=31.31.196.89; Database=u0805163_2iq; User Id=u0805163_user1; Password=!123qwe; MultipleActiveResultSets=True;");

        static void Main(string[] args)
        {
            connect.Open();
            const int port = 12000;
            const int portVoice = 12001;
            TcpListener server = null;
            TcpListener serverVoice = null;

            try
            {
                server = new TcpListener(IPAddress.Any, port);
                serverVoice = new TcpListener(IPAddress.Any,portVoice);       
                
                server.Start();
                serverVoice.Start();

                Console.WriteLine("Server started");

                while (true)
                {
                    CreateAndStartClient(server, serverVoice);
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

        private static void CreateAndStartClient(TcpListener server, TcpListener serverVoice)
        {
            TcpClient client = server.AcceptTcpClient(); //ждём клиента
            TcpClient clientVoice = serverVoice.AcceptTcpClient();
            ClientClass newClient = CreateClient(client, clientVoice);

            Thread clientThread = new Thread(new ThreadStart(newClient.Connect));
            clientThread.Start();
        }

        private static ClientClass CreateClient(TcpClient client, TcpClient clientVoice)
        {
            ClientClass newClient = new ClientClass(client, clientVoice, connect);
            clients.Add(newClient);
            Console.WriteLine("New client");
            return newClient;
        }
    }
}