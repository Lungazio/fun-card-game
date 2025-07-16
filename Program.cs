using Poker.Play;

class Program 
{
    static void Main(string[] args)
    {
        var gameManager = GameStarter.StartGame();
        
        // Print player funds after the hand is complete
        System.Console.WriteLine("\n" + new string('=', 50));
        System.Console.WriteLine("FINAL PLAYER BALANCES AFTER HAND");
        System.Console.WriteLine(new string('=', 50));
        
        foreach (var player in gameManager.Players)
        {
            System.Console.WriteLine($"{player.Name}: ${player.CurrentBalance:F2}");
        }
        
        System.Console.WriteLine($"\nGame Phase: {gameManager.CurrentPhase}");
        System.Console.WriteLine($"Next Dealer Position: {gameManager.DealerPosition}");
        System.Console.WriteLine($"Next Dealer: {gameManager.Players[gameManager.DealerPosition].Name}");
        
        System.Console.WriteLine("\nPress any key to exit...");
        System.Console.ReadLine();
    }
}