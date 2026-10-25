using System;
using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>Axis-aligned rectangle on the workshop floor (x/z plane).</summary>
    [Serializable]
    public struct Footprint
    {
        public float centerX, centerZ, width, depth;

        public Footprint(float centerX, float centerZ, float width, float depth)
        {
            this.centerX = centerX; this.centerZ = centerZ; this.width = width; this.depth = depth;
        }

        public float MinX => centerX - width * 0.5f;
        public float MaxX => centerX + width * 0.5f;
        public float MinZ => centerZ - depth * 0.5f;
        public float MaxZ => centerZ + depth * 0.5f;

        public bool Overlaps(Footprint other, float spacing)
        {
            return MinX < other.MaxX + spacing && MaxX + spacing > other.MinX &&
                   MinZ < other.MaxZ + spacing && MaxZ + spacing > other.MinZ;
        }

        public bool Inside(Footprint area)
        {
            return MinX >= area.MinX - 0.0001f && MaxX <= area.MaxX + 0.0001f &&
                   MinZ >= area.MinZ - 0.0001f && MaxZ <= area.MaxZ + 0.0001f;
        }
    }

    public class PlacementValidator
    {
        public Footprint BuildArea { get; }
        public float Spacing { get; }
        public float GridSize { get; }

        public PlacementValidator(Footprint buildArea, float spacing = 0.3f, float gridSize = 0.5f)
        {
            BuildArea = buildArea; Spacing = spacing; GridSize = gridSize;
        }

        public float Snap(float value) => GridSize <= 0f ? value : (float)Math.Round(value / GridSize) * GridSize;

        public bool IsValid(Footprint candidate, IEnumerable<Footprint> occupied)
        {
            if (!candidate.Inside(BuildArea)) return false;
            if (occupied != null)
                foreach (var o in occupied)
                    if (candidate.Overlaps(o, Spacing)) return false;
            return true;
        }
    }
}
