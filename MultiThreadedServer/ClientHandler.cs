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
                        case "[LOG]":               _break = !Login(); break;
                        case "[GET_FRIEND_LIST]":   GetFriendsList(); break;
                        case "[REG]":               Register(); break;
                        case "[SEND]":              Send(); break;
                        case "[GET_DIALOG]":        GetDialog();  break;
                        case "[GET_DIALOG_LIST]":   GetDialogList();  break;

                        default:
                            CWLColor("---", ConsoleColor.Red);
                            break;
                    }
                }
                catch (Exception e)
                {
                    CWLColor(e.Message, ConsoleColor.Red);
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
        private bool Register()
        {
            SqlResultTable table;
            string type;
            string username;
            string salt;
            string hash;
            string email;

            type = "[REG]";

            username = GetData(true);
            hash = GetData(true);
            salt = GetData(true);
            email = GetData(false);

            Console.WriteLine($"[Server-Query] {type}: Login-{username}");
            Console.WriteLine($"[Server-Query] {type}: Email-{email}");

            table = db.ExecuteQuery($"SELECT Login FROM Users WHERE lower(Login)='{username.ToLower()}'");
            
            // Если пользователь с данным логином существует {304}
            if (table.HasRows)
            {
                SendData("{304}");
                CWLColor($"[Server-Query] {type}: This Login is already taken!", ConsoleColor.Cyan);
            }
            // Если логин свободен
            else
            {
                table = db.ExecuteQuery($"SELECT Email FROM Users WHERE lower(Email)='{email.ToLower()}'");
                // Если почта занята {305}
                if (table.HasRows)
                {
                    SendData("{305}");
                    CWLColor($"[Server-Query] {type}: This email is already taken!", ConsoleColor.Cyan);
                }
                // Если почта не занятна {100}
                else
                {
                    db.ExecuteQuery($"INSERT INTO Users(Login, Password, Salt, Email) VALUES('{username}', '{hash}', '{salt}', '{email}')");
                    SendData("{100}");
                    CWLColor($"[Server-Query] {type}: Succes registration -> {username}, {hash}::{salt}, {email}", ConsoleColor.Cyan);
                    return true;
                }
            }
            return false;
        }
        private void Send()
        {
            SqlResultTable table;
            string type;
            string username;
            string key;
            string message;
            string senderId;
            string receiverId;

            type = "[SEND]";

            key = GetData(true); // кто отправляет
            username = GetData(true); // кому отправлять
            message = GetData(false); // сообщение

            Console.WriteLine($"[Server-Query] {type}: Login-{username}({key})");
            Console.WriteLine($"[Server-Query] {type}: Sending message-{message}");

            senderId = GetIdByKey(key);
            if (senderId != null)
            {
                receiverId = GetIdByLogin(username);
                if (receiverId != null)
                {
                    // отправить сообщение {101}
                    db.ExecuteQuery($"INSERT INTO Messages(Message) VALUES('{message}')");

                    db.ExecuteQuery($"INSERT INTO Communications(SenderID, ReceiverID, MessageID) VALUES('{senderId}', '{receiverId}', (SELECT max(ID) FROM Messages))");

                    db.ExecuteQuery($"INSERT INTO DeliveringMessages(Key, CommunicationID, UserID) VALUES('{key}', (SELECT max(ID) FROM Communications), '{receiverId}')");

                    table = db.ExecuteQuery($"SELECT max(ID) FROM Messages");
                    int id;
                    if (table.HasRows)
                    {
                        id = Convert.ToInt32(table[0][0]);
                    }
                    else
                        throw new Exception("[SEND]: EXCEPTION -> QUERY ERROR...");

                    SendData("{101}" + id);
                    CWLColor($"[Server-Query] {type}: Message sent from {senderId} to {receiverId}", ConsoleColor.Cyan);
                }
                // получатель не найден {306}
                else
                {
                    message = "Receiver is not founded!";
                    SendData("{306}");
                    CWLColor($"[Server-Query] {type}: {message}", ConsoleColor.Cyan);
                }
            }
            //  ключ недействителеный {303}
            else
            {
                CWLColor($"[Server-Query] {type}: {message}. Login-{username}. Wrong key-{key}", ConsoleColor.Cyan);
                message = "Key is not valid";
                SendData("{303}");
            }
        }
        private void GetDialog()
        {
            SqlResultTable table;
            string type;
            string username;
            string key;
            string senderId;
            string receiverId;
            string message;

            type = "[GET_DIALOG]";

            key = GetData(true); // кто запрашивает
            username = GetData(false); // второй участник диалога

            senderId = GetIdByLogin(username);
            // Пользователь-отправитель существует
            if (senderId != null)
            {
                receiverId = GetIdByKey(key);
                if (receiverId != null)
                {
                    // Получить сообщения диалога
                    table = db.ExecuteQuery($"SELECT M.ID, M.Date, M.Read, U.Login, M.Message FROM Communications C " +
                                            $"JOIN Messages M ON " +
                                            $"(C.SenderID={senderId} AND C.ReceiverID={receiverId} OR C.SenderID={receiverId} AND C.ReceiverID={senderId}) AND C.MessageID=M.ID " +
                                            $"JOIN Users U ON " +
                                            $"U.ID = C.SenderID;");
                    // Если сообщения есть {101}
                    if (table.HasRows)
                    {
                        string id;
                        string date;
                        string isRead; // значения: 1/0
                        string data = null;
                        int count = 0;

                        for (int i = 0; i < table.CountRows; i++)
                        {

                            id = table[i][0];
                            date = table[i][1].ToString();
                            isRead = Convert.ToInt32(table[i][2]) == 1 ? "1" : "0";
                            username = table[i][3].ToString();
                            message = table[i][4].ToString();

                            data += CreateMessage(id, date, isRead, username, message);

                            count++;
                        }

                        SendData("{101}" + data);
                        CWLColor($"[Server-Query] {type}: Pulled {count} message(s) for users {senderId}-{receiverId}", ConsoleColor.Cyan);
                    }
                    // Сообщений нет {100}
                    else
                    {
                        SendData("{100}");
                        CWLColor($"[Server-Query] {type}: Users {senderId}-{receiverId} do not have messages", ConsoleColor.Cyan);
                    }
                }
                //  Ключ недействителен {303}
                else
                {
                    message = "Key is not valid";
                    CWLColor($"[Server-Query] {type}: {message}. UserID-{receiverId}. Wrong key-{key}", ConsoleColor.Cyan);
                    SendData("{303}");
                }
            }
            // Пользователь-отправитель не существует {511}
            else
            {
                message = "Sender is not founded";
                CWLColor($"[Server-Query] {type}: {message}", ConsoleColor.Cyan);
                SendData("{511}");
            }
        }
        private void GetDialogList()
        {
            SqlResultTable table;
            string type;
            string key;
            string lastMessage;
            string userId;

            type = "[GET_DIALOG_LIST]";

            key = GetData(false); // кто запрашивает

            userId = GetIdByKey(key);
            // Пользователь найден. Ключ существует
            if (userId != null)
            {
                // получить Id и Username второго участника диалога
                table = db.ExecuteQuery($"SELECT DISTINCT U.ID, U.Login FROM Users U " +
                                        $"JOIN Communications C " +
                                        $"ON (C.SenderID={userId} AND U.ID=C.ReceiverID) " +
                                        $"OR (C.ReceiverID={userId} AND U.ID = C.SenderID)");

                // Найден минимум один диалог {101}
                if (table.HasRows)
                {
                    string data = "";
                    string secondUserId;
                    string secondUserUsername;

                    int count = 0;
                    for (int i = 0; i < table.CountRows; i++)
                    {
                        secondUserId = table[i][0];
                        secondUserUsername = table[i][1];

                        lastMessage = GetLastOrNewMessage(userId, secondUserId);

                        data += (lastMessage == "{NDA}" ? "{NDA}" : (lastMessage + "{sprt}")) + secondUserUsername + "\n";
                        count++;
                    }
                    SendData("{101}" + data);
                    CWLColor($"[Server-Query] {type}: Founed {count} chat(s) for user id-{userId} ({key})", ConsoleColor.Cyan);
                }
                // Диалогов нет {100}
                else
                {
                    SendData("{100}");
                    CWLColor($"[Server-Query] {type}: User {userId}({key}) do not have any dialogs", ConsoleColor.Cyan);
                }
            }
            //  Ключ не действителен {303}
            else
            {
                CWLColor($"[Server-Query] {type}: Key is not valid. UserID-{userId}. Wrong key {key}", ConsoleColor.Cyan);
                SendData("{303}");
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
                return null;
        }
        private string GetIdByLogin(string Login)
        {
            SqlResultTable table = db.ExecuteQuery($"SELECT id FROM Users WHERE lower(Login)='{Login.ToLower()}'");
            if (table.HasRows)
            {
                return table[0][0].ToString();
            }
            else
                return null;
        }
        private static string CreateMessage(string id, string date, string read, string username, string message) => $"{id}|{date}|{read}|{username}|{message}{{endl}}";
        private string GetLastOrNewMessage(string s, string r)
        { // s - sender / r - receiver
            SqlResultTable table = db.ExecuteQuery($"SELECT U.ID, U.Login, M.Message, M.Read FROM Messages M " +
                            $"JOIN Communications C ON " +
                            $"C.SenderID IN({r},{s}) AND C.ReceiverID IN({s},{r}) AND C.MessageID=M.ID " +
                            $"JOIN Users U ON " +
                            $"U.ID = C.SenderID " +
                            $"ORDER BY M.ID DESC LIMIT 1");

            if (table.HasRows)
            {
                return table[0][2];
            }
            else
            {
                return "(?)";
            }
        }

        #endregion
    }
}
