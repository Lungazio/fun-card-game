using System.Collections.Generic;
using Poker.Core;

namespace Poker.Evaluation
{
    public class PreEvaluationResult
    {
        public List<Card> SortedCards { get; set; }
        public Dictionary<int, int> RankCounts { get; set; }
        public List<KeyValuePair<int, int>> Duplicates { get; set; }
        public bool IsFlush { get; set; }
        public List<Card> FlushCards { get; set; }
        public bool IsStraight { get; set; }
        public int StraightHighCard { get; set; }
    }
}