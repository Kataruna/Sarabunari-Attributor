using System.Collections.Generic;
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
        [Show] private bool hiddenFlag = true;
        [Show] private int[] hiddenArray = { 10, 20 };
        [Show] private List<string> hiddenList = new List<string> { "alpha", "beta" };
        public void SetSecret(int value) => hiddenSecret = value;
        public void SetHiddenFlag(bool value) => hiddenFlag = value;
        public void AddToHiddenList(string item) => hiddenList.Add(item);

        [Button] public void IncrementCounter() => counter++;
        [Button("Go")] void Go() { }
        [Button] public void Save(int x = 5) => last = x;
        [Button] void Needs(int x) { }
        [Button] void Boom() { counter = 9; throw new System.InvalidOperationException("boom"); }
        [Button("Play", EButtonEnableMode.Playmode)] void PlayOnly() { }
        public float speed;
        [Button("Add 1 Hour", 1), Button("Add 2 Hours", 2)] public void AddHours(int amount) => counter += amount;
        [Button("Zero", 0)] void SetLast(int x) => last = x;
        [Button("Pair", 0, 5)] void SetPair(int a, int b) => last = a * 100 + b;
        [Button("Speed", 1)] void SetSpeed(float s) => speed = s;
        [Button("Bad", "text")] void TakesInt(int x) { }
        [Button("Too many", 1, 2)] void TakesOne(int x) { }

        [Group(3)] [Button] void A() { }
        [Group(3)] [Button] void B() { }
        [Group(3)] [Button] void C() { }
    }
}
