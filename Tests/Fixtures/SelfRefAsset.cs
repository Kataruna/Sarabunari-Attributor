using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class SelfRefAsset : ScriptableObject
    {
        [Expand] public SelfRefAsset other;
    }
}
