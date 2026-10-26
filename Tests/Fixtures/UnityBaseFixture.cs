using UnityEngine;

// Stands in for a Unity base class with serialized fields (e.g. MaskableGraphic): the collector stops at UnityEngine namespaces.
namespace UnityEngine.AttributorTestFake
{
    public class FakeUnityBase : MonoBehaviour
    {
        public int baseField;
    }
}

namespace Sarabunari.Attributor.Tests
{
    public class UnityBaseFixture : UnityEngine.AttributorTestFake.FakeUnityBase
    {
        public int own;
        [Button] void Rebuild() { }
    }
}
