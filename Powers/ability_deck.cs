using System;
using System.Collections.Generic;
using System.Linq;

namespace Poker.Power
{
    public class AbilityDeck
    {
        private List<Ability> _abilities;
        private Random _random;
        private int _nextAbilityId;
        
        public int RemainingAbilities => _abilities.Count;
        public bool IsEmpty => _abilities.Count == 0;
        
        public AbilityDeck(int playerCount, int? seed = null)
        {
            if (playerCount < 2)
                throw new ArgumentException("Need at least 2 players");
                
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _nextAbilityId = 1;
            _abilities = new List<Ability>();
            
            CreateAbilities(playerCount);
            Shuffle();
        }
        
        private void CreateAbilities(int playerCount)
        {
            int totalAbilities = playerCount * 2;
            
            // Calculate distribution based on ratios (now with 7 abilities, Chaos being rarest)
            int peekCount = (int)Math.Round(totalAbilities * 0.26);      // 26% (slightly reduced)
            int burnCount = (int)Math.Round(totalAbilities * 0.21);      // 21% (slightly reduced)
            int manifestCount = (int)Math.Round(totalAbilities * 0.17);  // 17% (slightly reduced)
            int trashmanCount = (int)Math.Round(totalAbilities * 0.14);  // 14% (slightly reduced)
            int deadmanCount = (int)Math.Round(totalAbilities * 0.10);   // 10% (slightly reduced)
            int yoinkCount = (int)Math.Round(totalAbilities * 0.07);     // 7% (NEW - uncommon ability)
            int chaosCount = (int)Math.Round(totalAbilities * 0.05);     // 5% (rarest ability)
            
            // Adjust for rounding discrepancies to ensure exact count
            int actualTotal = peekCount + burnCount + manifestCount + trashmanCount + deadmanCount + yoinkCount + chaosCount;
            int difference = totalAbilities - actualTotal;
            
            // Add the difference to the largest category (Peek)
            if (difference != 0)
            {
                peekCount += difference;
            }
            
            // Ensure at least 1 chaos ability if we have enough total abilities
            if (chaosCount == 0 && totalAbilities >= 7)
            {
                chaosCount = 1;
                peekCount -= 1; // Take one from peek to maintain total
            }
            
            // Ensure at least 1 yoink ability if we have enough total abilities
            if (yoinkCount == 0 && totalAbilities >= 6)
            {
                yoinkCount = 1;
                peekCount -= 1; // Take one from peek to maintain total
            }
            
            // Create abilities
            for (int i = 0; i < peekCount; i++)
            {
                _abilities.Add(new PeekAbility(_nextAbilityId++));
            }
            
            for (int i = 0; i < burnCount; i++)
            {
                _abilities.Add(new BurnAbility(_nextAbilityId++));
            }
            
            for (int i = 0; i < manifestCount; i++)
            {
                _abilities.Add(new ManifestAbility(_nextAbilityId++));
            }
            
            for (int i = 0; i < trashmanCount; i++)
            {
                _abilities.Add(new TrashmanAbility(_nextAbilityId++));
            }
            
            for (int i = 0; i < deadmanCount; i++)
            {
                _abilities.Add(new DeadmanAbility(_nextAbilityId++));
            }
            
            // NEW: Create Yoink abilities (uncommon)
            for (int i = 0; i < yoinkCount; i++)
            {
                _abilities.Add(new YoinkAbility(_nextAbilityId++));
            }
            
            // NEW: Create Chaos abilities (rarest)
            for (int i = 0; i < chaosCount; i++)
            {
                _abilities.Add(new ChaosAbility(_nextAbilityId++));
            }
        }
        
        public void Shuffle()
        {
            // Fisher-Yates shuffle for proper randomization
            for (int i = _abilities.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_abilities[i], _abilities[j]) = (_abilities[j], _abilities[i]);
            }
        }
        
        public Ability DealAbility()
        {
            if (_abilities.Count == 0)
                throw new InvalidOperationException("Cannot deal from empty ability deck");
                
            Ability ability = _abilities[_abilities.Count - 1];
            _abilities.RemoveAt(_abilities.Count - 1);
            return ability;
        }
        
