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
        //private bool showData = false;
        
        private ConsoleColor MessageColor = ConsoleColor.Cyan;
        private ConsoleColor ErrorColor = ConsoleColor.Red;

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
            //bool firstIteration = true;
            bool _break = false;

            while (!_break)
            {
                type = GetData(true);

                if (type == "{EXCEPTION}")
                    break;

                //if (firstIteration && type != "[LOG]" && type != "[REG]")
                //{
                //    AConsole.Print("Клиент переподключен", clientInfo, type, MessageColor);
                //}

                try
                {
                    switch (type)
                    {
                        case "[LOG]":               _break = !Login(); break;
                        case "[GET_FRIEND_LIST]":   GetFriendsList(); break;
                        case "[REG]":               _break = !Register(); break;
                        case "[SEND]":              Send(); break;
                        case "[GET_DIALOG]":        GetDialog();  break;
                        case "[GET_DIALOG_LIST]":   GetDialogList();  break;
                        case "[SEND_REQ]":          SendRequest();  break;
                        case "[GET_INC_REQ]":       GetIncomingRequests();  break;
                        case "[GET_OUT_REQ]":       GetOutgoingRequests();  break;
                        case "[ACCEPT_REQ]":        AcceptRequest(); break;
                        case "[DENY_ICN_REQ]":      DenyIncomingRequest(); break;
                        case "[CANCEL_OUT_REQ]":    CancelOutgoingRequest(); break;
                        case "[DELETE_FRIEND]":     DeleteFriend(); break;
                        case "[NEW_MSG]":           CheckNewMessages(); break;
                        case "[CHANGE]":            ChangePassword(); break;
                        case "[USER_EXISTS]":       UserExists(); break;
                        case "[READ_MESSAGE]":      ReadMessage(); break;

                        default:
                            if (!string.IsNullOrEmpty(type))
                                AConsole.Print("Не распознанный запрос", clientInfo, type, ErrorColor);
                            break;
                    }
                }
                catch (Exception e)
                {
                    AConsole.PrintException(e.Message, clientInfo);
                    break;
                }
                //firstIteration = false;
            }
            AConsole.Print("Клиент отключен", clientInfo, "[DISCONNECT]", ConsoleColor.DarkGreen);
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

            //Console.WriteLine($"[Server-Query] {type}: Login-{username}");

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
                    //CWLColor($"[Server-Query] {type}: Succes username -> {username}, {key}.", ConsoleColor.Cyan);

                    string id = GetIdByLogin(username);

                    clientInfo = new Client()
                    {
                        Username = username,
                        Key = key,
                        Salt = salt,
                        HashPassword = hash,
                        ID = id
                    };

                    AConsole.Print("Клиент подключен", clientInfo, type, ConsoleColor.Green);

                    return true;
                }
                // пароль не верный {301}
                else
                {
                    SendData("{301}");
                    AConsole.Print("Неверный пароль", clientInfo, type, ErrorColor);
                }
            }
            // Пользователя нет в БД {302]
            else
            {
                AConsole.Print("Неверное имя пользователя", clientInfo, type, ErrorColor);
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
                AConsole.Print($"Найдено {table.CountRows} друзей", clientInfo, type, MessageColor);
            }
            // если добавленных друзей нет {100}
            else
            {
                SendData("{100}");
                AConsole.Print($"Пользователь не имеет друзей", clientInfo, type, MessageColor);
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
                AConsole.Print($"Данное имя пользователя уже занято", clientInfo, type, ErrorColor);
            }
            // Если логин свободен
            else
            {
                table = db.ExecuteQuery($"SELECT Email FROM Users WHERE lower(Email)='{email.ToLower()}'");
                // Если почта занята {305}
                if (table.HasRows)
                {
                    SendData("{305}");
                    AConsole.Print($"Данный электронный адрес уже занят", clientInfo, type, ErrorColor);
                }
                // Если почта не занятна {100}
                else
                {
                    db.ExecuteQuery($"INSERT INTO Users(Login, Password, Salt, Email) VALUES('{username}', '{hash}', '{salt}', '{email}')");
                    SendData("{100}");

                    clientInfo.Username = username;
                    clientInfo.ID = GetIdByKey(username);

                    AConsole.Print($"Пользователь зарегистрирован", clientInfo, type, ConsoleColor.Green);
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
                    AConsole.Print($"Сообщение отправлено ({senderId} -> {receiverId})", clientInfo, type, MessageColor);
                }
                // получатель не найден {306}
                else
                {
                    SendData("{306}");
                    AConsole.Print($"Пользователь, которому направлялось сообщение не найден", clientInfo, type, MessageColor);
                }
            }
            //  ключ недействителеный {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
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
                        string isRead;
                        string data = null;
                        int count = 0;

                        for (int i = 0; i < table.CountRows; i++)
                        {

                            id = table[i][0];
                            date = table[i][1].ToString();
                            isRead = table[i][2] == "True" ? "1" : "0";
                            username = table[i][3].ToString();
                            message = table[i][4].ToString();

                            data += CreateMessage(id, date, isRead, username, message);

                            count++;
                        }

                        SendData("{101}" + data);
                        AConsole.Print($"Загружено {count} сообщений для диалога {senderId}-{receiverId}", clientInfo, type, MessageColor);
                    }
                    // Сообщений нет {100}
                    else
                    {
                        SendData("{100}");
                        AConsole.Print($"Сообщений нет {senderId}-{receiverId}", clientInfo, type, MessageColor);
                    }
                }
                //  Ключ недействителен {303}
                else
                {
                    AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                    SendData("{303}");
                }
            }
            // Пользователь-отправитель не существует {511}
            else
            {
                message = "Sender is not founded";
                AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, ErrorColor);
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

                    string dialog;

                    int count = 0;
                    for (int i = 0; i < table.CountRows; i++)
                    {
                        secondUserId = table[i][0];
                        secondUserUsername = table[i][1];

                        lastMessage = GetLastOrNewMessage(userId, secondUserId);

                        // format:
                        // 1) username|message\n
                        // 2) username|{NDA}\n
                        dialog = secondUserUsername + "|" + lastMessage + "\n";
                        
                        //CWLColor($"[Server-Query] {type}: {dialog}", ConsoleColor.Cyan);

                        data += dialog;
                        count++;
                    }
                    SendData("{101}" + data);
                    AConsole.Print($"Найдено {count} диалогов", clientInfo, type, MessageColor);
                }
                // Диалогов нет {100}
                else
                {
                    SendData("{100}");
                    AConsole.Print($"Нет диалогов", clientInfo, type, MessageColor);
                }
            }
            //  Ключ не действителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                SendData("{303}");
            }
        }
        private void SendRequest()
        {
            SqlResultTable table;
            string type;
            string username;
            string key;
            string senderId;
            string receiverId;

            type = "[SEND_REQ]";

            key = GetData(true); // ключ отправителя
            username = GetData(false); // логин получателя

            // вытянуть id получателя
            receiverId = GetIdByLogin(username);

            // Пользователь-получатель существует
            if (receiverId != null)
            {
                // Выитянуть id отправителя из БД
                senderId = GetIdByKey(key);

                // Если ключ имеется в БД, то отправитель идентифицирован
                if (senderId != null)
                {
                    // Не отправлен ли уже исходящий запрос
                    table = db.ExecuteQuery($"SELECT * FROM Requests WHERE SenderID='{senderId}' AND ReceiverID='{receiverId}'");
                    // Если еще не отправлен
                    if (!table.HasRows)
                    {
                        // Не отправлен ли уже входящий запрос
                        table = db.ExecuteQuery($"SELECT * FROM Requests WHERE SenderID = '{receiverId}' AND ReceiverID = '{senderId}'");

                        //  Не отправлен
                        if (!table.HasRows)
                        {
                            // Не являются ли пользователи уже друзьями
                            table = db.ExecuteQuery($"SELECT * FROM friends WHERE FirstUserID IN({receiverId}, {senderId}) AND SecondUserID IN({receiverId}, {senderId})");

                            // Еще не друзья {100}
                            if (!table.HasRows)
                            {
                                // Отправить запрос {100}
                                table = db.ExecuteQuery($"INSERT INTO Requests(SenderID, ReceiverID) VALUES ('{senderId}', '{receiverId}')");

                                SendData("{100}");
                                AConsole.Print($"Запрос отправлен пользователю '{receiverId}'", clientInfo, type, MessageColor);
                            }
                            // Уже друзья {307}
                            else
                            {
                                SendData("{307}");
                                AConsole.Print($"Невозможно отправить запрос пользователю '{receiverId}', т.к. пользователю уже друзья", clientInfo, type, MessageColor);
                            }
                        }
                        // отправлен входящий {308}
                        else
                        {
                            SendData("{308}");
                            AConsole.Print($"Невозможно отправить запрос пользователю '{receiverId}', т.к. уже получен входящий", clientInfo, type, MessageColor);
                        }
                    }
                    // если уже отправлен исходящий {309}
                    else
                    {
                        SendData("{309}");
                        AConsole.Print($"Невозможно отправить запрос пользователю '{receiverId}', т.к. он уже отправлен", clientInfo, type, MessageColor);
                    }
                }
                // ключ недействителен {303}
                else
                {
                    AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                    SendData("{303}");
                }
            }
            // получателя нет в БД {306}
            else
            {
                AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, ErrorColor);
                SendData("{306}");
            }
        }
        private void GetIncomingRequests()
        {
            SqlResultTable table;
            string type;
            string key;
            string receiverId;

            type = "[GET_INC_REQ]";

            key = GetData(false);

            // Логин пользователя, запрашивающего входящие запросы в друзья, так как запросы для него, то он является получателем
            receiverId = GetIdByKey(key);

            // выбрать входящие запросы
            table = db.ExecuteQuery($"SELECT Login FROM Users U JOIN Requests R ON U.ID = R.SenderID AND R.ReceiverID='{receiverId}'");

            // если входящие запросы имеются {101}
            if (table.HasRows)
            {
                string data = "";

                int count = 0;

                for (int i = 0; i < table.CountRows; i++)
                {
                    data += table[i][0] + "\n";
                    count++;
                }

                AConsole.Print($"Найдено {count} входящих запросов", clientInfo, type, MessageColor);
                SendData("{101}" + data);
            }
            // входящих запросов нет {100}
            else
            {
                SendData("{100}");
                AConsole.Print($"Входящих запросов нет", clientInfo, type, MessageColor);
            }
        }
        private void GetOutgoingRequests()
        {
            SqlResultTable table;
            string type;
            string key;
            string senderId;

            type = "[GET_OUT_REQ]";

            key = GetData(false);

            // Логин пользователя, запрашивающего исходящие запросы в друзья, так как иходящие от него, то он является отправителем
            senderId = GetIdByKey(key);

            // выбрать исходящие запросы
            table = db.ExecuteQuery($"SELECT Login FROM Users U JOIN Requests R ON U.ID = R.ReceiverID AND R.SenderID='{senderId}'");

            // если исходящие запросы имеются {101}
            if (table.HasRows)
            {
                string data = "";

                int count = 0;

                for (int i = 0; i < table.CountRows; i++)
                {
                    data += table[i][0] + "\n";

                    count++;
                }
                SendData("{101}" + data);
                AConsole.Print($"Загружено {count} исходящих запросов", clientInfo, type, MessageColor);
            }
            // исходящих запросов нет {100}
            else
            {
                SendData("{100}");
                AConsole.Print($"Исходящий запросов нет", clientInfo, type, MessageColor);
            }
        }
        private void AcceptRequest()
        {
            SqlResultTable table;
            string type;
            string key;
            string senderId;
            string receiverId;
            string username;

            type = "[ACCEPT_REQ]";

            key = GetData(true);
            username = GetData(false);

            receiverId = GetIdByKey(key);

            // Пользователь-получатель существует
            if (receiverId != null)
            {
                senderId = GetIdByLogin(username);
                // Пользователь-отправитель существует
                if (senderId != null)
                {
                    table = db.ExecuteQuery($"SELECT * FROM Requests WHERE ReceiverID='{receiverId}' AND SenderID='{senderId}'");

                    // запрос существует {100}
                    if (table.HasRows)
                    {
                        // удалить запрос в друзья из таблицы
                        db.ExecuteQuery($"DELETE FROM Requests WHERE ReceiverID='{receiverId}' AND SenderID='{senderId}'");
                        // зарегистрировать дружбу
                        db.ExecuteQuery($"INSERT INTO Friends(FirstUserID, SecondUserID) VALUES('{senderId}', '{receiverId}')");

                        SendData("{100}");
                        AConsole.Print($"Входящий запрос от пользователя '{senderId}' принят", clientInfo, type, MessageColor);
                    }
                    // Данного запроса в друзья не существует {310}
                    else
                    {
                        SendData("{310}");
                        AConsole.Print($"Входящий запрос от пользователя '{senderId}' не найден", clientInfo, type, MessageColor);
                    }
                }
                // отправитель не найден в БД {306}
                else
                {
                    SendData("{306}");
                    AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, ErrorColor);
                }
            }
            // ключ не действителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                SendData("{303}");
            }
        }
        private void DenyIncomingRequest()
        {
            SqlResultTable table;
            string type;
            string key;
            string senderId;
            string receiverId;
            string username;

            type = "[ACCEPT_REQ]";

            key = GetData(true);

            username = GetData(false);

            receiverId = GetIdByKey(key);
            // Пользователь существует
            if (receiverId != null)
            {
                senderId = GetIdByLogin(username);

                // Пользователь существует
                if (senderId != null)
                {
                    table = db.ExecuteQuery($"SELECT * FROM Requests WHERE ReceiverID='{receiverId}' AND SenderID='{senderId}'");

                    // запрос существует {100}
                    if (table.HasRows)
                    {
                        // удалить запрос в друзья из таблицы
                        table = db.ExecuteQuery($"DELETE FROM Requests WHERE ReceiverID='{receiverId}' AND SenderID='{senderId}'");

                        SendData("{100}");
                        AConsole.Print($"Входящий запрос пользователю '{senderId}' отменен", clientInfo, type, MessageColor);
                    }
                    // Данного запроса в друзья не существует {310}
                    else
                    {
                        SendData("{310}");
                        AConsole.Print($"Входящий запрос пользователю '{senderId}' не найден", clientInfo, type, MessageColor);
                    }
                }
                // отправитель не найден в БД {306}
                else
                {
                    SendData("{306}");
                    AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, ErrorColor);
                }
            }
            // ключ не действителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                SendData("{303}");
            }
        }
        private void CancelOutgoingRequest()
        {
            SqlResultTable table;
            string type;
            string key;
            string senderId;
            string receiverId;
            string username;

            type = "[CANCEL_OUT_REQ]";

            key = GetData(true);
            username = GetData(false);

            senderId = GetIdByKey(key);
            if (senderId != null)
            {
                receiverId = GetIdByLogin(username);
                if (receiverId != null)
                {
                    table = db.ExecuteQuery($"SELECT * FROM Requests WHERE ReceiverID='{receiverId}' AND SenderID='{senderId}'");

                    // запрос существует {100}
                    if (table.HasRows)
                    {
                        // удалить исходящий запрос из таблицы
                        db.ExecuteQuery($"DELETE FROM Requests WHERE ReceiverID='{receiverId}' AND SenderID='{senderId}'");

                        SendData("{100}");
                        AConsole.Print($"Запрос пользователю '{receiverId}' отменен", clientInfo, type, MessageColor);
                    }
                    // Данного запроса не существует {310}
                    else
                    {
                        SendData("{310}");
                        AConsole.Print($"Запрос пользователю '{receiverId}' недействительный", clientInfo, type, MessageColor);
                    }
                }
                // получатель не найден в БД {306}
                else
                {
                    SendData("{306}");
                    AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, MessageColor);
                }
            }
            // ключ не действителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                SendData("{212}");
            }
        }
        private void DeleteFriend()
        {
            SqlResultTable table;
            string type;
            string key;
            string senderId;
            string receiverId;
            string username;

            type = "[DELETE_FRIEND]";

            key = GetData(true);
            username = GetData(false);

            receiverId = GetIdByKey(key);
            // Если пользователь существует
            if (receiverId != null)
            {
                senderId = GetIdByLogin(username);
                // Если пользователь существует
                if (senderId != null)
                {
                    table = db.ExecuteQuery($"SELECT * FROM friends WHERE FirstUserID IN({receiverId}, {senderId}) AND SecondUserID IN({receiverId}, {senderId})");

                    // пользователи действительно друзья {100}
                    if (table.HasRows)
                    {
                        // удалить строку дружбы из таблицы friends
                        db.ExecuteQuery($"DELETE FROM Friends WHERE FirstUserID IN({receiverId}, {senderId}) AND SecondUserID IN({receiverId}, {senderId})");

                        SendData("{100}");
                        AConsole.Print($"Пользователь '{username}' удален из друзей", clientInfo, type, MessageColor);
                    }
                    // Пользователи не друзья {310}
                    else
                    {
                        SendData("{310}");
                        AConsole.Print($"Пользователь '{username}' не является другом", clientInfo, type, MessageColor);
                    }
                }
                // отправитель не найден в БД {306}
                else
                {
                    SendData("{306}");
                    AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, MessageColor);
                }
            }
            // ключ не действителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                SendData("{303}");
            }
        }
        private void CheckNewMessages()
        {
            SqlResultTable table;
            string type;
            string key;
            string receiverId;

            type = "[NEW_MSG]";

            key = GetData(false);

            receiverId = GetIdByKey(key);
            string _username = GetLoginById(receiverId);

            if (receiverId != null)
            {
                table = db.ExecuteQuery($"SELECT M.ID, M.Date, M.Read, U.Login, M.Message FROM DeliveringMessages D " +
                                        $"JOIN Communications C " + // + таблица "связь"
                                        $"ON D.CommunicationID = C.ID AND D.UserID='{receiverId}' AND D.Delivered='0' " + // выборка подходящих строк из таблицы "связь" 
                                        $"JOIN Messages M " + // + таблица "Сообщения"
                                        $"ON M.ID = C.MessageID " + // выборка подходящий сообщений
                                        $"JOIN Users U " + // + таблица пользователей
                                        $"ON U.ID = C.SenderID"); // выборка имен пользователей (отправителей)

                // новые сообщения есть {101}
                if (table.HasRows)
                {
                    string id;
                    string date;
                    string isRead; // значения: 1/0
                    string username;
                    string message = "";

                    string data = "";

                    for (int i = 0; i < table.CountRows; i++)
                    {
                        id = table[i][0].ToString();
                        date = table[i][1].ToString();
                        isRead = table[i][2] == "True" ? "1" : "0";
                        username = table[i][3].ToString();
                        message = table[i][4].ToString();

                        data += CreateMessage(id, date, isRead, username, message);
                    }

                    // пометить новые сообщения как доставленные
                    db.ExecuteQuery($"UPDATE DeliveringMessages SET Delivered='1' WHERE ID IN (SELECT ID FROM DeliveringMessages WHERE UserID='{receiverId}' AND Delivered='0')");

                    SendData("{101}" + data);
                }
                // Новых сообщений нет {100}
                else
                {
                    SendData("{100}");
                }
            }
            // ключ недействителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
                SendData("{303}");
            }
        }
        private void ChangePassword()
        {
            string type;
            string key;
            string salt;
            string hash;
            string userId;

            type = "[CHANGE]";

            key = GetData(true);
            hash = GetData(true);
            salt = GetData(false);

            userId = GetIdByKey(key);
            // ключ действительный
            if (userId != null)
            {
                db.ExecuteQuery($"UPDATE Users SET Password='{hash}' WHERE Key={key}");
                db.ExecuteQuery($"UPDATE Users SET Salt='{salt}' WHERE Key={key}");
                
                SendData("{100}");
                AConsole.Print($"Пароль изменен успешно", clientInfo, type, MessageColor);
            }
            // ключ недействительный
            else
            {
                SendData("{303}");
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
            }
        }
        private void UserExists()
        {
            SqlResultTable table;
            string username;
            string type;

            type = "[USER_EXISTS]";

            username = GetData(false);


            table = db.ExecuteQuery($"SELECT ID FROM Users WHERE lower(Login)='{username.ToLower()}'");
            // Пользователь существует {100}
            if (table.HasRows)
            {
                SendData("{100}");
                AConsole.Print($"Пользователь '{username}' существует'", clientInfo, type, MessageColor);
            }
            // Пользователь не существует {306}
            else
            {
                SendData("{306}");
                AConsole.Print($"Пользователь '{username}' не найден", clientInfo, type, MessageColor);
            }
        }
        private void ReadMessage()
        {
            string type;
            string key;
            string messageId;
            string userId;

            type = "[READ_MESSAGE]";

            key = GetData(true);

            userId = GetIdByKey(key);
            if (userId != null)
            {
                messageId = GetData(false);

                db.ExecuteQuery($"UPDATE Messages SET Read='1' WHERE ID='{messageId}';");
                AConsole.Print($"Сообщение (id:{messageId}) помечено как прочитанное", clientInfo, type, ConsoleColor.Cyan);
                SendData("{100}");
            }
            // ключ недействителен {303}
            else
            {
                AConsole.Print($"Ключ недействительный", clientInfo, type, ErrorColor);
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
        private string GetLoginById(string id)
        {
            SqlResultTable table = db.ExecuteQuery($"SELECT Login FROM Users WHERE id='{id}'");

            if (table.HasRows)
            {
                string t = table[0][0];
                return t;
            }
            else
            {
                return null;
            }
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
                // последнее сообщение не прочитано
                if (table[0][3] == "False")
                {
                    // сообщение наше
                    if (s == table[0][0])
                    {
                        return table[0][2];
                    }
                    return "{NDA}";
                }
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
