using System;
using System.Collections.Generic;

namespace Poker.Core
{
    public class Deck
    {
        private List<Card> _cards;
        private Random _random;
        
        public int RemainingCards => _cards.Count;
        
        public Deck()
        {
            _random = new Random();
            InitializeDeck();
        }
        
        public Deck(int seed) // For testing with predictable results
        {
            _random = new Random(seed);
            InitializeDeck();
        }
        
        private void InitializeDeck()
        {
            _cards = new List<Card>();
            string[] suits = { "Hearts", "Clubs", "Spades", "Diamonds" };
            
            foreach (string suit in suits)
            {
                for (int rank = 2; rank <= 14; rank++)
                {
                    _cards.Add(new Card(rank, suit));
                }
            }
        }
        
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }
        
        public Card DealCard()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot deal from empty deck");
                
            Card card = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            return card;
        }
        
        public List<Card> DealCards(int count)
        {
            if (count > _cards.Count)
                throw new InvalidOperationException($"Cannot deal {count} cards, only {_cards.Count} remaining");
                
            List<Card> dealtCards = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                dealtCards.Add(DealCard());
            }
            return dealtCards;
        }
        
        // NEW: Indexing capabilities
        public Card PeekTopCard()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot peek at empty deck");
                
            return _cards[_cards.Count - 1];
        }
        
        public Card PeekCardAt(int index)
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot peek at empty deck");
                
            if (index < 0 || index >= _cards.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_cards.Count - 1}");
                
            // Index 0 = top card, Index 1 = second from top, etc.
            return _cards[_cards.Count - 1 - index];
        }
        
        public List<Card> PeekTopCards(int count)
        {
            if (count > _cards.Count)
                throw new InvalidOperationException($"Cannot peek {count} cards, only {_cards.Count} remaining");
                
            if (count <= 0)
                throw new ArgumentException("Count must be positive");
                
            var peekedCards = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                peekedCards.Add(_cards[_cards.Count - 1 - i]);
            }
            return peekedCards;
        }
        
        public Card DealCardAt(int index)
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot deal from empty deck");
                
            if (index < 0 || index >= _cards.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_cards.Count - 1}");
                
            // Index 0 = top card, Index 1 = second from top, etc.
            int actualIndex = _cards.Count - 1 - index;
            Card card = _cards[actualIndex];
            _cards.RemoveAt(actualIndex);
            return card;
        }
        
        public Card InsertAt(int index, Card card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
                
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot insert into empty deck");
                
            if (index < 0 || index >= _cards.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {_cards.Count - 1}");
                
            // Index 0 = top card, Index 1 = second from top, etc.
            int actualIndex = _cards.Count - 1 - index;
            Card replacedCard = _cards[actualIndex];
            _cards[actualIndex] = card;
            return replacedCard;
        }
        
        public List<Card> DealCardsAt(List<int> indices)
        {
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));
                
            // Validate all indices first
            foreach (int index in indices)
            {
                if (index < 0 || index >= _cards.Count)
                    throw new ArgumentOutOfRangeException(nameof(indices), $"Index {index} is out of range");
            }
            
            // Sort indices in descending order to avoid index shifting issues when removing
            var sortedIndices = indices.OrderByDescending(i => i).ToList();
            var dealtCards = new List<Card>();
            
            foreach (int index in sortedIndices)
            {
                dealtCards.Add(DealCardAt(index));
            }
            
            // Return in original order
            dealtCards.Reverse();
            return dealtCards;
        }
        
        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < _cards.Count;
        }
        
        public int GetMaxValidIndex()
        {
            return Math.Max(0, _cards.Count - 1);
        }
        
        public void Reset()
        {
            InitializeDeck();
        }
        
        public void ResetAndShuffle()
        {
            Reset();
            Shuffle();
        }
        
        // Debug method to see deck state (useful for testing)
        public List<Card> GetDeckState()
        {
            return new List<Card>(_cards);
        }
    }
}