using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Core;
using Poker.Evaluation;
using Poker.Game;
using Poker.Players;

namespace Poker.Play
{
    public class PokerGameTester
    {
        private bool _debugMode;
        private int _testsRun = 0;
        private int _testsPassed = 0;
        
        public PokerGameTester(bool debugMode = false)
        {
            _debugMode = debugMode;
        }
        
        public void RunAllTests()
        {
            System.Console.Clear();
            System.Console.WriteLine("╔════════════════════════════════════════╗");
            System.Console.WriteLine("║         POKER ENGINE TEST SUITE       ║");
            System.Console.WriteLine("╚════════════════════════════════════════╝\n");
            
            _testsRun = 0;
            _testsPassed = 0;
            
            // Core component tests
            TestCardAndDeck();
            TestHandEvaluation();
            TestPlayerActions();
            TestSidePotCalculation();
            TestTurnManager();
            TestGameFlow();
            
            // Edge case tests
            TestHeadsUpScenarios();
            TestMultipleAllInScenarios();
            TestComplexSidePots();
            
            ShowTestSummary();
        }
        
        private void TestCardAndDeck()
        {
            StartTestCategory("Card and Deck Tests");
            
            // Test card creation
            RunTest("Card Creation", () =>
            {
                var card = new Card(14, "Hearts"); // Ace of Hearts
                return card.Rank == 14 && card.Suit == "Hearts" && card.GetRankName() == "Ace";
            });
            
            // Test deck initialization
            RunTest("Deck Initialization", () =>
            {
                var deck = new Deck();
                return deck.RemainingCards == 52;
            });
            
            // Test deck dealing
            RunTest("Deck Dealing", () =>
            {
                var deck = new Deck();
                var card1 = deck.DealCard();
                var card2 = deck.DealCard();
                return deck.RemainingCards == 50 && card1 != null && card2 != null;
            });
            
            // Test deck shuffle (cards should be different after shuffle)
            RunTest("Deck Shuffle", () =>
            {
                var deck1 = new Deck(seed: 123);
                var deck2 = new Deck(seed: 456);
                deck1.Shuffle();
                deck2.Shuffle();
                
                var card1 = deck1.DealCard();
                var card2 = deck2.DealCard();
                
                // With different seeds, first cards should likely be different
                return card1.Rank != card2.Rank || card1.Suit != card2.Suit;
            });
        }
        
        private void TestHandEvaluation()
        {
            StartTestCategory("Hand Evaluation Tests");
            
            var evaluator = new HandEvaluator();
            var board = new Board();
            
            // Test Royal Flush
            RunTest("Royal Flush Detection", () =>
            {
                board.Clear();
                board.AddCard(new Card(10, "Hearts"));
                board.AddCard(new Card(11, "Hearts"));
                board.AddCard(new Card(12, "Hearts"));
                board.AddCard(new Card(13, "Hearts"));
                board.AddCard(new Card(14, "Hearts"));
                
                var result = evaluator.EvaluateHand(new List<Card>(), board);
                return result.Rank == HandRank.RoyalFlush;
            });
            
            // Test Straight Flush
            RunTest("Straight Flush Detection", () =>
            {
                board.Clear();
                board.AddCard(new Card(5, "Spades"));
                board.AddCard(new Card(6, "Spades"));
                board.AddCard(new Card(7, "Spades"));
                board.AddCard(new Card(8, "Spades"));
                board.AddCard(new Card(9, "Spades"));
                
                var result = evaluator.EvaluateHand(new List<Card>(), board);
                return result.Rank == HandRank.StraightFlush;
            });
            
            // Test Four of a Kind
            RunTest("Four of a Kind Detection", () =>
            {
                board.Clear();
                board.AddCard(new Card(8, "Hearts"));
                board.AddCard(new Card(8, "Clubs"));
                board.AddCard(new Card(8, "Spades"));
                board.AddCard(new Card(8, "Diamonds"));
                board.AddCard(new Card(2, "Hearts"));
                
                var result = evaluator.EvaluateHand(new List<Card>(), board);
                return result.Rank == HandRank.FourOfAKind;
            });
            
            // Test Wheel (A-2-3-4-5 straight)
            RunTest("Wheel Straight Detection", () =>
            {
                board.Clear();
                board.AddCard(new Card(14, "Hearts")); // Ace
                board.AddCard(new Card(2, "Clubs"));
                board.AddCard(new Card(3, "Spades"));
                board.AddCard(new Card(4, "Diamonds"));
                board.AddCard(new Card(5, "Hearts"));
                
                var result = evaluator.EvaluateHand(new List<Card>(), board);
                return result.Rank == HandRank.Straight;
            });
        }
        
