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

    struct ResetingClient
    {
        public string Username { get; set; }
        public string Id { get; set; }
        public string Email { get; set; }
        public string Code { get; set; }

        public ResetingClient(string username, string email, string id, string code)
        {
            Username = username;
            Email = email;
            Id = id;
            Code = code;
        }
    }
}
