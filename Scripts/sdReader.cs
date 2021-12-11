
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Codel1417
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(BoxCollider))]
    public class sdReader : UdonSharpBehaviour
    {
        private Ender3 _printer;

        private void Start()
        {
            _printer = GetComponentInParent<Ender3>();
        }

        public void OnCollisionEnter(Collision collision){
            if (!Networking.IsMaster) {
                return;
            }
            sdCard item = collision.gameObject.GetComponent<sdCard>();
            
            if (!Utilities.IsValid(item) || item == null){
                return;
            }
            if (item.isAsdCard)
            {
                _printer._sdInsert(item.sdCardID);
            }
        }
    }

}