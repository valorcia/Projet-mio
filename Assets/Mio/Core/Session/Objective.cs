using System;
using System.Collections.Generic;
using Mio.Core.Common;

namespace Mio.Core.Session
{
    /// <summary>One line of an objective: "3 cotton products", "20 wood".</summary>
    public struct ObjectiveGoal
    {
        /// <summary>Short label for the HUD, e.g. "COTTON" or "DRESS".</summary>
        public string Label;

        public int Required;
        public int Current;

        public ObjectiveGoal(string label, int required)
        {
            Label = label;
            Required = required < 0 ? 0 : required;
            Current = 0;
        }

        public bool Done => Current >= Required;

        /// <summary>Capped so overshooting one goal cannot mask another.</summary>
        public int Counted => Current > Required ? Required : Current;
    }

    /// <summary>
    /// The obvious short-term objective every M0.2 prototype must show.
    ///
    /// Shared so that objective_progress means the same thing in all four data
    /// sets: the fraction of required items actually delivered, never a
    /// prototype's private idea of "how well it went".
    /// </summary>
    public sealed class Objective
    {
        private readonly ObjectiveGoal[] _goals;

        public Objective(params ObjectiveGoal[] goals)
        {
            _goals = goals ?? new ObjectiveGoal[0];
        }

        public IReadOnlyList<ObjectiveGoal> Goals => _goals;

        public int Count => _goals.Length;

        public ObjectiveGoal this[int index] => _goals[index];

        /// <summary>
        /// Adds to the first goal with a matching label. Unknown labels are
        /// ignored rather than throwing: a prototype may produce things the
        /// objective does not ask for, and that is not an error.
        /// </summary>
        public bool Add(string label, int amount = 1)
        {
            if (amount <= 0) return false;

            for (var i = 0; i < _goals.Length; i++)
            {
                if (!string.Equals(_goals[i].Label, label, StringComparison.Ordinal)) continue;

                _goals[i].Current += amount;
                return true;
            }

            return false;
        }

        public int RequiredTotal
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _goals.Length; i++) n += _goals[i].Required;
                return n;
            }
        }

        public int DeliveredTotal
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _goals.Length; i++) n += _goals[i].Counted;
                return n;
            }
        }

        /// <summary>
        /// Delivered over required, counted per item rather than per goal, so a
        /// goal asking for 20 weighs more than one asking for 1 — which is what
        /// a player feels.
        /// </summary>
        public float Progress01
        {
            get
            {
                var required = RequiredTotal;
                return required <= 0 ? 1f : MathK.Clamp01(DeliveredTotal / (float)required);
            }
        }

        public bool Completed
        {
            get
            {
                for (var i = 0; i < _goals.Length; i++)
                {
                    if (!_goals[i].Done) return false;
                }

                return true;
            }
        }

        public void Reset()
        {
            for (var i = 0; i < _goals.Length; i++) _goals[i].Current = 0;
        }
    }
}
