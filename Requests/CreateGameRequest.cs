namespace Poker.Requests
{
    public class CreateGameRequest
    {
        public List<PlayerRequest> Players { get; set; } = new();
        public decimal SmallBlind { get; set; }
        public decimal BigBlind { get; set; }
    }
}