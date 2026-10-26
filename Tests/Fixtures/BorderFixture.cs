using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class BorderFixture : MonoBehaviour
    {
        [Fold("Border")] [Group("Outer", 2)] public Transform tl, tr, bl, br;
        [Fold("Border")] [Group("Inner", 2)] public Transform itl, itr, ibl, ibr;
        [Fold("Border")] [Group("Outer")] [HideInInspector] public int hidden;
        [Header("H")] public int afterGroup;
    }
}
