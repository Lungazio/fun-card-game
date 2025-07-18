using System;
using System.Collections.Generic;
using Poker.Core;

namespace Poker.Players
{
    public class Player
    {
        private Funds _funds;
        private Hand _hand;
        private PlayerActions _actions;
        
        public int ID { get; private set; }
        public string Name { get; private set; }
        public bool IsMyTurn { get; private set; } = false;
        public decimal CurrentBalance => _funds.CurrentBalance;
        public bool IsBankrupt => _funds.IsBankrupt;
        public IReadOnlyList<Card> HoleCards => _hand.Cards;
        
        // Expose action state
        public bool IsFolded => _actions.IsFolded;
        public bool IsAllIn => _actions.IsAllIn;
        public decimal CurrentBet => _actions.CurrentBet;
        public decimal TotalBetThisHand => _actions.TotalBetThisHand;
        
        // Betting round tracking
        public bool HasActedThisRound { get; set; } = false;
        
        public Player(int id, string name, decimal startingFunds)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Player name cannot be empty");
                
            ID = id;
            Name = name;
            _funds = new Funds(startingFunds);
            _hand = new Hand();
            _actions = new PlayerActions(this, _funds);
        }
        
        public void SetTurn(bool isMyTurn)
        {
            IsMyTurn = isMyTurn;
        }
        
        public void AddHoleCard(Card card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
                
            if (_hand.Count >= 2)
                throw new InvalidOperationException("Player already has 2 hole cards");
                
            _hand.AddCard(card);
        }
        
        public void ClearHoleCards()
        {
            _hand.Clear();
        }
        
        public bool HasHoleCards()
        {
            return _hand.Count > 0;
        }
        
        public List<Card> GetHoleCardsList()
        {
            return _hand.ToList();
        }
        
        // Action methods - delegate to PlayerActions and set flags
        public ActionResult Call(decimal amount)
        {
            var result = _actions.Call(amount);
            if (result.Success)
                HasActedThisRound = true;
            return result;
        }
        
        public ActionResult Check()
        {
            var result = _actions.Check();
            if (result.Success)
                HasActedThisRound = true;
            return result;
        }
        
        public ActionResult Fold()
        {
            var result = _actions.Fold();
            if (result.Success)
                HasActedThisRound = true;
            return result;
        }
        
        public ActionResult Raise(decimal amount)
        {
            var result = _actions.Raise(amount);
            if (result.Success)
                HasActedThisRound = true;
            return result;
        }
        
        public ActionResult AllIn()
        {
            var result = _actions.AllIn();
            if (result.Success)
                HasActedThisRound = true;
            return result;
        }
        
        // Game management methods
        public void ResetForNewHand()
        {
            _actions.ResetForNewHand();
            ClearHoleCards();
            HasActedThisRound = false;
        }
        
        public void ResetForNewBettingRound()
        {
            _actions.ResetForNewBettingRound();
            HasActedThisRound = false;
        }
        
        public void ResetActionFlag()
        {
            HasActedThisRound = false;
        }
        
        public bool CanAct()
        {
            return _actions.CanAct();
        }
        
        public void AddFunds(decimal amount)
        {
            _funds.Add(amount);
        }
        
        public List<ActionType> GetValidActions(decimal currentBet, decimal minimumRaise)
        {
            return _actions.GetValidActions(currentBet, minimumRaise);
        }
        
        // Blind methods - these are forced and don't require turn validation
        public ActionResult PostSmallBlind(decimal amount)
        {
            if (amount <= 0)
                return new ActionResult(ActionType.SmallBlind, false, "Small blind amount must be positive");
            
            if (!_funds.CanAfford(amount))
            {
                // All-in for small blind
                var allInAmount = _funds.CurrentBalance;
                if (allInAmount <= 0)
                    return new ActionResult(ActionType.SmallBlind, false, "Player has no funds for small blind");
                
                _funds.ForceDeduct(allInAmount);
                _actions.AddToBet(allInAmount);
                _actions.SetAllIn();
                
                return new ActionResult(ActionType.SmallBlind, true, $"Posted small blind (all-in) ${allInAmount:F2}", allInAmount);
            }
            
            _funds.TryDeduct(amount);
            _actions.AddToBet(amount);
            
            return new ActionResult(ActionType.SmallBlind, true, $"Posted small blind ${amount:F2}", amount);
        }
        
        public ActionResult PostBigBlind(decimal amount)
        {
            if (amount <= 0)
                return new ActionResult(ActionType.BigBlind, false, "Big blind amount must be positive");
            
            if (!_funds.CanAfford(amount))
            {
                // All-in for big blind
                var allInAmount = _funds.CurrentBalance;
                if (allInAmount <= 0)
                    return new ActionResult(ActionType.BigBlind, false, "Player has no funds for big blind");
                
                _funds.ForceDeduct(allInAmount);
                _actions.AddToBet(allInAmount);
                _actions.SetAllIn();
                
                return new ActionResult(ActionType.BigBlind, true, $"Posted big blind (all-in) ${allInAmount:F2}", allInAmount);
            }
            
            _funds.TryDeduct(amount);
            _actions.AddToBet(amount);
            
            return new ActionResult(ActionType.BigBlind, true, $"Posted big blind ${amount:F2}", amount);
        }
        
        public override string ToString()
        {
            var turnStatus = IsMyTurn ? " [TURN]" : "";
            var cardInfo = _hand.Count > 0 ? $" - Cards: {_hand}" : "";
            var actionInfo = IsFolded || IsAllIn ? $" - {_actions}" : "";
            return $"{Name} - {_funds}{cardInfo}{actionInfo}{turnStatus}";
        }
    }
}