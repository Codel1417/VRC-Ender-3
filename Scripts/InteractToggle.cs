
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class InteractToggle : UdonSharpBehaviour
{
    public UdonSharpBehaviour printer;
   public override void Interact(){
       printer.SendCustomEvent("_ToggleMesh");
   }
}
