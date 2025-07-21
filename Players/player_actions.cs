using System;
using System.Collections.Generic;
using Poker.Power;

namespace Poker.Players
{
    public class PlayerActions
    {
        private Player _player;
        private Funds _funds;
        
        public bool IsFolded { get; private set; }
        public bool IsAllIn { get; private set; }
        public decimal CurrentBet { get; private set; }
        public decimal TotalBetThisHand { get; private set; }
        
        public PlayerActions(Player player, Funds funds)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _funds = funds ?? throw new ArgumentNullException(nameof(funds));
            ResetForNewHand();
        }
        
        public ActionResult Call(decimal amount)
        {
            if (!_player.IsMyTurn)
                return new ActionResult(ActionType.Call, false, "Not your turn");
                
            if (!CanAct())
                return new ActionResult(ActionType.Call, false, "Player cannot act");
                
            if (amount <= 0)
                return new ActionResult(ActionType.Call, false, "Call amount must be positive");
            
            if (!_funds.CanAfford(amount))
            {
                // Force all-in if insufficient funds
                return ExecuteAllIn();
            }
            
            _funds.TryDeduct(amount);
            CurrentBet += amount;
            TotalBetThisHand += amount;
            
            // NEW: Check if this call used up all remaining funds
            if (_funds.CurrentBalance == 0)
            {
                IsAllIn = true;
                return new ActionResult(ActionType.Call, true, $"Called ${amount:F2} (All-in)", amount);
            }
            
            return new ActionResult(ActionType.Call, true, $"Called ${amount:F2}", amount);
        }
        
        public ActionResult Check()
        {
            if (!_player.IsMyTurn)
                return new ActionResult(ActionType.Check, false, "Not your turn");
                
            if (!CanAct())
                return new ActionResult(ActionType.Check, false, "Player cannot act");
            
            return new ActionResult(ActionType.Check, true, "Checked");
        }
        
        public ActionResult Fold()
        {
            if (!_player.IsMyTurn)
                return new ActionResult(ActionType.Fold, false, "Not your turn");
                
            if (IsFolded)
                return new ActionResult(ActionType.Fold, false, "Player already folded");
                
            IsFolded = true;
            return new ActionResult(ActionType.Fold, true, "Folded");
        }
        
        public ActionResult Raise(decimal amount)
        {
            if (!_player.IsMyTurn)
                return new ActionResult(ActionType.Raise, false, "Not your turn");
                
            if (!CanAct())
                return new ActionResult(ActionType.Raise, false, "Player cannot act");
                
            if (amount <= 0)
                return new ActionResult(ActionType.Raise, false, "Raise amount must be positive");
            
            if (!_funds.CanAfford(amount))
                return new ActionResult(ActionType.Raise, false, "Insufficient funds");
            
            _funds.TryDeduct(amount);
            CurrentBet += amount;
            TotalBetThisHand += amount;
            
            // NEW: Check if this raise used up all remaining funds
            if (_funds.CurrentBalance == 0)
            {
                IsAllIn = true;
                return new ActionResult(ActionType.Raise, true, $"Raised ${amount:F2} (All-in)", amount);
            }
            
            return new ActionResult(ActionType.Raise, true, $"Raised ${amount:F2}", amount);
        }
        
        public ActionResult AllIn()
        {
            if (!_player.IsMyTurn)
                return new ActionResult(ActionType.AllIn, false, "Not your turn");
                
            if (!CanAct())
                return new ActionResult(ActionType.AllIn, false, "Player cannot act");
                
            return ExecuteAllIn();
        }
        
        // NEW: Ability action
        public ActionResult UseAbility()
        {
            if (!_player.IsMyTurn)
                return new ActionResult(ActionType.UseAbility, false, "Not your turn");
                
            if (!CanAct())
                return new ActionResult(ActionType.UseAbility, false, "Player cannot act");
            
            // Check if player has any abilities
            if (_player.AbilitySlot.IsEmpty)
                return new ActionResult(ActionType.UseAbility, false, "No abilities available");
            
            // Return success - this triggers the ability selection UI
            return new ActionResult(ActionType.UseAbility, true, $"{_player.Name} wants to use an ability");
        }
        
