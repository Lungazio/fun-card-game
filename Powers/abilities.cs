using System;
using System.Collections.Generic;
using System.Linq;
using Poker.Core;
using Poker.Players;

namespace Poker.Power
{
    // Enum for different ability types
    public enum AbilityType
    {
        Peek,
        Burn,
        Manifest,
        Trashman,
        Deadman,
        Chaos,
        Yoink // NEW: Added Yoink ability
    }

    // Abstract base class for all abilities
    public abstract class Ability
    {
        public int ID { get; protected set; }
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public AbilityType Type { get; protected set; }

        protected Ability(int id, string name, string description, AbilityType type)
        {
            ID = id;
            Name = name;
            Description = description;
            Type = type;
        }

        // Abstract method that each ability must implement
        public abstract AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null);

        public override string ToString()
        {
            return $"{Name} - {Description}";
        }

        // Equality based on ID and Type for object comparison
        public override bool Equals(object obj)
        {
            if (obj is Ability other)
                return ID == other.ID && Type == other.Type;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ID, Type);
        }
    }

    // Concrete implementation: Peek Ability
    public class PeekAbility : Ability
    {
        public PeekAbility(int id) : base(id, "Peek", "Look at one card from target player's hand", AbilityType.Peek)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            if (availableTargets == null || !availableTargets.Any())
                return new AbilityResult(false, "No valid targets available");

            // Expecting PeekData with target and card index
            if (!(additionalData is PeekData peekData))
                return new AbilityResult(false, "Invalid peek data provided");

            var targetPlayer = availableTargets.FirstOrDefault(p => p.ID == peekData.TargetPlayerID);
            if (targetPlayer == null)
                return new AbilityResult(false, "Target player not found");

            if (targetPlayer.ID == user.ID)
                return new AbilityResult(false, "Cannot peek at your own cards");

            if (!targetPlayer.HasHoleCards())
                return new AbilityResult(false, "Target player has no cards to peek at");

            if (peekData.CardIndex < 0 || peekData.CardIndex >= targetPlayer.HoleCards.Count)
                return new AbilityResult(false, "Invalid card index");

            // Get the peeked card
            var peekedCard = targetPlayer.HoleCards[peekData.CardIndex];

            return new AbilityResult(true, 
                $"{user.Name} peeked at {targetPlayer.Name}'s card #{peekData.CardIndex + 1}: {peekedCard}",
                new PeekResult(targetPlayer.ID, peekData.CardIndex, peekedCard));
        }
    }

    // Concrete implementation: Burn Ability
    public class BurnAbility : Ability
    {
        public BurnAbility(int id) : base(id, "Burn", "Burn the second card and choose to reveal either its suit or rank", AbilityType.Burn)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            // additionalData should contain the GameManager for burn pile access
            if (!(additionalData is BurnData burnData))
                return new AbilityResult(false, "Invalid burn data provided");

            var deck = burnData.Deck;
            var gameManager = burnData.GameManager;

            if (deck.RemainingCards < 2)
                return new AbilityResult(false, "Not enough cards in deck (need at least 2 for standard + burn ability)");

            // Burn the second card (index 1) - the standard burn would take index 0
            var burntCard = deck.DealCardAt(1);

            // Add the burnt card to the burn pile
            gameManager.AddToBurnPile(burntCard);

            // Return the burnt card so the game can prompt the player for their choice
            // The player will choose what to reveal after this
            return new AbilityResult(true,
                $"{user.Name} used Burn ability - awaiting reveal choice",
                new BurnPendingResult(burntCard));
        }
    }

    // Concrete implementation: Updated Manifest Ability
    public class ManifestAbility : Ability
    {
        public ManifestAbility(int id) : base(id, "Manifest", "Draw 1 card, then choose 1 card from your hand + drawn card to discard", AbilityType.Manifest)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            if (!(additionalData is Deck deck))
                return new AbilityResult(false, "Invalid deck provided");

            if (deck.RemainingCards < 1)
                return new AbilityResult(false, "Not enough cards in deck");

            if (!user.HasHoleCards() || user.HoleCards.Count < 2)
                return new AbilityResult(false, "Player must have hole cards to use manifest");

            // Draw 1 card from the deck
            var drawnCard = deck.DealCard();

            // Create list of all available cards (2 hole cards + 1 drawn card)
            var availableCards = new List<Card>(user.HoleCards) { drawnCard };

            // Return the pending result with all 3 cards for player to choose which to discard
            return new AbilityResult(true,
                $"{user.Name} used Manifest ability - choose which card to discard",
                new ManifestPendingResult(user.HoleCards.ToList(), drawnCard));
        }
    }

    // Concrete implementation: Trashman Ability
    public class TrashmanAbility : Ability
    {
        public TrashmanAbility(int id) : base(id, "Trashman", "Retrieve one of the most recent burnt cards and swap it with one of your hole cards", AbilityType.Trashman)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            // additionalData should contain both burn pile and game phase info
            if (!(additionalData is TrashmanData trashmanData))
                return new AbilityResult(false, "Invalid trashman data provided");

            var burnPile = trashmanData.BurnPile;
            var gamePhase = trashmanData.GamePhase;

            // NEW: Check if it's too early to use Trashman
            if (gamePhase == "Preflop" && burnPile.Count == 0)
                return new AbilityResult(false, "Trashman can only be used after the flop is dealt or after a burn ability has been used");

            if (burnPile.Count == 0)
                return new AbilityResult(false, "No burnt cards available to retrieve");

            if (!user.HasHoleCards() || user.HoleCards.Count < 2)
                return new AbilityResult(false, "Player must have hole cards to use trashman");

            // Get up to 3 most recent burnt cards (from end of list)
            var availableBurntCards = burnPile.TakeLast(Math.Min(3, burnPile.Count)).ToList();

            // Return the pending result with burnt cards for player to choose from
            return new AbilityResult(true,
                $"{user.Name} used Trashman ability - choose which burnt card to retrieve",
                new TrashmanPendingResult(user.HoleCards.ToList(), availableBurntCards));
        }
    }

    // Concrete implementation: Deadman Ability
    public class DeadmanAbility : Ability
    {
        public DeadmanAbility(int id) : base(id, "Deadman", "Reveal all folded players' hole cards", AbilityType.Deadman)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            // additionalData should contain all players (to find folded ones)
            if (!(additionalData is List<Player> allPlayers))
                return new AbilityResult(false, "Invalid player list provided");

            // Find all folded players
            var foldedPlayers = allPlayers.Where(p => p.IsFolded && p.HasHoleCards()).ToList();

            if (!foldedPlayers.Any())
                return new AbilityResult(false, "No folded players with cards to reveal");

            // Create the revelation data
            var deadmanResult = new DeadmanResult(foldedPlayers);

            return new AbilityResult(true,
                $"{user.Name} used Deadman ability - revealed {foldedPlayers.Count} folded player(s)' cards",
                deadmanResult);
        }
    }

    // NEW: Concrete implementation: Yoink Ability
    public class YoinkAbility : Ability
    {
        public YoinkAbility(int id) : base(id, "Yoink", "Switch one of your hole cards with a community card on the board", AbilityType.Yoink)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            // additionalData should contain board information
            if (!(additionalData is YoinkData yoinkData))
                return new AbilityResult(false, "Invalid yoink data provided");

            var board = yoinkData.Board;

            if (board.Count == 0)
                return new AbilityResult(false, "No community cards on the board to yoink");

            if (!user.HasHoleCards() || user.HoleCards.Count < 2)
                return new AbilityResult(false, "Player must have hole cards to use yoink");

            // Return the pending result with board cards and hole cards for player to choose from
            return new AbilityResult(true,
                $"{user.Name} used Yoink ability - choose which cards to switch",
                new YoinkPendingResult(user.HoleCards.ToList(), board.CommunityCards.ToList()));
        }
    }

    // NEW: Concrete implementation: Chaos Ability
    public class ChaosAbility : Ability
    {
        public ChaosAbility(int id) : base(id, "Chaos", "Shuffle all active players' hole cards and redistribute them randomly", AbilityType.Chaos)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            // additionalData should contain all players
            if (!(additionalData is List<Player> allPlayers))
                return new AbilityResult(false, "Invalid player list provided");

            // Get all active players (not folded and have hole cards)
            var activePlayers = allPlayers.Where(p => !p.IsFolded && p.HasHoleCards()).ToList();

            if (activePlayers.Count < 2)
                return new AbilityResult(false, "Need at least 2 active players to use Chaos");

            // Collect all hole cards from active players
            var allHoleCards = new List<Card>();
            var playerCardCounts = new Dictionary<int, int>();

            foreach (var player in activePlayers)
            {
                var playerCards = player.HoleCards.ToList();
                allHoleCards.AddRange(playerCards);
                playerCardCounts[player.ID] = playerCards.Count;
                
                // Clear the player's current hole cards
                player.ClearHoleCards();
            }

            // Shuffle all the collected cards
            var random = new Random();
            for (int i = allHoleCards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (allHoleCards[i], allHoleCards[j]) = (allHoleCards[j], allHoleCards[i]);
            }

            // Redistribute the shuffled cards back to players
            int cardIndex = 0;
            var redistributionInfo = new List<ChaosPlayerInfo>();

            foreach (var player in activePlayers)
            {
                var cardCount = playerCardCounts[player.ID];
                var newCards = new List<Card>();

                for (int i = 0; i < cardCount; i++)
                {
                    if (cardIndex < allHoleCards.Count)
                    {
                        var card = allHoleCards[cardIndex];
                        player.AddHoleCard(card);
                        newCards.Add(card);
                        cardIndex++;
                    }
                }

                redistributionInfo.Add(new ChaosPlayerInfo
                {
                    PlayerId = player.ID,
                    PlayerName = player.Name,
                    NewHoleCards = newCards.Select(c => c.ToString()).ToList()
                });
            }

            var chaosResult = new ChaosResult(redistributionInfo);

            return new AbilityResult(true,
                $"{user.Name} used Chaos ability - shuffled and redistributed all active players' hole cards",
                chaosResult);
        }
    }

    // New class for when trashman is cast but card choices haven't been made yet
    public class TrashmanPendingResult
    {
        public List<Card> OriginalHoleCards { get; set; }
        public List<Card> AvailableBurntCards { get; set; }

        public TrashmanPendingResult(List<Card> originalHoleCards, List<Card> availableBurntCards)
        {
            OriginalHoleCards = originalHoleCards;
            AvailableBurntCards = availableBurntCards;
        }

        // Method to complete the first choice (which burnt card to retrieve)
        public TrashmanStepTwoResult ChooseBurntCard(int burntCardIndex)
        {
            if (burntCardIndex < 0 || burntCardIndex >= AvailableBurntCards.Count)
                throw new ArgumentOutOfRangeException(nameof(burntCardIndex), "Invalid burnt card choice");

            var chosenBurntCard = AvailableBurntCards[burntCardIndex];
            return new TrashmanStepTwoResult(OriginalHoleCards, chosenBurntCard);
        }
    }

    // Class for second step of trashman (choosing which hole card to discard)
    public class TrashmanStepTwoResult
    {
        public List<Card> OriginalHoleCards { get; set; }
        public Card ChosenBurntCard { get; set; }

        public TrashmanStepTwoResult(List<Card> originalHoleCards, Card chosenBurntCard)
        {
            OriginalHoleCards = originalHoleCards;
            ChosenBurntCard = chosenBurntCard;
        }

        // Method to complete the trashman after player chooses which hole card to discard
        public TrashmanResult CompleteTrashman(int holeCardIndex, Player player)
        {
            if (holeCardIndex < 0 || holeCardIndex >= OriginalHoleCards.Count)
                throw new ArgumentOutOfRangeException(nameof(holeCardIndex), "Invalid hole card choice");

            var discardedHoleCard = OriginalHoleCards[holeCardIndex];
            var keptHoleCard = OriginalHoleCards.Where((card, index) => index != holeCardIndex).First();

            // Update player's hole cards: keep one original + add retrieved burnt card
            player.ClearHoleCards();
            player.AddHoleCard(keptHoleCard);
            player.AddHoleCard(ChosenBurntCard);

            return new TrashmanResult(ChosenBurntCard, discardedHoleCard, new List<Card> { keptHoleCard, ChosenBurntCard });
        }
    }

    // New class for when manifest is cast but card choice hasn't been made yet
    public class ManifestPendingResult
    {
        public List<Card> OriginalHoleCards { get; set; }
        public Card DrawnCard { get; set; }
        public List<Card> AllCards { get; set; }

        public ManifestPendingResult(List<Card> originalHoleCards, Card drawnCard)
        {
            OriginalHoleCards = originalHoleCards;
            DrawnCard = drawnCard;
            AllCards = new List<Card>(originalHoleCards) { drawnCard };
        }

        // Method to complete the manifest after player chooses which card to discard
        public ManifestResult CompleteManifest(int discardIndex, Player player)
        {
            if (discardIndex < 0 || discardIndex >= AllCards.Count)
                throw new ArgumentOutOfRangeException(nameof(discardIndex), "Invalid card choice");

            var discardedCard = AllCards[discardIndex];
            var keptCards = AllCards.Where((card, index) => index != discardIndex).ToList();

            // Update player's hole cards
            player.ClearHoleCards();
            foreach (var card in keptCards)
            {
                player.AddHoleCard(card);
            }

            return new ManifestResult(discardedCard, keptCards, DrawnCard);
        }
    }

    public class BurnPendingResult
    {
        public Card BurntCard { get; set; }

        public BurnPendingResult(Card burntCard)
        {
            BurntCard = burntCard;
        }

        // Method to complete the burn after player chooses
        public BurnResult CompleteReveal(bool revealSuit)
        {
            string revealedInfo;
            if (revealSuit)
            {
                revealedInfo = $"Suit: {BurntCard.Suit}";
            }
            else
            {
                revealedInfo = $"Rank: {BurntCard.GetRankName()}";
            }

            return new BurnResult(BurntCard, revealSuit, revealedInfo);
        }
    }

    // Supporting data classes for Peek ability
    public class PeekData
    {
        public int TargetPlayerID { get; set; }
        public int CardIndex { get; set; } // 0 or 1 for first or second hole card

        public PeekData(int targetPlayerID, int cardIndex)
        {
            TargetPlayerID = targetPlayerID;
            CardIndex = cardIndex;
        }
    }

    public class PeekResult
    {
        public int TargetPlayerID { get; set; }
        public int CardIndex { get; set; }
        public Card PeekedCard { get; set; }

        public PeekResult(int targetPlayerID, int cardIndex, Card peekedCard)
        {
            TargetPlayerID = targetPlayerID;
            CardIndex = cardIndex;
            PeekedCard = peekedCard;
        }

        public override string ToString()
        {
            return $"Peeked at card #{CardIndex + 1}: {PeekedCard}";
        }
    }

    // Supporting data classes for Deadman ability
    public class DeadmanData
    {
        public List<Player> AllPlayers { get; set; }

        public DeadmanData(List<Player> allPlayers)
        {
            AllPlayers = allPlayers;
        }
    }

    public class DeadmanResult
    {
        public List<FoldedPlayerInfo> FoldedPlayers { get; set; }

        public DeadmanResult(List<Player> foldedPlayers)
        {
            FoldedPlayers = foldedPlayers.Select(p => new FoldedPlayerInfo
            {
                PlayerId = p.ID,
                PlayerName = p.Name,
                HoleCards = p.HoleCards.Select(c => c.ToString()).ToList()
            }).ToList();
        }

        public override string ToString()
        {
            if (!FoldedPlayers.Any())
                return "No folded players to reveal";

            var revelations = FoldedPlayers.Select(fp => 
                $"{fp.PlayerName}: [{string.Join(", ", fp.HoleCards)}]");
            return $"Revealed folded players: {string.Join("; ", revelations)}";
        }
    }

    public class FoldedPlayerInfo
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public List<string> HoleCards { get; set; } = new();
    }

    // NEW: Supporting data classes for Chaos ability
    public class ChaosData
    {
        public List<Player> AllPlayers { get; set; }

        public ChaosData(List<Player> allPlayers)
        {
            AllPlayers = allPlayers;
        }
    }

    public class ChaosResult
    {
        public List<ChaosPlayerInfo> PlayersAfterChaos { get; set; }

        public ChaosResult(List<ChaosPlayerInfo> playersAfterChaos)
        {
            PlayersAfterChaos = playersAfterChaos;
        }

        public override string ToString()
        {
            if (!PlayersAfterChaos.Any())
                return "No players affected by chaos";

            var redistributions = PlayersAfterChaos.Select(pi => 
                $"{pi.PlayerName}: [{string.Join(", ", pi.NewHoleCards)}]");
            return $"Chaos redistributed cards: {string.Join("; ", redistributions)}";
        }
    }

    public class ChaosPlayerInfo
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public List<string> NewHoleCards { get; set; } = new();
    }

    // NEW: Supporting data classes for Yoink ability
    public class YoinkData
    {
        public Poker.Core.Board Board { get; set; }

        public YoinkData(Poker.Core.Board board)
        {
            Board = board;
        }
    }

    public class YoinkPendingResult
    {
        public List<Card> OriginalHoleCards { get; set; }
        public List<Card> AvailableBoardCards { get; set; }

        public YoinkPendingResult(List<Card> originalHoleCards, List<Card> availableBoardCards)
        {
            OriginalHoleCards = originalHoleCards;
            AvailableBoardCards = availableBoardCards;
        }

        // Method to complete the yoink after player chooses which cards to switch
        public YoinkResult CompleteYoink(int holeCardIndex, int boardCardIndex, Player player, Poker.Core.Board board)
        {
            if (holeCardIndex < 0 || holeCardIndex >= OriginalHoleCards.Count)
                throw new ArgumentOutOfRangeException(nameof(holeCardIndex), "Invalid hole card choice");

            if (boardCardIndex < 0 || boardCardIndex >= AvailableBoardCards.Count)
                throw new ArgumentOutOfRangeException(nameof(boardCardIndex), "Invalid board card choice");

            var holeCardToSwap = OriginalHoleCards[holeCardIndex];
            var boardCardToSwap = AvailableBoardCards[boardCardIndex];

            // Update player's hole cards
            player.ClearHoleCards();
            for (int i = 0; i < OriginalHoleCards.Count; i++)
            {
                if (i == holeCardIndex)
                {
                    player.AddHoleCard(boardCardToSwap); // Add the board card
                }
                else
                {
                    player.AddHoleCard(OriginalHoleCards[i]); // Keep original hole card
                }
            }

            // Update the board - replace the board card with the hole card
            // This is a bit tricky since Board doesn't have a direct replace method
            // We'll need to clear and rebuild the board
            var newBoardCards = new List<Card>();
            for (int i = 0; i < AvailableBoardCards.Count; i++)
            {
                if (i == boardCardIndex)
                {
                    newBoardCards.Add(holeCardToSwap); // Add the hole card
                }
                else
                {
                    newBoardCards.Add(AvailableBoardCards[i]); // Keep original board card
                }
            }

            // Clear and rebuild the board
            board.Clear();
            foreach (var card in newBoardCards)
            {
                board.AddCard(card);
            }

            var newHoleCards = player.HoleCards.ToList();

            return new YoinkResult(holeCardToSwap, boardCardToSwap, newHoleCards, newBoardCards);
        }
    }

    public class YoinkResult
    {
        public Card HoleCardSwapped { get; set; }
        public Card BoardCardSwapped { get; set; }
        public List<Card> NewHoleCards { get; set; }
        public List<Card> NewBoardCards { get; set; }

        public YoinkResult(Card holeCardSwapped, Card boardCardSwapped, List<Card> newHoleCards, List<Card> newBoardCards)
        {
            HoleCardSwapped = holeCardSwapped;
            BoardCardSwapped = boardCardSwapped;
            NewHoleCards = newHoleCards;
            NewBoardCards = newBoardCards;
        }

        public override string ToString()
        {
            var newHoleCardsStr = string.Join(", ", NewHoleCards.Select(c => c.ToString()));
            var newBoardCardsStr = string.Join(", ", NewBoardCards.Select(c => c.ToString()));
            return $"Yoinked {BoardCardSwapped} from board for {HoleCardSwapped} from hand. New hole cards: [{newHoleCardsStr}]. New board: [{newBoardCardsStr}]";
        }
    }

    // Supporting data classes for Trashman ability
    public class TrashmanData
    {
        public List<Card> BurnPile { get; set; }
        public string GamePhase { get; set; } = "";

        public TrashmanData(List<Card> burnPile, string gamePhase)
        {
            BurnPile = burnPile;
            GamePhase = gamePhase;
        }
    }

    public class TrashmanResult
    {
        public Card RetrievedCard { get; set; }
        public Card DiscardedCard { get; set; }
        public List<Card> NewHoleCards { get; set; }

        public TrashmanResult(Card retrievedCard, Card discardedCard, List<Card> newHoleCards)
        {
            RetrievedCard = retrievedCard;
            DiscardedCard = discardedCard;
            NewHoleCards = newHoleCards;
        }

        public override string ToString()
        {
            var newHoleCardsStr = string.Join(", ", NewHoleCards.Select(c => c.ToString()));
            return $"Retrieved {RetrievedCard}, discarded {DiscardedCard}, new hole cards: {newHoleCardsStr}";
        }
    }

    // Supporting data classes for Manifest ability - UPDATED
    public class ManifestData
    {
        public Deck Deck { get; set; }

        public ManifestData(Deck deck)
        {
            Deck = deck;
        }
    }

    public class ManifestResult
    {
        public Card DiscardedCard { get; set; }
        public List<Card> KeptCards { get; set; }
        public Card DrawnCard { get; set; }

        public ManifestResult(Card discardedCard, List<Card> keptCards, Card drawnCard)
        {
            DiscardedCard = discardedCard;
            KeptCards = keptCards;
            DrawnCard = drawnCard;
        }

        public override string ToString()
        {
            var keptNames = string.Join(", ", KeptCards.Select(c => c.ToString()));
            return $"Drew {DrawnCard}, discarded {DiscardedCard}, kept {keptNames}";
        }
    }

    public class BurnData
    {
        public Deck Deck { get; set; }
        public dynamic GameManager { get; set; } // Using dynamic to avoid circular reference

        public BurnData(Deck deck, dynamic gameManager)
        {
            Deck = deck;
            GameManager = gameManager;
        }
    }

    public class BurnResult
    {
        public Card BurntCard { get; set; }
        public bool WasSuitRevealed { get; set; }
        public string RevealedInformation { get; set; }

        public BurnResult(Card burntCard, bool wasSuitRevealed, string revealedInformation)
        {
            BurntCard = burntCard;
            WasSuitRevealed = wasSuitRevealed;
            RevealedInformation = revealedInformation;
        }

        public override string ToString()
        {
            return $"Burnt second card - {RevealedInformation}";
        }
    }

    // Result class for ability usage
    public class AbilityResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public object Data { get; private set; }

        public AbilityResult(bool success, string message, object data = null)
        {
            Success = success;
            Message = message;
            Data = data;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}