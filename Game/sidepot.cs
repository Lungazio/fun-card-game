using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Players;

namespace Poker.Game
{
    public class PotInfo
    {
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public List<int> EligiblePlayerIDs { get; set; }
        public decimal ContributionLevel { get; set; }
        
        public PotInfo()
        {
            EligiblePlayerIDs = new List<int>();
        }
        
        public override string ToString()
        {
            var playerIds = string.Join(", ", EligiblePlayerIDs);
            return $"{Name}: ${Amount:F2} (Eligible: [{playerIds}])";
        }
    }
    
    public class PlayerContribution
    {
        public int PlayerID { get; set; }
        public decimal TotalContributed { get; set; }  // Sum across ALL betting rounds
        public bool HasFolded { get; set; }
        
        public PlayerContribution(int playerId, decimal totalContributed, bool hasFolded)
        {
            PlayerID = playerId;
            TotalContributed = totalContributed;
            HasFolded = hasFolded;
        }
    }
    
    public class WorkingPlayer
    {
        public int ID { get; set; }
        public decimal RemainingContribution { get; set; }
        public bool HasFolded { get; set; }  // Track fold status in working player
    }
    
    public class WinnerResult
    {
        public int PlayerID { get; set; }
        public decimal Amount { get; set; }
        public string PotName { get; set; }
        
        public override string ToString()
        {
            return $"Player {PlayerID} wins ${Amount:F2} from {PotName}";
        }
    }
    
    public class SidePotCalculator
    {
        public static List<PotInfo> CalculateMainAndSidePots(List<Player> players)
        {
            var contributions = CreateContributionsFromPlayers(players);
            return CalculatePotsFromContributions(contributions);
        }
        
        public static List<PotInfo> CalculatePotsFromContributions(List<PlayerContribution> players)
        {
            var pots = new List<PotInfo>();
            var potSequence = 1;
            
            // Include ALL players with contributions (folded or not) for pot calculation
            // Folded players' money should be in the pot, they just can't win it
            var workingPlayers = players.Where(p => p.TotalContributed > 0)
                                       .Select(p => new WorkingPlayer 
                                       { 
                                           ID = p.PlayerID, 
                                           RemainingContribution = p.TotalContributed,
                                           HasFolded = p.HasFolded
                                       }).ToList();
            
            // Recursive layer peeling (danielpaz6 algorithm)
            while (workingPlayers.Count > 0)
            {
                // Step 2a: Find minimum contribution among remaining players
                decimal minContribution = workingPlayers.Min(p => p.RemainingContribution);
                
                if (minContribution <= 0)
                {
                    workingPlayers.RemoveAll(p => p.RemainingContribution <= 0);
                    continue;
                }
                
                // Step 2b: Extract equal layer from all players (including folded)
                // Pot amount includes contributions from ALL players (folded and active)
                decimal potAmount = minContribution * workingPlayers.Count;
                
                // Step 2c: Eligible players are only non-folded players
                var eligiblePlayerIDs = workingPlayers.Where(p => !p.HasFolded)
                                                     .Select(p => p.ID)
                                                     .ToList();
                
                // Step 2d: Create pot segment
                pots.Add(new PotInfo
                {
                    Name = pots.Count == 0 ? "Main Pot" : $"Side Pot {potSequence++}",
                    Amount = potAmount,  // Includes folded players' contributions
                    EligiblePlayerIDs = eligiblePlayerIDs,  // Excludes folded players from winning
                    ContributionLevel = minContribution
                });
                
                // Step 2e: Update remaining contributions for ALL players
                foreach (var player in workingPlayers)
                {
                    player.RemainingContribution -= minContribution;
                }
                
                // Step 2f: Remove players with no remaining contribution
                workingPlayers.RemoveAll(p => p.RemainingContribution <= 0);
            }
            
            return pots;
        }
        
        // Helper method to convert our Player objects to PlayerContribution format
        public static List<PlayerContribution> CreateContributionsFromPlayers(List<Player> players)
        {
            var contributions = new List<PlayerContribution>();
            
            foreach (var player in players)
            {
                contributions.Add(new PlayerContribution(
                    playerId: player.ID,
                    totalContributed: player.TotalBetThisHand,
                    hasFolded: player.IsFolded
                ));
            }
            
            return contributions;
        }
        
        // Helper method to get Player objects from IDs
        public static List<Player> GetPlayersFromIDs(List<int> playerIDs, List<Player> allPlayers)
        {
            return playerIDs.Select(id => allPlayers.First(p => p.ID == id)).ToList();
        }
        
        // Mathematical verification methods
        public static bool VerifyMoneyConservation(List<PlayerContribution> contributions, List<PotInfo> pots)
        {
            decimal totalContributed = contributions.Sum(p => p.TotalContributed);
            decimal totalInPots = pots.Sum(p => p.Amount);
            
            return Math.Abs(totalContributed - totalInPots) < 0.01m; // Allow for rounding
        }
        
        public static bool VerifyFairnessGuarantee(List<PlayerContribution> contributions, List<PotInfo> pots)
        {
            foreach (var player in contributions)
            {
                decimal playerTotal = player.TotalContributed;
                decimal eligiblePotSum = pots.Where(p => p.EligiblePlayerIDs.Contains(player.PlayerID))
                                             .Sum(p => CalculatePlayerPortionOfPot(player.PlayerID, p));
                
                if (Math.Abs(playerTotal - eligiblePotSum) > 0.01m)
                    return false;
            }
            
            return true;
        }
        
        private static decimal CalculatePlayerPortionOfPot(int playerId, PotInfo pot)
        {
            if (!pot.EligiblePlayerIDs.Contains(playerId))
                return 0;
                
            // Each eligible player contributed equally to this pot layer
            return pot.Amount / pot.EligiblePlayerIDs.Count;
        }
    }
}