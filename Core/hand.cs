using System;
using System.Collections.Generic;
using System.Linq;

namespace Poker.Core
{
    public class Hand
    {
        private List<Card> _cards;
        
        public int Count => _cards.Count;
        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
        
        public Hand()
        {
            _cards = new List<Card>();
        }
        
        public Hand(IEnumerable<Card> cards)
        {
            _cards = new List<Card>(cards);
        }
        
        public void AddCard(Card card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
                
            _cards.Add(card);
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
        
        public bool RemoveCard(Card card)
        {
            return _cards.Remove(card);
        }
        
        public Card RemoveCardAt(int index)
        {
            if (index < 0 || index >= _cards.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
                
            Card card = _cards[index];
            _cards.RemoveAt(index);
            return card;
        }
        
        public void Clear()
        {
            _cards.Clear();
        }
        
        public bool Contains(Card card)
        {
            return _cards.Contains(card);
        }
        
        public Card GetCard(int index)
        {
            if (index < 0 || index >= _cards.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
                
            return _cards[index];
        }
        
        public bool IsEmpty => _cards.Count == 0;
        
        public override string ToString()
        {
            if (IsEmpty)
                return "Empty hand";
                
            return string.Join(", ", _cards.Select(c => c.ToString()));
        }
        
        public List<Card> ToList()
        {
            return new List<Card>(_cards);
        }
    }
}