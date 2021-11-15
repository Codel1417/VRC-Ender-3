using System;
using System.Diagnostics;
using System.Globalization;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace Codel1417
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class Ender3 : UdonSharpBehaviour
    {
        private GcodeFile[] _files;
        private TextAsset[] _gCode;
        private string[] _modelName;
        private int[] _cardID;

        [Header("Colors")] [ColorUsage(false)] [SerializeField]
        private Color plasticColor = new Color(255, 0, 0);

        [ColorUsage(false)] [SerializeField] private Color backgroundColor = new Color(0, 0, 255);
        [ColorUsage(false)] [SerializeField] private Color foregroundColor = new Color(255, 255, 255);

        [Header("Other Options")]
        [Tooltip("Slows down the print to more realistic speeds")]
        [Range(50f, 1000f)]
        [UdonSynced]
        public float printerSpeed = 100f;

        [Tooltip("Temperature in C")] [SerializeField] [Range(10f, 45f)]
        private float ambientTemperature = 20f;

        [Range(0, 1)] [SerializeField] private float audioVolume = 1f;

        [Tooltip("Automatically print file 0 on sd card 0")] [SerializeField]
        private bool autoStartPrint;

        [Header("Printer Objects")] [Tooltip("Vertical Axis")] [SerializeField]
        private Transform zAxis;

        [Tooltip("Forword/Back Axis")] [SerializeField]
        private Transform yAxis;

        [Tooltip("Left/Right Axis")] [SerializeField]
        private Transform xAxis;

        [SerializeField] private Animator printFan;

        [Tooltip("The point the hotend nozzle is located")] [SerializeField]
        private Transform nozzle;

        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private BoxCollider pickupObject;
        private string[] _gcodeFile = new string[1];
        [UdonSynced] [HideInInspector] public int loadedSdCard;
        private int _gcodeFileSelected;
        [SerializeField] private GameObject gcodeFilesParent;
        [UdonSynced] [HideInInspector] public int networkFileSelected;
        private int _gcodeFilePosition;
        [UdonSynced] [HideInInspector] public int networkFilePosition;
        [NonSerialized] public string[] PopupOptions = { "X Axis", "Y Axis", "Z Axis" };

        [Header("Axis Assignment")] [SerializeField]
        private Vector3 minPosition = new Vector3(-0.03462034f, -0.04733565f, 0.007330472f);

        //[Popup("@popupOptions")]
        [SerializeField] public int xAxisMovementAxis;

        //[Popup("@popupOptions")]
        [SerializeField] public int yAxisMovementAxis = 2;

        [SerializeField]
        //[Popup("@popupOptions")]
        public int zAxisMovementAxis = 1;

        [Tooltip("The max position for each axis. How far should each axis move")] [SerializeField]
        private Vector3 maxPosition = new Vector3(0.2f, -0.2f, -0.24f);

        private Vector3 printerSizeInMM = new Vector3(235, 235, 250);
        private Vector3 _normalPosition;
        private Vector3 _calcVelocity, _currentPosition, _printerCordPosition;
        private Vector3 _velocity;
        private bool _isBusy;
        [UdonSynced] [HideInInspector] public bool isPrinting;
        private bool _isFileLoaded;
        [UdonSynced] [HideInInspector] public bool isPaused;
        private bool _isWaitingHotend, _isWaitingBed;
        private bool _isManualProgress;
        private float _printProgress;
        private bool _isMeshHidden;
        private float _fanSpeed;

        private float _feedRate,
            _currentBedTemperature,
            _targetBedTemperature,
            _currentHotendTemperature,
            _targetHotendTemperature;

        [Header("Display")] [SerializeField] private GameObject statusPage;

        [SerializeField] private Text textPage,
            textValue,
            textAdd1,
            textAdd10,
            textAdd25,
            textRemove1,
            textRemove10,
            textRemove25,
            textOption1,
            textOption2,
            textOption3,
            textOption4,
            textStatus,
            textHotendTargetTemperature,
            textHotendCurrentTemperature,
            textBedCurrentTemperature,
            textBedTargetTemperature,
            textFanSpeed,
            textFeedRate,
            textPrinterName,
            textXPos,
            textYPos,
            textZPos,
            textTime,
            textPageTitle,
            textCancel,
            textConfirmation;

        [SerializeField] private Slider textPrintProgress;

        [SerializeField] private Image imageUp,
            imageDown,
            imageGcodeConfirmation,
            imageHotend,
            imageBed,
            imageFan,
            imageBackground,
            imageMiddleBar,
            imageProgressBar,
            imageProgressBarFill,
            imageBackButton,
            imageConfirmationButton,
            imageCancelButton,
            imageSD;

        private float _timeMin;
        [SerializeField] private InputField gcodeInput;

        private float _printStartTime;

        //private MeshFilter[] meshObjects = new MeshFilter[1000];
        //private int meshObjectCount = 0;
        private string[] previousPage = new string[10];
        private int _pageDepth;
        private string _lcdMessage = "";
        private bool _extrudeCheck;
        private Stopwatch _stopWatch = new Stopwatch();
        [SerializeField] private Material lineMaterial;
        private float catchUpTimeout = 20f;
        [Header("Audio")] [SerializeField] private AudioSource fanAudio;
        [SerializeField] private AudioSource speaker, xMotorAudio, yMotorAudio, zMotorAudio;
        private const string VERSION_INFO = "V1.2 by CodeL1417";
        private float _printerScale;

        private string[] _options;

        //Where does the top of the 4 displayed options begin
        private int _listPagePosition;
        private int _lastSyncByteCount;
        private bool _lastSyncSuccessful = true;
        private int _totalVertices;
        private bool _isRelativeMovement;
        [SerializeField] private float trailOffset;
        [Header("Mesh")] public MeshFilter meshFilter;

        private bool _isFirstTime = true; // for stopping initial beep
        private VRCPickup _pickup;
        private VRCObjectSync _objectSync;
        
        private Mesh _trailGeneratedMesh;
        private CombineInstance[] _combine = new CombineInstance[2];
        private Mesh _bigMesh;
        private Mesh _combined;
        public override void OnDeserialization()
        {
            //If the person joins late make sure the correct file is loaded
            if (networkFileSelected == _gcodeFileSelected) return;
            _gcodeFileSelected = networkFileSelected;
            PrintFinished();
            StartPrint();
        }

        public void _VketStart()
        {
            InitialStart();
        }

        private void Start()
        {
            _VketStart();
        }

        public void _VketOnBoothEnter()
        {
            _isMeshHidden = false;
            meshFilter.gameObject.SetActive(!_isMeshHidden);
        }

        public void _VketOnBoothExit()
        {
            _isMeshHidden = true;
            meshFilter.gameObject.SetActive(!_isMeshHidden);
        }

        private void InitialStart()
        {       
            _combined = new Mesh();
            _trailGeneratedMesh = new Mesh();
            _trailGeneratedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _bigMesh = new Mesh();
            _bigMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.mesh = _bigMesh;
            _combine[0].mesh = _trailGeneratedMesh;
            _combine[1].mesh = _bigMesh;

            //Load files
            _files = gcodeFilesParent.GetComponentsInChildren<GcodeFile>();
            _modelName = new string[_files.Length];
            for (int i = 0; i < _files.Length; i++)
            {
                _modelName[i] = _files[i].name;
            }

            _gCode = new TextAsset[_files.Length];
            for (int i = 0; i < _files.Length; i++)
            {
                _gCode[i] = _files[i].file;
            }

            _cardID = new int[_files.Length];
            for (int i = 0; i < _files.Length; i++)
            {
                _cardID[i] = _files[i].sdCard;
            }
            _pickup = (VRCPickup)pickupObject.GetComponent(typeof(VRCPickup));
            _objectSync = (VRCObjectSync)pickupObject.GetComponent(typeof(VRCObjectSync));

            _printerScale = transform.localScale.x;
            _normalPosition = new Vector3(0.5f, 0.5f, 0.3f);
            _currentPosition = new Vector3(0.5f, 0.5f, 0.3f);
            _currentHotendTemperature = ambientTemperature;
            _currentBedTemperature = ambientTemperature;
            meshFilter.transform.position = Vector3.zero;

            Move();
            _displayStatus();
            lineMaterial.SetFloat("_MaxSegmentLength", 0.1f * _printerScale);
            lineMaterial.SetFloat("_Width", 0.00035f * _printerScale);
            M107(); //sets default fanspeed of 0
            _lcdMessage = VERSION_INFO;
            _printerCordPosition.x = Mathf.Lerp(0f, printerSizeInMM.x, _currentPosition.x);
            _printerCordPosition.y = Mathf.Lerp(0f, printerSizeInMM.y, _currentPosition.y);
            _printerCordPosition.z = Mathf.Lerp(0f, printerSizeInMM.z, _currentPosition.z);
            UpdateAudioVolume();
            isPrinting = autoStartPrint;
            ReadFile(_gCode[0]); //load default file

            //generateBounds();
        }

        public override void OnPreSerialization()
        {
            if (Networking.IsOwner(gameObject))
            {
                networkFilePosition = _gcodeFilePosition;
            }
        }

        public void PrintFinished()
        {
            isPrinting = false;
            _targetHotendTemperature = 0;
            _targetBedTemperature = 0;
            M107();
            _isManualProgress = false;
            _extrudeCheck = false;
            _lcdMessage = "Finished";
            GenerateMesh();
            meshFilter.transform.SetParent(pickupObject.transform);
            pickupObject.enabled = true;
        }

        private int _networkSyncOwner;
        private void ReSync()
        {
            _networkSyncOwner = networkFilePosition - _gcodeFilePosition;
            if (_networkSyncOwner > 500)
            {
                isPrinting = true;
                _stopWatch.Start();
                _lcdMessage = "Syncing";
                _isWaitingBed = false;
                _isWaitingHotend = false;
                _currentBedTemperature = _targetBedTemperature;
                _currentHotendTemperature = _targetHotendTemperature;
                while (_gcodeFilePosition < networkFilePosition)
                {
                    if (_isBusy)
                    {
                        FastMove();
                    }
                    else
                    {
                        ParseGcode(_gcodeFile[_gcodeFilePosition]);
                        _gcodeFilePosition++;
                    }

                    if (_stopWatch.ElapsedMilliseconds > catchUpTimeout)
                    {
                        break;
                    }
                }

                if (_networkSyncOwner < 500)
                {
                    _lcdMessage = VERSION_INFO;
                }

                _stopWatch.Stop();
                _stopWatch.Reset();
            }
        }

        private void _ToggleMesh()
        {
            speaker.Play();
            _isMeshHidden = !_isMeshHidden;
            meshFilter.gameObject.SetActive(!_isMeshHidden);
        }

        private void FixedUpdate()
        {
            UpdateLoop();
        }

        public void _VketFixedUpdate()
        {
            UpdateLoop();
        }

        private void UpdateLoop()
        {
            ReSync();
            Heaters();
            Move();
            MotorSounds();
            if (isPrinting)
            {
                if (_isFileLoaded)
                {
                    if (!_isBusy && !isPaused && !_isWaitingHotend && !_isWaitingBed)
                    {
                        if (_gcodeFilePosition + 1 < _gcodeFile.Length && _isFileLoaded)
                        {
                            ParseGcode(_gcodeFile[_gcodeFilePosition]);
                            _gcodeFilePosition++;
                        }
                        else if (Vector3.Distance(Vector3.one, _currentPosition) < 0.001f)
                        {
                            PrintFinished();
                        }
                        else
                        {
                            _normalPosition = Vector3.one;
                        }
                    }
                }
                else
                {
                    SendCustomNetworkEvent(NetworkEventTarget.All, "StartPrint");
                }
            }
            Periodically();
        }

        private void Periodically()
        {
            _timeMin += Time.fixedDeltaTime;
            if (_timeMin >= 1f)
            {
                _timeMin -= 1;
                Display();
                RequestSerialization(); //Manual Sync go burr
            }
            else if (_pageDepth != 0)
            {
                Display();
            }
        }

        public void StartPrint()
        {
            networkFileSelected = _gcodeFileSelected;
            ReadFile(_gCode[_gcodeFileSelected]);
            isPrinting = true;
            isPaused = false;
            _extrudeCheck = false;
            _lcdMessage = "Printing";
            _printStartTime = Time.time;
            _gcodeFilePosition = 0;
            networkFilePosition = 0;
            CleanupMesh();
            RequestSerialization();
        }

        //clears them display
        private void ResetDisplay()
        {
            UpdateColor();
            statusPage.SetActive(false);
            textPageTitle.gameObject.SetActive(false);
            imageBackButton.gameObject.SetActive(false);
            imageConfirmationButton.gameObject.SetActive(false);
            imageCancelButton.gameObject.SetActive(false);
            gcodeInput.gameObject.SetActive(false);
            imageGcodeConfirmation.gameObject.SetActive(false);
            textOption1.text = "";
            textOption2.text = "";
            textOption3.text = "";
            textOption4.text = "";
            textPageTitle.text = "";
            textPage.text = "";
            textOption1.gameObject.SetActive(false);
            textOption2.gameObject.SetActive(false);
            textOption3.gameObject.SetActive(false);
            textOption4.gameObject.SetActive(false);
            imageUp.gameObject.SetActive(false);
            imageDown.gameObject.SetActive(false);
            if (_isFirstTime)
            {
                _isFirstTime = false;
            }
            else
            {
                speaker.Play();
            }

            textAdd1.gameObject.SetActive(false);
            textAdd10.gameObject.SetActive(false);
            textAdd25.gameObject.SetActive(false);
            textRemove1.gameObject.SetActive(false);
            textRemove10.gameObject.SetActive(false);
            textRemove25.gameObject.SetActive(false);
            textValue.gameObject.SetActive(false);
            textPage.gameObject.SetActive(false);
        }

        private void _displayStatus()
        {
            ResetDisplay();
            _pageDepth = 0;
            statusPage.SetActive(true);
        }

        public void _displayMainMenu()
        {
            _displayListMenu("Main Menu");
        }

        private void _displayGcodeInput()
        {
            AddPage("Gcode Input");
            ResetDisplay();
            gcodeInput.gameObject.SetActive(true);
            imageGcodeConfirmation.gameObject.SetActive(true);
            textPageTitle.gameObject.SetActive(true);
            textPageTitle.text = "Enter Gcode";
            imageBackButton.gameObject.SetActive(true);
        }

        public void _confirm()
        {
            if (Networking.IsMaster)
            {
                switch (textPageTitle.text)
                {
                    case "Start Print?":
                        _displayStatus();
                        SendCustomNetworkEvent(NetworkEventTarget.All, "StartPrint");
                        break;
                    case "Cancel Print?":
                        SendCustomNetworkEvent(NetworkEventTarget.All, "PrintFinished");
                        _displayStatus();
                        break;
                    case "Reset Printer?":
                        SendCustomNetworkEvent(NetworkEventTarget.All, "ResetPrinter");
                        _displayStatus();
                        _displayStatus();
                        break;
                    case "Enter Gcode":
                        _displayStatus();
                        _gcodeFile = gcodeInput.text.Split('\n');
                        isPrinting = true;
                        _isFileLoaded = true;
                        _gcodeFilePosition = 0;
                        networkFilePosition = 0;
                        break;
                    default:
                        _back();
                        break;
                }
            }
            else _back();

        }

        public void _cancel()
        {
            _back();
        }

        private string _previous;
        public void _back()
        {
            if (_pageDepth <= 1)
            {
                _displayStatus();
                _pageDepth = 0;
                return;
            }
            _pageDepth = _pageDepth - 2;
            _previous = previousPage[_pageDepth];
            switch (_previous)
            {
                case "Gcode Input":
                case "Printer Control":
                case "Options":
                case "Main Menu":
                case "Debug":
                case "Files":
                    _displayListMenu(_previous);
                    break;
            }
        }

        private void AddPage(string page)
        {
            previousPage[_pageDepth] = page;
            _pageDepth++;
        }

        private void DisplayValueOption(String title)
        {
            ResetDisplay();
            AddPage(title);
            textPageTitle.text = title;
            textAdd1.gameObject.SetActive(true);
            textAdd10.gameObject.SetActive(true);
            textAdd25.gameObject.SetActive(true);
            textRemove1.gameObject.SetActive(true);
            textRemove10.gameObject.SetActive(true);
            textRemove25.gameObject.SetActive(true);
            textValue.gameObject.SetActive(true);
            textPageTitle.gameObject.SetActive(true);
            imageBackButton.gameObject.SetActive(true);
            DisplayValue();
        }

        private void _displayConfirmation(String title)
        {
            AddPage(title);
            ResetDisplay();
            textPageTitle.gameObject.SetActive(true);
            textPageTitle.text = title;
            imageBackButton.gameObject.SetActive(true);
            imageConfirmationButton.gameObject.SetActive(true);
            imageCancelButton.gameObject.SetActive(true);
        }

        private void UpdateColor()
        {
            trailRenderer.sharedMaterial.color = plasticColor;
            textStatus.color = foregroundColor;
            textHotendTargetTemperature.color = foregroundColor;
            textBedTargetTemperature.color = foregroundColor;
            textHotendCurrentTemperature.color = foregroundColor;
            textBedCurrentTemperature.color = foregroundColor;
            textFeedRate.color = foregroundColor;
            textFanSpeed.color = foregroundColor;
            textXPos.color = backgroundColor;
            textYPos.color = backgroundColor;
            textZPos.color = backgroundColor;
            textPrinterName.color = foregroundColor;
            imageBackground.color = backgroundColor;
            imageBed.color = foregroundColor;
            imageHotend.color = foregroundColor;
            imageFan.color = foregroundColor;
            imageMiddleBar.color = foregroundColor;
            textTime.color = foregroundColor;
            imageProgressBar.color = foregroundColor;
            imageProgressBarFill.color = foregroundColor;
            imageConfirmationButton.color = foregroundColor;
            imageGcodeConfirmation.color = foregroundColor;
            imageCancelButton.color = foregroundColor;
            textPageTitle.color = foregroundColor;
            imageBackButton.color = foregroundColor;
            imageSD.color = foregroundColor;
            textOption1.color = foregroundColor;
            textOption2.color = foregroundColor;
            textOption3.color = foregroundColor;
            textOption4.color = foregroundColor;
            imageUp.color = foregroundColor;
            imageDown.color = foregroundColor;
            textAdd1.color = foregroundColor;
            textAdd10.color = foregroundColor;
            textAdd25.color = foregroundColor;
            textRemove1.color = foregroundColor;
            textRemove10.color = foregroundColor;
            textRemove25.color = foregroundColor;
            textValue.color = foregroundColor;
            textPage.color = foregroundColor;
            textConfirmation.color = foregroundColor;
            textCancel.color = foregroundColor;
        }

        public void ResetPrinter()
        {
            isPrinting = false;
            _targetHotendTemperature = 0;
            _targetBedTemperature = 0;
            M107();
            _isManualProgress = false;
            _gcodeFilePosition = 0;
            networkFilePosition = 0;
            _displayStatus();
            _gcodeFile = new string[0];
            CleanupMesh();
            isPaused = false;
            _isBusy = false;
            _isWaitingBed = false;
            _isWaitingHotend = false;
            _isFileLoaded = false;
            _extrudeCheck = false;
            _lcdMessage = VERSION_INFO;
            _totalVertices = 0;
            RequestSerialization();
        }

        private void CleanupMesh()
        {
            _totalVertices = 0;
            trailRenderer.Clear();

            meshFilter.mesh.Clear();

            //force drop to return pickup to start position
            _pickup.Drop();
            _objectSync.Respawn();
            pickupObject.enabled = false;
        }

        private int[] _fileiD;
        private int _fileLength;
        private void GenerateListMenuItems()
        {
            _fileiD = new int[100];
            _fileLength = 0;
            string pageTitle = textPageTitle.text;
            if (pageTitle == "Files")
            {
                for (int i = 0; i < _gCode.Length; i++)
                {
                    if (_cardID[i] == loadedSdCard)
                    {
                        _fileiD[_fileLength] = i;
                        _fileLength++;
                    }
                }

                _options = new string[_fileLength];
                for (int i = 0; i < _fileLength; i++)
                {
                    _options[i] = _modelName[_fileiD[i]];
                }
            }
            else if (pageTitle == "Options")
            {
                _options = new[] { "Speed", "Audio Volume", "Toggle Mesh", "Reset Printer" };
            }
            else if (pageTitle == "Printer Control")
            {
                //lock during print, provide different menu
                if (isPrinting)
                {
                    _options = new[] { "Pause Print", "Cancel Print" };
                }
                else
                {
                    _options = new[]
                    {
                        "Bed Temp", "Hotend Temp", "Fan Speed", "Cooldown", "Auto Home", "Move X Axis", "Move Y Axis",
                        "Move Z Axis"
                    };
                }
            }
            else if (pageTitle == "Main Menu")
            {
                _options = new[] { "Printer Control", "SD Card", "Gcode Input", "Options", "Credits", "Debug" };
            }
            else if (pageTitle == "Debug")
            {
                _options = new[] { "Position", "Status", "GCode", "Mesh", "Network" };
            }
        }

        private string _selectedName;
        private void ListMenuSelection(int id)
        {
            _selectedName = "";
            switch (id)
            {
                case 0:
                    _selectedName = textOption1.text;
                    break;
                case 1:
                    _selectedName = textOption2.text;
                    break;
                case 2:
                    _selectedName = textOption3.text;
                    break;
                case 3:
                    _selectedName = textOption4.text;
                    break;
            }

            if (_selectedName == "")
            {
                return;
            }

            if (textPageTitle.text == "Files")
            {
                for (int i = 0; i < _gCode.Length; i++)
                {
                    if (_modelName[i] == _selectedName)
                    {
                        _gcodeFileSelected = i;
                        _displayConfirmation("Start Print?");
                        break;
                    }
                }
            }
            else
            {
                switch (_selectedName)
                {
                    case "Speed":
                    case "Audio Volume":
                    case "Hotend Temp":
                    case "Bed Temp":
                    case "Fan Speed":
                    case "Move X Axis":
                    case "Move Y Axis":
                    case "Move Z Axis":
                        DisplayValueOption(_selectedName);
                        break;
                    case "Plastic Color": //TODO: add color selection system.
                    case "Auto Home":
                        speaker.Play();
                        G28();
                        break;
                    case "Pause Print":
                        speaker.Play();
                        isPaused = !isPaused;
                        break;
                    case "Cancel Print":
                        _displayConfirmation("Cancel Print?");
                        break;
                    case "Printer Control":
                        _displayListMenu("Printer Control");
                        break;
                    case "SD Card":
                        _displayListMenu("Files");
                        break;
                    case "Options":
                        _displayListMenu("Options");
                        break;
                    case "Debug":
                        _displayListMenu("Debug");
                        break;
                    case "Gcode Input":
                        _displayGcodeInput();
                        break;
                    case "Cooldown":
                        speaker.Play();
                        _targetBedTemperature = 0;
                        _targetHotendTemperature = 0;
                        _displayStatus();
                        break;
                    case "Reset Printer":
                        _displayConfirmation("Reset Printer?");
                        break;
                    case "Toggle Mesh":
                        speaker.Play();
                        _ToggleMesh();
                        break;
                    case "Position":
                    case "GCode":
                    case "Mesh":
                    case "Network":
                    case "Credits":
                    case "Status":
                        DisplayTextPage(_selectedName);
                        break;
                }

                RequestSerialization();
            }

        }

        private void DisplayTextPage(string title)
        {
            ResetDisplay();
            textPageTitle.text = title;
            AddPage(title);
            textPage.gameObject.SetActive(true);
            textPageTitle.gameObject.SetActive(true);
            imageBackButton.gameObject.SetActive(true);
            Display();
        }

        public void _sdInsert()
        {
            _displayStatus();
            speaker.Play();
            _lcdMessage = "SD Card Inserted";
            RequestSerialization();
        }

        public void _up()
        {

            if (_listPagePosition > 0)
            {
                _listPagePosition--;
            }

            speaker.Play();
            DisplayOptionsList();
        }

        public void _down()
        {
            speaker.Play();
            _listPagePosition++;
            DisplayOptionsList();
        }

        private void _displayListMenu(String type)
        {
            AddPage(type);
            ResetDisplay();
            textPageTitle.text = type;
            _listPagePosition = 0;
            textOption1.gameObject.SetActive(true);
            textOption2.gameObject.SetActive(true);
            textOption3.gameObject.SetActive(true);
            textOption4.gameObject.SetActive(true);
            imageBackButton.gameObject.SetActive(true);
            textPageTitle.gameObject.SetActive(true);
            GenerateListMenuItems();
            DisplayOptionsList();
        }

        private void DisplayOptionsList()
        {
            if (_options.Length > (0 + _listPagePosition))
            {
                textOption1.text = _options[0 + _listPagePosition];
            }

            if (_options.Length > (1 + _listPagePosition))
            {
                textOption2.text = _options[1 + _listPagePosition];
            }

            if (_options.Length > (2 + _listPagePosition))
            {
                textOption3.text = _options[2 + _listPagePosition];
            }

            if (_options.Length > (3 + _listPagePosition))
            {
                textOption4.text = _options[3 + _listPagePosition];
            }

            if (_options.Length > (4 + _listPagePosition))
            {
                imageDown.gameObject.SetActive(true);
            }
            else
            {
                imageDown.gameObject.SetActive(false);
            }

            if (_listPagePosition > 0)
            {
                imageUp.gameObject.SetActive(true);
            }
            else
            {
                imageUp.gameObject.SetActive(false);
            }
        }

        public void _displayListOption1()
        {
            ListMenuSelection(0);
        }

        public void _displayListOption2()
        {
            ListMenuSelection(1);
        }

        public void _displayListOption3()
        {
            ListMenuSelection(2);
        }

        public void _displayListOption4()
        {
            ListMenuSelection(3);
        }

        private Vector3 _absVelocity;
        private void MotorSounds()
        {
            _absVelocity = new Vector3(Mathf.Abs(_calcVelocity.x), Mathf.Abs(_calcVelocity.y),
                Mathf.Abs(_calcVelocity.z));
            xMotorAudio.pitch = Mathf.Clamp(0.15f, 0f, _absVelocity.x);
            yMotorAudio.pitch = Mathf.Clamp(0.15f, 0f, _absVelocity.y);
            zMotorAudio.pitch = Mathf.Clamp(0.15f, 0f, _absVelocity.z);
            
            if (xMotorAudio.pitch == 0)
            {
                xMotorAudio.Stop();
            }
            else
            {
                xMotorAudio.Play();
            }
            if (yMotorAudio.pitch == 0)
            {
                yMotorAudio.Stop();
            }
            else
            {
                yMotorAudio.Play();
            }
            if (zMotorAudio.pitch == 0)
            {
                zMotorAudio.Stop();
            }
            else
            {
                zMotorAudio.Play();
            }
        }

        private void UpdateAudioVolume()
        {
            xMotorAudio.volume = audioVolume / 2;
            yMotorAudio.volume = audioVolume / 2;
            zMotorAudio.volume = audioVolume / 2;
            fanAudio.volume = (Mathf.InverseLerp(0, 512, _fanSpeed)) * audioVolume;
            speaker.volume = audioVolume;
        }

        private float _timeElapsed;
        private float _minutes;
        private float _hours;
        private float _seconds;
        private string TimeStringGen()
        {
            _timeElapsed = Time.time + _printStartTime;
            _minutes = Mathf.Floor(_timeElapsed / 60f);
            _hours = Mathf.Floor(_minutes / 60);
            _seconds = _timeElapsed - (_minutes * 60);
            if (!isPrinting)
            {
                return "00:00";
            }

            if (_hours > 0)
            {
                return $"{_hours:00}" + ":" + $"{_minutes / 60:00}";
            }

            return $"{_minutes:00}" + ":" + $"{_seconds:00}";
        }

        private void PrintProgressUpdate()
        {
            if (_isFileLoaded)
            {
                if (!_isManualProgress)
                {
                    textPrintProgress.maxValue = _gcodeFile.Length;
                    textPrintProgress.value = _gcodeFilePosition;
                }
                else
                {
                    textPrintProgress.maxValue = 100;
                    textPrintProgress.value = _printProgress;
                }
            }
        }

        public override void OnPostSerialization(SerializationResult result)
        {
            _lastSyncByteCount = result.byteCount;
            _lastSyncSuccessful = result.success;
        }

        private void Display()
        {
            if (_pageDepth == 0)
            {
                PrintProgressUpdate();
                textTime.text = TimeStringGen();
                textHotendTargetTemperature.text = Mathf.Floor(_targetHotendTemperature) + "°";
                textHotendCurrentTemperature.text = Mathf.Floor(_currentHotendTemperature) + "°";
                textBedTargetTemperature.text = Mathf.Floor(_targetBedTemperature) + "°";
                textBedCurrentTemperature.text = Mathf.Floor(_currentBedTemperature) + "°";
                textFanSpeed.text = Mathf.InverseLerp(0, 255, _fanSpeed) * 100 + "%";
                textStatus.text = _lcdMessage;
                textXPos.text = "X" + _printerCordPosition.x.ToString("F1", CultureInfo.InvariantCulture);
                textYPos.text = "Y" + _printerCordPosition.y.ToString("F1", CultureInfo.InvariantCulture);
                textZPos.text = "Z" + _printerCordPosition.z.ToString("F1", CultureInfo.InvariantCulture);
                return;
            }

            switch (textPageTitle.text)
            {
                case "Position":
                    textPage.text = "PrintPos: " + _printerCordPosition + "\nNormalPos: " + _normalPosition +
                                    "\nCurrentPos: " + _currentPosition + "\nVelocity: " + _calcVelocity +
                                    "\nFeedRate: " + _feedRate + "\nisRelativeG0: " + _isRelativeMovement;
                    break;
                case "Status":
                    textPage.text = "isPrinting: " + isPrinting + "\nisBusy: " + _isBusy + "\nisPaused: " + isPaused +
                                    "\nisWaitingHotend: " + _isWaitingHotend + "\nisWaitingBed: " + _isWaitingBed +
                                    "\nisManualProgress: " + _isManualProgress + "\nisExtrude: " + _extrudeCheck;
                    break;
                case "GCode":
                    int gcodeNum = Mathf.Clamp(_gcodeFilePosition - 1, 0, _gcodeFile.Length);
                    textPage.text = "FilePosition: " + _gcodeFilePosition + "\nNetFilePosition: " + networkFilePosition +
                                    "\nSDCard: " + loadedSdCard + "\nFileLines: " + _gcodeFile.Length + "\nFileID: " +
                                    _gcodeFileSelected + "\n> " + _gcodeFile[gcodeNum];
                    break;
                case "Mesh":
                    textPage.text = "\nTrail Size: " + trailRenderer.positionCount + "\nisMeshHidden: " + _isMeshHidden +
                                    "\nTrailOffset: " + lineMaterial.GetVector("_PositionOffset") +
                                    "\nMesh vertices: " + _totalVertices;
                    break;
                case "Network":
                    textPage.text = "isClogged: " + Networking.IsClogged + "\nisInstanceOwner: " +
                                    Networking.IsInstanceOwner + "\nisMaster: " + Networking.IsMaster +
                                    "\nisNetworkSettled: " + Networking.IsNetworkSettled + "\nisLastSyncSucessful: " +
                                    _lastSyncSuccessful + "\nlastSyncBytes: " + _lastSyncByteCount + "\nOwner: " +
                                    Networking.GetOwner(gameObject).displayName;
                    break; //breaks in Unity
                case "Credits":
                    textPage.text =
                        "-Code by Codel1417, Lyuma \n-Shader by Lyuma, phi16, Xiexe\n-UdonSharp By Merlin\n-Models by Creality3D, Playingbadly";
                    break;
            }
        }

        private Vector3 _nozzleLocal;
        private Vector3 _point;
        private Vector3 _nozzlePosition;
        private void AddVertToTrail(bool isExtrude)
        {
            if (trailRenderer.positionCount < 10000)
            {
                if (isExtrude)
                {
                    //Cold Extrusion Prevention
                    if (_currentHotendTemperature > 160f)
                    {
                        _nozzleLocal = transform.InverseTransformPoint(nozzle.position);
                        _point = Vector3.zero;
                        switch (yAxisMovementAxis)
                        {
                            case 0:
                                _point = new Vector3(_nozzleLocal.x, _nozzleLocal.y,
                                    -Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y));
                                break;
                            case 1:
                                _point = new Vector3(_nozzleLocal.x, _nozzleLocal.y,
                                    -Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y));
                                break;
                            case 2:
                                _point = new Vector3(_nozzleLocal.x, _nozzleLocal.y,
                                    -Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y));
                                break;
                        }

                        //The Nozzle isnt actually moving in the Y axis (Z World). We have to move the trail position to simulate the movement of the bed.
                        trailRenderer.AddPosition(transform.TransformPoint(_point));
                        _totalVertices += 2; //technically the shader adds 4 per vert 

                    }
                    else
                    {
                        _lcdMessage = "Cold Extrusion Prevented";
                    }
                }
                else
                {
                    _totalVertices += 2; //technically the shader adds 4 per vert but the trail adds 2 verts per point.
                    _nozzlePosition = nozzle.position;
                    trailRenderer.AddPosition(new Vector3(_nozzlePosition.x, _nozzlePosition.y - (20f * _printerScale),
                        _nozzlePosition.z));
                }
            }
            else
            {
                GenerateMesh();
                AddVertToTrail(isExtrude);
            }
        }


        private void GenerateMesh()
        {
            Mesh other = meshFilter.mesh;
            trailRenderer.BakeMesh(_trailGeneratedMesh);
            _combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            //_combine = new CombineInstance[2];
            _combine[0].mesh = _trailGeneratedMesh;
            _combine[1].mesh = other;
            _combined.CombineMeshes(_combine,true,false);
            //_combined.RecalculateBounds();
            //_combined.Optimize();
            //_bigMesh = combined;
            _trailGeneratedMesh.Clear();
            meshFilter.mesh = _combined;
            trailRenderer.Clear();
            other.Clear();
            _combined = other;
        }

        private void DisplayValue()
        {
            switch (textPageTitle.text)
            {
                case "Speed":
                    textValue.text = Convert.ToString(printerSpeed, CultureInfo.InvariantCulture) + "%";
                    return;
                case "Audio Volume":
                    textValue.text = Convert.ToString(audioVolume * 100, CultureInfo.InvariantCulture) + "%";
                    break;
                case "Hotend Temp":
                    textValue.text = Convert.ToString(_targetHotendTemperature, CultureInfo.InvariantCulture) + "°C";
                    return;
                case "Bed Temp":
                    textValue.text = Convert.ToString(_targetBedTemperature, CultureInfo.InvariantCulture) + "°C";
                    return;
                case "Fan Speed":
                    textValue.text = Convert.ToString(_fanSpeed, CultureInfo.InvariantCulture);
                    return;
                case "Move X Axis":
                    textValue.text = Convert.ToString(_printerCordPosition.x, CultureInfo.InvariantCulture) + "mm";
                    break;
                case "Move Y Axis":
                    textValue.text = Convert.ToString(_printerCordPosition.y, CultureInfo.InvariantCulture) + "mm";
                    break;
                case "Move Z Axis":
                    textValue.text = Convert.ToString(_printerCordPosition.z, CultureInfo.InvariantCulture) + "mm";
                    break;
            }
        }

        private void UpdateValue(int value)
        {
            speaker.Play();
            switch (textPageTitle.text)
            {
                case "Speed":
                    printerSpeed = Mathf.Clamp(printerSpeed + value, 50f, 1000f);
                    break;
                case "Audio Volume":
                    audioVolume = Mathf.Clamp(audioVolume + (value * 0.01f), 0f, 1f);
                    UpdateAudioVolume();
                    break;
                case "Hotend Temp":
                    _targetHotendTemperature = Mathf.Clamp(_targetHotendTemperature + value, 0, 260);
                    break;
                case "Bed Temp":
                    _targetBedTemperature = Mathf.Clamp(_targetBedTemperature + value, 0, 110);
                    break;
                case "Fan Speed":
                    _fanSpeed = Mathf.Clamp(_fanSpeed + value, 0, 255);
                    printFan.speed = _fanSpeed;
                    fanAudio.volume = (Mathf.InverseLerp(0, 1024, _fanSpeed));
                    break;
                case "Move X Axis":
                    _printerCordPosition.x = Mathf.Clamp(_printerCordPosition.x + value, 0, printerSizeInMM.x);
                    _normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, _printerCordPosition.x + value);
                    _isBusy = true;
                    _feedRate = 30;
                    break;
                case "Move Y Axis":
                    _printerCordPosition.y = Mathf.Clamp(_printerCordPosition.y + value, 0, printerSizeInMM.y);
                    _normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y, _printerCordPosition.y + value);
                    _isBusy = true;
                    _feedRate = 30;
                    break;
                case "Move Z Axis":
                    _printerCordPosition.z = Mathf.Clamp(_printerCordPosition.z + value, 0, printerSizeInMM.z);
                    _normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z, _printerCordPosition.z + value);
                    _isBusy = true;
                    break;
            }

            DisplayValue();
            RequestSerialization();
        }

        public void _add1()
        {
            UpdateValue(1);
        }

        public void _add10()
        {
            UpdateValue(10);
        }

        public void _add25()
        {
            UpdateValue(25);
        }

        public void _sub1()
        {
            UpdateValue(-1);
        }

        public void _sub10()
        {
            UpdateValue(-10);
        }

        public void _sub25()
        {
            UpdateValue(-25);
        }

        private Vector3 _previousPosition;
        private Vector3 _localPosition;
        private void Move()
        {
            _previousPosition = _currentPosition;
            _currentPosition.x = Mathf.SmoothDamp(_currentPosition.x, _normalPosition.x, ref _velocity.x, 0f,
                Mathf.Clamp(_feedRate, 0, 500 * printerSpeed * 0.01f));
            _currentPosition.y = Mathf.SmoothDamp(_currentPosition.y, _normalPosition.y, ref _velocity.z, 0f,
                Mathf.Clamp(_feedRate, 0, 500 * printerSpeed * 0.01f ));
            _currentPosition.z = Mathf.SmoothDamp(_currentPosition.z, _normalPosition.z, ref _velocity.z, 0f,
                Mathf.Clamp(_feedRate, 0, 5 * printerSpeed * 0.01f));

            _calcVelocity = (_previousPosition - _currentPosition) * 50; //used for sound

            switch (xAxisMovementAxis)
            {
                case 0:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(minPosition.x, maxPosition.x, _currentPosition.x),
                        _localPosition.y, _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(minPosition.x, maxPosition.x, _currentPosition.x), _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(minPosition.x, maxPosition.x, _currentPosition.x));
                    xAxis.localPosition = _localPosition;
                    break;
            }

            switch (yAxisMovementAxis)
            {
                case 0:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y),
                        _localPosition.y, _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y), _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y));
                    yAxis.localPosition = _localPosition;
                    break;
            }

            switch (zAxisMovementAxis)
            {
                case 0:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(minPosition.z, maxPosition.z, _currentPosition.z),
                        _localPosition.y, _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(minPosition.z, maxPosition.z, _currentPosition.z), _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(minPosition.z, maxPosition.z, _currentPosition.z));
                    zAxis.localPosition = _localPosition;
                    break;
            }

            if (Mathf.Approximately(_currentPosition.x, _normalPosition.x) &&
                Mathf.Approximately(_currentPosition.y, _normalPosition.y) &&
                Mathf.Approximately(_currentPosition.z, _normalPosition.z))
            {
                _isBusy = false;
                if (_extrudeCheck)
                {
                    AddVertToTrail(_extrudeCheck);
                }
            }

            switch (yAxisMovementAxis)
            {
                case 0:
                    lineMaterial.SetVector("_PositionOffset",
                        transform.TransformVector(new Vector3(0, 0, (yAxis.localPosition.x) - trailOffset)));
                    break;
                case 1:
                    lineMaterial.SetVector("_PositionOffset",
                        transform.TransformVector(new Vector3(0, 0, (yAxis.localPosition.y) - trailOffset)));
                    break;
                case 2:
                    lineMaterial.SetVector("_PositionOffset",
                        transform.TransformVector(new Vector3(0, 0, (yAxis.localPosition.z) - trailOffset)));
                    break;
            }
        }

        private void FastMove()
        {
            _currentPosition = _normalPosition;
            switch (xAxisMovementAxis)
            {
                case 0:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(minPosition.x, maxPosition.x, _currentPosition.x),
                        _localPosition.y, _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(minPosition.x, maxPosition.x, _currentPosition.x), _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(minPosition.x, maxPosition.x, _currentPosition.x));
                    xAxis.localPosition = _localPosition;
                    break;
            }

            switch (yAxisMovementAxis)
            {
                case 0:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y),
                        _localPosition.y, _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y), _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(minPosition.y, maxPosition.y, _currentPosition.y));
                    yAxis.localPosition = _localPosition;
                    break;
            }

            switch (zAxisMovementAxis)
            {
                case 0:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(minPosition.z, maxPosition.z, _currentPosition.z),
                        _localPosition.y, _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(minPosition.z, maxPosition.z, _currentPosition.z), _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(minPosition.z, maxPosition.z, _currentPosition.z));
                    zAxis.localPosition = _localPosition;
                    break;
            }

            _isBusy = false;
            if (_extrudeCheck)
            {
                AddVertToTrail(_extrudeCheck);
            }
        }

        private void Heaters()
        {
            //ambient cooldown
            _currentBedTemperature = Mathf.MoveTowards(_currentBedTemperature, ambientTemperature,
                0.1f * Time.deltaTime * (printerSpeed / 100));
            _currentHotendTemperature = Mathf.MoveTowards(_currentHotendTemperature, ambientTemperature,
                0.5f * Time.deltaTime * (printerSpeed / 100));

            if (_targetBedTemperature > ambientTemperature)
            {
                _currentBedTemperature = Mathf.MoveTowards(_currentBedTemperature, _targetBedTemperature,
                    0.5f * Time.deltaTime * (printerSpeed / 100));
            }

            if (_targetHotendTemperature > ambientTemperature)
            {
                _currentHotendTemperature = Mathf.MoveTowards(_currentHotendTemperature, _targetHotendTemperature,
                    2f * Time.deltaTime * (printerSpeed / 100));
            }

            if (Mathf.Approximately(_currentBedTemperature, _targetBedTemperature))
            {
                if (_isWaitingBed)
                {
                    _isBusy = false;
                    _lcdMessage = VERSION_INFO;
                }

                _isWaitingBed = false;
            }

            if (Mathf.Approximately(_currentHotendTemperature, _targetHotendTemperature))
            {
                if (_isWaitingHotend)
                {
                    _isBusy = false;
                    _lcdMessage = VERSION_INFO;
                }

                _isWaitingHotend = false;
            }
        }

        private void ReadFile(TextAsset text)
        {
            _gcodeFile = text.text.Split('\n');
            _isFileLoaded = true;
        }

        private string[] _splitGcodeLine;
        private void ParseGcode(string gcode)
        {
            if (string.IsNullOrEmpty(gcode) || gcode[0] == ';')
            {
                return;
            }

            if (gcode.Contains(";"))
            {
                //strip comments from gcode line
                gcode = gcode.Substring(0, gcode.IndexOf(';') - 1);
            }

            _splitGcodeLine = gcode.Split(' ');
            if (_splitGcodeLine.Length == 0)
            {
                //if empty line
                return;
            }

            if (_splitGcodeLine[0].Length <= 1)
            {
                //if too short to be a command
                return;
            }

            _isBusy = true;
            switch (_splitGcodeLine[0].Trim())
            {
                case "G0":
                case "G1":
                    G0(_splitGcodeLine);
                    break;
                case "G28":
                    G28();
                    break;
                case "M25":
                    M25();
                    break;
                case "M73":
                    M73(_splitGcodeLine);
                    break;
                case "M104":
                    M104(_splitGcodeLine);
                    break;
                case "M106":
                    M106(_splitGcodeLine);
                    break;
                case "M107":
                    M107();
                    break;
                case "M109":
                    M109(_splitGcodeLine);
                    break;
                case "M117":
                    M117(gcode);
                    break;
                case "M118":
                    M118(gcode);
                    break;
                case "M140":
                    M140(_splitGcodeLine);
                    break;
                case "M190":
                    M190(_splitGcodeLine);
                    break;
                case "G29": break; //bed leveling 
                case "M82":
                    _isRelativeMovement = true;
                    break;
                case "M105": break; //report temperature to serial
                case "M84": break; //disable steppers
                case "G90":
                    _isRelativeMovement = false;
                    break;
                case "G92": break; //set position.
                default:
                    UnityEngine.Debug.LogWarning("[ENDER 3] Unknown GCODE: " + gcode);
                    break;
            }
        }

        private float _value, _value1, _value2;
        private string _currentGcodeLineSection;

        //G0/G1 is a linear move;
        private void G0(string[] words)
        {
            _extrudeCheck = false;
            for (int i = 1; i < words.Length; i++)
            {

                if (words[i].Length > 0)
                {
                    _currentGcodeLineSection = words[i].Substring(1);
                }
                else continue;

                switch (words[i][0])
                {
                    case 'X':
                        _value = Convert.ToSingle(_currentGcodeLineSection);
                        if (_isRelativeMovement)
                        {
                            _printerCordPosition.x = Mathf.Clamp(_printerCordPosition.x + _value, 0f, printerSizeInMM.x);
                            _normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, _printerCordPosition.x);
                        }
                        else
                        {
                            _printerCordPosition.x = _value;
                            _normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, _value);
                        }

                        break;
                    case 'Y':
                        _value1 = Convert.ToSingle(_currentGcodeLineSection);
                        if (_isRelativeMovement)
                        {
                            _printerCordPosition.y = Mathf.Clamp(_printerCordPosition.y + _value1, 0f, printerSizeInMM.y);
                            _normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y, _printerCordPosition.y);

                        }
                        else
                        {
                            _printerCordPosition.y = _value1;
                            _normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y, _value1);
                        }

                        break;
                    case 'Z':
                        _value2 = Convert.ToSingle(_currentGcodeLineSection);
                        if (_isRelativeMovement)
                        {
                            _printerCordPosition.z = Mathf.Clamp(_printerCordPosition.z + _value2, 0f, printerSizeInMM.z);
                            _normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z, _printerCordPosition.z);

                        }
                        else
                        {
                            _printerCordPosition.z = _value2;
                            _normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z, _value2);
                        }

                        break;
                    case 'F':
                        _feedRate = Convert.ToSingle(_currentGcodeLineSection,
                            CultureInfo.InvariantCulture) * (printerSpeed * 0.0001f);
                        break;
                    case 'E':
                        _extrudeCheck = true;
                        break;
                }
            }

            AddVertToTrail(_extrudeCheck);
        }

        private void G28()
        {
            _normalPosition = Vector3.zero;
            _printerCordPosition = Vector3.zero;
            _feedRate = 15 * printerSpeed * 0.01f;
        }

        private void M25()
        {
            isPaused = !isPaused;
        }

        private void M73(String[] words)
        {
            _isManualProgress = true;
            for (int i = 1; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    _currentGcodeLineSection = words[i].Substring(1);
                }
                else continue;

                switch (words[i][0])
                {
                    case 'P':
                        _printProgress =
                            Mathf.Clamp(
                                Convert.ToSingle(_currentGcodeLineSection,
                                    CultureInfo.InvariantCulture), 0f, 255f);
                        break;
                }
            }
        }
        private void M104(string[] words)
        {
            for (int i = 1; i < words.Length; i++)
            {

                if (words[i].Length > 0)
                {
                    _currentGcodeLineSection = words[i].Substring(1);
                }
                else continue;

                switch (words[i][0])
                {
                    case 'S':
                        _targetHotendTemperature = Mathf.Clamp(Convert.ToInt32(_currentGcodeLineSection), 0, 260);
                        break;
                }
            }
        }

        private float _speed;
        private void M106(string[] words)
        {
            for (int i = 1; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    _currentGcodeLineSection = words[i].Substring(1);
                }
                else continue;

                switch (words[i][0])
                {
                    case 'S':
                        _speed =
                            Mathf.Clamp(
                                Convert.ToSingle(_currentGcodeLineSection,
                                    CultureInfo.InvariantCulture), 0f, 255f);
                        fanAudio.volume = (Mathf.InverseLerp(0, 512, _speed)) * audioVolume;
                        printFan.speed = _speed;
                        _fanSpeed = _speed;
                        break;
                }
            }
        }

        private void M107()
        {
            printFan.speed = 0;
            fanAudio.volume = 0;
            _fanSpeed = 0;
        }

        private void M109(string[] words)
        {
            _isWaitingHotend = true;
            _isBusy = true;
            _lcdMessage = "Heating Hotend";
            M104(words);
        }

        private void M117(string gcode)
        {
            _lcdMessage = gcode.Substring(4);
        }

        private void M118(string print)
        {
            UnityEngine.Debug.Log("[ENDER 3] " + print.Substring(4));
        }

        private void M140(string[] words)
        {
            for (int i = 1; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    _currentGcodeLineSection = words[i].Substring(1);
                }
                else continue;

                switch (words[i][0])
                {
                    case 'S':
                        _targetBedTemperature = Mathf.Clamp(Convert.ToInt32(_currentGcodeLineSection), 0, 110);
                        break;
                }
            }
        }

        private void M190(string[] words)
        {
            _isWaitingBed = true;
            _isBusy = true;
            _lcdMessage = "Heating Bed";
            M140(words);
        }
    }
}

