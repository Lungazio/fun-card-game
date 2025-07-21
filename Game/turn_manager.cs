using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Players;

namespace Poker.Game
{
    public class TurnManager
    {
        private List<Player> _players;
        private int _dealerPosition;
        private int _currentPlayerIndex;
        private decimal _currentBet;
        private decimal _minimumRaise;
        private Player _lastRaiser;
        private bool _bettingRoundActive;
        
        public Player CurrentPlayer => _bettingRoundActive && _currentPlayerIndex >= 0 && _currentPlayerIndex < _players.Count 
            ? _players[_currentPlayerIndex] : null;
        public decimal CurrentBet => _currentBet;
        public decimal MinimumRaise => _minimumRaise;
        public bool IsBettingRoundActive => _bettingRoundActive;
        public int PlayersRemaining => _players.Count(p => !p.IsFolded);
        public int PlayersCanAct => _players.Count(p => p.CanAct());
        
        public TurnManager(List<Player> players, int dealerPosition, decimal bigBlindAmount)
        {
            if (players == null || players.Count < 2)
                throw new ArgumentException("Need at least 2 players");
                
            if (dealerPosition < 0 || dealerPosition >= players.Count)
                throw new ArgumentException("Invalid dealer position");
                
            _players = new List<Player>(players);
            _dealerPosition = dealerPosition;
            _minimumRaise = bigBlindAmount;
            _currentBet = 0;
            _lastRaiser = null;
            _bettingRoundActive = false;
            _currentPlayerIndex = -1;
        }
        
        public void StartPreflop(decimal smallBlindAmount, decimal bigBlindAmount)
        {
            // Reset all players for new hand
            foreach (var player in _players)
            {
                player.SetTurn(false);
                player.ResetForNewHand();
            }
            
            StartPreflopWithoutReset(smallBlindAmount, bigBlindAmount);
        }
        
        public void StartPreflopWithoutReset(decimal smallBlindAmount, decimal bigBlindAmount)
        {
            // Post blinds
            var smallBlindPlayer = GetPlayerAtPosition((_dealerPosition + 1) % _players.Count);
            var bigBlindPlayer = GetPlayerAtPosition((_dealerPosition + 2) % _players.Count);
            
            smallBlindPlayer.PostSmallBlind(smallBlindAmount);
            bigBlindPlayer.PostBigBlind(bigBlindAmount);
            
            // Set current bet to big blind amount
            _currentBet = bigBlindAmount;
            _minimumRaise = bigBlindAmount;
            _lastRaiser = bigBlindPlayer; // Big blind is considered the initial "raiser"
            
            // Start betting with player after big blind (or small blind if heads-up)
            if (_players.Count == 2)
            {
                _currentPlayerIndex = (_dealerPosition + 1) % _players.Count; // Small blind acts first heads-up
            }
            else
            {
                _currentPlayerIndex = (_dealerPosition + 3) % _players.Count; // UTG in full game
            }
            
            _bettingRoundActive = true;
            SetCurrentPlayerTurn();
        }
        
        public void StartPostflopBetting()
        {
            // Reset for new betting round
            foreach (var player in _players)
            {
                player.SetTurn(false);
                player.ResetForNewBettingRound();
            }
            
            _currentBet = 0;
            _lastRaiser = null;
            
            // Start with first active player after dealer
            _currentPlayerIndex = FindNextActivePlayer(_dealerPosition);
            _bettingRoundActive = true;
            SetCurrentPlayerTurn();
        }
        
        public bool ProcessPlayerAction(Player player, ActionResult action)
        {
            if (!_bettingRoundActive || player != CurrentPlayer)
                return false;
                
            if (!action.Success && !action.IsCancelled)
                return false;
            
            // Handle cancelled actions - stay on same player's turn
            if (action.IsCancelled)
            {
                // Don't move to next player, let current player choose again
                return true;
            }
            
            // Handle different action types
            switch (action.Action)
            {
                case ActionType.Raise:
                    HandleRaise(player, action.Amount);
                    break;
                    
                case ActionType.AllIn:
                    HandleAllIn(player);
                    break;
                    
                case ActionType.Call:
                case ActionType.Check:
                case ActionType.Fold:
                    // These are poker actions - no special handling needed
                    break;
                    
                case ActionType.UseAbility:
                    // Ability actions don't end the turn - stay on same player
                    return true;
                    
                case ActionType.Cancel:
                    // Cancel actions don't end the turn - stay on same player
                    return true;
                    
                default:
                    return false;
            }
            
            // Only move to next player for poker actions (not abilities/cancel)
            if (IsPokerAction(action.Action))
            {
                MoveToNextPlayer();
            }
            
            return true;
        }
        
