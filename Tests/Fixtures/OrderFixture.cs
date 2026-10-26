using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class OrderFixture : MonoBehaviour
    {
        public int a;
        [Button] void B() { }
        public int c;
        [Show] float s;
        void SetLate() { late = 1; }
        int LateProp => late;
        [Button] void Early() { }
        public int late;
        [Button] void AfterLate() { }
        [Fold("A")] [Group("B")] public int x;
    }
}
