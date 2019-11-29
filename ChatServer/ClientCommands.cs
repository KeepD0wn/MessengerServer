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
    class ClientCommands
    {
        public event Mes MsgSendEvent;
        public event VoiceMes VoiceMsgSendEvent;

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

        public void SendMessageToClients(string name, string message) //событие
        {
            foreach (ClientClass c in Program.clients)
            {
                string messageToClient = $"4:&#:{name}:&#:{message}";
                byte[] buffer = Encoding.UTF8.GetBytes(messageToClient);
                c.Stream.Write(buffer, 0, buffer.Length);
            }
        }

        public void SendVoiceMessageToClients() //событие
        {
            foreach (ClientClass c in Program.clients)
            {
                string messageToClient = $"5:&#:";
                byte[] buffer = Encoding.UTF8.GetBytes(messageToClient);
                c.Stream.Write(buffer, 0, buffer.Length);

                int bufferSize = 1024;

                byte[] file = File.ReadAllBytes($"C:\\Users\\{Environment.UserName}\\Desktop\\ClientSoundMes.wav");
                byte[] fileLength = BitConverter.GetBytes(file.Length); //4 байта
                byte[] package = new byte[4 + file.Length];
                fileLength.CopyTo(package, 0);
                file.CopyTo(package, 4); //начиная с 5 байта пишем файл

                int bytesSent = 0; //отталкиваемся с какого байта отправлять
                int bytesLeft = package.Length; //смотрим сколько осталось

                while (bytesLeft > 0)
                {

                    int packetSize = (bytesLeft > bufferSize) ? bufferSize : bytesLeft; //если больше отправляем 1024, если меньше то остаток

                    c.StreamVoice.Write(package, bytesSent, packetSize);
                    bytesSent += packetSize;
                    bytesLeft -= packetSize;
                }
            }
        }

        public  void AddVoiceMessage(object st) //поток
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
                    MsgSendEvent?.Invoke(ClientClass.Login, "отправил голосовое сообщение!");
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
                string message = $"0:&#:confirmed";
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);

                Console.WriteLine("{" + name + "}" + " was added with password: " + password);
            }
            catch
            {
                string message = $"0:&#:unconfirmed";
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);
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
                            string message = $"2:&#:confirmed:&#:{viewed}:&#:{reader[0].ToString()}:&#:{reader[1].ToString()}:&#:{reader[2].ToString()}";
                            byte[] buffer = Encoding.UTF8.GetBytes(message);
                            stream.Write(buffer, 0, buffer.Length);
                            Console.WriteLine("{" + name + "}" + " just entred with password: " + password);

                            MsgSendEvent += SendMessageToClients;
                            VoiceMsgSendEvent += SendVoiceMessageToClients;
                            return name;
                        }
                        else
                        {
                            string message = $"2:&#:unconfirmed:&#:{viewed}";
                            byte[] buffer = Encoding.UTF8.GetBytes(message);
                            stream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    if (viewed == 0)
                    {
                        string message = $"2:&#:unconfirmed:&#:{viewed}";
                        byte[] buffer = Encoding.UTF8.GetBytes(message);
                        stream.Write(buffer, 0, buffer.Length);
                    }
                }
            }
            return null;
        }

        public void SendAllMessages(SqlConnection connect, NetworkStream stream)
        {
            Program.data.Clear();
            string qu = "select * from MessengerMessege"; //читаем БД и заносим все сообщения в лист
            using (SqlCommand com = new SqlCommand(qu, connect))
            {
                SqlDataReader reader = com.ExecuteReader();
                while (reader.Read())
                {
                    Program.data.Add(new string[2]); 
                    Program.data[Program.data.Count - 1][0] = reader[1].ToString(); //ник
                    Program.data[Program.data.Count - 1][1] = reader[2].ToString(); //сообщение
                }
                reader.Close();
            }

            XmlSerializer ser = new XmlSerializer(typeof(List<string[]>)); //сериализуем лист с сообщениями
            string path = $"C:\\Users\\{Environment.UserName}\\Desktop\\txtM.txt";
            FileStream fileM = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            ser.Serialize(fileM, Program.data);
            fileM.Close();
                        
            byte[] file = File.ReadAllBytes(path); 
            byte[] fileLength = BitConverter.GetBytes(file.Length); //4 байта
            byte[] package = new byte[4 + file.Length];
            fileLength.CopyTo(package, 0); //сначаал пишем длину до 4 байта
            file.CopyTo(package, 4); //начиная с 4 байта пишем файл

            int bufferSize = 1024;
            int bytesSent = 0; //отталкиваемся с какого байта отправлять
            int bytesLeft = package.Length; //смотрим сколько осталось

            while (bytesLeft > 0)
            {

                int packetSize = (bytesLeft > bufferSize) ? bufferSize : bytesLeft; //если больше отправляем 1024, если меньше то остаток

                stream.Write(package, bytesSent, packetSize);
                bytesSent += packetSize;
                bytesLeft -= packetSize;
            }
        }
    }
}
