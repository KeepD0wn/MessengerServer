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
    delegate void VoiceMes();
    class ClientClass:ClientCommands
    {
        public TcpClient client;
        public TcpClient clientVoice;
                
        NetworkStream streamVoice = null;
        NetworkStream stream = null;

        public static string Login { get; set; }
        public SqlConnection ConnectSQLProperty { get; set; }
        public NetworkStream Stream { get => stream; set => stream = value; }
        public NetworkStream StreamVoice{ get => streamVoice; set => streamVoice = value; }        

        public ClientClass(TcpClient client,TcpClient clientVoice,SqlConnection connect)
        {
            this.client = client;
            this.clientVoice = clientVoice;
            ConnectSQLProperty = connect;
        }

        public void Connect()
        {
            try
            {               
                Stream = client.GetStream();
                StreamVoice = clientVoice.GetStream();
                AddVoiceMessageAsync(StreamVoice);              

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
                    Console.WriteLine(msgWrite);

                    switch (words[0])
                    {
                        case "0":
                            AddUser(ConnectSQLProperty,Stream, words[1], words[2]);
                            break;
                        case "1":
                            AddMessage(ConnectSQLProperty, words[1], words[2]);
                            break;
                        case "2":
                           Login= ConfirmUserData(ConnectSQLProperty, Stream, words[1], words[2]);                            
                            break;
                        case "3":
                            SendAllMessages(ConnectSQLProperty, Stream);
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
            if (clientVoice != null)
                clientVoice.Close();
            if (streamVoice != null)
                streamVoice.Close();            
        }
    }
}
