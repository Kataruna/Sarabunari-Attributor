using System;
using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class PlainFixture : MonoBehaviour
    {
        [Serializable]
        public class Inner
        {
            [SceneName] public string scene;
        }

        public int x;
        public Inner inner;
    }
}
