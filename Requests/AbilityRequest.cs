namespace Poker.Requests
{
    public class AbilityRequest
    {
        public int PlayerId { get; set; }
        public string AbilityType { get; set; } = "";
        public int? TargetPlayerId { get; set; }
        public int? CardIndex { get; set; }
        public bool? RevealSuit { get; set; }
        
        // Properties for manifest completion
        public int? DiscardIndex { get; set; } // Which card to discard (0, 1, or 2)
        public int? DrawnCard { get; set; } // Rank of the drawn card (for verification)
        public string? DrawnCardSuit { get; set; } // Suit of the drawn card (for verification)
        
        // Properties for trashman completion
        public int? BurntCardIndex { get; set; } // Which burnt card to retrieve (0, 1, or 2)
        public int? HoleCardIndex { get; set; } // Which hole card to discard (0 or 1)
        public int? RetrievedCardIndex { get; set; } // For verification in final step
        
        // Legacy properties (can be removed eventually)
        public int? Rank { get; set; }
        public string? Suit { get; set; }
    }
}