namespace Poker.Requests
{
    public class PlayerRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal StartingFunds { get; set; }
    }
}