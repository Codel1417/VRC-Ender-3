
using UdonSharp;
using UnityEngine;

namespace Codel1417
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GcodeFile : UdonSharpBehaviour
    {
        public TextAsset file;
        public int sdCard = 0;
        [HideInInspector] public int index = 0;
    }
}