using System;
using System.Collections.Generic;
using System.Linq;

namespace Poker.Power
{
    public class Slot
    {
        private List<Ability> _abilities;
        
        public int Count => _abilities.Count;
        public IReadOnlyList<Ability> Abilities => _abilities.AsReadOnly();
        public bool IsEmpty => _abilities.Count == 0;
        public bool IsFull => _abilities.Count >= 2; // Max 2 abilities per game

        public Slot()
        {
            _abilities = new List<Ability>();
        }

        public bool AddAbility(Ability ability)
        {
            if (ability == null)
                throw new ArgumentNullException(nameof(ability));

            if (IsFull)
                return false; // Cannot add more than 2 abilities

            _abilities.Add(ability);
            return true;
        }

        public bool RemoveAbility(Ability ability)
        {
            return _abilities.Remove(ability);
        }

        public Ability RemoveAbilityAt(int index)
        {
            if (index < 0 || index >= _abilities.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var ability = _abilities[index];
            _abilities.RemoveAt(index);
            return ability;
        }

        public Ability GetAbility(int index)
        {
            if (index < 0 || index >= _abilities.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _abilities[index];
        }

        public Ability FindAbilityByType(AbilityType type)
        {
            return _abilities.FirstOrDefault(a => a.Type == type);
        }

        public bool HasAbility(Ability ability)
        {
            return _abilities.Contains(ability);
        }

        public bool HasAbilityOfType(AbilityType type)
        {
            return _abilities.Any(a => a.Type == type);
        }

        public AbilityResult UseAbility(Ability ability, Players.Player user, List<Players.Player> availableTargets, object additionalData = null)
        {
            if (!HasAbility(ability))
                return new AbilityResult(false, "Player does not have this ability");

            // Execute the ability
            var result = ability.Use(user, availableTargets, additionalData);

            // If successful, remove the ability from the slot (consumed)
            if (result.Success)
            {
                RemoveAbility(ability);
            }

            return result;
        }

        public bool Contains(Ability ability)
        {
            return _abilities.Contains(ability);
        }

        public void Clear()
        {
            _abilities.Clear();
        }

        public List<Ability> ToList()
        {
            return new List<Ability>(_abilities);
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "Empty slot";

            var abilityStrings = _abilities.Select(a => a.ToString());
            return $"Abilities: {string.Join(", ", abilityStrings)}";
        }
    }
}