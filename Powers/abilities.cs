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
        Manifest
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

            // additionalData should contain just the deck - no choice yet
            if (!(additionalData is Deck deck))
                return new AbilityResult(false, "Invalid deck provided");

            if (deck.RemainingCards < 2)
                return new AbilityResult(false, "Not enough cards in deck (need at least 2 for standard + burn ability)");

            // Burn the second card (index 1) - the standard burn would take index 0
            var burntCard = deck.DealCardAt(1);

            // Return the burnt card so the game can prompt the player for their choice
            // The player will choose what to reveal after this
            return new AbilityResult(true,
                $"{user.Name} used Burn ability - awaiting reveal choice",
                new BurnPendingResult(burntCard));
        }
    }

    // Concrete implementation: Manifest Ability
    public class ManifestAbility : Ability
    {
        public ManifestAbility(int id) : base(id, "Manifest", "Choose any card to place as the second card in the deck", AbilityType.Manifest)
        {
        }

        public override AbilityResult Use(Player user, List<Player> availableTargets, object additionalData = null)
        {
            if (user == null)
                return new AbilityResult(false, "Invalid user");

            // additionalData should contain just the deck - no choice yet
            if (!(additionalData is Deck deck))
                return new AbilityResult(false, "Invalid deck provided");

            if (deck.RemainingCards < 2)
                return new AbilityResult(false, "Not enough cards in deck (need at least 2 cards)");

            // Return success so the game can prompt the player for their card choice
            return new AbilityResult(true,
                $"{user.Name} used Manifest ability - awaiting card choice",
                new ManifestPendingResult(deck));
        }
    }

    // New class for when manifest is cast but card choice hasn't been made yet
    public class ManifestPendingResult
    {
        public Deck Deck { get; set; }

        public ManifestPendingResult(Deck deck)
        {
            Deck = deck;
        }

        // Method to complete the manifest after player chooses their card
        public ManifestResult CompleteManifest(int rank, string suit)
        {
            var chosenCard = new Card(rank, suit);
            var replacedCard = Deck.InsertAt(1, chosenCard); // Insert at position 1 (second card)

            return new ManifestResult(chosenCard, replacedCard);
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

    // Supporting data classes for Manifest ability
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
        public Card ChosenCard { get; set; }
        public Card ReplacedCard { get; set; }

        public ManifestResult(Card chosenCard, Card replacedCard)
        {
            ChosenCard = chosenCard;
            ReplacedCard = replacedCard;
        }

        public override string ToString()
        {
            return $"Manifested {ChosenCard} (replaced {ReplacedCard})";
        }
    }
    public class BurnData
    {
        public Deck Deck { get; set; }

        public BurnData(Deck deck)
        {
            Deck = deck;
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