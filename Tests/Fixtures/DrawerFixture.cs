using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class DrawerFixture : MonoBehaviour
    {
        [Line] [Range(0, 1)] public float f;
        [MinMaxSlider(0, 10)] public Vector2 range = new Vector2(2, 8);
        [Required("Canvas missing")] public CanvasGroup canvas;
    }
}
