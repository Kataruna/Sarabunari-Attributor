using UnityEngine;

namespace Sarabunari.Attributor.Tests
{
    public class TypoFixture : MonoBehaviour
    {
        [EnableIf("doesNotExist")] public int typo;
    }
}
