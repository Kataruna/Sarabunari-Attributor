using System.Collections.Generic;
using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class DrawerFixture : MonoBehaviour
    {
        [Line] [Range(0, 1)] public float f;
        [MinMaxSlider(0, 10)] public Vector2 range = new Vector2(2, 8);
        [Required("Canvas missing")] public CanvasGroup canvas;
        [SelfAssign] public BoxCollider box;
        [Expandable] public SelfRefAsset config;
        [GetChild] public List<SphereCollider> childSpheres = new List<SphereCollider>();
    }
}
