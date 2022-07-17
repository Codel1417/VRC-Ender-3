
using UdonSharp;
using UnityEngine;

namespace Codel1417
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(BoxCollider))]
    public class SDCard : UdonSharpBehaviour
    {
        [Tooltip("The ID associated with the gcode files on the printer")]
        public byte sdCardID = 1;
        [HideInInspector]
        public bool isAsdCard = true;
    }
}