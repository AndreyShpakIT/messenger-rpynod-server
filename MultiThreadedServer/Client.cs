namespace MultiThreadedServer
{
    struct Client
    {
        public string Username { get; set; }
        public string Key { get; set; }
        public string ID { get; set; }
        public string Salt { get; set; }
        public string HashPassword { get; set; }

        public override string ToString()
        {
            return string.Format("{0}({1}, {2})", 
                string.IsNullOrEmpty(Username) ? "Unknown" : Username,
                string.IsNullOrEmpty(ID) ? "?" : ID,
                string.IsNullOrEmpty(Key) ? "?" : Key);
        }
    }
}
