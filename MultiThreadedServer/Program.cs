using MultiThreadedServer;
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    class Program
    {
        private static string dbFileName = "DataBase.db";

        private static SQLiteConnection dbConnector;
        private static SQLiteCommand dbCommand;

        private static TcpListener Server;
        private static TcpClient client;
        private static NetworkStream stream;

        private static int port = 801;

        #region Settings

        private static int timeout = 7000;
        /// <summary>
        /// Shows info of method SendDate(...) if TRUE
        /// </summary>
        private static bool showInfoSendData = false;
        /// <summary>
        /// Shows info of method GetDate(...) if TRUE
        /// </summary>
        private static bool showInfoGetData = false;
        /// <summary>
        /// Shows info of other methods if TRUE
        /// </summary>
        private static bool showInfoOtherMethods = false;
        /// <summary>
        /// Shows addition info inside server-queries if TRUE
        /// </summary>
        private static bool showDataInsideServerQuery = true;
        private static bool showCheckingRequests = true;

        #endregion // Settings]

        private static void Main(string[] args)
        {
            TcpListener serverSocket = new TcpListener(801);
            TcpClient clientSocket = default(TcpClient);

            serverSocket.Start();
            Console.WriteLine(" >> " + "Server Started");

            while (true)
            {
                clientSocket = serverSocket.AcceptTcpClient();

                CWLColor("Client connected!", ConsoleColor.Green);

                ClientHandler clientHandler = new ClientHandler();
                clientHandler.StartClient(clientSocket);
            }

            return;

            Load();
            Connect();

            string message;
            string login;
            string type;
            string idSender;
            string idReceiver;
            string key;
            string data;

            DataBaseManipulation db = new DataBaseManipulation();
            db.Conncet();

            while (true)
            {
                try
                {
                    //Console.ForegroundColor = ConsoleColor.Yellow;
                    //Console.Write("\n[Server] Waiting for client... ");
                    Console.ForegroundColor = ConsoleColor.White;

                    client = Server.AcceptTcpClient();
                    client.ReceiveTimeout = timeout;
                    client.SendTimeout = timeout;


                    stream = client.GetStream();

                    type = GetData(true);

                    if (type != "[NEW_MSG]" || showCheckingRequests)
                        CWLColor("\n[Server] Client connected! Waiting for data...", ConsoleColor.Green);

                    if (type != "[NEW_MSG]")
                    {
                        Console.Write("[Server] Processing query is ");
                        CWLColor(type, ConsoleColor.Blue);
                    }

                    switch (type)
                    {
                        case "[LOG]": // log
                            {
                                login = GetData(false);
                                Console.WriteLine($"[Server-Query] {type}: Login-{login}");

                                dbCommand.CommandText = $"SELECT Salt FROM Users WHERE Login='{login}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                // если пользователь имеется в БД, вытянуть соль
                                if (r.HasRows)
                                {
                                    r.Read();
                                    string salt = r[0].ToString();
                                    r.Close();

                                    SendData("{101}" + salt);
                                    string hash = GetData(false);

                                    dbCommand.CommandText = $"SELECT ID FROM Users WHERE Password='{hash}';";
                                    r = dbCommand.ExecuteReader();
                                    // пароль верный
                                    if (r.HasRows)
                                    {
                                        r.Close();
                                        int newKey = 0;
                                        // Сгенерировать уникальный ключ 
                                        do
                                        {
                                            newKey = (new Random().Next(100000000, 1000000000));
                                            dbCommand.CommandText = $"SELECT ID FROM Users WHERE key='{newKey}';";
                                            r = dbCommand.ExecuteReader();
                                            if (!r.HasRows)
                                            {
                                                r.Close();
                                                break;
                                            }
                                        } while (true);

                                        // Обновить ключ
                                        dbCommand.CommandText = $"UPDATE Users SET key='{newKey}' WHERE Login='{login}';";
                                        dbCommand.ExecuteNonQuery();

                                        SendData("{101}" + newKey.ToString());
                                        CWLColor($"[Server-Query] {type}: Succes login -> {login}, {newKey}.", ConsoleColor.Cyan);
                                    }
                                    // пароль не верный {301}
                                    else
                                    {
                                        r.Close();
                                        SendData("{301}");
                                        CWLColor($"[Server-Query] {type}: Invalid password.", ConsoleColor.Cyan);
                                    }

                                }
                                // Пользователя нет в БД {302]
                                else
                                {
                                    //r.Close();
                                    CWLColor($"[Server-Query] {type}: Invalid username.", ConsoleColor.Cyan);
                                    SendData("{302}");
                                }

                                break;
                            }

                        case "[REG]": // reg
                            {
                                login = GetData(true);
                                string hash = GetData(true);
                                string Salt = GetData(true);
                                string email = GetData(false);

                                Console.WriteLine($"[Server-Query] {type}: Login-{login}");
                                Console.WriteLine($"[Server-Query] {type}: Email-{email}");

                                dbCommand.CommandText = $"SELECT Login FROM Users WHERE lower(Login)='{login.ToLower()}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();
                                // Если пользователь с данным логином существует {304}
                                if (r.HasRows)
                                {
                                    r.Close();
                                    message = "This Login is already taken!";
                                    SendData("{304}");
                                    CWLColor($"[Server-Query] {type}: {message}", ConsoleColor.Cyan);
                                }
                                // Если логин свободен
                                else
                                {
                                    r.Close();
                                    dbCommand.CommandText = $"SELECT Email FROM Users WHERE lower(Email)='{email.ToLower()}';";
                                    r = dbCommand.ExecuteReader();
                                    // Если почта занята {305}
                                    if (r.HasRows)
                                    {
                                        r.Close();
                                        message = "This email is already taken!";
                                        SendData("{305}");
                                        CWLColor($"[Server-Query] {type}: {message}", ConsoleColor.Cyan);
                                    }
                                    // Если почта не занятна {100}
                                    else
                                    {
                                        r.Close();
                                        dbCommand.CommandText = $"INSERT INTO Users(Login, Password, Salt, Email) VALUES('{login}', '{hash}', '{Salt}', '{email}');";
                                        dbCommand.ExecuteNonQuery();
                                        SendData("{100}");
                                        CWLColor($"[Server-Query] {type}: Succes registration -> {login}, {hash}::{Salt}, {email}", ConsoleColor.Cyan);
                                    }
                                }
                                if (!r.IsClosed)
                                    r.Close();
                                break;
                            }

                        case "[USER_EXISTS]": // user_exists
                            {
                                login = GetData(false);

                                Console.WriteLine($"[Server-Query] {type}: Login-{login}");

                                dbCommand.CommandText = $"SELECT ID FROM Users WHERE lower(Login)='{login.ToLower()}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();
                                // Пользователь существует {100}
                                if (r.HasRows)
                                {
                                    r.Close();
                                    SendData("{100}");
                                    CWLColor($"[Server-Query] {type}: '{login}' -> True", ConsoleColor.Cyan);
                                }
                                // Пользователь не существует {306}
                                else
                                {
                                    r.Close();
                                    SendData("{306}");
                                    CWLColor($"[Server-Query] {type}: '{login}' -> False", ConsoleColor.Cyan);
                                }

                                break;
                            }

                        case "[SEND]": // send
                            {

                                key = GetData(true); // кто отправляет
                                login = GetData(true); // кому отправлять
                                message = GetData(false);

                                Console.WriteLine($"[Server-Query] {type}: Login-{login}({key})");
                                Console.WriteLine($"[Server-Query] {type}: Sending message-{message}");

                                idSender = GetIdByKey(key);
                                if (idSender != null)
                                {
                                    idReceiver = GetIdByLogin(login);
                                    if (idReceiver != null)
                                    {
                                        // отправить сообщение {101}
                                        dbCommand.CommandText = $"INSERT INTO Messages(Message) VALUES('{message}');";
                                        dbCommand.ExecuteNonQuery();

                                        dbCommand.CommandText = $"INSERT INTO Communications(SenderID, ReceiverID, MessageID) VALUES('{idSender}', '{idReceiver}', (SELECT max(ID) FROM Messages));";
                                        dbCommand.ExecuteNonQuery();

                                        dbCommand.CommandText = $"INSERT INTO DeliveringMessages(Key, CommunicationID, UserID) VALUES('{key}', (SELECT max(ID) FROM Communications), '{idReceiver}')";
                                        dbCommand.ExecuteNonQuery();

                                        dbCommand.CommandText = $"SELECT max(ID) FROM Messages";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        int id;
                                        if (r.HasRows)
                                        {
                                            r.Read();
                                            id = Convert.ToInt32(r[0]);
                                            r.Close();
                                        }
                                        else
                                        {
                                            throw new Exception("[SEND]: EXCEPTION -> QUERY ERROR...");
                                        }

                                        SendData("{101}" + id);
                                        CWLColor($"[Server-Query] {type}: Message sent from {idSender} to {idReceiver}", ConsoleColor.Cyan);
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
                                    CWLColor($"[Server-Query] {type}: {message}. Login-{login}. Wrong key-{key}", ConsoleColor.Cyan);
                                    message = "Key is not valid";
                                    SendData("{303}");
                                }
                                break;
                            }

                        case "[GET_DIALOG]": // get_dialog
                            {
                                key = GetData(true); // кто запрашивает
                                login = GetData(false); // второй участник диалога

                                idSender = GetIdByLogin(login);
                                // Пользователь-отправитель существует
                                if (idSender != null)
                                {
                                    idReceiver = GetIdByKey(key);
                                    if (idReceiver != null)
                                    {
                                        // Получить сообщения диалога
                                        dbCommand.CommandText = $"SELECT M.ID, M.Date, M.Read, U.Login, M.Message FROM Communications C " +
                                                                $"JOIN Messages M ON " +
                                                                $"(C.SenderID={idSender} AND C.ReceiverID={idReceiver} OR C.SenderID={idReceiver} AND C.ReceiverID={idSender}) AND C.MessageID=M.ID " +
                                                                $"JOIN Users U ON " +
                                                                $"U.ID = C.SenderID;";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // Если сообщения есть {101}
                                        if (r.HasRows)
                                        {
                                            data = "";

                                            if (showDataInsideServerQuery)
                                                Console.WriteLine($"[Server-Query] {type}: Messages from users {idSender}-{idReceiver}:");
                                            int count = 0;

                                            string id;
                                            string date;
                                            string isRead; // значения: 1/0
                                            string username;
                                            message = "";

                                            while (r.Read())
                                            {
                                                id = r[0].ToString();
                                                date = r[1].ToString();
                                                isRead = Convert.ToInt32(r[2]) == 1 ? "1" : "0";
                                                username = r[3].ToString();
                                                message = r[4].ToString();

                                                data += CreateMessage(id, date, isRead, username, message);

                                                if (showDataInsideServerQuery)
                                                    Console.WriteLine($"[Server-Query] {type}: {username} ({date}): {message}");
                                                count++;
                                            }
                                            r.Close();

                                            dbCommand.ExecuteNonQuery();
                                            SendData("{101}" + data);
                                            CWLColor($"[Server-Query] {type}: Pulled {count} message(s) for users {idSender}-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                        // Сообщений нет {100}
                                        else
                                        {
                                            r.Close();
                                            SendData("{100}");
                                            CWLColor($"[Server-Query] {type}: Users {idSender}-{idReceiver} do not have messages", ConsoleColor.Cyan);
                                        }
                                    }
                                    //  Ключ недействителен {303}
                                    else
                                    {
                                        message = "Key is not valid";
                                        CWLColor($"[Server-Query] {type}: {message}. UserID-{idReceiver}. Wrong key-{key}", ConsoleColor.Cyan);
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

                                break;
                            }

                        case "[GET_DIALOG_LIST]": // get_dialog_users
                            {
                                key = GetData(false); // кто запрашивает

                                string idUser = GetIdByKey(key);
                                // Пользователь найден. Ключ существует
                                if (idUser != null)
                                {
                                    // получить сообщения диалога
                                    dbCommand.CommandText = $"SELECT DISTINCT U.ID, U.Login FROM Users U " +
                                                            $"JOIN Communications C ON " +
                                                            $"(C.SenderID={idUser} AND U.ID= C.ReceiverID) OR (C.ReceiverID={idUser} AND U.ID = C.SenderID)";


                                    SQLiteDataReader r = dbCommand.ExecuteReader();
                                    // Найден минимум один диалог {101}
                                    if (r.HasRows)
                                    {
                                        data = "";
                                        if (showDataInsideServerQuery)
                                            Console.WriteLine($"[Server-Query] {type}: Chat list:");
                                        int count = 0;
                                        while (r.Read())
                                        {
                                            message = GetLastOrNewMessage(r[0].ToString(), idUser);
                                            login = r[1].ToString();

                                            if (showDataInsideServerQuery)
                                                Console.WriteLine($"[Server-Query] {type}: {login}->" + (message == "{NDA}" ? "new data" : (message)));

                                            data += (message == "{NDA}" ? "{NDA}" : (message + "{sprt}")) + login + "\n";
                                            count++;
                                        }
                                        r.Close();
                                        SendData("{101}" + data);
                                        CWLColor($"[Server-Query] {type}: Founed {count} chat(s) for user id-{idUser} ({key})", ConsoleColor.Cyan);
                                    }
                                    // Диалогов нет {100}
                                    else
                                    {
                                        r.Close();
                                        SendData("{100}");
                                        CWLColor($"[Server-Query] {type}: User {idUser}({key}) do not have any dialogs", ConsoleColor.Cyan);
                                    }
                                }
                                //  Ключ не действителен {303}
                                else
                                {
                                    message = "Key is not valid";
                                    CWLColor($"[Server-Query] {type}: {message}. UserID-{idUser}. Wrong key {key}", ConsoleColor.Cyan);
                                    SendData("{303}");
                                }

                                break;
                            }

                        case "[SEND_REQ]": // add_friend
                            {
                                key = GetData(true); // ключ отправителя
                                login = GetData(false); // логин получателя

                                // вытянуть id получателя
                                idReceiver = GetIdByLogin(login);

                                // Пользователь-получатель существует
                                if (idReceiver != null)
                                {
                                    // Выитянуть id отправителя из БД
                                    idSender = GetIdByKey(key);

                                    // Если ключ имеется в БД, то отправитель идентифицирован
                                    if (idSender != null)
                                    {
                                        // Не отправлен ли уже исходящий запрос
                                        dbCommand.CommandText = $"SELECT * FROM Requests WHERE SenderID='{idSender}' AND ReceiverID='{idReceiver}';";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // Если еще не отправлен
                                        if (!r.HasRows)
                                        {
                                            r.Close();

                                            // Не отправлен ли уже входящий запрос
                                            dbCommand.CommandText = $"SELECT * FROM Requests WHERE SenderID = '{idReceiver}' AND ReceiverID = '{idSender}';";
                                            r = dbCommand.ExecuteReader();

                                            //  Не отправлен
                                            if (!r.HasRows)
                                            {
                                                r.Close();

                                                // Не являются ли пользователи уже друзьями
                                                dbCommand.CommandText = $"SELECT * FROM friends WHERE FirstUserID IN({idReceiver}, {idSender}) AND SecondUserID IN({idReceiver}, {idSender});";
                                                r = dbCommand.ExecuteReader();

                                                // Еще не друзья {100}
                                                if (!r.HasRows)
                                                {
                                                    r.Close();

                                                    // Отправить запрос {100}
                                                    dbCommand.CommandText = $"INSERT INTO Requests(SenderID, ReceiverID) VALUES ('{idSender}', '{idReceiver}');";
                                                    dbCommand.ExecuteNonQuery();

                                                    message = "Request has been sent!";
                                                    SendData("{100}");
                                                    CWLColor($"[Server-Query] {type}: {message} Users {idSender}->{idReceiver}", ConsoleColor.Cyan);
                                                }
                                                // Уже друзья {307}
                                                else
                                                {
                                                    r.Close();
                                                    message = "Failed: Users are already friends!";
                                                    SendData("{307}");
                                                    CWLColor($"[Server-Query] {type}: {message} Users {idSender}->{idReceiver}", ConsoleColor.Cyan);
                                                }
                                            }
                                            // отправлен входящий {308}
                                            else
                                            {
                                                r.Close();
                                                message = "Failed: Outgoing request has been already sent!";
                                                SendData("{308}");
                                                CWLColor($"[Server-Query] {type}: {message} Users {idSender}->{idReceiver}", ConsoleColor.Cyan);
                                            }
                                        }
                                        // если уже отправлен исходящий {309}
                                        else
                                        {
                                            r.Close();
                                            message = "Failed: Incoming request has been already sent!";
                                            SendData("{309}");
                                            CWLColor($"[Server-Query] {type}: {message} Users {idSender}->{idReceiver}", ConsoleColor.Cyan);
                                        }
                                    }
                                    // ключ недействителен {303}
                                    else
                                    {
                                        message = "Key is not valid";
                                        CWLColor($"[Server-Query] {type}: {message}. Login-{login}. Wrong key: {key}", ConsoleColor.Cyan);
                                        SendData("{303}");
                                    }
                                }
                                // получателя нет в БД {306}
                                else
                                {
                                    message = "Receiver is not founded";
                                    CWLColor($"[Server-Query] {type}: {message} Wrong login-{login}", ConsoleColor.Cyan);
                                    SendData("{306}");
                                }

                                break;
                            }

                        case "[GET_INC_REQ]": // check_requests
                            {
                                key = GetData(false);

                                // Логин пользователя, запрашивающего входящие запросы в друзья, так как запросы для него, то он является получателем
                                idReceiver = GetIdByKey(key);
                                data = "";

                                // выбрать входящие запросы
                                dbCommand.CommandText = $"SELECT Login FROM Users U JOIN Requests R ON U.ID = R.SenderID AND R.ReceiverID='{idReceiver}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                // если входящие запросы имеются {101}
                                if (r.HasRows)
                                {
                                    if (showDataInsideServerQuery)
                                        Console.WriteLine($"[Server-Query] {type}: Incoming requests to user id{idReceiver}:");
                                    while (r.Read())
                                    {
                                        data += r[0].ToString() + "\n";
                                        if (showDataInsideServerQuery)
                                            Console.WriteLine($"[Server-Query] {type}: {r[0]}:");
                                    }
                                    r.Close();

                                    CWLColor($"[Server-Query] {type}: Incoming requests founded. UserID {idReceiver}.", ConsoleColor.Cyan);
                                    SendData("{101}" + data);
                                }
                                // входящих запросов нет {100}
                                else
                                {
                                    r.Close();
                                    SendData("{100}");
                                    CWLColor($"[Server-Query] {type}: Incoming requests not founded. UserID {idReceiver}.", ConsoleColor.Cyan);
                                }

                                break;
                            }

                        case "[GET_OUT_REQ]": // check_outgoing
                            {
                                key = GetData(false);

                                // Логин пользователя, запрашивающего исходящие запросы в друзья, так как иходящие от него, то он является отправителем
                                idSender = GetIdByKey(key);
                                data = "";

                                // выбрать исходящие запросы
                                dbCommand.CommandText = $"SELECT Login FROM Users U JOIN Requests R ON U.ID = R.ReceiverID AND R.SenderID='{idSender}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                // если исходящие запросы имеются {101}
                                if (showDataInsideServerQuery)
                                    Console.WriteLine($"[Server-Query] {type}: Outgoing requests to user id{idSender}:");
                                if (r.HasRows)
                                {
                                    while (r.Read())
                                    {
                                        data += r[0].ToString() + "\n";
                                        if (showDataInsideServerQuery)
                                            Console.WriteLine($"[Server-Query] {type}: {r[0].ToString()}:");
                                    }
                                    r.Close();
                                    SendData("{101}" + data);
                                    CWLColor($"[Server-Query] {type}: Outgoing requests founded. UserID {idSender}.", ConsoleColor.Cyan);
                                }
                                // исходящих запросов нет {100}
                                else
                                {
                                    r.Close();
                                    SendData("{100}");
                                    CWLColor($"[Server-Query] {type}: Outgoing requests not founded. UserID {idSender}.", ConsoleColor.Cyan);
                                }

                                break;
                            }

                        case "[ACCEPT_REQ]": // accept_request
                            {
                                key = GetData(true);
                                login = GetData(false);

                                idReceiver = GetIdByKey(key);

                                // Пользователь-получатель существует
                                if (idReceiver != null)
                                {
                                    idSender = GetIdByLogin(login);
                                    // Пользователь-отправитель существует
                                    if (idSender != null)
                                    {
                                        dbCommand.CommandText = $"SELECT * FROM Requests WHERE ReceiverID='{idReceiver}' AND SenderID='{idSender}';";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // запрос существует {100}
                                        if (r.HasRows)
                                        {
                                            r.Close();
                                            // удалить запрос в друзья из таблицы
                                            dbCommand.CommandText = $"DELETE FROM Requests WHERE ReceiverID='{idReceiver}' AND SenderID='{idSender}';";
                                            dbCommand.ExecuteNonQuery();
                                            // зарегистрировать дружбу
                                            dbCommand.CommandText = $"INSERT INTO Friends(FirstUserID, SecondUserID) VALUES('{idSender}', '{idReceiver}');";
                                            dbCommand.ExecuteNonQuery();

                                            message = "Request accepted!";
                                            SendData("{100}");
                                            CWLColor($"[Server-Query] {type}: {message} SenderID {idSender}, ReceiverID {idReceiver}", ConsoleColor.Cyan);
                                        }
                                        // Данного запроса в друзья не существует {310}
                                        else
                                        {
                                            r.Close();
                                            message = "Request does not exist!";
                                            SendData("{310}");
                                            CWLColor($"[Server-Query] {type}: {message} SenderID {idSender}, ReceiverID {idReceiver}", ConsoleColor.Cyan);
                                        }
                                    }
                                    // отправитель не найден в БД {306}
                                    else
                                    {
                                        message = "Sender is not founded!";
                                        SendData("{306}");
                                        CWLColor($"[Server-Query] {type}: {message} Wrong login-{login}", ConsoleColor.Cyan);
                                    }
                                }
                                // ключ не действителен {303}
                                else
                                {
                                    message = "Key is not valid";
                                    CWLColor($"[Server-Query] {type}: {message}. Login-{login}. Wrong key-{key}", ConsoleColor.Cyan);
                                    SendData("{303}");
                                }

                                break;
                            }

                        case "[DENY_ICN_REQ]": // decline_incoming
                            {
                                key = GetData(true);
                                login = GetData(false);

                                idReceiver = GetIdByKey(key);
                                // Пользователь существует
                                if (idReceiver != null)
                                {
                                    idSender = GetIdByLogin(login);

                                    // Пользователь существует
                                    if (idSender != null)
                                    {
                                        dbCommand.CommandText = $"SELECT * FROM Requests WHERE ReceiverID='{idReceiver}' AND SenderID='{idSender}';";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // запрос существует {100}
                                        if (r.HasRows)
                                        {
                                            r.Close();
                                            // удалить запрос в друзья из таблицы
                                            dbCommand.CommandText = $"DELETE FROM Requests WHERE ReceiverID='{idReceiver}' AND SenderID='{idSender}';";
                                            dbCommand.ExecuteNonQuery();

                                            message = "Incoming request rejected!";
                                            SendData("{100}");
                                            CWLColor($"[Server-Query] {type}: {message} SenderID-{idSender}, ReceiverID-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                        // Данного запроса в друзья не существует {310}
                                        else
                                        {
                                            r.Close();
                                            message = "Incoming request does not exist!";
                                            SendData("{310}");
                                            CWLColor($"[Server-Query] {type}: {message} SenderID-{idSender}, ReceiverID-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                    }
                                    // отправитель не найден в БД {306}
                                    else
                                    {
                                        message = "Sender is not founded!";
                                        SendData("{306}");
                                        CWLColor($"[Server-Query] {type}: {message} Wrong login-{login}", ConsoleColor.Cyan);
                                    }
                                }
                                // ключ не действителен {303}
                                else
                                {
                                    message = "Key is not valid";
                                    CWLColor($"[Server-Query] {type}: {message}. Login-{login}. Wrong key-{key}", ConsoleColor.Cyan);
                                    SendData("{303}");
                                }

                                break;
                            }

                        case "[CANCEL_OUT_REQ]": // cancel_outgoing
                            {
                                key = GetData(true);
                                login = GetData(false);

                                idSender = GetIdByKey(key);
                                if (idSender != null)
                                {
                                    idReceiver = GetIdByLogin(login);
                                    if (idReceiver != null)
                                    {
                                        dbCommand.CommandText = $"SELECT * FROM Requests WHERE ReceiverID='{idReceiver}' AND SenderID='{idSender}';";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // запрос существует {100}
                                        if (r.HasRows)
                                        {
                                            r.Close();
                                            // удалить исходящий запрос из таблицы
                                            dbCommand.CommandText = $"DELETE FROM Requests WHERE ReceiverID='{idReceiver}' AND SenderID='{idSender}';";
                                            dbCommand.ExecuteNonQuery();

                                            message = "Outgoing request reje cted";
                                            SendData("{100}");
                                            CWLColor($"[Server-Query] {type}: {message}. SenderID-{idSender}. ReceiverID-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                        // Данного запроса не существует {310}
                                        else
                                        {
                                            r.Close();
                                            message = "Request does not exist";
                                            SendData("{310}");
                                            CWLColor($"[Server-Query] {type}: {message}. SenderID-{idSender}. ReceiverID-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                    }
                                    // получатель не найден в БД {306}
                                    else
                                    {
                                        message = "Receiver is not founded";
                                        SendData("{306}");
                                        CWLColor($"[Server-Query] {type}: {message}. Wrong login-{login}", ConsoleColor.Cyan);

                                    }
                                }
                                // ключ не действителен {303}
                                else
                                {
                                    message = "Key is not valid";
                                    CWLColor($"[Server-Query] {type}: {message}. Login-{login}. Wrong key-{key}", ConsoleColor.Cyan);
                                    SendData("{212}");
                                }

                                break;
                            }

                        case "[GET_FRIEND_LIST]": // show_friends
                            {
                                key = GetData(false);
                                string id = GetIdByKey(key);
                                data = "";

                                // выбрать друзей
                                dbCommand.CommandText = $"SELECT Login FROM Users " +
                                                        $"JOIN Friends ON " +
                                                        $"(Users.ID = Friends.FirstUserID AND Friends.SecondUserID='{id}') OR (Users.ID = Friends.SecondUserID AND Friends.FirstUserID='{id}');";
                                SQLiteDataReader r = dbCommand.ExecuteReader();
                                // если добавленные друзья есть {101}
                                if (r.HasRows)
                                {
                                    if (showDataInsideServerQuery)
                                        Console.WriteLine($"[Server-Query] {type}: Friend list:");
                                    int count = 0;
                                    while (r.Read())
                                    {
                                        data += r[0].ToString() + "\n";
                                        if (showDataInsideServerQuery)
                                            Console.WriteLine($"[Server-Query] {type}: " + r[0].ToString());
                                        count++;
                                    }
                                    r.Close();
                                    SendData("{101}" + data);
                                    CWLColor($"[Server-Query] {type}: Founded {count} friends. User {id}({key})", ConsoleColor.Cyan);
                                }
                                // если добавленных друзей нет {100}
                                else
                                {
                                    r.Close();
                                    SendData("{100}");
                                    CWLColor($"[Server-Query] {type}: User {id}({key}) do not have any friends.", ConsoleColor.Cyan);
                                }

                                break;
                            }

                        case "[DELETE_FRIEND]": // delete_friend
                            {
                                key = GetData(true);
                                login = GetData(false);

                                idReceiver = GetIdByKey(key);
                                // Если пользователь существует
                                if (idReceiver != null)
                                {
                                    idSender = GetIdByLogin(login);
                                    // Если пользователь существует
                                    if (idSender != null)
                                    {
                                        dbCommand.CommandText = $"SELECT * FROM friends WHERE FirstUserID IN({idReceiver}, {idSender}) AND SecondUserID IN({idReceiver}, {idSender});";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // пользователи действительно друзья {100}
                                        if (r.HasRows)
                                        {
                                            r.Close();
                                            // удалить строку дружбы из таблицы friends
                                            dbCommand.CommandText = $"DELETE FROM Friends WHERE FirstUserID IN({idReceiver}, {idSender}) AND SecondUserID IN({idReceiver}, {idSender});";
                                            dbCommand.ExecuteNonQuery();

                                            message = "Friend has been deleted!";
                                            SendData("{100}");
                                        }
                                        // Пользователи не друзья {310}
                                        else
                                        {
                                            r.Close();
                                            message = "This request is not founded!";
                                            SendData("{310}");
                                            CWLColor($"[Server-dataQuery] {type}: {message} Users {idSender}-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                    }
                                    // отправитель не найден в БД {306}
                                    else
                                    {
                                        message = "This user is not founded!";
                                        SendData("{306}");
                                        CWLColor($"[Server-dataQuery] {type}: {message} Wrong login-{login}", ConsoleColor.Cyan);
                                    }
                                }
                                // ключ не действителен {303}
                                else
                                {
                                    message = "Key is not valid";
                                    CWLColor($"[Server-Query] {type}: {message}. UserID-{idReceiver}. Wrong key-{key}", ConsoleColor.Cyan);
                                    SendData("{303}");
                                }

                                break;
                            }

                        case "[NEW_MSG]": //
                            {
                                // Входные данные: ключ
                                // Выходные данные: новые сообщения || ничего
                                key = GetData(false);

                                idReceiver = GetIdByKey(key);
                                string _username = GetLoginById(idReceiver);
                                if (idReceiver != null)
                                {
                                    dbCommand.CommandText = $"SELECT M.ID, M.Date, M.Read, U.Login, M.Message FROM DeliveringMessages D " +
                                                            $"JOIN Communications C " + // + таблица "связь"
                                                            $"ON D.CommunicationID = C.ID AND D.UserID='{idReceiver}' AND D.Delivered='0' " + // выборка подходящих строк из таблицы "связь" 
                                                            $"JOIN Messages M " + // + таблица "Сообщения"
                                                            $"ON M.ID = C.MessageID " + // выборка подходящий сообщений
                                                            $"JOIN Users U " + // + таблица пользователей
                                                            $"ON U.ID = C.SenderID"; // выборка имен пользователей (отправителей)
                                    SQLiteDataReader r = dbCommand.ExecuteReader();

                                    // новые сообщения есть {101}
                                    if (r.HasRows)
                                    {
                                        string id;
                                        string date;
                                        string isRead; // значения: 1/0
                                        string username;
                                        message = "";

                                        data = "";

                                        while (r.Read())
                                        {
                                            id = r[0].ToString();
                                            date = r[1].ToString();
                                            isRead = Convert.ToInt32(r[2]) == 1 ? "1" : "0";
                                            username = r[3].ToString();
                                            message = r[4].ToString();

                                            data += CreateMessage(id, date, isRead, username, message);
                                        }
                                        r.Close();

                                        // пометить новые сообщения как доставленные
                                        dbCommand.CommandText = $"UPDATE DeliveringMessages SET Delivered='1' WHERE ID IN (SELECT ID FROM DeliveringMessages WHERE UserID='{idReceiver}' AND Delivered='0');";
                                        dbCommand.ExecuteNonQuery();

                                        SendData("{101}" + data);
                                        if (showCheckingRequests)
                                            CWLColor($"[Server-Query] {type}: username:{_username} -> New messages availbe", ConsoleColor.Magenta);
                                    }
                                    // Новых сообщений нет {100}
                                    else
                                    {
                                        r.Close();
                                        message = "No new messages";
                                        SendData("{100}");
                                        if (showCheckingRequests)
                                            CWLColor($"[Server-Query] {type}: username:{_username}", ConsoleColor.Magenta);
                                    }
                                }
                                // ключ недействителен {303}
                                else
                                {
                                    message = "Key is not valid";
                                    CWLColor($"[Server-Query] {type}: username:{_username} -> Wrong key-{key}", ConsoleColor.Magenta);
                                    SendData("{303}");
                                }
                            }

                            break;
                        case "[SYNC]":
                            {
                                // Входные данные:
                                // ключ
                                // имя пользователя с которым ведется переписка
                                // последнее сообщение: id

                                key = GetData(true); // кто запрашивает
                                login = GetData(true); // второй участник диалога
                                string id = GetData(false); // id сообщения

                                idSender = GetIdByLogin(login);
                                // Пользователь-отправитель существует
                                if (idSender != null)
                                {
                                    idReceiver = GetIdByKey(key);
                                    if (idReceiver != null)
                                    {
                                        // Получить сообщения диалога
                                        dbCommand.CommandText = $"SELECT M.ID, M.Date, M.Read, U.Login, M.Message FROM Communications C " +
                                                                $"JOIN Messages M ON " +
                                                                $"(M.ID > {id} AND C.SenderID={idSender} AND C.ReceiverID={idReceiver} OR C.SenderID={idReceiver} AND C.ReceiverID={idSender}) AND C.MessageID=M.ID " +
                                                                $"JOIN Users U ON " +
                                                                $"U.ID = C.SenderID;";
                                        SQLiteDataReader r = dbCommand.ExecuteReader();

                                        // Если сообщения есть {101}
                                        if (r.HasRows)
                                        {
                                            data = "";

                                            if (showDataInsideServerQuery)
                                                Console.WriteLine($"[Server-Query] {type}: Messages from users {idSender}-{idReceiver}:");
                                            int count = 0;

                                            string date;
                                            string isRead; // значениe: 1/0
                                            string username;
                                            message = "";

                                            while (r.Read())
                                            {
                                                id = r[0].ToString();
                                                date = r[1].ToString();
                                                isRead = Convert.ToInt32(r[2]) == 1 ? "1" : "0";
                                                username = r[3].ToString();
                                                message = r[4].ToString();

                                                data += CreateMessage(id, date, isRead, username, message);

                                                if (showDataInsideServerQuery)
                                                    Console.WriteLine($"[Server-Query] {type}: {username} ({date}): {message}");
                                                count++;
                                            }
                                            r.Close();

                                            dbCommand.ExecuteNonQuery();
                                            SendData("{101}" + data);
                                            CWLColor($"[Server-Query] {type}: Pulled {count} message(s) for users {idSender}-{idReceiver}", ConsoleColor.Cyan);
                                        }
                                        // Сообщений нет {100}
                                        else
                                        {
                                            r.Close();
                                            SendData("{100}");
                                            CWLColor($"[Server-Query] {type}: Users {idSender}-{idReceiver} do not have messages", ConsoleColor.Cyan);
                                        }
                                    }
                                    //  Ключ не действителен {303}
                                    else
                                    {
                                        message = "Key is not valid";
                                        CWLColor($"[Server-Query] {type}: {message}. UserID-{idReceiver}. Wrong key-{key}", ConsoleColor.Cyan);
                                        SendData("{303}");
                                    }
                                }
                                // Пользователь-отправитель не существует {306}
                                else
                                {
                                    message = "Sender is not founded";
                                    CWLColor($"[Server-Query] {type}: {message}", ConsoleColor.Cyan);
                                    SendData("{306}");
                                }
                                break;
                            }
                        case "exit":
                            key = GetData(false);

                            dbCommand.CommandText = $"UPDATE Users SET key='0' WHERE key='{key}';";
                            dbCommand.ExecuteNonQuery();
                            break;
                    }

                    stream.Close();
                    client.Close();
                }
                catch (SQLiteException ex)
                {
                    CWLColor($"\n[Exception]: SQLiteException -> {ex.Message}, {ex.StackTrace}", ConsoleColor.Red);
                    SendData("{EXP}");
                }
                catch (Exception e)
                {
                    CWLColor($"\n[Exception]: Exception -> {e.Message}, {e.StackTrace}", ConsoleColor.Red);
                    SendData("{EXP}");
                }
            }
        }


        private static string GetData(bool send)
        {
            try
            {
                byte[] buff = new byte[255];
                stream.Read(buff, 0, buff.Length);

                string data = GetString(buff);

                while (stream.DataAvailable)
                {
                    buff = new byte[255];
                    stream.Read(buff, 0, buff.Length);
                    data += GetString(buff);
                }

                if (send)
                    SendData("{SYS}");

                if (showInfoGetData)
                    Console.WriteLine($"[Method] GetData({send}): " + data);

                return data;
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (System.IO.IOException e)
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
            return null;
        }
        private static void SendData(string data)
        {
            try
            {
                stream.Write(Encoding.UTF8.GetBytes(data), 0, Encoding.UTF8.GetBytes(data).Length);
                if (showInfoSendData)
                    Console.WriteLine("[Method] SendData(data): " + data);
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
        private static string GetString(byte[] b)
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
        private static void Load()
        {
            dbCommand = new SQLiteCommand();
            dbConnector = new SQLiteConnection();

            Server = new TcpListener(port);
            Server.Start();
        }

        // Устанавливает соединение с БД, если ее нет - создает
        private static void Connect()
        {
            if (!(File.Exists(dbFileName)))
            {
                SQLiteConnection.CreateFile(dbFileName);
                Console.WriteLine($"[Method] Connect(): File \"{dbFileName}\" was created.");
            }

            try
            {
                if (dbConnector.State != ConnectionState.Open)
                {
                    dbConnector = new SQLiteConnection($"Data source = {dbFileName};Version=3;");
                    dbConnector.Open();

                    dbCommand.Connection = dbConnector;

                    Console.WriteLine($"[Method] Connect(): Database \"{dbFileName}\" connected.");
                }
                else
                {
                    Console.WriteLine("[Method] Connect(): Database already connected!");
                }
            }
            catch (SQLiteException ex)
            {
                CWLColor("\n[Method - SQLiteException] Connect(): " + ex.Message, ConsoleColor.Red);
            }
        }
        private static string GetIdByKey(string key)
        {
            dbCommand.CommandText = $"SELECT id FROM Users WHERE key='{key}';";
            SQLiteDataReader r = dbCommand.ExecuteReader();
            r.Read();
            if (r.HasRows)
            {
                string t = r[0].ToString();
                r.Close();
                return t;
            }
            else
            {
                r.Close();
                return null;
            }
        }
        private static string GetIdByLogin(string Login)
        {
            SQLiteCommand c = new SQLiteCommand();
            c.Connection = dbConnector;

            c.CommandText = $"SELECT id FROM Users WHERE lower(Login)='{Login.ToLower()}';";

            SQLiteDataReader r = c.ExecuteReader();

            r.Read();
            if (r.HasRows)
            {
                string t = r[0].ToString();
                r.Close();
                return t;
            }
            else
            {
                r.Close();
                return null;
            }
        }
        private static string GetLoginById(string id)
        {
            SQLiteCommand c = new SQLiteCommand();
            c.Connection = dbConnector;

            c.CommandText = $"SELECT Login FROM Users WHERE id='{id}';";

            SQLiteDataReader r = c.ExecuteReader();

            r.Read();
            if (r.HasRows)
            {
                string t = r[0].ToString();
                r.Close();
                return t;
            }
            else
            {
                r.Close();
                return null;
            }
        }

        private static string CreateMessage(string id, string date, string read, string username, string message)
        {
            return $"{id}|{date}|{read}|{username}|{message}{{endl}}";
        }
        static string GetLastOrNewMessage(string s, string r)
        {
            SQLiteCommand c = new SQLiteCommand();
            c.Connection = dbConnector;

            //c.CommandText = $"SELECT isReaded FROM messages WHERE isReaded='0' AND sender='{idSender}' AND receiver='{idReceiver}' GROUP BY isReaded";

            c.CommandText = $"SELECT U.ID, U.Login, M.Message, M.Read FROM Messages M " +
                            $"JOIN Communications C ON " +
                            $"C.SenderID IN({r},{s}) AND C.ReceiverID IN({s},{r}) AND C.MessageID=M.ID " +
                            $"JOIN Users U ON " +
                            $"U.ID = C.SenderID " +
                            $"ORDER BY M.ID DESC LIMIT 1;";

            SQLiteDataReader row = c.ExecuteReader();
            string t = "";
            if (row.HasRows)
            {
                row.Read();

                // Если последнее сообщение собственное
                if (row[0].ToString() == r)
                {
                    // Возвращаем само сообщение
                    if (showInfoOtherMethods)
                        Console.WriteLine($"[Method] GetLastOrNewMessage({s}, {r}): {row[1].ToString()}: {{LM}}{row[2].ToString()}");
                    t = row[2].ToString();
                    row.Close();
                    return t;
                }
                else
                {
                    // Если сообщение не прочитано
                    if (row[3].ToString() == "False")
                    {
                        // Помечаем доступность нового сообщения
                        if (showInfoOtherMethods)
                            Console.WriteLine($"[Method] GetLastOrNewMessage({s}, {r}): {row[1].ToString()}: {{NM}}{row[2].ToString()}");
                        row.Close();
                        return "{NDA}";
                    }
                    // Если сообщение не собственное но прочитано
                    else
                    {
                        // Возвращаем последнее сообщение
                        if (showInfoOtherMethods)
                            Console.WriteLine($"[Method] GetLastOrNewMessage({s}, {r}): {row[1].ToString()}: {{LM}}{row[2].ToString()}");
                        t = row[2].ToString();
                        row.Close();
                        return t;
                    }
                }


            }
            else
            {
                if (showInfoOtherMethods)
                    Console.WriteLine($"[Method] GetLastOrNewMessage({s}, {r}): {row[1].ToString()}: No messages");
                row.Close();
                return "";
            }
        }

        private static bool UserExists(string login)
        {

            dbCommand.CommandText = $"SELECT ID FROM Users WHERE Login='{login}';";
            SQLiteDataReader r = dbCommand.ExecuteReader();
            if (r.HasRows)
            {
                r.Close();
                if (showInfoOtherMethods)
                    Console.WriteLine($"[Method] UserExists({login}): -> true");
                return true;
            }
            else
            {
                r.Close();
                if (showInfoOtherMethods)
                    Console.WriteLine($"[Method] UserExists({login}): -> false");
                return false;
            }
        }
        private static void CWLColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}