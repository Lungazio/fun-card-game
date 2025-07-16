using System.Collections.Generic;
using Poker.Core;

namespace Poker.Evaluation
{
    public class HandResult
    {
        public HandRank Rank { get; private set; }
        public double Score { get; private set; }
        public List<Card> BestFiveCards { get; private set; }
        
        public HandResult(HandRank rank, double score, List<Card> bestFiveCards)
        {
            Rank = rank;
            Score = score;
            BestFiveCards = bestFiveCards;
        }
        
        public override string ToString()
        {
            return $"{Rank} (Score: {Score:F2}) - {string.Join(", ", BestFiveCards)}";
        }
    }
}