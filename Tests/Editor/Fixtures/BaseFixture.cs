using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class BaseFixture : MonoBehaviour
    {
        public int b1;
        [Button] void BaseButton() { }
        public int b2;
    }
}
