using System;
using System.Collections.Generic;
using System.Linq;

namespace Poker.Game
{
    public class Pot
    {
        private decimal _totalAmount;
        private Dictionary<string, decimal> _currentContributions;
        
        public decimal TotalAmount => _totalAmount;
        public IReadOnlyDictionary<string, decimal> CurrentContributions => _currentContributions.AsReadOnly();
        
        public Pot()
        {
            _totalAmount = 0;
            _currentContributions = new Dictionary<string, decimal>();
        }
        
        public void AddToPot(decimal amount, string playerName = "")
        {
            if (amount < 0)
                throw new ArgumentException("Amount cannot be negative");
                
            if (amount == 0)
                return; // No point adding zero
                
            _totalAmount += amount;
            
            if (!string.IsNullOrEmpty(playerName))
            {
                if (_currentContributions.ContainsKey(playerName))
                    _currentContributions[playerName] += amount;
                else
                    _currentContributions[playerName] = amount;
            }
        }
        
        public decimal TakeWinnings()
        {
            var winnings = _totalAmount;
            Reset();
            return winnings;
        }
        
        public void Reset()
        {
            _totalAmount = 0;
            _currentContributions.Clear();
        }
        
        public decimal GetPlayerContribution(string playerName)
        {
            return _currentContributions.ContainsKey(playerName) ? _currentContributions[playerName] : 0;
        }
        
        public override string ToString()
        {
            if (_totalAmount == 0)
                return "Pot: Empty";
                
            var contributionInfo = "";
            if (_currentContributions.Count > 0)
            {
                var contributions = _currentContributions.Select(kvp => $"{kvp.Key}: ${kvp.Value:F2}");
                contributionInfo = $" ({string.Join(", ", contributions)})";
            }
            
            return $"Pot: ${_totalAmount:F2}{contributionInfo}";
        }
    }
}