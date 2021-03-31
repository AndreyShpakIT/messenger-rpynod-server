namespace MultiThreadedServer
{
    struct Client
    {
        public string Username { get; set; }
        public string Key { get; set; }
        public string ID { get; set; }
        public string Salt { get; set; }
        public string HashPassword { get; set; }
    }
}
