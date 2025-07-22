using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using Poker.Game;
using Poker.Players;
using Poker.Core;

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
                // Process the action through GameManager (no duplicate calls)
                var gameProcessed = game.ProcessPlayerAction(request.ActionType, request.Amount);
                
                if (!gameProcessed)
                {
                    return BadRequest(new { 
                        error = "Action failed",
                        message = "Game manager rejected the action",
                        playerBalance = player.CurrentBalance,
                        actionType = request.ActionType.ToString()
                    });
                }
                
                return Ok(new { 
                    Success = true,
                    ActionResult = "Action processed successfully",
                    PlayerAction = new {
                        PlayerId = player.ID,
                        PlayerName = player.Name,
                        Action = request.ActionType.ToString(),
                        Amount = request.Amount,
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
                        // Updated: Pass BurnData with both deck and game manager
                        additionalData = new Poker.Power.BurnData(game.Deck, game);
                        break;
                        
                    case "manifest":
                        // Check if this is the initial manifest or completion
                        if (request.DiscardIndex.HasValue)
                        {
                            // This is completing a pending manifest
                            return CompleteManifest(gameId, request, game, player);
                        }
                        else
                        {
                            // This is starting a new manifest
                            additionalData = game.Deck;
                        }
                        break;
                        
                    case "trashman":
                        // Check if this is initial trashman, step 2, or final completion
                        if (request.BurntCardIndex.HasValue && request.HoleCardIndex.HasValue)
                        {
                            // This is final completion (step 3) - both indices provided
                            return CompleteTrashman(gameId, request, game, player);
                        }
                        else if (request.BurntCardIndex.HasValue)
                        {
                            // This is step 2 (choosing hole card to discard) - only burnt card index provided
                            return TrashmanStepTwo(gameId, request, game, player);
                        }
                        else
                        {
                            // This is initial trashman (step 1) - no indices provided
                            additionalData = game.BurnPile.ToList();
                        }
                        break;
                        
                    case "deadman":
                        // Deadman is a simple one-step ability - just needs all players
                        additionalData = game.Players.ToList();
                        break;
                        
                    default:
                        return BadRequest($"Unknown ability type: {request.AbilityType}");
                }
                
                var availableTargets = game.Players.Where(p => p.ID != request.PlayerId && !p.IsFolded).ToList();
                var result = player.UseAbility(ability, availableTargets, additionalData);
                
                if (result.Success)
                {
                    // Handle pending results for Burn, Manifest, and Trashman
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
                        // Return the choice for the player
                        return Ok(new {
                            Success = true,
                            Message = "Choose which card to discard",
                            AbilityUsed = request.AbilityType,
                            PlayerId = request.PlayerId,
                            PlayerName = player.Name,
                            ChoiceRequired = true,
                            AvailableCards = manifestPending.AllCards.Select((card, index) => new {
                                Index = index,
                                Card = card.ToString(),
                                Rank = card.GetRankName(),
                                Suit = card.Suit,
                                IsDrawnCard = card.Equals(manifestPending.DrawnCard),
                                CardType = card.Equals(manifestPending.DrawnCard) ? "Drawn" : "Hole Card"
                            }),
                            DrawnCard = new {
                                Rank = manifestPending.DrawnCard.Rank,
                                Suit = manifestPending.DrawnCard.Suit,
                                Card = manifestPending.DrawnCard.ToString()
                            },
                            Instructions = "Select one card to discard. The remaining 2 will become your new hole cards.",
                            Timestamp = DateTime.Now
                        });
                    }
                    else if (result.Data is Poker.Power.TrashmanPendingResult trashmanPending)
                    {
                        // Return the first choice for the player (which burnt card to retrieve)
                        return Ok(new {
                            Success = true,
                            Message = "Choose which burnt card to retrieve",
                            AbilityUsed = request.AbilityType,
                            PlayerId = request.PlayerId,
                            PlayerName = player.Name,
                            ChoiceRequired = true,
                            Step = 1,
                            AvailableBurntCards = trashmanPending.AvailableBurntCards.Select((card, index) => new {
                                Index = index,
                                Card = card.ToString(),
                                Rank = card.GetRankName(),
                                Suit = card.Suit
                            }),
                            CurrentHoleCards = trashmanPending.OriginalHoleCards.Select(card => card.ToString()),
                            Instructions = "Select which burnt card to retrieve. You will then choose which hole card to discard.",
                            Timestamp = DateTime.Now
                        });
                    }
                    else if (result.Data is Poker.Power.DeadmanResult deadmanResult)
                    {
                        // Return the revealed folded players' cards
                        return Ok(new {
                            Success = true,
                            Message = $"Deadman ability used - revealed {deadmanResult.FoldedPlayers.Count} folded player(s)' cards",
                            AbilityUsed = request.AbilityType,
                            PlayerId = request.PlayerId,
                            PlayerName = player.Name,
                            FoldedPlayers = deadmanResult.FoldedPlayers.Select(fp => new {
                                PlayerId = fp.PlayerId,
                                PlayerName = fp.PlayerName,
                                HoleCards = fp.HoleCards
                            }),
                            Summary = deadmanResult.ToString(),
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

        // Helper method to handle Trashman Step 2 (choosing hole card to discard)
        private Microsoft.AspNetCore.Mvc.ActionResult TrashmanStepTwo(string gameId, AbilityRequest request, GameManager game, Player player)
        {
            if (!request.BurntCardIndex.HasValue)
                return BadRequest("BurntCardIndex is required for Trashman step 2");

            // Find the trashman ability (it should still be there since we didn't consume it yet)
            var trashmanAbility = player.AbilitySlot.FindAbilityByType(Poker.Power.AbilityType.Trashman);
            if (trashmanAbility == null)
                return BadRequest("Player no longer has trashman ability");

            // Get burn pile and validate choice
            var burnPile = game.BurnPile.ToList();
            var availableBurntCards = burnPile.TakeLast(Math.Min(3, burnPile.Count)).ToList();
            
            if (request.BurntCardIndex < 0 || request.BurntCardIndex >= availableBurntCards.Count)
                return BadRequest($"BurntCardIndex must be between 0 and {availableBurntCards.Count - 1}");

            var chosenBurntCard = availableBurntCards[request.BurntCardIndex.Value];

            // Return step 2 choice (which hole card to discard)
            return Ok(new {
                Success = true,
                Message = "Choose which hole card to discard",
                AbilityUsed = request.AbilityType,
                PlayerId = request.PlayerId,
                PlayerName = player.Name,
                ChoiceRequired = true,
                Step = 2,
                ChosenBurntCard = new {
                    Index = request.BurntCardIndex.Value,
                    Card = chosenBurntCard.ToString(),
                    Rank = chosenBurntCard.GetRankName(),
                    Suit = chosenBurntCard.Suit
                },
                AvailableHoleCards = player.HoleCards.Select((card, index) => new {
                    Index = index,
                    Card = card.ToString(),
                    Rank = card.GetRankName(),
                    Suit = card.Suit
                }),
                Instructions = $"You chose to retrieve {chosenBurntCard}. Now select which hole card to discard.",
                Timestamp = DateTime.Now
            });
        }

        // Helper method to complete Trashman (final step)
        private Microsoft.AspNetCore.Mvc.ActionResult CompleteTrashman(string gameId, AbilityRequest request, GameManager game, Player player)
        {
            if (!request.BurntCardIndex.HasValue || !request.HoleCardIndex.HasValue)
                return BadRequest("BurntCardIndex and HoleCardIndex are required to complete trashman");

            // Find the trashman ability
            var trashmanAbility = player.AbilitySlot.FindAbilityByType(Poker.Power.AbilityType.Trashman);
            if (trashmanAbility == null)
                return BadRequest("Player no longer has trashman ability");

            // Get burn pile and validate burnt card choice
            var burnPile = game.BurnPile.ToList();
            var availableBurntCards = burnPile.TakeLast(Math.Min(3, burnPile.Count)).ToList();
            
            if (request.BurntCardIndex < 0 || request.BurntCardIndex >= availableBurntCards.Count)
                return BadRequest($"BurntCardIndex must be between 0 and {availableBurntCards.Count - 1}");

            if (request.HoleCardIndex < 0 || request.HoleCardIndex >= player.HoleCards.Count)
                return BadRequest($"HoleCardIndex must be between 0 and {player.HoleCards.Count - 1}");

            var chosenBurntCard = availableBurntCards[request.BurntCardIndex.Value];
            var originalHoleCards = player.HoleCards.ToList();
            var discardedHoleCard = originalHoleCards[request.HoleCardIndex.Value];
            var keptHoleCard = originalHoleCards.Where((card, index) => index != request.HoleCardIndex.Value).First();

            // Update player's hole cards
            player.ClearHoleCards();
            player.AddHoleCard(keptHoleCard);
            player.AddHoleCard(chosenBurntCard);

            // Add discarded hole card to burn pile (becomes most recent)
            game.AddToBurnPile(discardedHoleCard);

            // Consume the ability since it's complete
            player.AbilitySlot.ConsumeAbility(trashmanAbility);

            var result = new Poker.Power.TrashmanResult(chosenBurntCard, discardedHoleCard, new List<Card> { keptHoleCard, chosenBurntCard });

            return Ok(new {
                Success = true,
                Message = $"Trashman completed - {result}",
                AbilityUsed = request.AbilityType,
                PlayerId = request.PlayerId,
                PlayerName = player.Name,
                Result = new {
                    RetrievedCard = chosenBurntCard.ToString(),
                    DiscardedCard = discardedHoleCard.ToString(),
                    NewHoleCards = new List<string> { keptHoleCard.ToString(), chosenBurntCard.ToString() }
                },
                GameState = GetGameState(game),
                Timestamp = DateTime.Now
            });
        }
        private Microsoft.AspNetCore.Mvc.ActionResult CompleteManifest(string gameId, AbilityRequest request, GameManager game, Player player)
        {
            if (!request.DiscardIndex.HasValue)
                return BadRequest("DiscardIndex is required to complete manifest");

            if (!request.DrawnCard.HasValue || string.IsNullOrEmpty(request.DrawnCardSuit))
                return BadRequest("DrawnCard rank and suit are required to complete manifest");

            // Reconstruct the drawn card from the request
            var drawnCard = new Card(request.DrawnCard.Value, request.DrawnCardSuit);
            
            // Get current hole cards and add drawn card
            var allCards = new List<Card>(player.HoleCards) { drawnCard };
            
            if (request.DiscardIndex < 0 || request.DiscardIndex >= allCards.Count)
                return BadRequest($"DiscardIndex must be between 0 and {allCards.Count - 1}");

            var discardedCard = allCards[request.DiscardIndex.Value];
            var keptCards = allCards.Where((card, index) => index != request.DiscardIndex.Value).ToList();

            // Update player's hole cards
            player.ClearHoleCards();
            foreach (var card in keptCards)
            {
                player.AddHoleCard(card);
            }

            var result = new Poker.Power.ManifestResult(discardedCard, keptCards, drawnCard);

            return Ok(new {
                Success = true,
                Message = $"Manifest completed - {result}",
                AbilityUsed = request.AbilityType,
                PlayerId = request.PlayerId,
                PlayerName = player.Name,
                Result = new {
                    DrawnCard = drawnCard.ToString(),
                    DiscardedCard = discardedCard.ToString(),
                    NewHoleCards = keptCards.Select(c => c.ToString()).ToList()
                },
                GameState = GetGameState(game),
                Timestamp = DateTime.Now
            });
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
            
            // Properties for manifest completion
            public int? DiscardIndex { get; set; } // Which card to discard (0, 1, or 2)
            public int? DrawnCard { get; set; } // Rank of the drawn card (for verification)
            public string? DrawnCardSuit { get; set; } // Suit of the drawn card (for verification)
            
            // NEW: Properties for trashman completion
            public int? BurntCardIndex { get; set; } // Which burnt card to retrieve (0, 1, or 2)
            public int? HoleCardIndex { get; set; } // Which hole card to discard (0 or 1)
            public int? RetrievedCardIndex { get; set; } // For verification in final step
            
            // Legacy properties (can be removed eventually)
            public int? Rank { get; set; }
            public string? Suit { get; set; }
        }
    }
}