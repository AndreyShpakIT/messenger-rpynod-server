using System;

namespace MultiThreadedServer
{
    static class AConsole
    {
        public static void Print(string message, string type, ConsoleColor color = ConsoleColor.White)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string messageToPrint = string.Format("{0} {1}: {2}", time, type.PadRight(18), message);

            CWLColor(messageToPrint, color);
        }
        public static void Print(string message, Client client, string type, ConsoleColor color = ConsoleColor.White)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string messageToPrint = string.Format("{0} {1}: {2} | {3}", time, type.PadRight(18), client, message);

            CWLColor(messageToPrint, color);
        }
        public static void PrintException(string message, Client client)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string messageToPrint = string.Format("{0} {1}: {2} | {3}", time, "[EXCEPTION]".PadRight(18), client, message);
            
            CWLColor(messageToPrint, ConsoleColor.Red);
        }

        public static void CWLColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
