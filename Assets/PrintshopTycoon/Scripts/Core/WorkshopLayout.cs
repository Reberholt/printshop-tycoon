using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// Floor plan of the first workshop (metres, x = right, z = away from the camera).
    /// Shared by the 3D builder and the placement rules so they can never disagree.
    /// </summary>
    public static class WorkshopLayout
    {
        public const float HalfWidth = 6f;   // room spans x -6..6
        public const float HalfDepth = 8f;   // room spans z -8..8

        public static readonly Footprint Shelf = new Footprint(-4.6f, 6.6f, 2.2f, 1.0f);
        public static readonly Footprint OrderBoard = new Footprint(-1.2f, 7.6f, 2.0f, 0.4f);
        public static readonly Footprint DtfPrinter = new Footprint(2.2f, 6.4f, 2.0f, 1.2f);
        public static readonly Footprint StarterPress = new Footprint(4.6f, 3.6f, 1.6f, 2.4f);
        public static readonly Footprint PackingTable = new Footprint(-4.6f, -2.5f, 1.4f, 2.4f);

        /// <summary>Free floor where new machines can be placed.</summary>
        public static readonly Footprint BuildArea = new Footprint(3.4f, -3.0f, 4.8f, 7.0f);

        public const string StarterPressId = "press_1";

        public static IEnumerable<Footprint> FixedObstacles
        {
            get
            {
                yield return Shelf;
                yield return OrderBoard;
                yield return DtfPrinter;
                yield return PackingTable;
            }
        }
    }
}