        public List<Ability> DealAbilities(int count)
        {
            if (count > _abilities.Count)
                throw new InvalidOperationException($"Cannot deal {count} abilities, only {_abilities.Count} remaining");
                
            List<Ability> dealtAbilities = new List<Ability>();
            for (int i = 0; i < count; i++)
            {
                dealtAbilities.Add(DealAbility());
            }
            return dealtAbilities;
        }
        
        public Ability PeekTopAbility()
        {
            if (_abilities.Count == 0)
                throw new InvalidOperationException("Cannot peek at empty ability deck");
                
            return _abilities[_abilities.Count - 1];
        }
        
        // Multiple shuffles for maximum randomization
        public void ShuffleThoroughly()
        {
            // Shuffle multiple times to ensure maximum randomization
            for (int shuffleRound = 0; shuffleRound < 3; shuffleRound++)
            {
                Shuffle();
            }
        }
        
        // Deal abilities randomly to players (not in order)
        public void DealToPlayers(List<Players.Player> players)
        {
            if (players == null || players.Count == 0)
                throw new ArgumentException("No players provided");
                
            if (_abilities.Count < players.Count * 2)
                throw new InvalidOperationException("Not enough abilities for all players");
            
            // Shuffle thoroughly before dealing
            ShuffleThoroughly();
            
            // Deal 2 abilities to each player
            foreach (var player in players)
            {
                for (int i = 0; i < 2; i++)
                {
                    var ability = DealAbility();
                    player.AddAbility(ability);
                }
            }
        }
        
        // Get distribution info for debugging/display
        public AbilityDistribution GetDistribution()
        {
            var distribution = new AbilityDistribution();
            
            foreach (var ability in _abilities)
            {
                switch (ability.Type)
                {
                    case AbilityType.Peek:
                        distribution.PeekCount++;
                        break;
                    case AbilityType.Burn:
                        distribution.BurnCount++;
                        break;
                    case AbilityType.Manifest:
                        distribution.ManifestCount++;
                        break;
                    case AbilityType.Trashman:
                        distribution.TrashmanCount++;
                        break;
                    case AbilityType.Deadman:
                        distribution.DeadmanCount++;
                        break;
                    case AbilityType.Yoink:
                        distribution.YoinkCount++;
                        break;
                    case AbilityType.Chaos:
                        distribution.ChaosCount++;
                        break;
                }
            }
            
            return distribution;
        }
        
        public void Reset(int playerCount)
        {
            _abilities.Clear();
            _nextAbilityId = 1;
            CreateAbilities(playerCount);
            ShuffleThoroughly(); // Use thorough shuffle on reset
        }
        
        public override string ToString()
        {
            if (IsEmpty)
                return "Ability Deck: Empty";
                
            var dist = GetDistribution();
            return $"Ability Deck: {RemainingAbilities} abilities (Peek: {dist.PeekCount}, Burn: {dist.BurnCount}, Manifest: {dist.ManifestCount}, Trashman: {dist.TrashmanCount}, Deadman: {dist.DeadmanCount}, Yoink: {dist.YoinkCount}, Chaos: {dist.ChaosCount})";
        }
        
        // Debug method to see current deck state
        public List<Ability> GetDeckState()
        {
            return new List<Ability>(_abilities);
        }
    }
    
    public class AbilityDistribution
    {
        public int PeekCount { get; set; }
        public int BurnCount { get; set; }
        public int ManifestCount { get; set; }
        public int TrashmanCount { get; set; }
        public int DeadmanCount { get; set; }
        public int YoinkCount { get; set; } // NEW: Added Yoink count
        public int ChaosCount { get; set; } // NEW: Added Chaos count
        
        public int TotalCount => PeekCount + BurnCount + ManifestCount + TrashmanCount + DeadmanCount + YoinkCount + ChaosCount;
        
        public override string ToString()
        {
            return $"Peek: {PeekCount}, Burn: {BurnCount}, Manifest: {ManifestCount}, Trashman: {TrashmanCount}, Deadman: {DeadmanCount}, Yoink: {YoinkCount}, Chaos: {ChaosCount} (Total: {TotalCount})";
        }
    }
}