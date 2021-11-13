
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[RequireComponent(typeof(BoxCollider))]

public class sdReader : UdonSharpBehaviour
{
    public Ender3 _printer;

    public void OnCollisionEnter(Collision collision){
        if (!Networking.IsMaster) {
            return;
        }
        sdCard item = collision.gameObject.GetComponent<sdCard>();
        if (!Utilities.IsValid(item) || item == null){
            return;
        }
        if ((bool) item._is_a_SD_Card){
            _printer.loadedSdCard = item._SDCard_id;
            _printer._sdInsert();
        }
    }
}
