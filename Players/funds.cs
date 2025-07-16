using System;

namespace Poker.Players
{
    public class Funds
    {
        private decimal _currentBalance;
        
        public decimal CurrentBalance => _currentBalance;
        public bool IsBankrupt => _currentBalance <= 0;
        
        public Funds(decimal startingBalance)
        {
            if (startingBalance < 0)
                throw new ArgumentException("Starting balance cannot be negative");
                
            _currentBalance = startingBalance;
        }
        
        public bool CanAfford(decimal amount)
        {
            return _currentBalance >= amount;
        }
        
        public bool TryDeduct(decimal amount)
        {
            if (amount < 0)
                throw new ArgumentException("Amount to deduct cannot be negative");
                
            if (!CanAfford(amount))
                return false;
                
            _currentBalance -= amount;
            return true;
        }
        
        public void ForceDeduct(decimal amount)
        {
            if (amount < 0)
                throw new ArgumentException("Amount to deduct cannot be negative");
                
            _currentBalance -= amount;
        }
        
        public void Add(decimal amount)
        {
            if (amount < 0)
                throw new ArgumentException("Amount to add cannot be negative");
                
            _currentBalance += amount;
        }
        
        public void Reset(decimal newBalance)
        {
            if (newBalance < 0)
                throw new ArgumentException("New balance cannot be negative");
                
            _currentBalance = newBalance;
        }
        
        public override string ToString()
        {
            var status = IsBankrupt ? "BANKRUPT" : "Active";
            return $"Balance: ${_currentBalance:F2} - {status}";
        }
    }
}