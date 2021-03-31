using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MultiThreadedServer
{
    class ClientHandler
    {
        #region Fields
        private TcpClient clientSocket;
        private NetworkStream networkStream;
        private DataBaseManipulation db;
        private Client clientInfo;
        #endregion

        #region Methods
        public void StartClient(TcpClient inClientSocket)
        {
            clientSocket = inClientSocket;

            db = new DataBaseManipulation();
            db.Conncet();

            Thread thread = new Thread(Run);
            thread.Start();
        }
        private void Run()
        {
            byte[] bytes = new byte[255];

            // Получение подключения
            try
            {
                networkStream = clientSocket.GetStream();
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Cannot connect to client.");
                CloseClient();
                return;
            }

            string type;
            bool _break = false;

            while (!_break)
            {
                type = GetData(true);

                if (type == "{EXCEPTION}")
                    break;
                try
                {
                    switch (type)
                    {
                        case "[LOG]": _break = !Login(); break;
                        
                        default:
                            CWLColor("---", ConsoleColor.Red);
                            break;
                    }
                }
                catch
                {
                    break;
                }
            }
            Console.WriteLine("Client(" + clientInfo.Username + ") has been disconnected.", ConsoleColor.Blue);
            CloseClient();
        }
        private void CloseClient()
        {
            db?.Disconnect();
            networkStream?.Close();
            clientSocket?.Close();

            networkStream = null;
            clientSocket = null;
            db = null;
        }
        #endregion

        #region Query Handlers

        private bool Login()
        {
            SqlResultTable table;
            string type;
            string username;
            string key;
            string salt;
            string hash;

            type = "[LOG]";
            username = GetData(false);

            Console.WriteLine($"[Server-Query] {type}: Login-{username}");

            table = db.ExecuteQuery($"SELECT Salt FROM Users WHERE Login='{username}'");

            // если пользователь имеется в БД, вытянуть соль
            if (table.HasRows)
            {
                salt = table[0][0];

                SendData("{101}" + salt);
                hash = GetData(false);

                table = db.ExecuteQuery($"SELECT ID FROM Users WHERE Password='{hash}'");
                // пароль верный
                if (table.HasRows)
                {
                    // Сгенерировать уникальный ключ 
                    do
                    {
                        key = new Random().Next(100000000, 1000000000).ToString();

                        // Не занят ли ключ
                        table = db.ExecuteQuery($"SELECT ID FROM Users WHERE key='{key}'");
                        if (!table?.HasRows ?? true)
                        {
                            break;
                        }
                    } while (true);

                    // Обновить ключ
                    db.ExecuteQuery($"UPDATE Users SET key='{key}' WHERE Login='{username}'");

                    SendData("{101}" + key.ToString());
                    CWLColor($"[Server-Query] {type}: Succes login -> {username}, {key}.", ConsoleColor.Cyan);

                    clientInfo = new Client()
                    {
                        Username = username,
                        Key = key,
                        Salt = salt,
                        HashPassword = hash,
                    };
                    return true;
                }
                // пароль не верный {301}
                else
                {
                    SendData("{301}");
                    CWLColor($"[Server-Query] {type}: Invalid password.", ConsoleColor.Cyan);
                }
            }
            // Пользователя нет в БД {302]
            else
            {
                CWLColor($"[Server-Query] {type}: Invalid username.", ConsoleColor.Cyan);
                SendData("{302}");
            }
            return false;
        }
        private void GetFriendsList()
        {
            SqlResultTable table;
            string type;
            string key;
            string id;
            string list = null;

            type = "[GET_FRIEND_LIST]";
            key = GetData(false);
            id = GetIdByKey(key);

            // выбрать друзей
            table = db.ExecuteQuery($"SELECT Login FROM Users " +
                                    $"JOIN Friends " +
                                    $"ON (Users.ID = Friends.FirstUserID AND Friends.SecondUserID='{id}') " +
                                    $"OR (Users.ID = Friends.SecondUserID AND Friends.FirstUserID='{id}')");
            // если добавленные друзья есть {101}
            if (table.HasRows)
            {
                for (int i = 0; i < table.CountRows; i++)
                {
                    list += table[i][0].ToString() + "\n";
                }

                SendData("{101}" + list);
                CWLColor($"[Server-Query] {type}: Founded {table.CountRows} friends. User {id}({key})", ConsoleColor.Cyan);
            }
            // если добавленных друзей нет {100}
            else
            {
                SendData("{100}");
                CWLColor($"[Server-Query] {type}: User {id}({key}) do not have any friends.", ConsoleColor.Cyan);
            }
        }

        #endregion

        #region Other Methods
        private string GetData(bool send)
        {
            try
            {
                byte[] buff = new byte[255];
                networkStream.Read(buff, 0, buff.Length);

                string data = GetString(buff);

                while (networkStream.DataAvailable)
                {
                    buff = new byte[255];
                    networkStream.Read(buff, 0, buff.Length);
                    data += GetString(buff);
                }

                if (send)
                    SendData("{SYS}");

                return data;
            }
            catch (ArgumentNullException e)
            {
               // Console.WriteLine(e.Message);
            }
            catch (System.IO.IOException e)
            {
               // Console.WriteLine(e.Message);
            }
            catch (ObjectDisposedException e)
            {
               // Console.WriteLine(e.Message);
            }
            catch (ArgumentOutOfRangeException e)
            {
               // Console.WriteLine(e.Message);
            }
            catch { }
            return "{EXCEPTION}";
        }
        private void SendData(string data)
        {
            try
            {
                networkStream.Write(Encoding.UTF8.GetBytes(data), 0, Encoding.UTF8.GetBytes(data).Length);
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Console.WriteLine(e.Message);
            }
            catch { }
        }
        private string GetString(byte[] b)
        {
            if (b == null)
                throw new NullReferenceException("Ссылка на пустой массив байтов в Server.GetString() : byte[] b == null");

            string s = Encoding.UTF8.GetString(b);
            string t = "";
            for (int i = 0; i < s.Length && s[i] != '\0'; i++)
            {
                t += s[i];
            }
            return t;
        }
        private void CWLColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }
        private string GetIdByKey(string key)
        {
            SqlResultTable table = db.ExecuteQuery($"SELECT id FROM Users WHERE key='{key}'");
            if (table.HasRows)
            {
                return table[0][0].ToString(); ;
            }
            else
            {
                return null;
            }
        }
        #endregion
    }
}
