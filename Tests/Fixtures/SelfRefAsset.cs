using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class SelfRefAsset : ScriptableObject
    {
        public int power = 10;
        [Expandable] public SelfRefAsset other;
    }
}