        private void TestPlayerActions()
        {
            StartTestCategory("Player Action Tests");
            
            // Test player creation
            RunTest("Player Creation", () =>
            {
                var player = new Player(1, "Test Player", 1000);
                return player.Name == "Test Player" && player.CurrentBalance == 1000;
            });
            
            // Test betting actions
            RunTest("Player Call Action", () =>
            {
                var player = new Player(1, "Test", 1000);
                player.SetTurn(true);
                var result = player.Call(50);
                return result.Success && player.CurrentBet == 50 && player.CurrentBalance == 950;
            });
            
            // Test insufficient funds
            RunTest("Insufficient Funds Handling", () =>
            {
                var player = new Player(1, "Test", 30);
                player.SetTurn(true);
                var result = player.Call(50); // Should trigger all-in
                return result.Success && player.IsAllIn && player.CurrentBalance == 0;
            });
            
            // Test fold action
            RunTest("Player Fold Action", () =>
            {
                var player = new Player(1, "Test", 1000);
                player.SetTurn(true);
                var result = player.Fold();
                return result.Success && player.IsFolded;
            });
        }
        
        private void TestSidePotCalculation()
        {
            StartTestCategory("Side Pot Calculation Tests");
            
            // Test simple side pot scenario
            RunTest("Simple Side Pot", () =>
            {
                var players = new List<Player>
                {
                    new Player(1, "Player1", 0) { }, // All-in for 100
                    new Player(2, "Player2", 50) { }, // Has 150 left
                    new Player(3, "Player3", 100) { } // Has 100 left
                };
                
                // Simulate betting: Player1 all-in 100, Player2 calls 100, Player3 calls 100
                // But Player1 only had 100 total
                var contributions = new List<PlayerContribution>
                {
                    new PlayerContribution(1, 100, false),
                    new PlayerContribution(2, 100, false),
                    new PlayerContribution(3, 100, false)
                };
                
                var pots = SidePotCalculator.CalculatePotsFromContributions(contributions);
                
                // Should create one main pot of 300 with all players eligible
                return pots.Count == 1 && pots[0].Amount == 300 && pots[0].EligiblePlayerIDs.Count == 3;
            });
            
            // Test complex side pot scenario
            RunTest("Complex Side Pot", () =>
            {
                var contributions = new List<PlayerContribution>
                {
                    new PlayerContribution(1, 100, false), // All-in 100
                    new PlayerContribution(2, 200, false), // All-in 200  
                    new PlayerContribution(3, 300, false), // Bet 300
                    new PlayerContribution(4, 0, true)     // Folded
                };
                
                var pots = SidePotCalculator.CalculatePotsFromContributions(contributions);
                var totalPotAmount = pots.Sum(p => p.Amount);
                var totalContributions = contributions.Where(c => !c.HasFolded).Sum(c => c.TotalContributed);
                
                return Math.Abs(totalPotAmount - totalContributions) < 0.01m && pots.Count >= 1;
            });
            
            // Test money conservation
            RunTest("Money Conservation", () =>
            {
                var contributions = new List<PlayerContribution>
                {
                    new PlayerContribution(1, 50, false),
                    new PlayerContribution(2, 150, false),
                    new PlayerContribution(3, 300, false)
                };
                
                var pots = SidePotCalculator.CalculatePotsFromContributions(contributions);
                return SidePotCalculator.VerifyMoneyConservation(contributions, pots);
            });
        }
        
        private void TestTurnManager()
        {
            StartTestCategory("Turn Manager Tests");
            
            // Test turn manager initialization
            RunTest("Turn Manager Initialization", () =>
            {
                var players = CreateTestPlayers(3);
                var turnManager = new TurnManager(players, dealerPosition: 0, bigBlindAmount: 10);
                return turnManager != null;
            });
            
            // Test preflop start
            RunTest("Preflop Blind Posting", () =>
            {
                var players = CreateTestPlayers(3);
                var turnManager = new TurnManager(players, dealerPosition: 0, bigBlindAmount: 10);
                turnManager.StartPreflop(smallBlindAmount: 5, bigBlindAmount: 10);
                
                var smallBlindPlayer = players[1]; // Position 1 = small blind
                var bigBlindPlayer = players[2];   // Position 2 = big blind
                
                return smallBlindPlayer.CurrentBet >= 5 && bigBlindPlayer.CurrentBet >= 10;
            });
        }
        
