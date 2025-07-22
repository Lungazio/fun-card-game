using Poker.Players;

namespace Poker.Requests
{
    public class ActionRequest
    {
        public int PlayerId { get; set; }
        public ActionType ActionType { get; set; }
        public decimal Amount { get; set; }
    }
}