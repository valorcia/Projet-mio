using UnityEngine;

namespace Mio.Unity.Config
{
    /// <summary>
    /// Placeholder art direction. Everything in M0 is drawn from flat colour
    /// rectangles, so the palette is effectively the entire art pipeline for
    /// now and swapping it restyles all three prototypes at once.
    /// </summary>
    [CreateAssetMenu(menuName = "MIO/Config/Palette", fileName = "Palette")]
    public sealed class PrototypePalette : ScriptableObject
    {
        [Header("Field")]
        public Color Background = new Color(0.09f, 0.10f, 0.15f);
        public Color FieldTint = new Color(1f, 1f, 1f, 0.04f);
        public Color GridLine = new Color(1f, 1f, 1f, 0.07f);

        [Header("Actors")]
        [Tooltip("Cycled for board pieces and blobs.")]
        public Color[] Pieces =
        {
            new Color(0.30f, 0.78f, 0.98f),
            new Color(0.99f, 0.45f, 0.55f),
            new Color(0.55f, 0.90f, 0.52f),
            new Color(0.99f, 0.80f, 0.35f),
            new Color(0.75f, 0.58f, 0.98f),
            new Color(0.99f, 0.62f, 0.38f)
        };

        [Header("Signals")]
        public Color Good = new Color(0.45f, 0.95f, 0.60f);
        public Color Bad = new Color(0.99f, 0.40f, 0.42f);
        public Color Neutral = new Color(1f, 1f, 1f, 0.85f);
        public Color Hazard = new Color(0.95f, 0.30f, 0.38f);

        [Header("HUD")]
        public Color MeterFill = new Color(0.40f, 0.88f, 0.99f);
        public Color MeterTrack = new Color(1f, 1f, 1f, 0.12f);
        public Color TimerFill = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>Safe indexer: wraps, and survives an empty array.</summary>
        public Color Piece(int index)
        {
            if (Pieces == null || Pieces.Length == 0) return Color.white;
            var i = index % Pieces.Length;
            if (i < 0) i += Pieces.Length;
            return Pieces[i];
        }
    }
}
