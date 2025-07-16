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
        
        public Card PeekTopCard()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot peek at empty deck");
                
            return _cards[_cards.Count - 1];
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
    }
}