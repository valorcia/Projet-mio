using System;

namespace Mio.Core.Pack
{
    [Serializable]
    public sealed class PackConfig
    {
        public float Duration = 60f;

        public int Width = 8;
        public int Height = 8;

        /// <summary>Pieces offered at once. Refilled when all are used.</summary>
        public int TraySize = 3;

        public int ScorePerCell = 5;
        public int ScorePerLine = 60;

        /// <summary>Extra points per line beyond the first in one placement.</summary>
        public int ComboBonusPerExtraLine = 40;

        public int TargetScore = 800;

        /// <summary>
        /// Board rectangle in play-field space. The view fills this rect
        /// exactly, so its proportions decide the cells': these defaults give
        /// square cells for an 8x8 board on a 9:16 field.
        /// </summary>
        public float FieldMinX = 0.08f;
        public float FieldMaxX = 0.92f;
        public float FieldMinY = 0.30f;
        public float FieldMaxY = 0.77f;

        /// <summary>Vertical centre of the tray, below the board.</summary>
        public float TrayY = 0.15f;

        /// <summary>Half-height of a tray slot's touch target.</summary>
        public float TrayTouchRadius = 0.11f;

        /// <summary>
        /// How far above the finger the held piece floats, in cells. Without
        /// this the thumb covers exactly the thing the player is aiming.
        /// </summary>
        public float DragLiftCells = 1.6f;

        /// <summary>Optional custom catalogue. Falls back to PackShapes.Default.</summary>
        public PackShape[] Shapes;

        public PackShape[] ResolveShapes()
        {
            return Shapes == null || Shapes.Length == 0 ? PackShapes.Default : Shapes;
        }

        public PackConfig Clone() => (PackConfig)MemberwiseClone();
    }
}
