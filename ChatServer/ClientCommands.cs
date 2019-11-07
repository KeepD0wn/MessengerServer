using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Threading;
using System.Net.Sockets;

namespace ChatServer
{
    class ClientCommands
    {
        public event Mes MsgSendEvent;
        
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

            if (MsgSendEvent != null)
            {
                MsgSendEvent(name,message);
            }
        }

        public void OnMessageSended(string name, string message)
        {
            foreach (ClientClass c in Program.clients)
            {
                string messageToClient = $"4:&#:{name}:&#:{message}";
                byte[] buffer = Encoding.UTF8.GetBytes(messageToClient);
                c.Stream.Write(buffer, 0, buffer.Length);
            }
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

                string message = $"0:&#:confirmed";
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);

                Console.WriteLine("{" + name + "}" + " was added with password: " + password);
            }
            catch (System.Data.SqlClient.SqlException)
            {
                string message = $"0:&#:unconfirmed";
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);
            }            
        }

        public string ConfirmUserData(SqlConnection connect, NetworkStream stream, string name, string password)
        {
            SqlCommand sql = new SqlCommand("select * from MessengerUsers where UserName = @login", connect);
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
                        MsgSendEvent += OnMessageSended;
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
            return null;
        }

        public void SendAllMessages(SqlConnection connect, NetworkStream stream)
        {
            string qu = "select * from MessengerMessege";
            Program.data.Clear();
            using (SqlCommand com = new SqlCommand(qu, connect))
            {
                SqlDataReader reader = com.ExecuteReader();
                while (reader.Read())
                {
                    Program.data.Add(new string[2]); //читаем БД и заносим все сообщения в лист
                    Program.data[Program.data.Count - 1][0] = reader[1].ToString();
                    Program.data[Program.data.Count - 1][1] = reader[2].ToString();
                }
                reader.Close();
            }

            string message = $"{Program.data.Count}"; //отвечаем сколько сообщений надо прочитать
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            stream.Write(buffer, 0, buffer.Length);

            for (int i = 0; i < Program.data.Count; i++)
            {
                Thread.Sleep(5); //если не усыплять, то клиент не будет успевать обрабатывать 
                string message2 = $"3:{Program.data[i][0]}:{Program.data[i][1]}";
                byte[] buffer2 = Encoding.UTF8.GetBytes(message2);
                stream.Write(buffer2, 0, buffer2.Length);
            }
        }
    }
}