        private bool IsPokerAction(ActionType actionType)
        {
            return actionType == ActionType.Call ||
                   actionType == ActionType.Check ||
                   actionType == ActionType.Fold ||
                   actionType == ActionType.Raise ||
                   actionType == ActionType.AllIn;
        }
        
        private void HandleAllIn(Player player)
        {
            // If all-in amount exceeds current bet, treat it like a raise
            if (player.CurrentBet > _currentBet)
            {
                HandleRaise(player, player.CurrentBet - _currentBet);
            }
            // If all-in is below current bet, no special action needed
        }
        
        private void HandleRaise(Player raiser, decimal raiseAmount)
        {
            // Update current bet (player's total bet becomes the new current bet)
            _currentBet = raiser.CurrentBet;
            _lastRaiser = raiser;
            
            // Reset action flags for all other players who can still act
            foreach (var player in _players)
            {
                if (player != raiser && player.CanAct())
                {
                    player.ResetActionFlag();
                }
            }
        }
        
        private void MoveToNextPlayer()
        {
            CurrentPlayer?.SetTurn(false);
            
            // Find next player who can act and hasn't acted this round (or needs to respond to raise)
            int startIndex = _currentPlayerIndex;
            int attempts = 0;
            
            do
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                attempts++;
                
                // Check if we've gone full circle
                if (attempts > _players.Count)
                {
                    // No more players need to act - end betting round
                    _bettingRoundActive = false;
                    _currentPlayerIndex = -1;
                    return;
                }
                
                var nextPlayer = _players[_currentPlayerIndex];
                
                // Player needs to act if:
                // 1. They can act (not folded/all-in)
                // 2. They haven't acted this round, OR
                // 3. They need to respond to a raise (their bet < current bet)
                if (nextPlayer.CanAct() && 
                    (!nextPlayer.HasActedThisRound || nextPlayer.CurrentBet < _currentBet))
                {
                    SetCurrentPlayerTurn();
                    return;
                }
                
            } while (true);
        }
        
        private void SetCurrentPlayerTurn()
        {
            if (_currentPlayerIndex >= 0 && _currentPlayerIndex < _players.Count)
            {
                _players[_currentPlayerIndex].SetTurn(true);
            }
        }
        
        private int FindNextActivePlayer(int startPosition)
        {
            for (int i = 1; i <= _players.Count; i++)
            {
                int index = (startPosition + i) % _players.Count;
                if (_players[index].CanAct())
                {
                    return index;
                }
            }
            return -1; // No active players found
        }
        
        private Player GetPlayerAtPosition(int position)
        {
            return _players[position];
        }
        
        public bool IsBettingComplete()
        {
            if (!_bettingRoundActive)
                return true;
                
            // Include ALL non-folded players, not just those who can still bet
            var activePlayers = _players.Where(p => !p.IsFolded).ToList();
            
            if (activePlayers.Count <= 1)
                return true;
                
            // Check if all non-folded players have either:
            // 1. Acted and matched the bet, OR
            // 2. Are all-in (can't bet more)
            return activePlayers.All(p => 
                (p.HasActedThisRound && p.CurrentBet == _currentBet) || 
                p.IsAllIn
            );
        }
                
        public void EndBettingRound()
        {
            foreach (var player in _players)
            {
                player.SetTurn(false);
            }
            _bettingRoundActive = false;
            _currentPlayerIndex = -1;
        }
        
        public List<Player> GetActivePlayers()
        {
            return _players.Where(p => !p.IsFolded).ToList();
        }
        
        public override string ToString()
        {
            if (!_bettingRoundActive)
                return "Turn Manager: Betting round not active";
                
            var currentPlayerName = CurrentPlayer?.Name ?? "None";
            return $"Turn Manager: {currentPlayerName}'s turn - Bet: ${_currentBet:F2}, Players remaining: {PlayersRemaining}";
        }
    }
}