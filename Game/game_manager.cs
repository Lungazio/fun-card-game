using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Core;
using Poker.Evaluation;
using Poker.Players;

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
        private GamePhase _currentPhase;
        private int _dealerPosition;
        private decimal _smallBlindAmount;
        private decimal _bigBlindAmount;
        
        public GamePhase CurrentPhase => _currentPhase;
        public Board Board => _board;
        public Pot Pot => _pot;
        public TurnManager TurnManager => _turnManager;
        public Player CurrentPlayer => _turnManager?.CurrentPlayer;
        public bool IsGameActive => _currentPhase != GamePhase.Finished;
        public IReadOnlyList<Player> Players => _players.AsReadOnly();
        public int DealerPosition => _dealerPosition;  // <-- ADD THIS
        
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
            _currentPhase = GamePhase.NotStarted;
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
            
            // Reset all players for new hand BEFORE dealing cards
            foreach (var player in _players)
            {
                player.SetTurn(false);
                player.ResetForNewHand();
            }
            
            // Deal hole cards
            DealHoleCards();
            
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
                default:
                    return false;
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
            
            // Check if betting round is complete
            if (_turnManager.IsBettingComplete())
            {
                EndCurrentBettingRound();
                AdvanceToNextPhase();
            }
            else
            {
                Console.WriteLine(); // Add spacing before next turn
                DisplayCurrentTurn();
            }
            
            return true;
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
                Console.WriteLine($"  {player.Name}: {status}{betInfo} - Balance: ${player.CurrentBalance:F2}");
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
            _board.DealFlop(_deck);
            Console.WriteLine($"\n{new string('=', 30)}");
            Console.WriteLine($"FLOP");
            Console.WriteLine($"{new string('=', 30)}");
            Console.WriteLine($"Board: {_board}");
        }
        
        private void DealTurn()
        {
            _currentPhase = GamePhase.Turn;
            _board.DealTurn(_deck);
            Console.WriteLine($"\n{new string('=', 30)}");
            Console.WriteLine($"TURN");
            Console.WriteLine($"{new string('=', 30)}");
            Console.WriteLine($"Board: {_board}");
        }
        
        private void DealRiver()
        {
            _currentPhase = GamePhase.River;
            _board.DealRiver(_deck);
            Console.WriteLine($"\n{new string('=', 30)}");
            Console.WriteLine($"RIVER");
            Console.WriteLine($"{new string('=', 30)}");
            Console.WriteLine($"Board: {_board}");
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
            Console.WriteLine($"Your hand: {string.Join(", ", CurrentPlayer.HoleCards)}"); // ADD THIS LINE
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
                Console.WriteLine($"  {player.Name}: ${player.CurrentBalance:F2}");
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