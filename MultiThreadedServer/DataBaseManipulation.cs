using System;
using System.Data;
using System.Data.SQLite;
using System.IO;


namespace MultiThreadedServer
{
    class DataBaseManipulation
    {
        private SQLiteConnection dbConnector;
        private SQLiteCommand dbCommand;

        public string DbFileName { get; } = "DataBase.db";

        public SqlResultTable ExecuteQuery(string query)
        {
            dbCommand.CommandText = query;

            SqlResultTable table = new SqlResultTable();

            if (query.ToLower().Contains("select"))
            {
                SQLiteDataReader r = dbCommand.ExecuteReader();

                if (r.HasRows)
                {
                    int columns = r.FieldCount;
                    table = new SqlResultTable(columns, 0);

                    string[] columnValues = new string[columns];

                    while (r.Read())
                    {
                        try
                        {
                            for (int j = 0; j < columns; j++)
                            {
                                columnValues[j] = r[j].ToString();
                            }
                            table.AddRow(new Row(columnValues));
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
                r.Close();
            }
            else
            {
                dbCommand.ExecuteNonQuery();
            }
            return table;
        }

        public void Conncet()
        {
            dbCommand = new SQLiteCommand();
            dbConnector = new SQLiteConnection();

            if (!File.Exists(DbFileName))
            {
                SQLiteConnection.CreateFile(DbFileName);
                Console.WriteLine($"[Method] Connect(): File \"{DbFileName}\" was created.");
            }

            try
            {
                if (dbConnector.State != ConnectionState.Open)
                {
                    dbConnector = new SQLiteConnection($"Data source = {DbFileName};Version=3;");
                    dbConnector.Open();

                    dbCommand.Connection = dbConnector;

                    Console.WriteLine($"[Method] Connect(): Database \"{DbFileName}\" connected.");
                }
                else
                {
                    Console.WriteLine("[Method] Connect(): Database already connected!");
                }
            }
            catch (SQLiteException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[Method - SQLiteException] Connect(): " + ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
        public void Disconnect()
        {
            
        }
    }

}