        private void TestGameFlow()
        {
            StartTestCategory("Complete Game Flow Tests");
            
            // Test basic game creation
            RunTest("Game Manager Creation", () =>
            {
                var players = CreateTestPlayers(3);
                var gameManager = new GameManager(players, smallBlind: 5, bigBlind: 10);
                return gameManager != null && gameManager.Players.Count == 3;
            });
            
            // Test hand start
            RunTest("Hand Start Process", () =>
            {
                var players = CreateTestPlayers(3);
                var gameManager = new GameManager(players, smallBlind: 5, bigBlind: 10);
                
                try
                {
                    gameManager.StartNewHand();
                    return gameManager.CurrentPhase == GamePhase.Preflop && gameManager.CurrentPlayer != null;
                }
                catch
                {
                    return false;
                }
            });
        }
        
        private void TestHeadsUpScenarios()
        {
            StartTestCategory("Heads-Up Scenarios");
            
            // Test heads-up blind posting
            RunTest("Heads-Up Blind Order", () =>
            {
                var players = CreateTestPlayers(2);
                var turnManager = new TurnManager(players, dealerPosition: 0, bigBlindAmount: 10);
                turnManager.StartPreflop(smallBlindAmount: 5, bigBlindAmount: 10);
                
                // In heads-up, dealer (position 0) posts small blind, other player posts big blind
                return players[0].CurrentBet >= 5 && players[1].CurrentBet >= 10;
            });
        }
        
        private void TestMultipleAllInScenarios()
        {
            StartTestCategory("Multiple All-In Scenarios");
            
            // Test multiple all-ins with different stack sizes
            RunTest("Multiple All-In Side Pots", () =>
            {
                var contributions = new List<PlayerContribution>
                {
                    new PlayerContribution(1, 50, false),  // All-in 50
                    new PlayerContribution(2, 150, false), // All-in 150
                    new PlayerContribution(3, 200, false), // All-in 200
                    new PlayerContribution(4, 200, false)  // Call 200
                };
                
                var pots = SidePotCalculator.CalculatePotsFromContributions(contributions);
                var isValid = SidePotCalculator.VerifyMoneyConservation(contributions, pots);
                
                return isValid && pots.Count > 1; // Should create multiple pots
            });
        }
        
        private void TestComplexSidePots()
        {
            StartTestCategory("Complex Side Pot Edge Cases");
            
            // Test scenario with folded players
            RunTest("Side Pots with Folded Players", () =>
            {
                var contributions = new List<PlayerContribution>
                {
                    new PlayerContribution(1, 100, false), // Active
                    new PlayerContribution(2, 150, true),  // Folded
                    new PlayerContribution(3, 200, false), // Active
                    new PlayerContribution(4, 50, true)    // Folded
                };
                
                var pots = SidePotCalculator.CalculatePotsFromContributions(contributions);
                
                // Only active players should be eligible for pots
                return pots.All(pot => pot.EligiblePlayerIDs.All(id => 
                    contributions.First(c => c.PlayerID == id).HasFolded == false));
            });
        }
        
        private List<Player> CreateTestPlayers(int count)
        {
            var players = new List<Player>();
            for (int i = 0; i < count; i++)
            {
                players.Add(new Player(i + 1, $"TestPlayer{i + 1}", 1000));
            }
            return players;
        }
        
        private void StartTestCategory(string categoryName)
        {
            System.Console.WriteLine($"\n--- {categoryName} ---");
        }
        
        private void RunTest(string testName, Func<bool> testFunction)
        {
            _testsRun++;
            
            try
            {
                bool result = testFunction();
                
                if (result)
                {
                    _testsPassed++;
                    System.Console.WriteLine($"✅ {testName}");
                }
                else
                {
                    System.Console.WriteLine($"❌ {testName} - FAILED");
                    if (_debugMode)
                    {
                        System.Console.WriteLine("   Test returned false");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ {testName} - ERROR: {ex.Message}");
                if (_debugMode)
                {
                    System.Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                }
            }
        }
        
        private void ShowTestSummary()
        {
            System.Console.WriteLine($"\n" + "═".PadRight(50, '═'));
            System.Console.WriteLine("TEST SUITE SUMMARY");
            System.Console.WriteLine("═".PadRight(50, '═'));
            System.Console.WriteLine($"Tests Run: {_testsRun}");
            System.Console.WriteLine($"Tests Passed: {_testsPassed}");
            System.Console.WriteLine($"Tests Failed: {_testsRun - _testsPassed}");
            System.Console.WriteLine($"Success Rate: {(_testsRun > 0 ? (double)_testsPassed / _testsRun * 100 : 0):F1}%");
            
            if (_testsPassed == _testsRun)
            {
                System.Console.WriteLine("\n🎉 ALL TESTS PASSED! Your poker engine is working correctly.");
            }
            else
            {
                System.Console.WriteLine($"\n⚠️ {_testsRun - _testsPassed} test(s) failed. Check the implementation.");
            }
        }
    }
}