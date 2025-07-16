using System;

namespace Poker.Core
{
    public class Card
    {
        public int Rank { get; private set; }     // 2-14 (where 14 = Ace)
        public string Suit { get; private set; }  // "Hearts", "Clubs", "Spades", "Diamonds"
        
        public Card(int rank, string suit)
        {
            if (rank < 2 || rank > 14)
                throw new ArgumentException("Rank must be between 2 and 14");
                
            if (!IsValidSuit(suit))
                throw new ArgumentException("Invalid suit");
                
            Rank = rank;
            Suit = suit;
        }
        
        private bool IsValidSuit(string suit)
        {
            return suit == "Hearts" || suit == "Clubs" || suit == "Spades" || suit == "Diamonds";
        }
        
        public string GetRankName()
        {
            return Rank switch
            {
                11 => "Jack",
                12 => "Queen", 
                13 => "King",
                14 => "Ace",
                _ => Rank.ToString()
            };
        }
        
        public override string ToString()
        {
            return $"{GetRankName()} of {Suit}";
        }
        
        public override bool Equals(object obj)
        {
            if (obj is Card other)
                return Rank == other.Rank && Suit == other.Suit;
            return false;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, Suit);
        }
    }
}