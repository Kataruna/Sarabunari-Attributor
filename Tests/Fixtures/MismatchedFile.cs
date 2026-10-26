using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class NotMatchingName : MonoBehaviour
    {
        [Button] void Btn() { }
        public int f;
    }
}
