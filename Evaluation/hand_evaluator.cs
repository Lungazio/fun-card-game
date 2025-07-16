using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Core;

namespace Poker.Evaluation
{
    public class HandEvaluator
    {
        public HandResult EvaluateHand(List<Card> playerCards, Board board)
        {
            if (playerCards == null)
                throw new ArgumentNullException(nameof(playerCards));
            if (board == null)
                throw new ArgumentNullException(nameof(board));
                
            // Combine player cards with community cards
            var allCards = new List<Card>(playerCards);
            allCards.AddRange(board.CommunityCards);
            
            if (allCards.Count < 5)
            {
                // Not enough cards for full evaluation, evaluate what we have
                return EvaluateBestHand(allCards);
            }
            else if (allCards.Count == 5)
            {
                // Exactly 5 cards, evaluate directly
                return EvaluateBestHand(allCards);
            }
            else
            {
                // More than 5 cards, find best 5-card combination
                return FindBestFiveCardHand(allCards);
            }
        }
        
        private HandResult FindBestFiveCardHand(List<Card> allCards)
        {
            var combinations = GetFiveCardCombinations(allCards);
            HandResult bestHand = null;
            double bestScore = -1;
            
            foreach (var combination in combinations)
            {
                var result = EvaluateBestHand(combination);
                if (result.Score > bestScore)
                {
                    bestScore = result.Score;
                    bestHand = result;
                }
            }
            
            return bestHand;
        }
        
        private List<List<Card>> GetFiveCardCombinations(List<Card> cards)
        {
            var combinations = new List<List<Card>>();
            GenerateCombinations(cards, 5, 0, new List<Card>(), combinations);
            return combinations;
        }
        
