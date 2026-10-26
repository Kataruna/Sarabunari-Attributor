using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class ActionsFixture : MonoBehaviour
    {
        public int counter;
        public int last;
        public bool flag = true;
        bool Prop => false;
        bool Method() => true;

        [Locked] public int locked;

        [Button] public void IncrementCounter() => counter++;
        [Button("Go")] void Go() { }
        [Button] public void Save(int x = 5) => last = x;
        [Button] void Needs(int x) { }
        [Button("Play", EButtonEnableMode.Playmode)] void PlayOnly() { }

        [Group(3)] [Button] void A() { }
        [Group(3)] [Button] void B() { }
        [Group(3)] [Button] void C() { }
    }
}
