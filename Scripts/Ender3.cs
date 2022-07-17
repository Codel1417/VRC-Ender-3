using System;
using System.Diagnostics;
using System.Globalization;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// Unreachable code detected
#pragma warning disable CS0162


namespace Codel1417
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class Ender3 : UdonSharpBehaviour
    {
        private DisplayManager _displayManager;

        [Header("Other Options")]
        [Tooltip("Slows down the print to more realistic speeds")]
        [Range(50, 1000)]
        [UdonSynced]
        public int printerSpeed = 100;

        [Tooltip("Temperature in C")] [SerializeField] [Range(10f, 45f)]
        private float ambientTemperature = 20f;

        [Range(0, 1)] public float audioVolume = 0.1f;

        [Tooltip(
            "Number of trail points before merging into a single mesh. Too low and you will have stuttering. Too high and framerate will suffer.")]
        [Range(500, 20000)]
        [SerializeField]
        private int trailGenMesh = 10000;

        [Tooltip("Automatically print file 0 on sd card 0")] [SerializeField]
        private bool autoStartPrint = true;
        [Tooltip("Randomly picks a color when startimg a print")]
        [SerializeField] private bool randomPlasticColor = false;

        [Tooltip("Run in FixedUpdate instead if Update")] [SerializeField]
        private bool useFixedUpdate = true;

        [Tooltip("Print debug logs to the console")] [SerializeField]
        public bool debugLogging = true;

        [Tooltip("Prints the current gcode line to the console. Requires Debug Logging to be enabled")] [SerializeField]
        private bool verboseDebugLogging = false;

        [Header("Printer Objects")] [Tooltip("Vertical Axis")] [SerializeField]
        private Transform zAxis;

        [Tooltip("Forword/Back Axis")] [SerializeField]
        private Transform yAxis;

        [Tooltip("Left/Right Axis")] [SerializeField]
        private Transform xAxis;

        [NonSerialized] public Animator PrintFan;

        [Tooltip("The point the hotend nozzle is located")] [SerializeField]
        private Transform nozzle;

        private TrailRenderer _trailRenderer;
        private BoxCollider _pickupObject;


        //[NonSerialized] public string[] PopupOptions = { "X Axis", "Y Axis", "Z Axis" };

        [Header("Axis Assignment")]
        private Vector3 _minPosition = new Vector3(0.03462034f, -0.03400593f, -0.004123812f);

        //[Popup("@popupOptions")]
        private const int XAxisMovementAxis = 0;

        //[Popup("@popupOptions")]
        private const int YAxisMovementAxis = 2;

        //[Popup("@popupOptions")]
        private const int ZAxisMovementAxis = 1;

        [Tooltip("The max position for each axis. How far should each axis move")]
        private Vector3 _maxPosition = new Vector3(-0.217f, 0.2102f, 0.2503f);

        [NonSerialized] public Vector3 PrinterSizeInMm = new Vector3(235, 235, 250);
        [NonSerialized] public Vector3 NormalPosition;
        [NonSerialized] public Vector3 CalcVelocity, CurrentPosition, PrinterCordPosition;
        private Vector3 _velocity;
        [NonSerialized] public bool IsBusy;
        [NonSerialized] public bool IsPrinting;
        [NonSerialized] public bool IsFileLoaded;
        [UdonSynced] [HideInInspector] public bool isPaused;
        [NonSerialized] public bool IsWaitingHotend, IsWaitingBed;
        [NonSerialized] public float PrintProgress;
        [NonSerialized] public bool IsMeshHidden;
        [NonSerialized] public float FanSpeed;

        [NonSerialized] public float FeedRate,
            CurrentBedTemperature,
            TargetBedTemperature,
            CurrentHotendTemperature,
            TargetHotendTemperature;


        //private MeshFilter[] meshObjects = new MeshFilter[1000];
        //private int meshObjectCount = 0;

        [NonSerialized] public bool ExtrudeCheck;
        private Stopwatch _stopWatch = new Stopwatch();
        private Material _lineMaterial;
        [NonSerialized] public AudioSource FanAudio;
        private AudioSource _xMotorAudio, _yMotorAudio, _zMotorAudio;
        private float _printerScale;
        private SDReader _sdReader;

        //Where does the top of the 4 displayed options begin

        [NonSerialized] public int TotalVertices;
        [NonSerialized] public bool IsRelativeMovement;
        private const float TRAIL_OFFSET = 0.092f;
        private MeshFilter _meshFilter;

        private VRCPickup _pickup;
        private VRCObjectSync _objectSync;

        private Mesh _trailGeneratedMesh;
        private CombineInstance[] _combineInstances = new CombineInstance[2];
        private Mesh _bigMesh;
        private Mesh _combinedMesh;
        private bool _isOwner;
        [NonSerialized] public float TimeStep;
        private bool _isReady = false;
        [UdonSynced] [SerializeField] [ColorUsage(false, false)] private Color plasticColor;
        [SerializeField] private Camera trailBakeCamera;
        #region Vket
        [NonSerialized] public bool IsVket;
        public void _VketStart()
        {
            // Skip for Vket
            CurrentBedTemperature = 60;
            TargetBedTemperature = 60;
            CurrentHotendTemperature = 200;
            TargetHotendTemperature = 200;
            _displayManager._Log("Vket Start");
            IsVket = true;
            _InitialStart();
        }
    
        public void _VketOnBoothEnter()
        {
            _displayManager._Log("VKet Booth Entered");
            _xMotorAudio.enabled = true;
            _yMotorAudio.enabled = true;
            _zMotorAudio.enabled = true;
            FanAudio.enabled = true;

            IsVket = true;
            IsMeshHidden = false;
            _meshFilter.gameObject.SetActive(!IsMeshHidden);
        }

        public void _VketOnBoothExit()
        {
            _displayManager._Log("VKet Booth Exited");
            _xMotorAudio.enabled = false;
            _yMotorAudio.enabled = false;
            _zMotorAudio.enabled = false;
            FanAudio.enabled = false;
            IsVket = true;
            IsMeshHidden = true;
            _meshFilter.gameObject.SetActive(!IsMeshHidden);
        }
        
        public void _VketFixedUpdate()
        {
            if (useFixedUpdate)
            {
                TimeStep = Time.fixedDeltaTime;
                UpdateLoop();
            }
        }

        public void _VketUpdate()
        {
            if (!useFixedUpdate)
            {
                TimeStep = Time.deltaTime;
                UpdateLoop();
            }
        }
        #endregion
        
        private void Start()
        {
            _InitialStart();
        }
        
        private void _InitialStart()
        {
            _sdReader = GetComponentInChildren<SDReader>();
            _meshFilter = transform.Find("Print Mesh").GetComponent<MeshFilter>();
            _pickupObject = _meshFilter.GetComponent<BoxCollider>();
            _trailRenderer = GetComponentInChildren<TrailRenderer>();
            _displayManager = GetComponentInChildren<DisplayManager>();
            _isOwner = Networking.GetOwner(gameObject).playerId == Networking.LocalPlayer.playerId;
            _combinedMesh = new Mesh();
            _trailGeneratedMesh = new Mesh();
            _trailGeneratedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _bigMesh = new Mesh();
            _bigMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _meshFilter.mesh = _bigMesh;
            _combineInstances[0].mesh = _trailGeneratedMesh;
            _combineInstances[1].mesh = _bigMesh;

            _trailRenderer = GetComponentInChildren<TrailRenderer>();
            _lineMaterial = _trailRenderer.sharedMaterial;
            PrintFan = xAxis.GetComponentInChildren<Animator>();

            // Set up audio
            _xMotorAudio = zAxis.transform.Find("X Axis Audio").GetComponent<AudioSource>();
            _yMotorAudio = transform.Find("Y Axis Audio").GetComponent<AudioSource>();
            _zMotorAudio = transform.Find("Z Axis Audio").GetComponent<AudioSource>();
            FanAudio = PrintFan.GetComponent<AudioSource>();
            _xMotorAudio.enabled = true;
            _yMotorAudio.enabled = true;
            _zMotorAudio.enabled = true;
            FanAudio.enabled = true;
            _UpdateAudioVolume(audioVolume);

            //Load files
            _pickup = (VRCPickup)_pickupObject.GetComponent(typeof(VRCPickup));
            _objectSync = (VRCObjectSync)_pickupObject.GetComponent(typeof(VRCObjectSync));

            _printerScale = transform.localScale.x;

            _pickupObject.transform.position = Vector3.zero;

            _lineMaterial.SetFloat("_MaxSegmentLength", 0.1f * _printerScale);
            _lineMaterial.SetFloat("_Width", 0.00035f * _printerScale);
            PrinterCordPosition.x = Mathf.Lerp(0f, PrinterSizeInMm.x, CurrentPosition.x);
            PrinterCordPosition.y = Mathf.Lerp(0f, PrinterSizeInMm.y, CurrentPosition.y);
            PrinterCordPosition.z = Mathf.Lerp(0f, PrinterSizeInMm.z, CurrentPosition.z);


            Move();
        }

        private void _isReadyTasks()
        {

            _displayManager._InitialStart(this, _sdReader, debugLogging);
            _sdReader._Init();
            _isReady = true;
            _displayManager._displayStatus();
            if (IsVket || Networking.IsMaster)
            {
                ReadFile(_sdReader.GetGcodeFile().file); //load default file
                IsPrinting = autoStartPrint;
                NormalPosition = new Vector3(0.5f, 0.5f, 0.3f);
                CurrentPosition = new Vector3(0.5f, 0.5f, 0.3f);
                CurrentHotendTemperature = ambientTemperature;
                CurrentBedTemperature = ambientTemperature;
                M107();
                StartPrint();
            }

            _displayManager._Log(_displayManager.GetLocalizedString(38));
        }

        public void PrintFinished()
        {
            if (_isReady)
            {
                //TODO: Offset the box collider center with trail position so collider matches mesh location
                IsPrinting = false;
                TargetHotendTemperature = 0;
                TargetBedTemperature = 0;
                M107();
                _displayManager.IsManualProgress = false;
                ExtrudeCheck = false;
                _displayManager._SetLcdText(_displayManager.GetLocalizedString(36));
                GenerateMesh();
                //meshFilter.transform.SetParent(pickupObject.transform);
                Mesh mesh;
                (mesh = _meshFilter.mesh).RecalculateBounds();
                _pickupObject.enabled = true;
                _pickupObject.center = mesh.bounds.center;
                _pickupObject.size = mesh.bounds.size;
                _pickup.enabled = true;
                _pickup.UseText = _displayManager.GetLocalizedString(0) + ": " + _sdReader.GetGcodeFile().name;
                _pickup.InteractionText = _pickup.UseText;
                _objectSync.enabled = true;
                _displayManager._Log(_displayManager.GetLocalizedString(37));
            }
        }

        public void _ToggleMesh()
        {
            _displayManager._Log(_displayManager.GetLocalizedString(7));
            _displayManager._Beep();
            IsMeshHidden = !IsMeshHidden;
            _meshFilter.gameObject.SetActive(!IsMeshHidden);
        }

        private void FixedUpdate()
        {
            if (useFixedUpdate)
            {
                TimeStep = Time.fixedDeltaTime;
                UpdateLoop();
            }
        }

        private void Update()
        {
            if (!useFixedUpdate)
            {
                TimeStep = Time.deltaTime;
                UpdateLoop();
            }
        }
        
        private double _timeFromStart = 0;
        private void UpdateLoop()
        {
            _timeFromStart += TimeStep;
            if (_isReady)
            {
                ReSync();
                Heaters();
                if (IsBusy)
                {
                    Move();
                }

                if (audioVolume > 0.01f)
                {
                    MotorSounds();
                }

                if (IsPrinting)
                {
                    if (IsFileLoaded)
                    {
                        if (!IsBusy && !isPaused && !IsWaitingHotend && !IsWaitingBed)
                        {
                            if (GcodeFilePosition + 1 < GcodeFileFromTextAsset.Length && IsFileLoaded)
                            {
                                ParseGcode(GcodeFileFromTextAsset[GcodeFilePosition]);
                                GcodeFilePosition++;
                            }
                            else 
                            {
                                NormalPosition = Vector3.one;
                                PrintFinished();
                            }
                        }
                    }
                    else
                    {
                        if (IsVket)
                        {
                            StartPrint();
                        }
                        else
                        {
                            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(StartPrint));
                        }
                    }
                }

                _displayManager._Periodically();
            }
            else
            {
                if (_timeFromStart > 1)
                {
                    _isReady = true;
                    _isReadyTasks();
                    if (_resetRequestedWhileStarting)
                    {
                        ResetPrinter();
                    }

                    if (_startRequestedWhileStarting)
                    {
                        StartPrint();
                    }
                }
            }
        }
        private bool _startRequestedWhileStarting = false;
        private GcodeFile _gcodeFile;
        public void StartPrint()
        {
            if (_isReady)
            {
                _gcodeFile = _sdReader.GetGcodeFile();
                GcodeFileSelected = _gcodeFile.index;
                ReadFile(_gcodeFile.file);
                IsPrinting = true;
                isPaused = false;
                ExtrudeCheck = false;
                _displayManager._SetLcdText(_displayManager.GetLocalizedString(41));
                _displayManager.PrintStartTime = Time.time;
                GcodeFilePosition = 0;
                CleanupMesh();
                _requestNetworkUpdate();
                _displayManager._Log(_displayManager.GetLocalizedString(42));
                if (randomPlasticColor) plasticColor = GetRandomColor();
                _trailRenderer.startColor = plasticColor;
                _trailRenderer.endColor = plasticColor;
                _lineMaterial.SetColor("_Color", plasticColor);
            }
            else
            {
                _startRequestedWhileStarting = true;
            }
        }
        
        private bool _resetRequestedWhileStarting = false;
        public void ResetPrinter()
        {
            if (_isReady)
            {
                _displayManager._Log(_displayManager.GetLocalizedString(6));
                IsPrinting = false;
                TargetHotendTemperature = 0;
                TargetBedTemperature = 0;
                M107();
                _displayManager.IsManualProgress = false;
                GcodeFilePosition = 0;
                networkFilePosition = 0;
                _displayManager._displayStatus();
                GcodeFileFromTextAsset = new string[0];
                CleanupMesh();
                isPaused = false;
                IsBusy = false;
                IsWaitingBed = false;
                IsWaitingHotend = false;
                IsFileLoaded = false;
                ExtrudeCheck = false;
                _displayManager._SetDefaultLcdTxt();
                TotalVertices = 0;
                _requestNetworkUpdate();
            }
            else
            {
                _resetRequestedWhileStarting = true;
            }
        }

        private void CleanupMesh()
        {
            TotalVertices = 0;
            _trailRenderer.Clear();

            _meshFilter.mesh.Clear();

            //force drop to return pickup to start position
            _pickup.Drop();
            _objectSync.Respawn();
            _pickupObject.transform.position = Vector3.zero;
            _pickupObject.enabled = false;
            _objectSync.enabled = false;
            _pickup.enabled = false;
            _pickup.UseText = _displayManager.GetLocalizedString(0);
            _pickup.InteractionText = _pickup.UseText;
            _displayManager._Log(_displayManager.GetLocalizedString(43));
        }


        private int[] _fileiD;
        private int _fileLength;


        private Vector3 _absVelocity;

        private void MotorSounds()
        {
            _absVelocity = new Vector3(Mathf.Abs(CalcVelocity.x), Mathf.Abs(CalcVelocity.y),
                Mathf.Abs(CalcVelocity.z));
            _xMotorAudio.pitch = Mathf.Clamp(0.15f, 0f, _absVelocity.x);
            _yMotorAudio.pitch = Mathf.Clamp(0.15f, 0f, _absVelocity.y);
            _zMotorAudio.pitch = Mathf.Clamp(0.15f, 0f, _absVelocity.z);
        }

        public void _UpdateAudioVolume(float volume)
        {
            _xMotorAudio.volume = volume / 2f;
            _yMotorAudio.volume = volume / 2f;
            _zMotorAudio.volume = volume / 2f;
            FanAudio.volume = (Mathf.InverseLerp(0, 512, FanSpeed)) * audioVolume;
        }
        
        private Vector3 _nozzleLocal;
        private Vector3 _point;
        private Vector3 _nozzlePosition;

        private void AddVertToTrail(bool isExtrude)
        {
            if (_trailRenderer.positionCount < trailGenMesh)
            {
                if (isExtrude)
                {
                    //Cold Extrusion Prevention
                    if (CurrentHotendTemperature > 160f)
                    {
                        _nozzleLocal = transform.InverseTransformPoint(nozzle.position);
                        _point = Vector3.zero;
                        switch (YAxisMovementAxis)
                        {
                            case 0:
                                _point = new Vector3(_nozzleLocal.x, _nozzleLocal.y,
                                    -Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y));
                                break;
                            case 1:
                                _point = new Vector3(_nozzleLocal.x, _nozzleLocal.y,
                                    -Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y));
                                break;
                            case 2:
                                _point = new Vector3(_nozzleLocal.x, _nozzleLocal.y,
                                    -Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y));
                                break;
                        }
                        
                        //The Nozzle isnt actually moving in the Y axis (Z World). We have to move the trail position to simulate the movement of the bed.
                        _trailRenderer.AddPosition(transform.TransformPoint(_point));
                        TotalVertices += 2; //technically the shader adds 4 per vert 
                    }
                    else
                    {
                        _displayManager._SetLcdText(_displayManager.GetLocalizedString(44));
                    }
                }
                else
                {
                    TotalVertices += 2; //technically the shader adds 4 per vert but the trail adds 2 verts per point.
                    _nozzlePosition = nozzle.position;
                    _trailRenderer.AddPosition(new Vector3(_nozzlePosition.x, _nozzlePosition.y - (20f * _printerScale),
                        _nozzlePosition.z));
                }
            }
            else
            {
                GenerateMesh();
                AddVertToTrail(isExtrude);
            }
        }

        private Color GetRandomColor()
        {
            return new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        }

        private void GenerateMesh()
        {
            Mesh originalMesh = _meshFilter.mesh;
            _trailRenderer.BakeMesh(_trailGeneratedMesh,trailBakeCamera, false);
            _combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            //_combine = new CombineInstance[2];
            _combineInstances[0].mesh = _trailGeneratedMesh;
            _combineInstances[1].mesh = originalMesh;
            _combinedMesh.CombineMeshes(_combineInstances, true, false);
            //_combined.RecalculateBounds();
            //_combined.Optimize();
            //_bigMesh = combined;
            _meshFilter.mesh = _combinedMesh;
            _trailGeneratedMesh.Clear();
            _trailRenderer.Clear();
            originalMesh.Clear();
            _combinedMesh = originalMesh;
            _displayManager._Log(_displayManager.GetLocalizedString(45));
        }


        private Vector3 _previousPosition;
        private Vector3 _localPosition;

        private void Move()
        {
            _previousPosition = CurrentPosition;
            CurrentPosition.x = Mathf.SmoothDamp(CurrentPosition.x, NormalPosition.x, ref _velocity.x, 0f,
                Mathf.Clamp(FeedRate, 0, 500 * printerSpeed * 0.01f));
            CurrentPosition.y = Mathf.SmoothDamp(CurrentPosition.y, NormalPosition.y, ref _velocity.z, 0f,
                Mathf.Clamp(FeedRate, 0, 500 * printerSpeed * 0.01f));
            CurrentPosition.z = Mathf.SmoothDamp(CurrentPosition.z, NormalPosition.z, ref _velocity.z, 0f,
                Mathf.Clamp(FeedRate, 0, 5 * printerSpeed * 0.01f));

            CalcVelocity = (_previousPosition - CurrentPosition) * 50; //used for sound

            switch (XAxisMovementAxis)
            {
                case 0:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(_minPosition.x, _maxPosition.x, CurrentPosition.x),
                        _localPosition.y, _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(_minPosition.x, _maxPosition.x, CurrentPosition.x), _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(_minPosition.x, _maxPosition.x, CurrentPosition.x));
                    xAxis.localPosition = _localPosition;
                    break;
            }

            switch (YAxisMovementAxis)
            {
                case 0:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y),
                        _localPosition.y, _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y), _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y));
                    yAxis.localPosition = _localPosition;
                    break;
            }

            switch (ZAxisMovementAxis)
            {
                case 0:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(_minPosition.z, _maxPosition.z, CurrentPosition.z),
                        _localPosition.y, _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(_minPosition.z, _maxPosition.z, CurrentPosition.z), _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(_minPosition.z, _maxPosition.z, CurrentPosition.z));
                    zAxis.localPosition = _localPosition;
                    break;
            }

            if (Mathf.Approximately(CurrentPosition.x, NormalPosition.x) &&
                Mathf.Approximately(CurrentPosition.y, NormalPosition.y) &&
                Mathf.Approximately(CurrentPosition.z, NormalPosition.z))
            {
                IsBusy = false;
                if (ExtrudeCheck)
                {
                    AddVertToTrail(ExtrudeCheck);
                }
            }

            switch (YAxisMovementAxis)
            {
                case 0:
                    _lineMaterial.SetVector("_PositionOffset",
                        transform.TransformVector(new Vector3(0, 0, (yAxis.localPosition.x) - TRAIL_OFFSET)));
                    break;
                case 1:
                    _lineMaterial.SetVector("_PositionOffset",
                        transform.TransformVector(new Vector3(0, 0, (yAxis.localPosition.y) - TRAIL_OFFSET)));
                    break;
                case 2:
                    _lineMaterial.SetVector("_PositionOffset",
                        transform.TransformVector(new Vector3(0, 0, (yAxis.localPosition.z) - TRAIL_OFFSET)));
                    break;
            }
        }

        private void FastMove()
        {
            CurrentPosition = NormalPosition;
            switch (XAxisMovementAxis)
            {
                case 0:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(_minPosition.x, _maxPosition.x, CurrentPosition.x),
                        _localPosition.y, _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(_minPosition.x, _maxPosition.x, CurrentPosition.x), _localPosition.z);
                    xAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = xAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(_minPosition.x, _maxPosition.x, CurrentPosition.x));
                    xAxis.localPosition = _localPosition;
                    break;
            }

            switch (YAxisMovementAxis)
            {
                case 0:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y),
                        _localPosition.y, _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y), _localPosition.z);
                    yAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = yAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(_minPosition.y, _maxPosition.y, CurrentPosition.y));
                    yAxis.localPosition = _localPosition;
                    break;
            }

            switch (ZAxisMovementAxis)
            {
                case 0:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(Mathf.Lerp(_minPosition.z, _maxPosition.z, CurrentPosition.z),
                        _localPosition.y, _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 1:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x,
                        Mathf.Lerp(_minPosition.z, _maxPosition.z, CurrentPosition.z), _localPosition.z);
                    zAxis.localPosition = _localPosition;
                    break;
                case 2:
                    _localPosition = zAxis.localPosition;
                    _localPosition = new Vector3(_localPosition.x, _localPosition.y,
                        Mathf.Lerp(_minPosition.z, _maxPosition.z, CurrentPosition.z));
                    zAxis.localPosition = _localPosition;
                    break;
            }

            IsBusy = false;
            if (ExtrudeCheck)
            {
                AddVertToTrail(ExtrudeCheck);
            }
        }

        private void Heaters()
        {
            //ambient cooldown
            CurrentBedTemperature = Mathf.MoveTowards(CurrentBedTemperature, ambientTemperature,
                0.1f * TimeStep * (printerSpeed / 100f));
            CurrentHotendTemperature = Mathf.MoveTowards(CurrentHotendTemperature, ambientTemperature,
                0.5f * TimeStep * (printerSpeed / 100f));

            if (TargetBedTemperature > ambientTemperature)
            {
                CurrentBedTemperature = Mathf.MoveTowards(CurrentBedTemperature, TargetBedTemperature,
                    0.5f * TimeStep * (printerSpeed / 100f));
            }

            if (TargetHotendTemperature > ambientTemperature)
            {
                CurrentHotendTemperature = Mathf.MoveTowards(CurrentHotendTemperature, TargetHotendTemperature,
                    2f * TimeStep * (printerSpeed / 100f));
            }

            if (Mathf.Approximately(CurrentBedTemperature, TargetBedTemperature))
            {
                if (IsWaitingBed)
                {
                    IsBusy = false;
                    _displayManager._SetDefaultLcdTxt();
                    
                }

                IsWaitingBed = false;
            }

            if (Mathf.Approximately(CurrentHotendTemperature, TargetHotendTemperature))
            {
                if (IsWaitingHotend)
                {
                    IsBusy = false;
                    _displayManager._SetDefaultLcdTxt();
                    
                }

                IsWaitingHotend = false;
            }
        }

        public void PrintGCode(string text)
        {
            GcodeFileFromTextAsset = text.Split('\n');
            IsPrinting = true;
            IsFileLoaded = true;
            GcodeFilePosition = 0;
            networkFilePosition = 0;
        }

        #region GCode
        [NonSerialized] public string[] GcodeFileFromTextAsset = new string[1];
        [NonSerialized] public int GcodeFileSelected;
        [NonSerialized] public int GcodeFilePosition;
        private void ReadFile(TextAsset text)
        {
            GcodeFileFromTextAsset = text.text.Split('\n');
            IsFileLoaded = true;
            _displayManager._Log( _displayManager.GetLocalizedString(46) + ": " + text.name);
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

            if (verboseDebugLogging)
            {
                _displayManager._Log( _displayManager.GetLocalizedString(30) + ": " + gcode);
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

            IsBusy = true;
            switch (_splitGcodeLine[0].Trim())
            {
                case "G0":
                case "G1":
                    _G0(_splitGcodeLine);
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
                case "M82": // E Absolute
                    IsRelativeMovement = false;
                    _displayManager._Log(_displayManager.GetLocalizedString(48));
                    break;
                case "M83": // E Relative
                    IsRelativeMovement = true;
                    _displayManager._Log(_displayManager.GetLocalizedString(47));
                    break;
                case "M105": break; //report temperature to serial
                case "M84": break; //disable steppers
                case "G90":// Absolute Positioning
                    IsRelativeMovement = false;
                    _displayManager._Log(_displayManager.GetLocalizedString(48));
                    break;
                case "G91": // Relative Positioning
                    IsRelativeMovement = true;
                    _displayManager._Log(_displayManager.GetLocalizedString(47));
                    break;
                case "G92": break; //set position.
                default:
                    _displayManager._Log(_displayManager.GetLocalizedString(49) + ": " + gcode);
                    break;
            }
        }

        private float _value, _value1, _value2;
        private string _currentGcodeLineSection;

        //G0/G1 is a linear move;
        private void _G0(string[] words)
        {
            ExtrudeCheck = false;
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
                        if (IsRelativeMovement)
                        {
                            PrinterCordPosition.x = Mathf.Clamp(PrinterCordPosition.x + _value, 0f, PrinterSizeInMm.x);
                            NormalPosition.x = Mathf.InverseLerp(0, PrinterSizeInMm.x, PrinterCordPosition.x);
                        }
                        else
                        {
                            PrinterCordPosition.x = _value;
                            NormalPosition.x = Mathf.InverseLerp(0, PrinterSizeInMm.x, _value);
                        }

                        break;
                    case 'Y':
                        _value1 = Convert.ToSingle(_currentGcodeLineSection);
                        if (IsRelativeMovement)
                        {
                            PrinterCordPosition.y = Mathf.Clamp(PrinterCordPosition.y + _value1, 0f, PrinterSizeInMm.y);
                            NormalPosition.y = Mathf.InverseLerp(0, PrinterSizeInMm.y, PrinterCordPosition.y);
                        }
                        else
                        {
                            PrinterCordPosition.y = _value1;
                            NormalPosition.y = Mathf.InverseLerp(0, PrinterSizeInMm.y, _value1);
                        }

                        break;
                    case 'Z':
                        _value2 = Convert.ToSingle(_currentGcodeLineSection);
                        if (IsRelativeMovement)
                        {
                            PrinterCordPosition.z = Mathf.Clamp(PrinterCordPosition.z + _value2, 0f, PrinterSizeInMm.z);
                            NormalPosition.z = Mathf.InverseLerp(0, PrinterSizeInMm.z, PrinterCordPosition.z);
                        }
                        else
                        {
                            PrinterCordPosition.z = _value2;
                            NormalPosition.z = Mathf.InverseLerp(0, PrinterSizeInMm.z, _value2);
                        }

                        break;
                    case 'F':
                        FeedRate = Convert.ToSingle(_currentGcodeLineSection,
                            CultureInfo.InvariantCulture) * (printerSpeed * 0.0001f);
                        break;
                    case 'E':
                        ExtrudeCheck = true;
                        break;
                }
            }

            AddVertToTrail(ExtrudeCheck);
        }

        public void G28()
        {
            NormalPosition = Vector3.zero;
            PrinterCordPosition = Vector3.zero;
            FeedRate = 15 * printerSpeed * 0.01f;
            IsBusy = true;
            _displayManager._Log(_displayManager.GetLocalizedString(50));
        }

        private void M25()
        {
            isPaused = !isPaused;
            _displayManager._Log(_displayManager.GetLocalizedString(51));
            _displayManager._Beep();
        }

        private void M73(string[] words)
        {
            _displayManager.IsManualProgress = true;
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
                        PrintProgress =
                            Mathf.Clamp(
                                Convert.ToSingle(_currentGcodeLineSection,
                                    CultureInfo.InvariantCulture), 0f, 255f);
                        _displayManager._Log( _displayManager.GetLocalizedString(52) + ": " + PrintProgress);
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
                        TargetHotendTemperature = Mathf.Clamp(Convert.ToInt32(_currentGcodeLineSection), 0, 260);
                        _displayManager._Log(_displayManager.GetLocalizedString(53) + ": " + TargetHotendTemperature);
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
                        FanAudio.volume = (Mathf.InverseLerp(0, 512, _speed)) * audioVolume;
                        PrintFan.speed = _speed;
                        FanSpeed = _speed;
                        _displayManager._Log(_displayManager.GetLocalizedString(54) + ": " + _speed);
                        break;
                }
            }
        }

        private void M107()
        {
            PrintFan.speed = 0;
            FanAudio.volume = 0;
            FanSpeed = 0;
            _displayManager._Log(_displayManager.GetLocalizedString(55));
        }

        private void M109(string[] words)
        {
            IsWaitingHotend = true;
            IsBusy = true;
            _displayManager._SetLcdText(_displayManager.GetLocalizedString(56));
            M104(words);
        }

        private void M117(string gcode)
        {
            _displayManager._SetLcdText(gcode.Substring(4));
        }

        private void M118(string print)
        {
            _displayManager._Log(print.Substring(4));
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
                        TargetBedTemperature = Mathf.Clamp(Convert.ToInt32(_currentGcodeLineSection), 0, 110);
                        _displayManager._Log(_displayManager.GetLocalizedString(57) + ": " + TargetBedTemperature);
                        break;
                }
            }
        }

        private void M190(string[] words)
        {
            IsWaitingBed = true;
            IsBusy = true;
            _displayManager._SetLcdText(_displayManager.GetLocalizedString(58));
            M140(words);
        }

        #endregion
        
        #region Networking
        [UdonSynced] [HideInInspector] public int networkFileSelected;
        [UdonSynced] [HideInInspector] public int networkFilePosition;
        [UdonSynced] [HideInInspector] public bool isNetworkPrinting;
        private int _networkSyncGap;
        private const float CatchUpTimeout = 20f;
        
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            _isOwner = Networking.GetOwner(gameObject).playerId == player.playerId;
        }
        private bool isResync;
        private void ReSync()
        {
            if (_isReady)
            {
                _networkSyncGap = networkFilePosition - GcodeFilePosition;
                if ( _networkSyncGap > 500)
                {
                    IsPrinting = true;
                    _stopWatch.Start();
                    if (!isResync){} _displayManager._SetLcdText(_displayManager.GetLocalizedString(39));
                    isResync = true;
                    IsWaitingBed = false;
                    IsWaitingHotend = false;
                    CurrentBedTemperature = TargetBedTemperature;
                    CurrentHotendTemperature = TargetHotendTemperature;
                    while (GcodeFilePosition < networkFilePosition)
                    {
                        if (IsBusy)
                        {
                            FastMove();
                        }
                        else
                        {
                            ParseGcode(GcodeFileFromTextAsset[GcodeFilePosition]);
                            GcodeFilePosition++;
                        }

                        if (_stopWatch.ElapsedMilliseconds > CatchUpTimeout)
                        {
                            break;
                        }
                    }

                    _stopWatch.Stop();
                    _stopWatch.Reset();
                }
                else if (_networkSyncGap < -500 && !_isOwner)
                {
                    IsPrinting = false;
                    if (isResync) _displayManager._SetLcdText(_displayManager.GetLocalizedString(40));
                    isResync = false;
                }
                else
                {
                    if (isResync) _displayManager._SetDefaultLcdTxt();
                    isResync = false;
                }
            }
        }
        public override void OnDeserialization()
        {
            if (IsVket) return;
            if (Networking.IsClogged) return;
            //If the person joins late make sure the correct file is loaded
            if (networkFileSelected == GcodeFileSelected) return;
            if (isNetworkPrinting)
            {
                _displayManager._Log("Network file changed.");
                GcodeFileSelected = networkFileSelected;
                ReadFile(_sdReader.GetGCodeFileFromIndex(networkFileSelected).file);
                PrintFinished();
                StartPrint();
            }
        }

        public override void OnPreSerialization()
        {
            if (Networking.IsOwner(gameObject) && !IsVket)
            {
                networkFilePosition = GcodeFilePosition;
                isNetworkPrinting = IsPrinting;
                networkFileSelected = GcodeFileSelected;
            }
        }

        public void _requestNetworkUpdate()
        {
            if (!IsVket)
            {
                RequestSerialization();
                Networking.SetOwner(Networking.LocalPlayer, _sdReader.gameObject);
                Networking.SetOwner(Networking.LocalPlayer, _displayManager.gameObject);
                _sdReader.RequestSerialization();
                //_displayManager.RequestSerialization();
            }
        }
        #endregion
    }
}