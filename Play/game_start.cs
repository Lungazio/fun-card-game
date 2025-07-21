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
                        
                    // NEW: Ability-related actions
                    case "useability":
                    case "ability":
                    case "abilities":
                    case "use" when validActions.Contains(ActionType.UseAbility):
                        return HandleAbilitySelection(currentPlayer, gameManager);
                        
                    case "cancel" when validActions.Contains(ActionType.Cancel):
                        return (ActionType.Cancel, 0);
                        
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
                        if (validActions.Contains(ActionType.UseAbility))
                        {
                            Console.WriteLine($"   For abilities, type: 'ability' or 'use'");
                        }
                        break;
                }
            }
        }
        
        private static (ActionType actionType, decimal amount) HandleAbilitySelection(Player currentPlayer, GameManager gameManager)
        {
            if (currentPlayer.AbilitySlot.IsEmpty)
            {
                Console.WriteLine("❌ You have no abilities available.");
                return GetPlayerAction(currentPlayer, gameManager); // Return to main menu
            }
            
            while (true)
            {
                Console.WriteLine($"\n--- {currentPlayer.Name}'s Ability Menu ---");
                Console.WriteLine("Available abilities:");
                
                var abilities = currentPlayer.AbilitySlot.Abilities.ToList();
                for (int i = 0; i < abilities.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {abilities[i].Name} - {abilities[i].Description}");
                }
                Console.WriteLine($"  {abilities.Count + 1}. Cancel (return to main actions)");
                
                Console.Write($"\nChoose ability (1-{abilities.Count + 1}): ");
                string input = Console.ReadLine()?.Trim();
                
                if (int.TryParse(input, out int choice))
                {
                    if (choice >= 1 && choice <= abilities.Count)
                    {
                        // Player chose a specific ability
                        var chosenAbility = abilities[choice - 1];
                        return ExecuteAbility(currentPlayer, chosenAbility, gameManager);
                    }
                    else if (choice == abilities.Count + 1)
                    {
                        // Player chose cancel
                        Console.WriteLine("↩️ Returning to main actions...");
                        return (ActionType.Cancel, 0);
                    }
                }
                
                Console.WriteLine("❌ Invalid choice. Please try again.");
            }
        }
        
        private static (ActionType actionType, decimal amount) ExecuteAbility(Player currentPlayer, Poker.Power.Ability ability, GameManager gameManager)
        {
            Console.WriteLine($"\n🔮 {currentPlayer.Name} is using {ability.Name}!");
            
            try
            {
                switch (ability.Type)
                {
                    case Poker.Power.AbilityType.Peek:
                        return ExecutePeekAbility(currentPlayer, ability, gameManager);
                        
                    case Poker.Power.AbilityType.Burn:
                        return ExecuteBurnAbility(currentPlayer, ability, gameManager);
                        
                    case Poker.Power.AbilityType.Manifest:
                        return ExecuteManifestAbility(currentPlayer, ability, gameManager);
                        
                    default:
                        Console.WriteLine("❌ Unknown ability type.");
                        return (ActionType.Cancel, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error using ability: {ex.Message}");
                return (ActionType.Cancel, 0);
            }
        }
        
        private static (ActionType actionType, decimal amount) ExecutePeekAbility(Player currentPlayer, Poker.Power.Ability ability, GameManager gameManager)
        {
            // Get available targets (other players with hole cards)
            var availableTargets = gameManager.Players.Where(p => p.ID != currentPlayer.ID && p.HasHoleCards() && !p.IsFolded).ToList();
            
            if (!availableTargets.Any())
            {
                Console.WriteLine("❌ No valid targets for peek ability.");
                return (ActionType.Cancel, 0);
            }
            
            // Show target selection
            Console.WriteLine("Choose target player:");
            for (int i = 0; i < availableTargets.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {availableTargets[i].Name}");
            }
            Console.WriteLine($"  {availableTargets.Count + 1}. Cancel");
            
            Console.Write($"Choose target (1-{availableTargets.Count + 1}): ");
            string? targetInput = Console.ReadLine()?.Trim();
            
            if (!int.TryParse(targetInput, out int targetChoice) || targetChoice < 1 || targetChoice > availableTargets.Count + 1)
            {
                Console.WriteLine("❌ Invalid target choice.");
                return (ActionType.Cancel, 0);
            }
            
            if (targetChoice == availableTargets.Count + 1)
            {
                Console.WriteLine("↩️ Peek cancelled.");
                return (ActionType.Cancel, 0);
            }
            
            var targetPlayer = availableTargets[targetChoice - 1];
            
            // Choose which card to peek at
            Console.WriteLine($"Choose which card to peek at from {targetPlayer.Name}:");
            Console.WriteLine("  1. First hole card");
            Console.WriteLine("  2. Second hole card");
            Console.WriteLine("  3. Cancel");
            
            Console.Write("Choose card (1-3): ");
            string? cardInput = Console.ReadLine()?.Trim();
            
            if (!int.TryParse(cardInput, out int cardChoice) || cardChoice < 1 || cardChoice > 3)
            {
                Console.WriteLine("❌ Invalid card choice.");
                return (ActionType.Cancel, 0);
            }
            
            if (cardChoice == 3)
            {
                Console.WriteLine("↩️ Peek cancelled.");
                return (ActionType.Cancel, 0);
            }
            
            // Execute the peek
            var peekData = new Poker.Power.PeekData(targetPlayer.ID, cardChoice - 1);
            var result = currentPlayer.UseAbility(ability, availableTargets, peekData);
            
            if (result.Success)
            {
                Console.WriteLine($"✅ {result.Message}");
                return (ActionType.Cancel, 0); // Return Cancel to stay on same turn
            }
            else
            {
                Console.WriteLine($"❌ {result.Message}");
                return (ActionType.Cancel, 0);
            }
        }
        
        private static (ActionType actionType, decimal amount) ExecuteBurnAbility(Player currentPlayer, Poker.Power.Ability ability, GameManager gameManager)
        {
            // Choose what to reveal
            Console.WriteLine("Choose what to reveal from the burnt card:");
            Console.WriteLine("  1. Suit (Hearts, Clubs, Spades, Diamonds)");
            Console.WriteLine("  2. Rank (2, 3, 4... Jack, Queen, King, Ace)");
            Console.WriteLine("  3. Cancel");
            
            Console.Write("Choose revelation (1-3): ");
            string? revealInput = Console.ReadLine()?.Trim();
            
            if (!int.TryParse(revealInput, out int revealChoice) || revealChoice < 1 || revealChoice > 3)
            {
                Console.WriteLine("❌ Invalid choice.");
                return (ActionType.Cancel, 0);
            }
            
            if (revealChoice == 3)
            {
                Console.WriteLine("↩️ Burn cancelled.");
                return (ActionType.Cancel, 0);
            }
            
            bool revealSuit = revealChoice == 1;
            
            // Execute the burn using the exposed deck
            var burnResult = currentPlayer.UseAbility(ability, new List<Player>(), gameManager.Deck);
            
            if (burnResult.Success && burnResult.Data is Poker.Power.BurnPendingResult pendingResult)
            {
                // Complete the burn with player's choice
                var finalResult = pendingResult.CompleteReveal(revealSuit);
                Console.WriteLine($"✅ {currentPlayer.Name} used Burn ability - {finalResult.RevealedInformation}");
                return (ActionType.Cancel, 0); // Return Cancel to stay on same turn
            }
            else
            {
                Console.WriteLine($"❌ {burnResult.Message}");
                return (ActionType.Cancel, 0);
            }
        }
        
        private static (ActionType actionType, decimal amount) ExecuteManifestAbility(Player currentPlayer, Poker.Power.Ability ability, GameManager gameManager)
        {
            Console.WriteLine("Choose a card to manifest:");
            
            // Get rank choice
            Console.Write("Enter rank (2-14, where 11=Jack, 12=Queen, 13=King, 14=Ace): ");
            string? rankInput = Console.ReadLine()?.Trim();
            
            if (!int.TryParse(rankInput, out int rank) || rank < 2 || rank > 14)
            {
                Console.WriteLine("❌ Invalid rank. Must be 2-14.");
                return (ActionType.Cancel, 0);
            }
            
            // Get suit choice
            Console.WriteLine("Choose suit:");
            Console.WriteLine("  1. Hearts");
            Console.WriteLine("  2. Clubs");
            Console.WriteLine("  3. Spades");
            Console.WriteLine("  4. Diamonds");
            Console.WriteLine("  5. Cancel");
            
            Console.Write("Choose suit (1-5): ");
            string? suitInput = Console.ReadLine()?.Trim();
            
            if (!int.TryParse(suitInput, out int suitChoice) || suitChoice < 1 || suitChoice > 5)
            {
                Console.WriteLine("❌ Invalid suit choice.");
                return (ActionType.Cancel, 0);
            }
            
            if (suitChoice == 5)
            {
                Console.WriteLine("↩️ Manifest cancelled.");
                return (ActionType.Cancel, 0);
            }
            
            string[] suits = { "Hearts", "Clubs", "Spades", "Diamonds" };
            string chosenSuit = suits[suitChoice - 1];
            
            // Execute the manifest using the exposed deck
            var manifestResult = currentPlayer.UseAbility(ability, new List<Player>(), gameManager.Deck);
            
            if (manifestResult.Success && manifestResult.Data is Poker.Power.ManifestPendingResult pendingResult)
            {
                // Complete the manifest with player's choice
                var finalResult = pendingResult.CompleteManifest(rank, chosenSuit);
                Console.WriteLine($"✅ {currentPlayer.Name} manifested {finalResult.ChosenCard} as the next card to be dealt!");
                return (ActionType.Cancel, 0); // Return Cancel to stay on same turn
            }
            else
            {
                Console.WriteLine($"❌ {manifestResult.Message}");
                return (ActionType.Cancel, 0);
            }
        }
        
        private static (ActionType actionType, decimal amount) HandleRaiseInput(string input, Player currentPlayer, GameManager gameManager)
        {
            var parts = input.Split(' ');
            if (parts.Length == 2 && decimal.TryParse(parts[1], out decimal raiseAmount))
            {
                // Calculate total amount needed (current bet + raise amount - player's current bet)
                var totalAmount = raiseAmount - currentPlayer.CurrentBet;
                
                if (raiseAmount >= gameManager.TurnManager.CurrentBet + gameManager.TurnManager.MinimumRaise)
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
                    Console.WriteLine($"❌ Minimum raise to ${gameManager.TurnManager.CurrentBet + gameManager.TurnManager.MinimumRaise:F2}");
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