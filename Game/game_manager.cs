using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Core;
using Poker.Evaluation;
using Poker.Players;
using Poker.Power;

namespace Poker.Game
{
    public class GameManager
    {
        private List<Player> _players;
        private Deck _deck;
        private Board _board;
        private Pot _pot;
        private TurnManager _turnManager;
        private HandEvaluator _evaluator;
        private AbilityDeck _abilityDeck;
        private GamePhase _currentPhase;
        private int _dealerPosition;
        private decimal _smallBlindAmount;
        private decimal _bigBlindAmount;
        private bool _preflopAbilitiesDealt;
        private bool _postflopAbilitiesDealt;
        
        // NEW: Burn pile tracking for Trashman ability
        private List<Card> _burnPile;
        
        public GamePhase CurrentPhase => _currentPhase;
        public Board Board => _board;
        public Pot Pot => _pot;
        public TurnManager TurnManager => _turnManager;
        public Player CurrentPlayer => _turnManager?.CurrentPlayer;
        public bool IsGameActive => _currentPhase != GamePhase.Finished;
        public IReadOnlyList<Player> Players => _players.AsReadOnly();
        public int DealerPosition => _dealerPosition;
        public AbilityDeck AbilityDeck => _abilityDeck;
        public Deck Deck => _deck;
        
        // NEW: Expose burn pile for abilities
        public IReadOnlyList<Card> BurnPile => _burnPile.AsReadOnly();
        
        public GameManager(List<Player> players, decimal smallBlind, decimal bigBlind)
        {
            if (players == null || players.Count < 2)
                throw new ArgumentException("Need at least 2 players");

            _players = new List<Player>(players);
            _smallBlindAmount = smallBlind;
            _bigBlindAmount = bigBlind;
            _dealerPosition = 0;

            _deck = new Deck();
            _board = new Board();
            _pot = new Pot();
            _evaluator = new HandEvaluator();
            _abilityDeck = new AbilityDeck(_players.Count);
            _currentPhase = GamePhase.NotStarted;
            _preflopAbilitiesDealt = false;
            _postflopAbilitiesDealt = false;
            
            // NEW: Initialize burn pile
            _burnPile = new List<Card>();
        }
        
        // NEW: Method to add cards to burn pile
        public void AddToBurnPile(Card? card)
        {
            if (card != null)
            {
                _burnPile.Add(card);
                Console.WriteLine($"Card added to burn pile: {card}");
            }
        }
        
        public void StartNewHand()
        {
            Console.WriteLine($"\n{new string('=', 60)}");
            Console.WriteLine($"NEW HAND - Dealer: {_players[_dealerPosition].Name}");
            Console.WriteLine($"{new string('=', 60)}");
            
            // Reset everything for new hand
            _deck.ResetAndShuffle();
            _board.Clear();
            _pot.Reset();
            _currentPhase = GamePhase.Preflop;
            _preflopAbilitiesDealt = false;
            _postflopAbilitiesDealt = false;
            
            // NEW: Reset burn pile for new hand
            _burnPile.Clear();
            Console.WriteLine("Burn pile cleared for new hand");
            
            // Reset all players for new hand BEFORE dealing cards
            foreach (var player in _players)
            {
                player.SetTurn(false);
                player.ResetForNewHand();
            }
            
            // Deal hole cards
            DealHoleCards();
            
            // Deal first ability to each player (preflop)
            DealPreflopAbilities();
            
            // Initialize turn manager and start preflop betting (without resetting players again)
            _turnManager = new TurnManager(_players, _dealerPosition, _bigBlindAmount);
            _turnManager.StartPreflopWithoutReset(_smallBlindAmount, _bigBlindAmount);
            
            // Show initial state with blinds posted
            Console.WriteLine($"\nBlinds Posted:");
            var smallBlindPlayer = _players[(_dealerPosition + 1) % _players.Count];
            var bigBlindPlayer = _players[(_dealerPosition + 2) % _players.Count];
            Console.WriteLine($"  {smallBlindPlayer.Name}: Small Blind ${_smallBlindAmount:F2}");
            Console.WriteLine($"  {bigBlindPlayer.Name}: Big Blind ${_bigBlindAmount:F2}");
            
            DisplayCurrentTurn();
        }
        
