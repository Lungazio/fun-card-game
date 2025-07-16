using System;
using System.Collections.Generic;
using Poker.Players;

namespace Poker.Play
{
    public class PokerSetup
    {
        public static int GetPlayerCount()
        {
            while (true)
            {
                System.Console.Write("How many players? (2-8): ");
                string input = System.Console.ReadLine();
                
                if (int.TryParse(input, out int count))
                {
                    if (count >= 2 && count <= 8)
                    {
                        return count;
                    }
                    else
                    {
                        System.Console.WriteLine("❌ Please enter a number between 2 and 8.");
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Please enter a valid number.");
                }
            }
        }
        
        public static decimal GetInitialFunds()
        {
            while (true)
            {
                System.Console.Write("Initial funds per player ($): ");
                string input = System.Console.ReadLine();
                
                if (decimal.TryParse(input, out decimal funds))
                {
                    if (funds > 0)
                    {
                        return funds;
                    }
                    else
                    {
                        System.Console.WriteLine("❌ Initial funds must be greater than 0.");
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Please enter a valid amount.");
                }
            }
        }
        
        public static List<Player> CreatePlayers(int playerCount, decimal initialFunds)
        {
            var players = new List<Player>();
            
            System.Console.WriteLine($"\nEnter names for {playerCount} players:");
            
            for (int i = 1; i <= playerCount; i++)
            {
                string name = GetPlayerName(i);
                players.Add(new Player(i, name, initialFunds));
            }
            
            return players;
        }
        
        public static string GetPlayerName(int playerNumber)
        {
            while (true)
            {
                System.Console.Write($"Player {playerNumber} name (or press Enter for 'Player {playerNumber}'): ");
                string input = System.Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    return $"Player {playerNumber}";
                }
                else if (input.Trim().Length > 0)
                {
                    return input.Trim();
                }
                else
                {
                    System.Console.WriteLine("❌ Name cannot be empty or just spaces.");
                }
            }
        }
        
        public static (decimal smallBlind, decimal bigBlind) GetBlindAmounts()
        {
            System.Console.WriteLine("\nSet blind amounts:");
            
            decimal smallBlind = GetSmallBlind();
            decimal bigBlind = GetBigBlind(smallBlind);
            
            return (smallBlind, bigBlind);
        }
        
        public static decimal GetSmallBlind()
        {
            while (true)
            {
                System.Console.Write("Small blind amount ($): ");
                string input = System.Console.ReadLine();
                
                if (decimal.TryParse(input, out decimal amount))
                {
                    if (amount > 0)
                    {
                        return amount;
                    }
                    else
                    {
                        System.Console.WriteLine("❌ Small blind must be greater than 0.");
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Please enter a valid amount.");
                }
            }
        }
        
        public static decimal GetBigBlind(decimal smallBlind)
        {
            while (true)
            {
                System.Console.Write($"Big blind amount ($) [must be at least ${smallBlind}]: ");
                string input = System.Console.ReadLine();
                
                if (decimal.TryParse(input, out decimal amount))
                {
                    if (amount >= smallBlind)
                    {
                        return amount;
                    }
                    else
                    {
                        System.Console.WriteLine($"❌ Big blind must be at least ${smallBlind} (the small blind amount).");
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Please enter a valid amount.");
                }
            }
        }
        
        public static void DisplaySetupSummary(List<Player> players, decimal smallBlind, decimal bigBlind)
        {
            System.Console.WriteLine("\n" + "=".PadRight(40, '='));
            System.Console.WriteLine("GAME SETUP COMPLETE");
            System.Console.WriteLine("=".PadRight(40, '='));
            
            System.Console.WriteLine($"Number of players: {players.Count}");
            System.Console.WriteLine($"Small blind: ${smallBlind:F2}");
            System.Console.WriteLine($"Big blind: ${bigBlind:F2}");
            
            System.Console.WriteLine("\nPlayers:");
            foreach (var player in players)
            {
                System.Console.WriteLine($"  {player.ID}. {player.Name} - ${player.CurrentBalance:F2}");
            }
            
            System.Console.WriteLine("\n✅ All players created successfully!");
        }
    }
}