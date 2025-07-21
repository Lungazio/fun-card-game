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
            
            return Ok(new { 
                message = "C# Poker API is working!", 
                timestamp = DateTime.Now,
                version = "1.0.0",
                activeGames = _games.Count
            });
        }

        [HttpPost("create")]
        public Microsoft.AspNetCore.Mvc.ActionResult CreateGame([FromBody] CreateGameRequest request)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            try
            {
                // Validate request
                if (request.Players == null || request.Players.Count < 2 || request.Players.Count > 8)
                    return BadRequest("Game must have 2-8 players");
                
                if (request.SmallBlind <= 0 || request.BigBlind <= 0)
                    return BadRequest("Blinds must be positive");
                
                if (request.BigBlind <= request.SmallBlind)
                    return BadRequest("Big blind must be greater than small blind");
                
                // Validate player data
                var playerIds = request.Players.Select(p => p.Id).ToList();
                if (playerIds.Distinct().Count() != playerIds.Count)
                    return BadRequest("Player IDs must be unique");
                
                var gameId = Guid.NewGuid().ToString();
                var players = request.Players.Select(p => new Player(p.Id, p.Name, p.StartingFunds)).ToList();
                var gameManager = new GameManager(players, request.SmallBlind, request.BigBlind);
                
                _games[gameId] = gameManager;
                
                return Ok(new { 
                    GameId = gameId, 
                    Success = true,
                    PlayerCount = players.Count,
                    Players = players.Select(p => new { p.ID, p.Name, Balance = p.CurrentBalance }),
                    Blinds = new { Small = request.SmallBlind, Big = request.BigBlind },
                    CreatedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, details = ex.StackTrace });
            }
        }

        [HttpPost("{gameId}/start")]
        public Microsoft.AspNetCore.Mvc.ActionResult StartGame(string gameId)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
            
            // Fixed: Check the phase, not IsGameActive
            if (game.CurrentPhase != GamePhase.NotStarted)
            {
                return BadRequest(new { 
                    error = "Game has already been started",
                    currentPhase = game.CurrentPhase.ToString(),
                    message = "Can only start games that are in NotStarted phase"
                });
            }
            
            try
            {
                game.StartNewHand();
                return Ok(new { 
                    Success = true, 
                    Message = "Hand started",
                    GameState = GetGameState(game),
                    StartedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    error = ex.Message, 
                    details = ex.StackTrace,
                    currentPhase = game.CurrentPhase.ToString()
                });
            }
        }

        [HttpPost("{gameId}/action")]
        public Microsoft.AspNetCore.Mvc.ActionResult ProcessAction(string gameId, [FromBody] ActionRequest request)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
            
            // Enhanced validation
            if (!game.IsGameActive)
            {
                return BadRequest(new { 
                    error = "Game is not active",
                    currentPhase = game.CurrentPhase.ToString(),
                    gameStatus = "inactive"
                });
            }
            
            if (game.TurnManager?.IsBettingRoundActive != true)
            {
                return BadRequest(new { 
                    error = "No active betting round",
                    currentPhase = game.CurrentPhase.ToString(),
                    bettingActive = false
                });
            }
            
            // Validate player and turn
            var player = game.Players.FirstOrDefault(p => p.ID == request.PlayerId);
            if (player == null)
            {
                return BadRequest(new { 
                    error = "Player not found",
                    playerId = request.PlayerId,
                    availablePlayers = game.Players.Select(p => new { p.ID, p.Name })
                });
            }
            
            if (game.CurrentPlayer?.ID != request.PlayerId)
            {
                return BadRequest(new { 
                    error = "Not your turn", 
                    currentPlayer = game.CurrentPlayer?.Name,
                    currentPlayerId = game.CurrentPlayer?.ID,
                    yourPlayerId = request.PlayerId,
                    turnOrder = game.Players.Select(p => new { p.ID, p.Name, p.IsMyTurn })
                });
            }
            
            try
            {
                Poker.Players.ActionResult actionResult;
                
                // Validate and execute action
                switch (request.ActionType)
                {
                    case Poker.Players.ActionType.Call:
                        var callAmount = CalculateCallAmount(game, player);
                        if (callAmount == 0)
                        {
                            return BadRequest(new { 
                                error = "Cannot call when no bet to match - use Check instead",
                                currentBet = game.TurnManager.CurrentBet,
                                yourBet = player.CurrentBet
                            });
                        }
                        actionResult = player.Call(callAmount);
                        break;
                        
                    case Poker.Players.ActionType.Check:
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
                        // Enhanced raise validation
                        if (request.Amount <= 0)
                        {
                            return BadRequest(new { 
                                error = "Raise amount must be positive",
                                providedAmount = request.Amount
                            });
                        }
                        
                        var totalRaiseAmount = game.TurnManager.CurrentBet + request.Amount;
                        var minimumRaiseTotal = game.TurnManager.CurrentBet + game.TurnManager.MinimumRaise;
                        
                        if (totalRaiseAmount < minimumRaiseTotal)
                        {
                            return BadRequest(new { 
                                error = "Raise amount too small",
                                minimumRaise = game.TurnManager.MinimumRaise,
                                currentBet = game.TurnManager.CurrentBet,
                                minimumTotalBet = minimumRaiseTotal,
                                yourRaiseAmount = request.Amount,
                                requiredRaiseAmount = minimumRaiseTotal - game.TurnManager.CurrentBet
                            });
                        }
                        
                        var raiseAmountNeeded = totalRaiseAmount - player.CurrentBet;
                        if (player.CurrentBalance < raiseAmountNeeded)
                        {
                            return BadRequest(new { 
                                error = "Insufficient funds for raise",
                                requiredAmount = raiseAmountNeeded,
                                availableBalance = player.CurrentBalance,
                                suggestAllIn = true
                            });
                        }
                        
                        actionResult = player.Raise(raiseAmountNeeded);
                        break;
                        
                    case Poker.Players.ActionType.AllIn:
                        if (player.CurrentBalance <= 0)
                        {
                            return BadRequest(new { 
                                error = "No funds available for all-in",
                                currentBalance = player.CurrentBalance
                            });
                        }
                        actionResult = player.AllIn();
                        break;
                        
                    default:
                        return BadRequest(new { 
                            error = "Invalid action type",
                            providedAction = request.ActionType.ToString(),
                            validActions = player.GetValidActions(game.TurnManager.CurrentBet, game.TurnManager.MinimumRaise)
                        });
                }
                
                if (!actionResult.Success)
                {
                    return BadRequest(new { 
                        error = "Action failed",
                        message = actionResult.Message,
                        playerBalance = player.CurrentBalance,
                        actionType = actionResult.Action.ToString()
                    });
                }
                
                // Process the action and get the result
                var gameProcessed = game.ProcessPlayerAction(request.ActionType, request.Amount);
                
                return Ok(new { 
                    Success = true,
                    ActionResult = actionResult.Message,
                    PlayerAction = new {
                        PlayerId = player.ID,
                        PlayerName = player.Name,
                        Action = request.ActionType.ToString(),
                        Amount = actionResult.Amount,
                        Timestamp = DateTime.Now
                    },
                    GameState = GetGameState(game),
                    // Additional info for debugging/logging
                    GameInfo = new {
                        Phase = game.CurrentPhase.ToString(),
                        NextPlayer = game.CurrentPlayer?.Name,
                        BettingComplete = game.TurnManager?.IsBettingComplete() ?? false
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    error = ex.Message, 
                    details = ex.StackTrace,
                    context = new {
                        gameId,
                        playerId = request.PlayerId,
                        actionType = request.ActionType.ToString(),
                        amount = request.Amount
                    }
                });
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
                GameState = GetGameState(game),
                Timestamp = DateTime.Now,
                ServerInfo = new {
                    Version = "1.0.0",
                    ActiveGames = _games.Count
                }
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
                ActivePlayers = game.Players.Count(p => !p.IsFolded),
                CurrentPlayer = game.CurrentPlayer?.Name,
                CurrentPlayerId = game.CurrentPlayer?.ID,
                BettingActive = game.TurnManager?.IsBettingRoundActive ?? false,
                Pot = game.Players.Sum(p => p.TotalBetThisHand),
                BoardCards = game.Board.Count,
                Timestamp = DateTime.Now
            });
        }

        [HttpPost("{gameId}/abilities/use")]
        public Microsoft.AspNetCore.Mvc.ActionResult UseAbility(string gameId, [FromBody] AbilityRequest request)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (!_games.TryGetValue(gameId, out var game))
                return NotFound("Game not found");
            
            // Enhanced validation for abilities
            var player = game.Players.FirstOrDefault(p => p.ID == request.PlayerId);
            if (player == null)
                return BadRequest("Player not found");
            
            if (!game.IsGameActive)
                return BadRequest("Game is not active");
            
            if (game.CurrentPlayer?.ID != request.PlayerId)
            {
                return BadRequest(new { 
                    error = "Not your turn", 
                    currentPlayer = game.CurrentPlayer?.Name,
                    currentPlayerId = game.CurrentPlayer?.ID 
                });
            }
            
            if (player.AbilitySlot.IsEmpty)
            {
                return BadRequest(new {
                    error = "No abilities available",
                    playerId = request.PlayerId,
                    playerName = player.Name
                });
            }
            
            try
            {
                var ability = player.AbilitySlot.Abilities.FirstOrDefault(a => 
                    a.Type.ToString().ToLower() == request.AbilityType.ToLower());
                
                if (ability == null)
                    return BadRequest(new {
                        error = $"Player doesn't have {request.AbilityType} ability",
                        availableAbilities = player.AbilitySlot.Abilities.Select(a => a.Type.ToString())
                    });
                
                // Handle different ability types with enhanced validation
                object additionalData = null;
                switch (request.AbilityType.ToLower())
                {
                    case "peek":
                        if (!request.TargetPlayerId.HasValue || !request.CardIndex.HasValue)
                            return BadRequest("Peek ability requires targetPlayerId and cardIndex");
                        
                        var targetPlayer = game.Players.FirstOrDefault(p => p.ID == request.TargetPlayerId.Value);
                        if (targetPlayer == null)
                            return BadRequest("Target player not found");
                        
                        if (targetPlayer.ID == request.PlayerId)
                            return BadRequest("Cannot peek at your own cards");
                        
                        if (targetPlayer.IsFolded)
                            return BadRequest("Cannot peek at folded player's cards");
                        
                        if (request.CardIndex < 0 || request.CardIndex > 1)
                            return BadRequest("Card index must be 0 or 1");
                        
                        additionalData = new Poker.Power.PeekData(request.TargetPlayerId.Value, request.CardIndex.Value);
                        break;
                        
                    case "burn":
                        if (!request.RevealSuit.HasValue)
                            return BadRequest("Burn ability requires revealSuit parameter");
                        additionalData = game.Deck;
                        break;
                        
                    case "manifest":
                        if (!request.Rank.HasValue || string.IsNullOrEmpty(request.Suit))
                            return BadRequest("Manifest ability requires rank and suit");
                        
                        if (request.Rank < 2 || request.Rank > 14)
                            return BadRequest("Rank must be between 2 and 14");
                        
                        var validSuits = new[] { "Hearts", "Clubs", "Spades", "Diamonds" };
                        if (!validSuits.Contains(request.Suit))
                            return BadRequest($"Suit must be one of: {string.Join(", ", validSuits)}");
                        
                        additionalData = game.Deck;
                        break;
                        
                    default:
                        return BadRequest($"Unknown ability type: {request.AbilityType}");
                }
                
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
                            AbilityUsed = request.AbilityType,
                            PlayerId = request.PlayerId,
                            PlayerName = player.Name,
                            Result = finalResult.RevealedInformation,
                            GameState = GetGameState(game),
                            Timestamp = DateTime.Now
                        });
                    }
                    else if (result.Data is Poker.Power.ManifestPendingResult manifestPending)
                    {
                        var finalResult = manifestPending.CompleteManifest(request.Rank ?? 14, request.Suit ?? "Hearts");
                        return Ok(new {
                            Success = true,
                            Message = $"Manifested {finalResult.ChosenCard}",
                            AbilityUsed = request.AbilityType,
                            PlayerId = request.PlayerId,
                            PlayerName = player.Name,
                            Result = finalResult.ChosenCard.ToString(),
                            GameState = GetGameState(game),
                            Timestamp = DateTime.Now
                        });
                    }
                }
                
                return Ok(new {
                    Success = result.Success,
                    Message = result.Message,
                    AbilityUsed = request.AbilityType,
                    PlayerId = request.PlayerId,
                    PlayerName = player.Name,
                    GameState = GetGameState(game),
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    error = ex.Message, 
                    details = ex.StackTrace,
                    context = new {
                        gameId,
                        playerId = request.PlayerId,
                        abilityType = request.AbilityType
                    }
                });
            }
        }

        [HttpDelete("{gameId}")]
        public Microsoft.AspNetCore.Mvc.ActionResult EndGame(string gameId)
        {
            if (!ValidateApiKey()) return Unauthorized();
            
            if (_games.TryRemove(gameId, out var game))
            {
                return Ok(new { 
                    Success = true, 
                    Message = "Game ended and removed",
                    GameId = gameId,
                    FinalState = new {
                        Phase = game.CurrentPhase.ToString(),
                        Players = game.Players.Select(p => new {
                            p.ID,
                            p.Name,
                            FinalBalance = p.CurrentBalance,
                            TotalBet = p.TotalBetThisHand,
                            Status = p.IsFolded ? "Folded" : p.IsAllIn ? "All-in" : "Active"
                        })
                    },
                    EndedAt = DateTime.Now
                });
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
                ActivePlayers = kvp.Value.Players.Count(p => !p.IsFolded),
                Phase = kvp.Value.CurrentPhase.ToString(),
                IsActive = kvp.Value.IsGameActive,
                CurrentPlayer = kvp.Value.CurrentPlayer?.Name,
                Pot = kvp.Value.Players.Sum(p => p.TotalBetThisHand),
                Players = kvp.Value.Players.Select(p => new { p.ID, p.Name, p.CurrentBalance })
            }).ToList();
            
            return Ok(new { 
                ActiveGames = activeGames, 
                Count = activeGames.Count,
                Timestamp = DateTime.Now,
                ServerStats = new {
                    TotalGamesCreated = _games.Count,
                    Uptime = DateTime.Now // Could track actual uptime
                }
            });
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
        
        // Enhanced helper method to get complete game state
        private object GetGameState(GameManager game)
        {
            var calculatedPot = game.Players.Sum(p => p.TotalBetThisHand);
            
            return new
            {
                // Core game state
                CurrentPhase = game.CurrentPhase.ToString(),
                CurrentPlayer = game.CurrentPlayer?.Name,
                CurrentPlayerId = game.CurrentPlayer?.ID,
                IsGameActive = game.IsGameActive,
                DealerPosition = game.DealerPosition,
                
                // Enhanced player information
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
                    
                    // Enhanced abilities info
                    Abilities = p.AbilitySlot.Abilities.Select(a => new { 
                        Id = a.ID,
                        Name = a.Name, 
                        Description = a.Description, 
                        Type = a.Type.ToString() 
                    }).ToList(),
                    AbilityCount = p.AbilitySlot.Count,
                    
                    // Enhanced valid actions with more context
                    ValidActions = game.IsGameActive && p.IsMyTurn ? 
                        p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0)
                        .Select(a => a.ToString()).ToList() : new List<string>(),
                    
                    // Action context for UI
                    ActionContext = game.IsGameActive && p.IsMyTurn ? new {
                        CanCall = p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0).Contains(ActionType.Call),
                        CanCheck = p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0).Contains(ActionType.Check),
                        CanRaise = p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0).Contains(ActionType.Raise),
                        CanFold = p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0).Contains(ActionType.Fold),
                        CanAllIn = p.GetValidActions(game.TurnManager?.CurrentBet ?? 0, game.TurnManager?.MinimumRaise ?? 0).Contains(ActionType.AllIn),
                        CallAmount = Math.Max(0, (game.TurnManager?.CurrentBet ?? 0) - p.CurrentBet),
                        MinRaiseAmount = game.TurnManager?.MinimumRaise ?? 0,
                        MaxRaiseAmount = p.CurrentBalance
                    } : null
                }).ToList(),
                
                // Board state
                Board = game.Board.CommunityCards.Select(c => $"{c.GetRankName()} of {c.Suit}").ToList(),
                BoardState = new {
                    CardsDealt = game.Board.Count,
                    IsFlopDealt = game.Board.IsFlopDealt,
                    IsTurnDealt = game.Board.IsTurnDealt,
                    IsRiverDealt = game.Board.IsRiverDealt
                },
                
                // Pot information
                Pot = calculatedPot,
                PotBreakdown = game.Players.Select(p => new {
                    PlayerId = p.ID,
                    PlayerName = p.Name,
                    Contributed = p.TotalBetThisHand
                }).Where(p => p.Contributed > 0).ToList(),
                
                // Turn manager state
                TurnManager = game.TurnManager != null ? new {
                    CurrentBet = game.TurnManager.CurrentBet,
                    MinimumRaise = game.TurnManager.MinimumRaise,
                    IsBettingRoundActive = game.TurnManager.IsBettingRoundActive,
                    PlayersRemaining = game.TurnManager.PlayersRemaining,
                    PlayersCanAct = game.TurnManager.PlayersCanAct
                } : null,
                
                // Ability deck state
                AbilityDeck = new {
                    RemainingAbilities = game.AbilityDeck.RemainingAbilities,
                    IsEmpty = game.AbilityDeck.IsEmpty,
                    Distribution = game.AbilityDeck.GetDistribution()
                },
                
                // Game statistics for UI
                GameStats = new {
                    HandsPlayed = 1, // Could track this
                    AveragePot = calculatedPot, // Could track average
                    LargestPot = calculatedPot, // Could track maximum
                    PlayersRemaining = game.Players.Count(p => !p.IsFolded),
                    AllInPlayers = game.Players.Count(p => p.IsAllIn)
                }
            };
        }

        // Request classes with enhanced validation
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
            public int PlayerId { get; set; }
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