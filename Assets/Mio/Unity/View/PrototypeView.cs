using Mio.Core.Session;
using Mio.Unity.Config;
using Mio.Unity.Feedback;
using UnityEngine;

namespace Mio.Unity.View
{
    /// <summary>
    /// Shared wiring for the three prototype views.
    ///
    /// A view only ever reads the rules. All state lives in Core, so the view
    /// can be rebuilt, restyled or thrown away without touching gameplay.
    /// </summary>
    public abstract class PrototypeView : MonoBehaviour
    {
        protected RectTransform Root { get; private set; }
        protected PrototypePalette Palette { get; private set; }
        protected PunchAnimator Punch { get; private set; }

        public void Initialise(RectTransform root, PrototypePalette palette, PunchAnimator punch)
        {
            Root = root;
            Palette = palette;
            Punch = punch;
            Build();
        }

        /// <summary>Creates the persistent visual objects. Called once.</summary>
        protected abstract void Build();

        /// <summary>Rebuilds transient state at the start of a session.</summary>
        public virtual void OnSessionBegan() { }

        /// <summary>Pushes the current simulation state onto the visuals.</summary>
        public abstract void Refresh();

        /// <summary>Optional per-cue reaction, e.g. punching the tapped cell.</summary>
        public virtual void OnCue(in FeedbackCue cue) { }

        protected Color Dim(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
    }
}
