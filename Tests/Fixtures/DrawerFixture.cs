using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class DrawerFixture : MonoBehaviour
    {
        [Line] [Range(0, 1)] public float f;
    }
}
