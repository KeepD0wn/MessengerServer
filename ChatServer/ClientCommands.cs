using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Threading;
using System.Net.Sockets;
using System.IO;
using System.Xml.Serialization;

namespace ChatServer
{
    delegate void Mes(string name, string mes);
    delegate void VoiceMes();
    public class ClientCommands
    {
        event Mes MsgSendEvent;
        event VoiceMes VoiceMsgSendEvent;
        public List<string[]> data = new List<string[]>();
        User user;
        
        public void AddMessage(SqlConnection connect, string name, string message)
        {
            string sql = string.Format("Insert into MessengerMessege (MessegeUserName,MessegeText,MessegeDate) values (@login , @txt , GETDATE());");
            using (SqlCommand cmd = new SqlCommand(sql, connect))
            {
                cmd.Parameters.AddWithValue("@login", name);
                cmd.Parameters.AddWithValue("@txt", message);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine("{"+name+"}"+" send message: "+message);

            MsgSendEvent?.Invoke(name, message);
        }

        private void OnSendMessageToClients(string name, string message) 
        {
            foreach (ClientClass c in Program.clients)
            {
                string messageToClient = $"4:&#:{name}:&#:{message}";
                byte[] buffer = Encoding.UTF8.GetBytes(messageToClient);
                c.Stream.Write(buffer, 0, buffer.Length);
            }
        }

        private void OnSendVoiceMessageToClients() 
        {
            foreach (ClientClass c in Program.clients)
            {
                SendFile($"C:\\Users\\{Environment.UserName}\\Desktop\\ClientSoundMes.wav",c.StreamVoice);
            }
            Console.WriteLine("Server sent voice messages to clients");
        }

        private void AddVoiceMessage(object st) //поток
        {
            try
            {
                while (true)
                {
                    NetworkStream stream = (NetworkStream)st;
                    int bufferSize = 1024;
                    int bytesRead = 0;
                    int allBytesRead = 0;

                    byte[] length = new byte[4];
                    bytesRead = stream.Read(length, 0, 4); //записываем размер файла, если никто не передаёт войс, то здесь остановка пока не отправят
                    int fileLength = BitConverter.ToInt32(length, 0);

                    int bytesLeft = fileLength;
                    byte[] data = new byte[fileLength];

                    while (bytesLeft > 0)
                    {

                        int PacketSize = (bytesLeft > bufferSize) ? bufferSize : bytesLeft;

                        bytesRead = stream.Read(data, allBytesRead, PacketSize);
                        allBytesRead += bytesRead;
                        bytesLeft -= bytesRead;

                    }

                    File.WriteAllBytes($"C:\\Users\\{Environment.UserName}\\Desktop\\ClientSoundMes.wav", data);

                    VoiceMsgSendEvent?.Invoke();                   
                    MsgSendEvent(user.login, "отправил голосовое сообщение!");
                }
            }
            catch
            {
            }           
        }

        public async void AddVoiceMessageAsync(object st)
        {
            await Task.Run(()=>AddVoiceMessage(st));
        }

        public void AddUser(SqlConnection connect,NetworkStream stream, string name, string password)
        {
            try
            {
                string qu = "Insert into MessengerUsers (UserName,UserPassword) values (@log,@pas);";
                using (SqlCommand com = new SqlCommand(qu, connect))
                {
                    com.Parameters.AddWithValue("@log", name);
                    com.Parameters.AddWithValue("@pas", password);
                    com.ExecuteNonQuery();
                }

                //если что-то не так, то вызывается ошибка и код ниже не работает
                Send(stream,"0", "confirmed");

                Console.WriteLine("{" + name + "}" + " was added with password: " + password);
            }
            catch
            {
                Send(stream, "0", "unconfirmed");
            }            
        }               

        public string ConfirmUserData(SqlConnection connect, NetworkStream stream, string name, string password)
        {
            using (SqlCommand sql = new SqlCommand("select * from MessengerUsers where UserName = @login", connect))
            {
                sql.Parameters.AddWithValue("@login", name);

                using (SqlDataReader reader = sql.ExecuteReader())
                {
                    int viewed = 0;   //если 0, то пользователя с таким логином нет и ридер не запустится, если 1, то есть             
                    while (reader.Read())
                    {
                        viewed += 1;
                        if (password == reader[2].ToString())
                        {
                            string id = reader[0].ToString();
                            Send(stream,"2", "confirmed",id,name,password);
                            Console.WriteLine("{" + name + "}" + " just entred with password: " + password);
                            user = new User(name,id);

                            MsgSendEvent += OnSendMessageToClients;
                            VoiceMsgSendEvent += OnSendVoiceMessageToClients;
                            return name;
                        }
                        else
                        {
                            Send(stream,"2", "unconfirmed", viewed.ToString());
                        }
                    }
                    if (viewed == 0)
                    {
                        Send(stream, "2", "unconfirmed", viewed.ToString());
                    }
                }
            }
            return null;
        }

        public void SendAllMessages(SqlConnection connect, NetworkStream stream)
        {
            data.Clear();
            string qu = "select * from MessengerMessege"; //читаем БД и заносим все сообщения в лист
            using (SqlCommand com = new SqlCommand(qu, connect))
            {
                SqlDataReader reader = com.ExecuteReader();
                while (reader.Read())
                {
                    data.Add(new string[2]); 
                    data[data.Count - 1][0] = reader[1].ToString(); //ник
                    data[data.Count - 1][1] = reader[2].ToString(); //сообщение
                }
                reader.Close();
            }

            XmlSerializer ser = new XmlSerializer(typeof(List<string[]>)); //сериализуем лист с сообщениями
            string path = $"C:\\Users\\{Environment.UserName}\\Desktop\\txtM.txt";
            FileStream fileM = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            ser.Serialize(fileM, data);
            fileM.Close();

            SendFile(path,stream);
        }

        private void Send(NetworkStream stream, params string[] data)
        {
            string message = default;
            foreach (string s in data)
            {
                message += $"{s}:&#:";
            }
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            stream.Write(buffer, 0, buffer.Length);
        }

        private void SendFile(string path, NetworkStream streamVoice)
        {
            int bufferSize = 1024;

            byte[] file = File.ReadAllBytes(path);
            byte[] fileLength = BitConverter.GetBytes(file.Length); //4 байта
            byte[] package = new byte[4 + file.Length];
            fileLength.CopyTo(package, 0);
            file.CopyTo(package, 4); //начиная с 5 байта пишем файл

            int bytesSent = 0; //отталкиваемся с какого байта отправлять
            int bytesLeft = package.Length; //смотрим сколько осталось

            while (bytesLeft > 0)
            {

                int packetSize = (bytesLeft > bufferSize) ? bufferSize : bytesLeft; //если больше отправляем 1024, если меньше то остаток

                streamVoice.Write(package, bytesSent, packetSize);
                bytesSent += packetSize;
                bytesLeft -= packetSize;
            }
        }
    }
}
