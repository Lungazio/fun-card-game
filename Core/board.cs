using System;
using System.Collections.Generic;
using System.Linq;

namespace Poker.Core
{
    public class Board
    {
        private List<Card> _communityCards;
        
        public int Count => _communityCards.Count;
        public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();
        
        public Board()
        {
            _communityCards = new List<Card>();
        }
        
        public void AddCard(Card card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
                
            if (_communityCards.Count >= 5)
                throw new InvalidOperationException("Board cannot have more than 5 community cards");
                
            _communityCards.Add(card);
        }
        
        public void AddCards(IEnumerable<Card> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
                
            foreach (var card in cards)
            {
                AddCard(card);
            }
        }
        
        public void DealFlop(Deck deck)
        {
            if (_communityCards.Count > 0)
                throw new InvalidOperationException("Flop already dealt");
                
            BurnCard(deck);
            AddCards(deck.DealCards(3));
        }
        
        public void DealTurn(Deck deck)
        {
            if (_communityCards.Count != 3)
                throw new InvalidOperationException("Turn can only be dealt after flop");
                
            BurnCard(deck);
            AddCard(deck.DealCard());
        }
        
        public void DealRiver(Deck deck)
        {
            if (_communityCards.Count != 4)
                throw new InvalidOperationException("River can only be dealt after turn");
                
            BurnCard(deck);
            AddCard(deck.DealCard());
        }
        
        private void BurnCard(Deck deck)
        {
            if (deck.RemainingCards > 0)
            {
                deck.DealCard(); // Burn (discard) one card
            }
        }
        
        public Card GetCard(int index)
        {
            if (index < 0 || index >= _communityCards.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
                
            return _communityCards[index];
        }
        
        public List<Card> GetFlop()
        {
            if (_communityCards.Count < 3)
                throw new InvalidOperationException("Flop not yet dealt");
                
            return _communityCards.Take(3).ToList();
        }
        
        public Card GetTurn()
        {
            if (_communityCards.Count < 4)
                throw new InvalidOperationException("Turn not yet dealt");
                
            return _communityCards[3];
        }
        
        public Card GetRiver()
        {
            if (_communityCards.Count < 5)
                throw new InvalidOperationException("River not yet dealt");
                
            return _communityCards[4];
        }
        
        public bool IsFlopDealt => _communityCards.Count >= 3;
        public bool IsTurnDealt => _communityCards.Count >= 4;
        public bool IsRiverDealt => _communityCards.Count >= 5;
        public bool IsComplete => _communityCards.Count == 5;
        public bool IsEmpty => _communityCards.Count == 0;
        
        public void Clear()
        {
            _communityCards.Clear();
        }
        
        public override string ToString()
        {
            if (IsEmpty)
                return "Board: Empty";
                
            return $"Board: {string.Join(", ", _communityCards.Select(c => c.ToString()))}";
        }
        
        public List<Card> ToList()
        {
            return new List<Card>(_communityCards);
        }
    }
}