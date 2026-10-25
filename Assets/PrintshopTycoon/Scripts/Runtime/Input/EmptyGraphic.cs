using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>Invisible raycast target - receives touches without drawing anything.</summary>
    public class EmptyGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
