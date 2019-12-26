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
    struct User
    {
        public string id;
        public string login;
        public User(string login, string id)
        {
            this.login = login;
            this.id = id;
        }
    }

    public class ClientClass
    {
        public TcpClient client;
        public TcpClient clientVoice;
        ClientCommands commands = new ClientCommands();

        public string Login { get; set; }
        public SqlConnection ConnectSQLProperty { get; set; }
        public NetworkStream Stream { get ; set; }
        public NetworkStream StreamVoice{ get ; set ; }        

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
                commands.AddVoiceMessageAsync(StreamVoice);              

                while (true)
                {
                    string[] words = GetClientAnswer();
                    CompareData(words);
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

        private void CompareData(string[] words)
        {
            switch (words[0])
            {
                case "0":
                    commands.AddUser(ConnectSQLProperty, Stream, words[1], words[2]);
                    break;
                case "1":
                    commands.AddMessage(ConnectSQLProperty, words[1], words[2]);
                    break;
                case "2":
                    Login = commands.ConfirmUserData(ConnectSQLProperty, Stream, words[1], words[2]);
                    break;
                case "3":
                    commands.SendAllMessages(ConnectSQLProperty, Stream);
                    break;
            }
        }

        private string[] GetClientAnswer()
        {
            byte[] IncomingMessage = GetByteClientAnswer();
            return DecodeClientAnswer(IncomingMessage);
        }

        private static string[] DecodeClientAnswer(byte[] IncomingMessage)
        {
            string msgWrite = Encoding.UTF8.GetString(IncomingMessage).TrimEnd('\0');
            string[] words = msgWrite.Split(new char[] { ':', '&', '#', ':' }, StringSplitOptions.RemoveEmptyEntries); //разделяем пришедшую команду
            Console.WriteLine(msgWrite);
            return words;
        }

        private byte[] GetByteClientAnswer()
        {
            byte[] IncomingMessage = new byte[256];
            do
            {
                int bytes = Stream.Read(IncomingMessage, 0, IncomingMessage.Length); //ждём команды 
            }
            while (Stream.DataAvailable); // пока данные есть в потоке
            return IncomingMessage;
        }

        private void Disconnect()
        {
            if (client != null)
                client.Close();
            if (Stream != null)
                Stream.Close();
            if (clientVoice != null)
                clientVoice.Close();
            if (StreamVoice != null)
                StreamVoice.Close();            
        }
    }
}
