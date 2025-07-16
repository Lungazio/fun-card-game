using System;
using System.Collections.Generic;
using Poker.Core;
using Poker.Players;
using Poker.Game;

namespace Poker.Play
{
    public class GameStarter
    {
        public static GameManager StartGame()
        {
            Console.WriteLine("=== POKER GAME SETUP ===\n");
            
            // Setup game
            int playerCount = PokerSetup.GetPlayerCount();
            decimal initialFunds = PokerSetup.GetInitialFunds();
            var players = PokerSetup.CreatePlayers(playerCount, initialFunds);
            var (smallBlind, bigBlind) = PokerSetup.GetBlindAmounts();
            
            PokerSetup.DisplaySetupSummary(players, smallBlind, bigBlind);
            
            Console.WriteLine("\n=== STARTING PREFLOP ===");
            
            // Create GameManager and start the hand
            var gameManager = new GameManager(players, smallBlind, bigBlind);
            gameManager.StartNewHand();
            
            // Handle preflop betting
            HandlePreflopBetting(gameManager);
            
            return gameManager;
        }
        
        private static void HandlePreflopBetting(GameManager gameManager)
        {
            Console.WriteLine("\n=== PREFLOP BETTING ===");
            
            // Continue betting until round is complete
            while (gameManager.IsGameActive && gameManager.TurnManager.IsBettingRoundActive)
            {
                var currentPlayer = gameManager.CurrentPlayer;
                if (currentPlayer == null)
                    break;
                    
                // Get player's action
                var action = GetPlayerAction(currentPlayer, gameManager);
                
                // Process the action
                if (!gameManager.ProcessPlayerAction(action.actionType, action.amount))
                {
                    Console.WriteLine("❌ Invalid action, please try again.");
                    continue; // Try again
                }
                
                // Small delay for readability
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
            }
            
            Console.WriteLine("🎯 PREFLOP BETTING COMPLETE!");
        }
        
        private static (ActionType actionType, decimal amount) GetPlayerAction(Player currentPlayer, GameManager gameManager)
        {
            var validActions = currentPlayer.GetValidActions(gameManager.TurnManager.CurrentBet, gameManager.TurnManager.MinimumRaise);
            
            while (true)
            {
                Console.Write($"\n{currentPlayer.Name}, choose action ");
                Console.Write($"[{string.Join(", ", validActions)}]: ");
                
                string input = Console.ReadLine()?.ToLower().Trim();
                
                switch (input)
                {
                    case "call" when validActions.Contains(ActionType.Call):
                        var callAmount = Math.Max(0, gameManager.TurnManager.CurrentBet - currentPlayer.CurrentBet);
                        return (ActionType.Call, callAmount);
                        
                    case "check" when validActions.Contains(ActionType.Check):
                        return (ActionType.Check, 0);
                        
                    case "fold" when validActions.Contains(ActionType.Fold):
                        return (ActionType.Fold, 0);
                        
                    case "allin" when validActions.Contains(ActionType.AllIn):
                        return (ActionType.AllIn, 0);
                        
                    case var raise when raise?.StartsWith("raise") == true && validActions.Contains(ActionType.Raise):
                        var result = HandleRaiseInput(input, currentPlayer, gameManager);
                        if ((int)result.actionType == -1) // Invalid action
                        {
                            continue; // Go back to asking for input
                        }
                        return result;
                        
                    default:
                        Console.WriteLine($"❌ Invalid action. Valid options: [{string.Join(", ", validActions)}]");
                        if (validActions.Contains(ActionType.Raise))
                        {
                            Console.WriteLine($"   For raise, type: 'raise <amount>' (min: ${gameManager.TurnManager.MinimumRaise:F2})");
                        }
                        break;
                }
            }
        }
        
        private static (ActionType actionType, decimal amount) HandleRaiseInput(string input, Player currentPlayer, GameManager gameManager)
        {
            var parts = input.Split(' ');
            if (parts.Length == 2 && decimal.TryParse(parts[1], out decimal raiseAmount))
            {
                // Calculate total amount needed (current bet + raise amount - player's current bet)
                var totalAmount = gameManager.TurnManager.CurrentBet + raiseAmount - currentPlayer.CurrentBet;
                
                if (raiseAmount >= gameManager.TurnManager.MinimumRaise)
                {
                    if (currentPlayer.CurrentBalance >= totalAmount)
                    {
                        return (ActionType.Raise, totalAmount);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Insufficient funds. You have ${currentPlayer.CurrentBalance:F2}, need ${totalAmount:F2}");
                        Console.WriteLine("   Type 'allin' to go all-in instead.");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Minimum raise is ${gameManager.TurnManager.MinimumRaise:F2}");
                }
            }
            else
            {
                Console.WriteLine("❌ Invalid raise format. Use: 'raise <amount>'");
                Console.WriteLine($"   Example: 'raise {gameManager.TurnManager.MinimumRaise:F2}'");
            }
            
            // Return a special marker that indicates invalid input - don't use a real action
            return ((ActionType)(-1), 0); // Invalid action type
        }
        

    }
}