        private void DealPreflopAbilities()
        {
            if (_preflopAbilitiesDealt)
                return;
                
            Console.WriteLine("\nDealing preflop abilities...");
            
            foreach (var player in _players)
            {
                if (_abilityDeck.RemainingAbilities > 0)
                {
                    var ability = _abilityDeck.DealAbility();
                    player.AddAbility(ability);
                    Console.WriteLine($"  {player.Name} received: {ability.Name}");
                }
            }
            
            _preflopAbilitiesDealt = true;
            Console.WriteLine($"Abilities remaining in deck: {_abilityDeck.RemainingAbilities}");
        }
        
        private void DealPostflopAbilities()
        {
            if (_postflopAbilitiesDealt || _currentPhase != GamePhase.Flop)
                return;
                
            Console.WriteLine("\nDealing postflop abilities...");
            
            foreach (var player in _players)
            {
                if (_abilityDeck.RemainingAbilities > 0 && !player.IsFolded)
                {
                    var ability = _abilityDeck.DealAbility();
                    player.AddAbility(ability);
                    Console.WriteLine($"  {player.Name} received: {ability.Name}");
                }
                else if (player.IsFolded)
                {
                    Console.WriteLine($"  {player.Name} (folded) - no ability");
                }
            }
            
            _postflopAbilitiesDealt = true;
            Console.WriteLine($"Abilities remaining in deck: {_abilityDeck.RemainingAbilities}");
        }
        
