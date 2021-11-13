
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GcodeFile : UdonSharpBehaviour
{
    public TextAsset file;
    public int sdCard = 0;
}
