using System;
using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Session;

namespace Mio.Core.Playtest
{
    /// <summary>The five post-test questions, asked only once all four are done.</summary>
    public enum PostTestQuestion
    {
        PlayAgain = 0,
        EasiestToUnderstand = 1,
        MostSatisfying = 2,
        MostDifferent = 3,
        WouldOpenNow = 4
    }

    /// <summary>One tester's answers. No personal data, only a local label.</summary>
    public sealed class PostTestAnswers
    {
        public string TesterId = string.Empty;
        public DateTime AnsweredUtc;

        private readonly Dictionary<PostTestQuestion, PrototypeId> _answers =
            new Dictionary<PostTestQuestion, PrototypeId>();

        public IReadOnlyDictionary<PostTestQuestion, PrototypeId> Answers => _answers;

        public void Answer(PostTestQuestion question, PrototypeId choice)
        {
            _answers[question] = choice;
            AnsweredUtc = DateTime.UtcNow;
        }

        public bool TryGet(PostTestQuestion question, out PrototypeId choice) =>
            _answers.TryGetValue(question, out choice);

        public bool Complete => _answers.Count >= 5;

        public static string TextOf(PostTestQuestion question)
        {
            switch (question)
            {
                case PostTestQuestion.PlayAgain:
                    return "Which game would you most like to play again?";
                case PostTestQuestion.EasiestToUnderstand:
                    return "Which game was easiest to understand?";
                case PostTestQuestion.MostSatisfying:
                    return "Which game felt most satisfying?";
                case PostTestQuestion.MostDifferent:
                    return "Which game felt most different from games you already know?";
                default:
                    return "If I gave you the phone back right now, which would you open?";
            }
        }
    }

    /// <summary>
    /// One tester playing all four candidates, in a shuffled order.
    ///
    /// The order is randomised because whichever prototype goes first gets a
    /// fresh, patient tester and whichever goes last gets a tired one. Always
    /// testing in the same order would bake that advantage into the
    /// comparison and we would never see it.
    ///
    /// The tester is not asked which they preferred until every prototype has
    /// been played, so the question is about all four rather than about the
    /// one still on screen.
    /// </summary>
    public sealed class PlaytestRound
    {
        private readonly PrototypeId[] _order;
        private int _index;

        public PlaytestRound(string testerId, DeterministicRng rng = null, PrototypeId[] prototypes = null)
        {
            TesterId = string.IsNullOrEmpty(testerId) ? "T000" : testerId;

            var source = prototypes ?? PrototypeIds.Candidates;
            _order = (PrototypeId[])source.Clone();

            if (rng != null) Shuffle(_order, rng);

            _index = 0;
            Answers = new PostTestAnswers { TesterId = TesterId };
        }

        public string TesterId { get; }

        /// <summary>The shuffled running order for this tester.</summary>
        public IReadOnlyList<PrototypeId> Order => _order;

        public PostTestAnswers Answers { get; }

        /// <summary>How many prototypes have been finished.</summary>
        public int Completed => _index;

        public int Total => _order.Length;

        public bool Finished => _index >= _order.Length;

        /// <summary>The prototype to play now, or null once all are done.</summary>
        public PrototypeId? Current => Finished ? (PrototypeId?)null : _order[_index];

        /// <summary>Marks the current prototype finished and moves on.</summary>
        public PrototypeId? Advance()
        {
            if (!Finished) _index++;
            return Current;
        }

        public void Restart() => _index = 0;

        /// <summary>Fisher-Yates, driven by the seeded generator so a round is reproducible.</summary>
        private static void Shuffle(PrototypeId[] items, DeterministicRng rng)
        {
            for (var i = items.Length - 1; i > 0; i--)
            {
                var j = rng.Range(0, i + 1);
                var swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }
    }
}