        public bool ProcessPlayerAction(ActionType actionType, decimal amount = 0)
        {
            var currentPlayer = CurrentPlayer;
            if (currentPlayer == null || !_turnManager.IsBettingRoundActive)
                return false;
            
            ActionResult result = null;
            
            // Execute the action
            switch (actionType)
            {
                case ActionType.Call:
                    var callAmount = Math.Max(0, _turnManager.CurrentBet - currentPlayer.CurrentBet);
                    result = currentPlayer.Call(callAmount);
                    break;
                case ActionType.Check:
                    result = currentPlayer.Check();
                    break;
                case ActionType.Fold:
                    result = currentPlayer.Fold();
                    break;
                case ActionType.Raise:
                    result = currentPlayer.Raise(amount);
                    break;
                case ActionType.AllIn:
                    result = currentPlayer.AllIn();
                    break;
                case ActionType.UseAbility:
                    result = currentPlayer.UseAbility();
                    break;
                case ActionType.Cancel:
                    result = ActionResult.Cancelled("Returned to main actions");
                    break;
                default:
                    return false;
            }
            
            // Handle cancelled actions - don't advance turn
            if (result.IsCancelled)
            {
                Console.WriteLine($"↩️ {result.Message}");
                DisplayCurrentTurn(); // Show options again
                return true; // Stay on same player's turn
            }
            
            if (!result.Success)
            {
                Console.WriteLine($"❌ {result}");
                return false;
            }
            
            Console.WriteLine($"✅ {currentPlayer.Name}: {result}");
            
            // Process action in turn manager
            _turnManager.ProcessPlayerAction(currentPlayer, result);
            
            // Show player statuses after each action
            DisplayPlayerStatuses();
            
            // Only check for betting completion after poker actions
            if (IsPokerAction(result.Action) && _turnManager.IsBettingComplete())
            {
                EndCurrentBettingRound();
                AdvanceToNextPhase();
            }
            else if (!IsPokerAction(result.Action))
            {
                // For abilities, stay on same turn but refresh display
                Console.WriteLine(); // Add spacing
                DisplayCurrentTurn();
            }
            else
            {
                // Poker action but betting not complete - show next turn
                Console.WriteLine(); // Add spacing before next turn
                DisplayCurrentTurn();
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
        
        private void EndCurrentBettingRound()
        {
            _turnManager.EndBettingRound();
            Console.WriteLine($"\n{new string('-', 40)}");
            Console.WriteLine($"{_currentPhase.ToString().ToUpper()} BETTING COMPLETE");
            Console.WriteLine($"{new string('-', 40)}");
            
            // Calculate and display pots using SidePotCalculator
            DisplayPotsAndPlayerStatuses();
        }
        
        private void DisplayPotsAndPlayerStatuses()
        {
            // Calculate side pots using danielpaz6's algorithm
            var pots = SidePotCalculator.CalculateMainAndSidePots(_players);
            
            Console.WriteLine("\nPOT BREAKDOWN:");
            if (pots.Count == 0)
            {
                Console.WriteLine("  No pots created yet.");
            }
            else
            {
                foreach (var pot in pots)
                {
                    var playerNames = pot.EligiblePlayerIDs
                        .Select(id => _players.First(p => p.ID == id).Name);
                    Console.WriteLine($"  {pot.Name}: ${pot.Amount:F2} (Players: {string.Join(", ", playerNames)})");
                }
                Console.WriteLine($"  TOTAL POT: ${pots.Sum(p => p.Amount):F2}");
            }
            
            Console.WriteLine("\nPLAYER STATUS:");
            DisplayPlayerStatuses();
        }
        
        private void DisplayPlayerStatuses()
        {
            foreach (var player in _players)
            {
                var status = GetPlayerStatus(player);
                var betInfo = player.CurrentBet > 0 ? $" (Bet: ${player.CurrentBet:F2})" : "";
                var abilityInfo = !player.AbilitySlot.IsEmpty ? $" [Abilities: {player.AbilitySlot.Count}]" : "";
                Console.WriteLine($"  {player.Name}: {status}{betInfo} - Balance: ${player.CurrentBalance:F2}{abilityInfo}");
            }
        }
        
        private string GetPlayerStatus(Player player)
        {
            if (player.IsFolded) return "FOLDED";
            if (player.IsAllIn) return "ALL-IN";
            return "Active";
        }
        
        private void AdvanceToNextPhase()
        {
            switch (_currentPhase)
            {
                case GamePhase.Preflop:
                    if (ShouldEndHand()) 
                    {
                        EndHand();
                        return;
                    }
                    DealFlop();
                    break;
                    
                case GamePhase.Flop:
                    if (ShouldEndHand()) 
                    {
                        EndHand();
                        return;
                    }
                    DealTurn();
                    break;
                    
                case GamePhase.Turn:
                    if (ShouldEndHand()) 
                    {
                        EndHand();
                        return;
                    }
                    DealRiver();
                    break;
                    
                case GamePhase.River:
                    EndHand();
                    return;
            }
            
            // Start new betting round
            _turnManager.StartPostflopBetting();
            Console.WriteLine(); // Add spacing
            DisplayCurrentTurn();
        }
        
        private void DealHoleCards()
        {
            Console.WriteLine("\nDealing hole cards...");
            
            // Deal 2 cards to each player
            for (int cardNum = 0; cardNum < 2; cardNum++)
            {
                foreach (var player in _players)
                {
                    player.AddHoleCard(_deck.DealCard());
                }
            }
            
            // Display hole cards
            foreach (var player in _players)
            {
                Console.WriteLine($"  {player.Name}: {string.Join(", ", player.HoleCards)}");
            }
        }
        
        private void DealFlop()
        {
            _currentPhase = GamePhase.Flop;
            
            // Deal flop and capture the burnt card
            Card? burntCard = _board.DealFlop(_deck);
            if (burntCard != null)
                AddToBurnPile(burntCard);
            
            Console.WriteLine($"\n{new string('=', 30)}");
            Console.WriteLine($"FLOP");
            Console.WriteLine($"{new string('=', 30)}");
            Console.WriteLine($"Board: {_board}");
            Console.WriteLine($"Burn pile now has {_burnPile.Count} card(s)");
            
            // Deal second ability to each player (postflop)
            DealPostflopAbilities();
        }
        
        private void DealTurn()
        {
            _currentPhase = GamePhase.Turn;
            
            // Deal turn and capture the burnt card
            Card? burntCard = _board.DealTurn(_deck);
            if (burntCard != null)
                AddToBurnPile(burntCard);
            
            Console.WriteLine($"\n{new string('=', 30)}");
            Console.WriteLine($"TURN");
            Console.WriteLine($"{new string('=', 30)}");
            Console.WriteLine($"Board: {_board}");
            Console.WriteLine($"Burn pile now has {_burnPile.Count} card(s)");
        }
        
        private void DealRiver()
        {
            _currentPhase = GamePhase.River;
            
            // Deal river and capture the burnt card
            Card? burntCard = _board.DealRiver(_deck);
            if (burntCard != null)
                AddToBurnPile(burntCard);
            
            Console.WriteLine($"\n{new string('=', 30)}");
            Console.WriteLine($"RIVER");
            Console.WriteLine($"{new string('=', 30)}");
            Console.WriteLine($"Board: {_board}");
            Console.WriteLine($"Burn pile now has {_burnPile.Count} card(s)");
        }
        
        private bool ShouldEndHand()
        {
            var activePlayers = _players.Where(p => !p.IsFolded).ToList();
            
            // End if only one player left
            if (activePlayers.Count <= 1) return true;
            
            // End if all remaining players are all-in (no more betting possible)
            if (activePlayers.All(p => p.IsAllIn)) return true;
            
            return false;
        }
        
        private void EndHand()
        {
            _currentPhase = GamePhase.Finished;
            Console.WriteLine($"\n{new string('=', 50)}");
            Console.WriteLine($"HAND COMPLETE");
            Console.WriteLine($"{new string('=', 50)}");
            Console.WriteLine($"Final Board: {_board}");
            Console.WriteLine($"Final burn pile: {_burnPile.Count} card(s) - {string.Join(", ", _burnPile)}");
            
            var activePlayers = _players.Where(p => !p.IsFolded).ToList();
            
            if (activePlayers.Count == 1)
            {
                // Only one player left - they win everything
                var winner = activePlayers[0];
                
                // Calculate pots to determine total winnings
                var allPots = SidePotCalculator.CalculateMainAndSidePots(_players);
                var totalWinnings = allPots.Sum(p => p.Amount);
                
                winner.AddFunds(totalWinnings);
                Console.WriteLine($"\n🏆 {winner.Name} wins ${totalWinnings:F2} (everyone else folded)");
            }
            else
            {
                // Multiple players - use side pot system
                DetermineWinnersWithSidePots(activePlayers);
            }
            
            DisplayFinalResults();
            
            // Move dealer button
            _dealerPosition = (_dealerPosition + 1) % _players.Count;
        }
        
        private void DetermineWinnersWithSidePots(List<Player> activePlayers)
        {
            System.Console.WriteLine("\n--- SHOWDOWN ---");
            
            // Calculate all pots (main and side pots)
            var allPots = SidePotCalculator.CalculateMainAndSidePots(_players);
            
            System.Console.WriteLine("\nFINAL POT BREAKDOWN:");
            foreach (var pot in allPots)
            {
                var playerNames = pot.EligiblePlayerIDs
                    .Select(id => _players.First(p => p.ID == id).Name);
                System.Console.WriteLine($"  {pot.Name}: ${pot.Amount:F2} (Eligible: {string.Join(", ", playerNames)})");
            }
            
            // Display all hands
            System.Console.WriteLine("\nHAND EVALUATIONS:");
            var handEvaluations = new Dictionary<int, HandResult>();
            foreach (var player in activePlayers)
            {
                var result = _evaluator.EvaluateHand(player.GetHoleCardsList(), _board);
                handEvaluations[player.ID] = result;
                System.Console.WriteLine($"  {player.Name}: {result}");
            }
            
            // Distribute each pot
            System.Console.WriteLine("\nWINNERS:");
            foreach (var pot in allPots)
            {
                var eligiblePlayers = pot.EligiblePlayerIDs
                    .Select(id => _players.First(p => p.ID == id))
                    .Where(p => !p.IsFolded)
                    .ToList();
                    
                if (eligiblePlayers.Count == 1)
                {
                    var winner = eligiblePlayers[0];
                    winner.AddFunds(pot.Amount);
                    System.Console.WriteLine($"  🏆 {winner.Name} wins ${pot.Amount:F2} from {pot.Name}");
                }
                else
                {
                    // Find best hand among eligible players
                    var bestScore = eligiblePlayers.Max(p => handEvaluations[p.ID].Score);
                    var winners = eligiblePlayers.Where(p => Math.Abs(handEvaluations[p.ID].Score - bestScore) < 0.001).ToList();
                    
                    var amountPerWinner = pot.Amount / winners.Count;
                    foreach (var winner in winners)
                    {
                        winner.AddFunds(amountPerWinner);
                        System.Console.WriteLine($"  🏆 {winner.Name} wins ${amountPerWinner:F2} from {pot.Name}");
                    }
                }
            }
        }
        
        private void DisplayCurrentTurn()
        {
            if (CurrentPlayer == null || !_turnManager.IsBettingRoundActive)
                return;
                
            Console.WriteLine($">>> {CurrentPlayer.Name}'S TURN <<<");
            Console.WriteLine($"Your hand: {string.Join(", ", CurrentPlayer.HoleCards)}");
            
            // Show player's abilities
            if (!CurrentPlayer.AbilitySlot.IsEmpty)
            {
                Console.WriteLine($"Your abilities: {CurrentPlayer.AbilitySlot}");
            }
            else
            {
                Console.WriteLine("Your abilities: None");
            }
            
            Console.WriteLine($"Balance: ${CurrentPlayer.CurrentBalance:F2}");
            
            var amountToCall = Math.Max(0, _turnManager.CurrentBet - CurrentPlayer.CurrentBet);
            if (amountToCall > 0)
            {
                Console.WriteLine($"Current bet to match: ${_turnManager.CurrentBet:F2} (You need ${amountToCall:F2} to call)");
            }
            else
            {
                Console.WriteLine($"No bet to call - you can check");
            }
            
            if (_board.Count > 0)
                Console.WriteLine($"Board: {_board}");
                
            // NEW: Show burn pile info if it exists
            if (_burnPile.Count > 0)
                Console.WriteLine($"Burn pile: {_burnPile.Count} card(s) available for Trashman");
                
            var validActions = CurrentPlayer.GetValidActions(_turnManager.CurrentBet, _turnManager.MinimumRaise);
            Console.WriteLine($"Valid actions: [{string.Join(", ", validActions)}]");
            
            if (validActions.Contains(ActionType.Raise))
            {
                Console.WriteLine($"Minimum raise: ${_turnManager.MinimumRaise:F2}");
            }
        }
        
        private void DisplayFinalResults()
        {
            Console.WriteLine($"\n--- FINAL BALANCES ---");
            foreach (var player in _players)
            {
                var abilityInfo = !player.AbilitySlot.IsEmpty ? $" (Unused abilities: {player.AbilitySlot.Count})" : "";
                Console.WriteLine($"  {player.Name}: ${player.CurrentBalance:F2}{abilityInfo}");
            }
        }
    }
    
    public enum GamePhase
    {
        NotStarted,
        Preflop,
        Flop,
        Turn,
        River,
        Finished
    }
}