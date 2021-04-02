using MultiThreadedServer;
using System.Net.Sockets;

namespace Server
{
    class Program
    {
        private static int port = 801;

        private static void Main(string[] args)
        {
            TcpListener serverSocket = new TcpListener(port);
            TcpClient clientSocket = default(TcpClient);

            serverSocket.Start();

            //AConsole.CWLColor("Сервер запущен", System.ConsoleColor.Yellow);

            while (true)
            {
                clientSocket = serverSocket.AcceptTcpClient();

                ClientHandler clientHandler = new ClientHandler();
                clientHandler.StartClient(clientSocket);
            }
        }
    }
}