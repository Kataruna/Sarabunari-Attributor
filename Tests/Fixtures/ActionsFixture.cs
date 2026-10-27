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
        [ReadOnly] public int readOnlyField = 12;
        [Show] private int hiddenSecret = 77;
        public void SetSecret(int value) => hiddenSecret = value;

        [Button] public void IncrementCounter() => counter++;
        [Button("Go")] void Go() { }
        [Button] public void Save(int x = 5) => last = x;
        [Button] void Needs(int x) { }
        [Button] void Boom() { counter = 9; throw new System.InvalidOperationException("boom"); }
        [Button("Play", EButtonEnableMode.Playmode)] void PlayOnly() { }

        [Group(3)] [Button] void A() { }
        [Group(3)] [Button] void B() { }
        [Group(3)] [Button] void C() { }
    }
}
