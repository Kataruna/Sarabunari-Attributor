using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    // Source text that used to confuse the declaration finder.
    public class TrickyFixture : MonoBehaviour
    {
        public int a;
        public char open = '{';
        public string path = @"C:\";
        /* } { */
        int Twice => 2 + c;
        [Button] void B() { }
        public int c;
    }
}