        private void GenerateCombinations(List<Card> cards, int k, int start, List<Card> current, List<List<Card>> result)
        {
            if (current.Count == k)
            {
                result.Add(new List<Card>(current));
                return;
            }
            
            for (int i = start; i < cards.Count; i++)
            {
                current.Add(cards[i]);
                GenerateCombinations(cards, k, i + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }
        
        private HandResult EvaluateBestHand(List<Card> cards)
        {
            // Pre-evaluation: analyze card properties
            var preEval = PreEvaluateCards(cards);
            
            // Determine hand rank and calculate score using danielpaz6's method
            return DetermineHandRank(cards, preEval);
        }
        
        private PreEvaluationResult PreEvaluateCards(List<Card> cards)
        {
            var result = new PreEvaluationResult();
            
            // Sort cards by rank (descending)
            var sortedCards = cards.OrderByDescending(c => c.Rank).ToList();
            result.SortedCards = sortedCards;
            
            // Count duplicates (pairs, trips, quads)
            var rankCounts = cards.GroupBy(c => c.Rank)
                                 .ToDictionary(g => g.Key, g => g.Count());
            result.RankCounts = rankCounts;
            result.Duplicates = rankCounts.Where(kvp => kvp.Value > 1)
                                         .OrderByDescending(kvp => kvp.Value)
                                         .ThenByDescending(kvp => kvp.Key)
                                         .ToList();
            
            // Check for flush
            var suitCounts = cards.GroupBy(c => c.Suit)
                                 .ToDictionary(g => g.Key, g => g.ToList());
            var flushSuit = suitCounts.FirstOrDefault(kvp => kvp.Value.Count >= 5);
            if (flushSuit.Key != null)
            {
                result.IsFlush = true;
                result.FlushCards = flushSuit.Value.OrderByDescending(c => c.Rank).ToList();
            }
            
            // Check for straight
            result.StraightHighCard = CheckForStraight(sortedCards);
            result.IsStraight = result.StraightHighCard > 0;
            
            return result;
        }
        
        private int CheckForStraight(List<Card> sortedCards)
        {
            var uniqueRanks = sortedCards.Select(c => c.Rank).Distinct().OrderByDescending(r => r).ToList();
            
            // Check for Ace-low straight (A,2,3,4,5)
            if (uniqueRanks.Contains(14) && uniqueRanks.Contains(2) && uniqueRanks.Contains(3) && 
                uniqueRanks.Contains(4) && uniqueRanks.Contains(5))
            {
                return 5; // 5-high straight
            }
            
            // Check for regular straights
            for (int i = 0; i <= uniqueRanks.Count - 5; i++)
            {
                bool isStraight = true;
                for (int j = 0; j < 4; j++)
                {
                    if (uniqueRanks[i + j] - uniqueRanks[i + j + 1] != 1)
                    {
                        isStraight = false;
                        break;
                    }
                }
                if (isStraight)
                {
                    return uniqueRanks[i]; // Return highest card in straight
                }
            }
            
            return 0; // No straight
        }
        
        private HandResult DetermineHandRank(List<Card> cards, PreEvaluationResult preEval)
        {
            // Check hand ranks from highest to lowest (danielpaz6's approach)
            
            // Royal Flush (900+ points)
            if (preEval.IsFlush && preEval.IsStraight && preEval.StraightHighCard == 14)
            {
                return new HandResult(HandRank.RoyalFlush, 900 + preEval.StraightHighCard / 14.0 * 99, 
                                     preEval.FlushCards.Take(5).ToList());
            }
            
            // Straight Flush (800+ points)
            if (preEval.IsFlush && preEval.IsStraight)
            {
                return new HandResult(HandRank.StraightFlush, 800 + preEval.StraightHighCard / 14.0 * 99,
                                     preEval.FlushCards.Take(5).ToList());
            }
            
            // Four of a Kind (700+ points)
            var fourOfAKind = preEval.Duplicates.FirstOrDefault(d => d.Value == 4);
            if (fourOfAKind.Key != 0)
            {
                double score = 700 + fourOfAKind.Key / 14.0 * 50 + 
                              EvaluateRankByHighestCards(preEval.SortedCards, fourOfAKind.Key, -1, 1, 49);
                return new HandResult(HandRank.FourOfAKind, score, preEval.SortedCards.Take(5).ToList());
            }
            
            // Full House (600+ points)
            var threeOfAKind = preEval.Duplicates.FirstOrDefault(d => d.Value == 3);
            var pair = preEval.Duplicates.FirstOrDefault(d => d.Value == 2);
            if (threeOfAKind.Key != 0 && pair.Key != 0)
            {
                double score = 600 + threeOfAKind.Key / 14.0 * 50 + pair.Key / 14.0 * 49;
                return new HandResult(HandRank.FullHouse, score, preEval.SortedCards.Take(5).ToList());
            }
            
            // Flush (500+ points)
            if (preEval.IsFlush)
            {
                double score = 500 + EvaluateRankByHighestCards(preEval.FlushCards.Take(5).ToList());
                return new HandResult(HandRank.Flush, score, preEval.FlushCards.Take(5).ToList());
            }
            
            // Straight (400+ points)
            if (preEval.IsStraight)
            {
                double score = 400 + preEval.StraightHighCard / 14.0 * 99;
                return new HandResult(HandRank.Straight, score, preEval.SortedCards.Take(5).ToList());
            }
            
            // Three of a Kind (300+ points)
            if (threeOfAKind.Key != 0)
            {
                double score = 300 + threeOfAKind.Key / 14.0 * 50 + 
                              EvaluateRankByHighestCards(preEval.SortedCards, threeOfAKind.Key, -1, 2, 49);
                return new HandResult(HandRank.ThreeOfAKind, score, preEval.SortedCards.Take(5).ToList());
            }
            
            // Two Pair (200+ points)
            var pairs = preEval.Duplicates.Where(d => d.Value == 2).Take(2).ToList();
            if (pairs.Count == 2)
            {
                var highPair = pairs[0].Key;
                var lowPair = pairs[1].Key;
                double score = 200 + highPair / 14.0 * 50 + lowPair / 14.0 * 49 +
                              EvaluateRankByHighestCards(preEval.SortedCards, highPair, lowPair, 1, 48);
                return new HandResult(HandRank.TwoPair, score, preEval.SortedCards.Take(5).ToList());
            }
            
            // One Pair (100+ points)
            if (pair.Key != 0)
            {
                double score = 100 + pair.Key / 14.0 * 50 + 
                              EvaluateRankByHighestCards(preEval.SortedCards, pair.Key, -1, 3, 49);
                return new HandResult(HandRank.OnePair, score, preEval.SortedCards.Take(5).ToList());
            }
            
            // High Card (0+ points)
            double highCardScore = EvaluateRankByHighestCards(preEval.SortedCards);
            return new HandResult(HandRank.HighCard, highCardScore, preEval.SortedCards.Take(5).ToList());
        }
        
        // danielpaz6's base-13 evaluation system
        private double EvaluateRankByHighestCards(List<Card> cards, int excludeCardValue = -1, 
                                                 int excludeCardValue2 = -1, int limitCheck = 5, double normalize = 99)
        {
            var eligibleCards = cards.Where(c => c.Rank != excludeCardValue && c.Rank != excludeCardValue2)
                                    .Take(limitCheck)
                                    .Select(c => c.Rank - 2) // Normalize to 0-12 range
                                    .ToList();
            
            double score = 0;
            for (int i = 0; i < eligibleCards.Count; i++)
            {
                score += eligibleCards[i] * Math.Pow(13, eligibleCards.Count - 1 - i);
            }
            
            return score / Math.Pow(13, 5) * normalize;
        }
    }
}