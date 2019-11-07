using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Data.SqlClient;
using System.IO;

namespace ChatServer
{
    delegate void Mes(string name,string mes);
    class ClientClass:ClientCommands
    {
        public TcpClient client;
        SqlConnection connect = null;
        NetworkStream stream = null;
        public string Login { get; set; }
        public SqlConnection ConnectProperty { get => connect; set => connect = value; }
        public NetworkStream Stream { get => stream; set => stream = value; }        

        public ClientClass(TcpClient client)
        {
            this.client = client;
        }

        public void Connect()
        {
            try
            {
                Stream = client.GetStream();
                ConnectProperty = new SqlConnection("Server=31.31.196.89; Database=u0805163_2iq; User Id=u0805163_user1; Password=!123qwe; MultipleActiveResultSets=True;");
                ConnectProperty.Open();

                while (true)
                {
                    byte[] IncomingMessage = new byte[256];
                    do
                    {
                        int bytes = Stream.Read(IncomingMessage, 0, IncomingMessage.Length); //ждём команды 
                    }
                    while (Stream.DataAvailable); // пока данные есть в потоке

                    string msgWrite = Encoding.UTF8.GetString(IncomingMessage).TrimEnd('\0');
                    string[] words = msgWrite.Split(new char[] { ':', '&','#',':' }, StringSplitOptions.RemoveEmptyEntries); //разделяем пришедшую команду

                    switch (words[0])
                    {
                        case "0":
                            AddUser(ConnectProperty,Stream, words[1], words[2]);
                            break;
                        case "1":
                            AddMessage(ConnectProperty, words[1], words[2]);
                            break;
                        case "2":
                           Login= ConfirmUserData(ConnectProperty, Stream, words[1], words[2]);
                            break;
                        case "3":
                            SendAllMessages(ConnectProperty, Stream);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{"+Login+"}" + " " + ex.Message);
            }
            finally
            {
                Disconnect();                
                Program.clients.Remove(this);
            }
        }

        public void Disconnect()
        {
            if (client != null)
                client.Close();
            if (Stream != null)
                Stream.Close();
        }
    }
}
