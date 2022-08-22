
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Codel1417
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [RequireComponent(typeof(BoxCollider))]
    public class SDReader : UdonSharpBehaviour
    {
        [NonSerialized] GcodeFile[] _files;
        [UdonSynced] [HideInInspector]
        public int currentCard = 0;
        [UdonSynced] [HideInInspector]
        public int gcodeFileSelected = 0;
        [SerializeField] private GameObject gcodeFilesParent;
        
        private GcodeFile[] _gcodeFilesOnCurrentCard;
        private bool _isReady = false;
        public void _Init()
        {
            _files = gcodeFilesParent.GetComponentsInChildren<GcodeFile>();
            SetFileIds();
            _isReady = true;
            _gcodeFilesOnCurrentCard = GetFilesOnCard();
        }

        private SDCard _lastSDCard;
        public void OnCollisionEnter(Collision collision){
            if (_isReady)
            {
                if (!Networking.IsMaster) {
                    return;
                }
                _lastSDCard = collision.gameObject.GetComponent<SDCard>();
            
                if (!Utilities.IsValid(_lastSDCard) || _lastSDCard == null){
                    return;
                }
                if (_lastSDCard.isAsdCard)
                {
                    //_printer._sdInsert(item.sdCardID);
                    currentCard = _lastSDCard.sdCardID;
                    _gcodeFilesOnCurrentCard = GetFilesOnCard();
                }
            }
        }

        private void SetFileIds()
        {
            for (int i = 0; i < _files.Length; i++)
            {
                _files[i].index = i;
            }
        }

        private GcodeFile[] _filestemp1;
        private int _filesTempIndex;
        private GcodeFile[] _files2;

        private GcodeFile[] GetFilesOnCard()
        {
            if (_isReady)
            {
                _filestemp1 = new GcodeFile[_files.Length];
                _filesTempIndex = 0;
                for (int i = 0; i < _files.Length; i++)
                {
                    if (_files[i].sdCard == currentCard)
                    {
                        _filestemp1[_filesTempIndex] = _files[i];
                        _filesTempIndex++;
                    }
                } 
                //resize array
                _files2 = new GcodeFile[_filesTempIndex];
                for (int i = 0; i < _filesTempIndex; i++)
                {
                    _files2[i] = _filestemp1[i];
                }
                return _files2;
            }

            return new GcodeFile[0];
        }

        private string[] _names;
        public string[] GetModelNamesFromCurrentCard()
        {
            if (_isReady)
            {
                _names = new string[_gcodeFilesOnCurrentCard.Length];
                for (int i = 0; i < _gcodeFilesOnCurrentCard.Length; i++)
                {
                    _names[i] = _gcodeFilesOnCurrentCard[i].name;
                }
                return _names;
            }

            return new string[0];
        }
        public GcodeFile GetGCodeFileFromName(String name)
        {
            if (_isReady)
            {
                for (int i = 0; i < _files.Length; i++)
                {
                    if (_files[i].name == name)
                    {
                        gcodeFileSelected = i;
                        RequestSerialization();
                        return _files[i];
                    }
                }
            }
            return null;
        }
        public GcodeFile GetGCodeFileFromIndex(int index)
        {
            if (_isReady && index < _files.Length)
            {
                gcodeFileSelected = index;
                RequestSerialization();
                return _files[index];
            }
            return null;
        }

        public GcodeFile GetGcodeFile()
        {
            if (_isReady)
            {
                return _files[gcodeFileSelected];
            }
            return null;
        }
    }

}