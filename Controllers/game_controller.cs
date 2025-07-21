using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using Poker.Game;
using Poker.Players;

namespace Poker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, GameManager> _games = new();
        private const string API_KEY = "poker-game-api-key-2024";

        [HttpPost("test")]
        public Microsoft.AspNetCore.Mvc.ActionResult Test()
        {
            if (!ValidateApiKey()) return Unauthorized("Invalid API key");
            
            return Ok(new { message = "C# Poker API is working!", timestamp = DateTime.Now });
        }

        [HttpPost("create")]
        public Microsoft.AspNetCore.Mvc.ActionResult CreateGame([FromBody] CreateGameRequest request)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            try
            {
                var gameId = Guid.NewGuid().ToString();
                var players = request.Players.Select(p => new Player(p.Id, p.Name, p.StartingFunds)).ToList();
                var gameManager = new GameManager(players, request.SmallBlind, request.BigBlind);
                
                _games[gameId] = gameManager;
                
                return Ok(new { 
                    GameId = gameId, 
                    Success = true,
                    PlayerCount = players.Count,
                    Blinds = new { Small = request.SmallBlind, Big = request.BigBlind }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{gameId}/start")]
        public Microsoft.AspNetCore.Mvc.ActionResult StartGame(string gameId)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
            
            try
            {
                game.StartNewHand();
                return Ok(new { 
                    Success = true, 
                    Message = "Hand started",
                    GameState = GetGameState(game)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{gameId}/action")]
        public Microsoft.AspNetCore.Mvc.ActionResult ProcessAction(string gameId, [FromBody] ActionRequest request)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
            
            // Validate game is active
            if (!game.IsGameActive || game.TurnManager?.IsBettingRoundActive != true)
            {
                return BadRequest(new { 
                    error = "Game is not active or betting round is not active",
                    currentPhase = game.CurrentPhase.ToString(),
                    isGameActive = game.IsGameActive
                });
            }
            
            // Validate it's this player's turn
            if (game.CurrentPlayer?.ID != request.PlayerId)
            {
                return BadRequest(new { 
                    error = "Not your turn", 
                    currentPlayer = game.CurrentPlayer?.Name,
                    currentPlayerId = game.CurrentPlayer?.ID,
                    yourPlayerId = request.PlayerId
                });
            }
            
            // Find the specific player
            var player = game.Players.FirstOrDefault(p => p.ID == request.PlayerId);
            if (player == null)
            {
                return BadRequest(new { error = "Player not found" });
            }
            
            try
            {
                ActionResult actionResult;
                
                // Execute the specific player's action with validation
                switch (request.ActionType)
                {
                    case Poker.Players.ActionType.Call:
                        var callAmount = CalculateCallAmount(game, player);
                        actionResult = player.Call(callAmount);
                        break;
                        
                    case Poker.Players.ActionType.Check:
                        // Validate player can check (no bet to call)
                        if (game.TurnManager.CurrentBet > player.CurrentBet)
                        {
                            return BadRequest(new { 
                                error = "Cannot check when there's a bet to call",
                                currentBet = game.TurnManager.CurrentBet,
                                yourBet = player.CurrentBet,
                                amountToCall = game.TurnManager.CurrentBet - player.CurrentBet
                            });
                        }
                        actionResult = player.Check();
                        break;
                        
                    case Poker.Players.ActionType.Fold:
                        actionResult = player.Fold();
                        break;
                        
                    case Poker.Players.ActionType.Raise:
                        // Validate raise amount
                        var totalRaiseAmount = game.TurnManager.CurrentBet + request.Amount;
                        if (totalRaiseAmount < game.TurnManager.CurrentBet + game.TurnManager.MinimumRaise)
                        {
                            return BadRequest(new { 
                                error = "Raise amount too small",
                                minimumRaise = game.TurnManager.MinimumRaise,
                                currentBet = game.TurnManager.CurrentBet,
                                minimumTotalBet = game.TurnManager.CurrentBet + game.TurnManager.MinimumRaise
                            });
                        }
                        
                        var raiseAmountNeeded = totalRaiseAmount - player.CurrentBet;
                        actionResult = player.Raise(raiseAmountNeeded);
                        break;
                        
                    case Poker.Players.ActionType.AllIn:
                        actionResult = player.AllIn();
                        break;
                        
                    default:
                        return BadRequest(new { error = "Invalid action type" });
                }
                
                if (!actionResult.Success)
                {
                    return BadRequest(new { 
                        error = "Action failed",
                        message = actionResult.Message,
                        playerBalance = player.CurrentBalance
                    });
                }
                
                // Process the action through GameManager (handles turn advancement)
                var gameProcessed = game.ProcessPlayerAction(request.ActionType, request.Amount);
                
                return Ok(new { 
                    Success = true,
                    ActionResult = actionResult.Message,
                    PlayerAction = new {
                        Player = player.Name,
                        Action = request.ActionType.ToString(),
                        Amount = actionResult.Amount
                    },
                    GameState = GetGameState(game)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{gameId}/state")]
        public Microsoft.AspNetCore.Mvc.ActionResult GetFullGameState(string gameId)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
                
            return Ok(new {
                GameId = gameId,
                GameState = GetGameState(game)
            });
        }

        [HttpGet("{gameId}/status")]
        public Microsoft.AspNetCore.Mvc.ActionResult GetGameStatus(string gameId)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
                
            return Ok(new {
                GameId = gameId,
                Phase = game.CurrentPhase.ToString(),
                IsActive = game.IsGameActive,
                PlayerCount = game.Players.Count,
                CurrentPlayer = game.CurrentPlayer?.Name,
                CurrentPlayerId = game.CurrentPlayer?.ID
            });
        }

        [HttpPost("{gameId}/abilities/use")]
        public Microsoft.AspNetCore.Mvc.ActionResult UseAbility(string gameId, [FromBody] AbilityRequest request)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
            
            // Validate it's this player's turn
            if (game.CurrentPlayer?.ID != request.PlayerId)
            {
                return BadRequest(new { 
                    error = "Not your turn", 
                    currentPlayer = game.CurrentPlayer?.Name,
                    currentPlayerId = game.CurrentPlayer?.ID 
                });
            }
            
            try
            {
                var player = game.Players.FirstOrDefault(p => p.ID == request.PlayerId);
                if (player == null)
                    return BadRequest("Player not found");
                    
                var ability = player.AbilitySlot.Abilities.FirstOrDefault(a => a.Type.ToString().ToLower() == request.AbilityType.ToLower());
                if (ability == null)
                    return BadRequest($"Player doesn't have {request.AbilityType} ability");
                
                // Handle different ability types
                object additionalData = request.AbilityType.ToLower() switch
                {
                    "peek" => new Poker.Power.PeekData(request.TargetPlayerId ?? 0, request.CardIndex ?? 0),
                    "burn" => game.Deck,
                    "manifest" => game.Deck,
                    _ => null
                };
                
                var availableTargets = game.Players.Where(p => p.ID != request.PlayerId && !p.IsFolded).ToList();
                var result = player.UseAbility(ability, availableTargets, additionalData);
                
                if (result.Success)
                {
                    // Handle pending results for Burn and Manifest
                    if (result.Data is Poker.Power.BurnPendingResult burnPending)
                    {
                        var finalResult = burnPending.CompleteReveal(request.RevealSuit ?? false);
                        return Ok(new {
                            Success = true,
                            Message = $"Burn ability used - {finalResult.RevealedInformation}",
                            AbilityUsed = true,
                            GameState = GetGameState(game)
                        });
                    }
                    else if (result.Data is Poker.Power.ManifestPendingResult manifestPending)
                    {
                        var finalResult = manifestPending.CompleteManifest(request.Rank ?? 14, request.Suit ?? "Hearts");
                        return Ok(new {
                            Success = true,
                            Message = $"Manifested {finalResult.ChosenCard}",
                            AbilityUsed = true,
                            GameState = GetGameState(game)
                        });
                    }
                }
                
                return Ok(new {
                    Success = result.Success,
                    Message = result.Message,
                    AbilityUsed = result.Success,
                    GameState = GetGameState(game)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{gameId}")]
        public Microsoft.AspNetCore.Mvc.ActionResult EndGame(string gameId)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (_games.TryRemove(gameId, out var game))
            {
                return Ok(new { Success = true, Message = "Game ended and removed" });
            }
            
            return NotFound("Game not found");
        }

        [HttpGet("active")]
        public Microsoft.AspNetCore.Mvc.ActionResult GetActiveGames()
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            var activeGames = _games.Select(kvp => new {
                GameId = kvp.Key,
                PlayerCount = kvp.Value.Players.Count,
                Phase = kvp.Value.CurrentPhase.ToString(),
                IsActive = kvp.Value.IsGameActive,
                CurrentPlayer = kvp.Value.CurrentPlayer?.Name
            }).ToList();
            
            return Ok(new { ActiveGames = activeGames, Count = activeGames.Count });
        }

        // Helper Methods
        private decimal CalculateCallAmount(GameManager game, Player player)
        {
            return Math.Max(0, game.TurnManager.CurrentBet - player.CurrentBet);
        }

        private bool ValidateApiKey()
        {
            var providedKey = Request.Headers["X-API-Key"].FirstOrDefault();
            return providedKey == API_KEY;
        }
        
        // Helper method to get complete game state
        private object GetGameState(GameManager game)
        {
            var calculatedPot = game.Players.Sum(p => p.TotalBetThisHand);
            
            return new
            {
                CurrentPhase = game.CurrentPhase.ToString(),
                CurrentPlayer = game.CurrentPlayer?.Name,
                CurrentPlayerId = game.CurrentPlayer?.ID,
                IsGameActive = game.IsGameActive,
                DealerPosition = game.DealerPosition,
                
                Players = game.Players.Select(p => new
                {
                    Id = p.ID,
                    Name = p.Name,
                    Balance = p.CurrentBalance,
                    CurrentBet = p.CurrentBet,
                    TotalBetThisHand = p.TotalBetThisHand,
                    IsFolded = p.IsFolded,
                    IsAllIn = p.IsAllIn,
                    HasActedThisRound = p.HasActedThisRound,
                    IsMyTurn = p.IsMyTurn,
                    HoleCards = p.HoleCards.Select(c => $"{c.GetRankName()} of {c.Suit}").ToList(),
                    Abilities = p.AbilitySlot.Abilities.Select(a => new { 
                        Id = a.ID,
                        Name = a.Name, 
                        Description = a.Description, 
                        Type = a.Type.ToString() 
                    }).ToList(),
                    ValidActions = game.IsGameActive && p.IsMyTurn ? 
                        p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0)
                        .Select(a => a.ToString()).ToList() : new List<string>()
                }).ToList(),
                
                Board = game.Board.CommunityCards.Select(c => $"{c.GetRankName()} of {c.Suit}").ToList(),
                Pot = calculatedPot,
                PotBreakdown = game.Players.Select(p => new {
                    Player = p.Name,
                    Contributed = p.TotalBetThisHand
                }).Where(p => p.Contributed > 0).ToList(),
                
                TurnManager = game.TurnManager != null ? new {
                    CurrentBet = game.TurnManager.CurrentBet,
                    MinimumRaise = game.TurnManager.MinimumRaise,
                    IsBettingRoundActive = game.TurnManager.IsBettingRoundActive,
                    PlayersRemaining = game.TurnManager.PlayersRemaining
                } : null,
                
                AbilityDeck = new {
                    RemainingAbilities = game.AbilityDeck.RemainingAbilities,
                    IsEmpty = game.AbilityDeck.IsEmpty
                }
            };
        }

        // Request classes
        public class CreateGameRequest
        {
            public List<PlayerRequest> Players { get; set; } = new();
            public decimal SmallBlind { get; set; }
            public decimal BigBlind { get; set; }
        }

        public class PlayerRequest
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal StartingFunds { get; set; }
        }

        public class ActionRequest
        {
            public int PlayerId { get; set; }           // NEW: Required player ID
            public Poker.Players.ActionType ActionType { get; set; }
            public decimal Amount { get; set; }
        }

        public class AbilityRequest
        {
            public int PlayerId { get; set; }
            public string AbilityType { get; set; } = "";
            public int? TargetPlayerId { get; set; }
            public int? CardIndex { get; set; }
            public bool? RevealSuit { get; set; }
            public int? Rank { get; set; }
            public string? Suit { get; set; }
        }
    }
}