        private ActionResult ExecuteAllIn()
        {
            var amount = _funds.CurrentBalance;
            
            if (amount <= 0)
                return new ActionResult(ActionType.AllIn, false, "No funds available");
            
            _funds.ForceDeduct(amount);
            CurrentBet += amount;
            TotalBetThisHand += amount;
            IsAllIn = true;
            
            return new ActionResult(ActionType.AllIn, true, $"All-in for ${amount:F2}", amount);
        }
        
        public void ResetForNewHand()
        {
            IsFolded = false;
            IsAllIn = false;
            CurrentBet = 0;
            TotalBetThisHand = 0;
        }
        
        public void ResetForNewBettingRound()
        {
            CurrentBet = 0;
            // Keep TotalBetThisHand, IsFolded, and IsAllIn
        }
        
        public bool CanAct()
        {
            return !IsFolded && !IsAllIn;
        }
        
        public List<ActionType> GetValidActions(decimal currentBet, decimal minimumRaise)
        {
            var validActions = new List<ActionType>();
            
            // Player can't act if not their turn or can't act
            if (!_player.IsMyTurn || !CanAct())
                return validActions; // Return empty list
            
            // Fold is always available (unless already folded)
            validActions.Add(ActionType.Fold);
            
            // Determine if there's a bet to call
            var amountToCall = Math.Max(0, currentBet - CurrentBet);
            
            if (amountToCall == 0)
            {
                // No bet to call - can check
                validActions.Add(ActionType.Check);
            }
            else
            {
                // There's a bet to call
                if (_funds.CanAfford(amountToCall))
                {
                    validActions.Add(ActionType.Call);
                }
            }
            
            // Check if player can raise
            var totalRaiseAmount = currentBet + minimumRaise - CurrentBet;
            if (_funds.CanAfford(totalRaiseAmount))
            {
                validActions.Add(ActionType.Raise);
            }
            
            // All-in is available if player has any money
            if (_funds.CurrentBalance > 0)
            {
                validActions.Add(ActionType.AllIn);
            }
            
            // NEW: Ability action is available if player has abilities
            if (!_player.AbilitySlot.IsEmpty)
            {
                validActions.Add(ActionType.UseAbility);
            }
            
            return validActions;
        }
        
        public override string ToString()
        {
            if (IsFolded) return "FOLDED";
            if (IsAllIn) return $"ALL-IN (${TotalBetThisHand:F2})";
            return $"Active (Bet: ${CurrentBet:F2})";
        }
        
        // Internal methods for Player class to update state directly (for blinds)
        internal void AddToBet(decimal amount)
        {
            CurrentBet += amount;
            TotalBetThisHand += amount;
        }
        
        internal void SetAllIn()
        {
            IsAllIn = true;
        }
    }
    
    public enum ActionType
    {
        Call,
        Check,
        Fold,
        Raise,
        AllIn,
        SmallBlind,
        BigBlind,
        UseAbility,
        Cancel
    }
    
    public class ActionResult
    {
        public ActionType Action { get; private set; }
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public decimal Amount { get; private set; }
        public bool IsCancelled { get; private set; }
        
        public ActionResult(ActionType action, bool success, string message, decimal amount = 0, bool isCancelled = false)
        {
            Action = action;
            Success = success;
            Message = message ?? "";
            Amount = amount;
            IsCancelled = isCancelled;
        }
        
        // Static factory method for cancelled results
        public static ActionResult Cancelled(string message = "Action cancelled")
        {
            return new ActionResult(ActionType.Cancel, true, message, 0, true);
        }
        
        public override string ToString()
        {
            if (IsCancelled)
                return $"Cancelled: {Message}";
            
            if (Amount > 0)
                return $"{Action}: {Message} (${Amount:F2})";
            else
                return $"{Action}: {Message}";
        }
    }
}