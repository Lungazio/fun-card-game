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
            
            // Calculate distribution based on ratios (now with 5 abilities)
            int peekCount = (int)Math.Round(totalAbilities * 0.30);      // 30% (reduced)
            int burnCount = (int)Math.Round(totalAbilities * 0.25);      // 25% (reduced)
            int manifestCount = (int)Math.Round(totalAbilities * 0.20);  // 20% (reduced)
            int trashmanCount = (int)Math.Round(totalAbilities * 0.15);  // 15% (same)
            int deadmanCount = (int)Math.Round(totalAbilities * 0.10);   // 10% (new ability)
            
            // Adjust for rounding discrepancies to ensure exact count
            int actualTotal = peekCount + burnCount + manifestCount + trashmanCount + deadmanCount;
            int difference = totalAbilities - actualTotal;
            
            // Add the difference to the largest category (Peek)
            if (difference != 0)
            {
                peekCount += difference;
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
            
            // NEW: Create Deadman abilities
            for (int i = 0; i < deadmanCount; i++)
            {
                _abilities.Add(new DeadmanAbility(_nextAbilityId++));
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
            return $"Ability Deck: {RemainingAbilities} abilities (Peek: {dist.PeekCount}, Burn: {dist.BurnCount}, Manifest: {dist.ManifestCount}, Trashman: {dist.TrashmanCount})";
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
        
        public int TotalCount => PeekCount + BurnCount + ManifestCount + TrashmanCount + DeadmanCount;
        
        public override string ToString()
        {
            return $"Peek: {PeekCount}, Burn: {BurnCount}, Manifest: {ManifestCount}, Trashman: {TrashmanCount}, Deadman: {DeadmanCount} (Total: {TotalCount})";
        }
    }
}