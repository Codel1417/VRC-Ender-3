using System;
using System.Globalization;
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UnityEngine.UI;
using VRC.Udon.Common.Interfaces;

namespace Codel1417
{
    [RequireComponent(typeof(AudioSource))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DisplayManager : UdonSharpBehaviour
    
{       
    [ColorUsage(false,false)] [SerializeField] private Color backgroundColor = new Color(0, 0, 255);
    [ColorUsage(false,false)] [SerializeField] private Color foregroundColor = new Color(255, 255, 255);
    [SerializeField] private GameObject statusPage;
 
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
             imageSD,
             imageQrCode;
 
         private float _timeMin;
         [SerializeField] private InputField gcodeInput;
         private string _lcdMessage = "";
         private string[] _previousPage = new string[10];
         private int _pageDepth;
         [NonSerialized] public float PrintStartTime;
         private int _listPagePosition;
         private string[] _options;
         private bool _isFirstTime = true; // for stopping initial beep
         [NonSerialized] public bool IsManualProgress;
         private Ender3 _ender3;
         private SDReader _sdReader;
         private AudioSource _speaker;
         private TrailRenderer _trailRenderer;
         private Material _lineMaterial;
         [Header("Language")]
         public TextAsset[] languageFiles;
         [Tooltip("The full name of the language file to default to. If not set, the first language file will be used.")]
         [SerializeField]
         private string defaultLanguage = "English";
         private string[][] _languageStrings;
         private int _loadedLanguage = 0;
         private bool _debugLogging = true;
    public void _InitialStart(Ender3 ender3, SDReader sdReader, bool debugLogging)
    {
        _SetLanguageUsingString(defaultLanguage);
        _debugLogging = debugLogging;
        _ender3 = ender3;
        _sdReader = sdReader;
        _trailRenderer = transform.parent.GetComponentInChildren<TrailRenderer>();
        _lineMaterial = _trailRenderer.sharedMaterial;
        _speaker = GetComponent<AudioSource>();
        _SetDefaultLcdTxt();
        _displayStatus();

    }

    public void _Periodically()
    {
        _timeMin += _ender3.TimeStep;
        _timeSinceLastSound += _ender3.TimeStep;
        if (_timeMin >= 1f)
        {
            _timeMin -= 1;
            UpdateDisplayContent();
            _ender3._requestNetworkUpdate();
            if (_timeSinceLastSound >= 1f)
            {
                _timeSinceLastSound = 0;
                _speaker.enabled = false;
            }
        }
        else if (_pageDepth != 0)
        {
            UpdateDisplayContent();
        }
    }
    public void _ResetDisplay()
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
        imageQrCode.gameObject.SetActive(false);
        if (_isFirstTime)
        {
            _isFirstTime = false;
        }
        else
        {
            _Beep();
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
         public void _displayStatus()
        {
            _ResetDisplay();
            _pageDepth = 0;
            statusPage.SetActive(true);
            textPrinterName.text = GetLocalizedString(0);
        }
         [UsedImplicitly]
         public void _displayMainMenu()
        {
            _displayListMenu(GetLocalizedString(1));
        }

         private void _displayGcodeInput()
        {
            AddPage(GetLocalizedString(9));
            _ResetDisplay();
            gcodeInput.gameObject.SetActive(true);
            imageGcodeConfirmation.gameObject.SetActive(true);
            textPageTitle.gameObject.SetActive(true);
            textPageTitle.text = GetLocalizedString(2);
            imageBackButton.gameObject.SetActive(true);
        }

        


        private void AddPage(string page)
        {
            _previousPage[_pageDepth] = page;
            _pageDepth++;
        }

        private void DisplayValueOption(String title)
        {
            _ResetDisplay();
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

        private void _displayConfirmation(string title)
        {
            AddPage(title);
            _ResetDisplay();
            textPageTitle.gameObject.SetActive(true);
            textPageTitle.text = title;
            imageBackButton.gameObject.SetActive(true);
            imageConfirmationButton.gameObject.SetActive(true);
            imageCancelButton.gameObject.SetActive(true);
        }

        private void UpdateColor()
        {
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
            imageQrCode.color = foregroundColor;
        }
        private void DisplayQrCode()
        {
            _ResetDisplay();
            AddPage(GetLocalizedString(27));
            imageQrCode.gameObject.SetActive(true);
            imageBackButton.gameObject.SetActive(true);
        }
        private string pageTitle = "";
        private void GenerateListMenuItems()
        {
            {
                pageTitle = textPageTitle.text;
                if (pageTitle == GetLocalizedString(24))
                {
                    _options = _sdReader.GetModelNamesFromCurrentCard();
                }
                else if (pageTitle == GetLocalizedString(25)) // Options Menu
                {
                    _options = new string[5];
                    _options[0] = GetLocalizedString(59);
                    _options[1] = GetLocalizedString(13);
                    _options[2] = GetLocalizedString(14);
                    _options[3] = GetLocalizedString(7);
                    _options[4] = GetLocalizedString(6);
                }
                else if (pageTitle == GetLocalizedString(12))
                {
                    //lock during print, provide different menu
                    if (_ender3.IsPrinting)
                    {
                        _options = new string[2]; // Print job controls
                        _options[0] = GetLocalizedString(22);
                        _options[1] = GetLocalizedString(23);
                    }
                    else
                    {
                        _options = new string[8]; //manual printer controls
                        _options[0] = GetLocalizedString(16);
                        _options[1] = GetLocalizedString(15);
                        _options[2] = GetLocalizedString(17);
                        _options[3] = GetLocalizedString(8);
                        _options[4] = GetLocalizedString(21);
                        _options[5] = GetLocalizedString(18);
                        _options[6] = GetLocalizedString(19);
                        _options[7] = GetLocalizedString(20);
                    }
                }
                else if (pageTitle == GetLocalizedString(1)) //main menu
                {
                    _options = new string[7];
                    _options[0] = GetLocalizedString(12);
                    _options[1] = GetLocalizedString(24);
                    _options[2] = GetLocalizedString(27);
                    _options[3] = GetLocalizedString(25);
                    _options[4] = GetLocalizedString(31);
                    _options[5] = GetLocalizedString(9);
                    _options[6] = GetLocalizedString(10);
                }
                else if (pageTitle == GetLocalizedString(10)) //debug
                {
                    _options = new string[5];
                    _options[0] = GetLocalizedString(26);
                    _options[1] = GetLocalizedString(29);
                    _options[2] = GetLocalizedString(30);
                    _options[3] = GetLocalizedString(32);
                    _options[4] = GetLocalizedString(28);
                    
                }
                else if (pageTitle == GetLocalizedString(59)) // language list
                {
                    _options = GetLanguageNames();
                }
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

            if (textPageTitle.text == GetLocalizedString(24)) // Start Print?
            {
               _sdReader.GetGCodeFileFromName(_selectedName);
               _displayConfirmation(GetLocalizedString(3));
            }
            else
            {
                if (_selectedName == GetLocalizedString(13) || _selectedName == GetLocalizedString(14) || _selectedName == GetLocalizedString(15) ||
                    _selectedName == GetLocalizedString(16) || _selectedName == GetLocalizedString(17) || _selectedName == GetLocalizedString(18) ||
                    _selectedName == GetLocalizedString(19) || _selectedName == GetLocalizedString(20))
                {
                    DisplayValueOption(_selectedName);
                }
                else if (_selectedName == GetLocalizedString(21)) // Auto Home
                {
                    _Beep();
                    _ender3.G28();
                }
                else if (_selectedName == GetLocalizedString(22)) // Pause print
                {
                    _Beep();
                    _ender3.isPaused = !_ender3.isPaused;
                }
                else if (_selectedName == GetLocalizedString(23)) // cancel print
                {
                    _displayConfirmation(GetLocalizedString(4));
                }
                else if (_selectedName == GetLocalizedString(12))
                {
                    _displayListMenu(_selectedName);
                }
                else if (_selectedName == GetLocalizedString(24))
                {
                    _displayListMenu(GetLocalizedString(24));
                }
                else if (_selectedName == GetLocalizedString(25))
                {
                    _displayListMenu(_selectedName);
                }
                else if (_selectedName == GetLocalizedString(10))
                {
                    _displayListMenu(_selectedName);
                }
                else if (_selectedName == GetLocalizedString(9)) // Enter GCode
                {
                    _displayGcodeInput();
                }
                else if (_selectedName == GetLocalizedString(8))
                {
                    _Beep();
                    _ender3.TargetBedTemperature = 0;
                    _ender3.TargetHotendTemperature = 0;

                    _displayStatus();
                }
                else if (_selectedName == GetLocalizedString(6))
                {
                    _displayConfirmation(GetLocalizedString(5));
                }
                else if (_selectedName == GetLocalizedString(7))
                {
                    _Beep();
                    _ender3._ToggleMesh();
                }
                else if (_selectedName == GetLocalizedString(26) || _selectedName == GetLocalizedString(30) || _selectedName == GetLocalizedString(32) ||
                         _selectedName == GetLocalizedString(28) || _selectedName == GetLocalizedString(31) || _selectedName == GetLocalizedString(29))
                {
                    DisplayTextPage(_selectedName);
                }
                else if (_selectedName == GetLocalizedString(27))
                {
                    DisplayQrCode();
                } else if (_selectedName == GetLocalizedString(59))
                {
                    _displayListMenu(_selectedName);
                }
                else if (IsLanguage(_selectedName))
                {
                    _SetLanguageUsingString(_selectedName);
                }

                _ender3._requestNetworkUpdate();
            }

        }
        private void DisplayTextPage(string title)
        {
            _ResetDisplay();
            
                textPageTitle.text = title;
                AddPage(title);
                textPage.gameObject.SetActive(true);
                textPageTitle.gameObject.SetActive(true);
            

            imageBackButton.gameObject.SetActive(true);
            UpdateDisplayContent();
        }
        [UsedImplicitly]
        public void _sdInsert(int id)
        {
            _displayStatus();
            _Beep();
            _lcdMessage = GetLocalizedString(33);
            _ender3._requestNetworkUpdate();
        }
        
        private void UpdateDisplayContent()
        {
            if (_pageDepth == 0)
            {
                PrintProgressUpdate();
                textTime.text = TimeStringGen();
                textFeedRate.text = "FR " + _ender3.printerSpeed + "%";
                textHotendTargetTemperature.text = Mathf.Floor(_ender3.TargetHotendTemperature) + "°";
                textHotendCurrentTemperature.text = Mathf.Floor(_ender3.CurrentHotendTemperature) + "°";
                textBedTargetTemperature.text = Mathf.Floor(_ender3.TargetBedTemperature) + "°";
                textBedCurrentTemperature.text = Mathf.Floor(_ender3.CurrentBedTemperature) + "°";
                textFanSpeed.text = Mathf.InverseLerp(0, 255, _ender3.FanSpeed) * 100 + "%";
                textStatus.text = _lcdMessage;
                textXPos.text = "X" + _ender3.PrinterCordPosition.x.ToString("F1", CultureInfo.InvariantCulture);
                textYPos.text = "Y" + _ender3.PrinterCordPosition.y.ToString("F1", CultureInfo.InvariantCulture);
                textZPos.text = "Z" + _ender3.PrinterCordPosition.z.ToString("F1", CultureInfo.InvariantCulture);
                return;
            }

            if (textPageTitle.text == GetLocalizedString(26))
            {
                textPage.text = "PrintPos: " + _ender3.PrinterCordPosition + "\nNormalPos: " + _ender3.NormalPosition +
                                "\nCurrentPos: " + _ender3.CurrentPosition + "\nVelocity: " + _ender3.CalcVelocity +
                                "\nFeedRate: " + _ender3.FeedRate + "\nisRelativeG0: " + _ender3.IsRelativeMovement;
            }
            else if (textPageTitle.text == GetLocalizedString(29))
            {
                textPage.text = "isPrinting: " + _ender3.IsPrinting + "\nisBusy: " + _ender3.IsBusy + "\nisPaused: " +
                                _ender3.isPaused +
                                "\nisWaitingHotend: " + _ender3.IsWaitingHotend + "\nisWaitingBed: " +
                                _ender3.IsWaitingBed +
                                "\nisManualProgress: " + IsManualProgress + "\nisExtrude: " + _ender3.ExtrudeCheck;
            }
            else if (textPageTitle.text == GetLocalizedString(30))
            {
                int gcodeNum = Mathf.Clamp(_ender3.GcodeFilePosition - 1, 0, _ender3.GcodeFileFromTextAsset.Length);
                textPage.text = "FilePosition: " + _ender3.GcodeFilePosition + "\nNetFilePosition: " +
                                _ender3.networkFilePosition +
                                "\nSDCard: " + _sdReader.currentCard + "\nFileLines: " +
                                _ender3.GcodeFileFromTextAsset.Length + "\nFileID: " +
                                _ender3.GcodeFileSelected + "\n> " + _ender3.GcodeFileFromTextAsset[gcodeNum];
            }
            else if (textPageTitle.text == GetLocalizedString(32))
            {
                textPage.text = "\nTrail Size: " + _trailRenderer.positionCount + "\nisMeshHidden: " +
                                _ender3.IsMeshHidden +
                                "\nTrailOffset: " + _lineMaterial.GetVector("_PositionOffset") +
                                "\nMesh vertices: " + _ender3.TotalVertices;
            }
            else if (textPageTitle.text == GetLocalizedString(28))
            {
                textPage.text = "isClogged: " + Networking.IsClogged + "\nisInstanceOwner: " +
                                Networking.IsInstanceOwner + "\nisMaster: " + Networking.IsMaster +
                                "\nisNetworkSettled: " + Networking.IsNetworkSettled + "\nOwner: " +
                                Networking.GetOwner(gameObject).displayName;
            }
            else if (textPageTitle.text == GetLocalizedString(31))
            {
                textPage.text =
                    GetLocalizedString(60) + "\n" + GetLocalizedString(61) + " \n" + GetLocalizedString(62) + "\n" +
                    GetLocalizedString(63);
            }
        }
        private float _timeElapsed = 0;
        private float _minutes = 0;
        private float _seconds = 0;
        private float _hours = 0;
        private string TimeStringGen()
        {
            _timeElapsed = Time.time + PrintStartTime;
            _minutes = Mathf.Floor(_timeElapsed / 60f);
            _hours = Mathf.Floor(_minutes / 60);
            _seconds = _timeElapsed - (_minutes * 60);
            if (!_ender3.IsPrinting)
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
            if (_ender3.IsFileLoaded)
            {
                if (!IsManualProgress)
                {
                    textPrintProgress.maxValue = _ender3.GcodeFileFromTextAsset.Length;
                    textPrintProgress.value = _ender3.GcodeFilePosition;
                }
                else
                {
                    textPrintProgress.maxValue = 100;
                    textPrintProgress.value = _ender3.PrintProgress;
                }
            }
        }
        private void DisplayValue()
        {
            if (textPageTitle.text == GetLocalizedString(13))
            {
                if (textValue != null)
                    textValue.text = Convert.ToString(_ender3.printerSpeed, CultureInfo.InvariantCulture) + "%";
                return;
            }
            else if (textPageTitle.text == GetLocalizedString(14))
            {
                if (textValue != null)
                    textValue.text = Convert.ToString(_ender3.audioVolume * 100, CultureInfo.InvariantCulture) +
                                     "%";
            }
            else if (textPageTitle.text == GetLocalizedString(15))
            {
                if (textValue != null)
                    textValue.text =
                        Convert.ToString(_ender3.TargetHotendTemperature, CultureInfo.InvariantCulture) + "°C";
                return;
            }
            else if (textPageTitle.text == GetLocalizedString(16))
            {
                if (textValue != null)
                    textValue.text =
                        Convert.ToString(_ender3.TargetBedTemperature, CultureInfo.InvariantCulture) +
                        "°C";
                return;
            }
            else if (textPageTitle.text == GetLocalizedString(17))
            {
                if (textValue != null)
                    textValue.text = Convert.ToString(_ender3.FanSpeed, CultureInfo.InvariantCulture);
                return;
            }
            else if (textPageTitle.text == GetLocalizedString(18))
            {
                if (textValue != null)
                    textValue.text =
                        Convert.ToString(_ender3.PrinterCordPosition.x, CultureInfo.InvariantCulture) +
                        "mm";
            }
            else if (textPageTitle.text == GetLocalizedString(19))
            {
                if (textValue != null)
                    textValue.text =
                        Convert.ToString(_ender3.PrinterCordPosition.y, CultureInfo.InvariantCulture) +
                        "mm";
            }
            else if (textPageTitle.text == GetLocalizedString(20))
            {
                if (textValue != null)
                    textValue.text =
                        Convert.ToString(_ender3.PrinterCordPosition.z, CultureInfo.InvariantCulture) +
                        "mm";
            }
        }

        private void UpdateValue(int value)
        {
            _Beep();
            if (textPageTitle.text == GetLocalizedString(13))
            {
                _ender3.printerSpeed = Mathf.Clamp(_ender3.printerSpeed + value, 50, 1000);
            }
            else if (textPageTitle.text == GetLocalizedString(14))
            {
                _ender3.audioVolume = Mathf.Clamp(_ender3.audioVolume + (value * 0.01f), 0f, 1f);
                _ender3._UpdateAudioVolume(_ender3.audioVolume);
            }
            else if (textPageTitle.text == GetLocalizedString(15))
            {
                _ender3.TargetHotendTemperature = Mathf.Clamp(_ender3.TargetHotendTemperature + value, 0, 260);
            }
            else if (textPageTitle.text == GetLocalizedString(16))
            {
                _ender3.TargetBedTemperature = Mathf.Clamp(_ender3.TargetBedTemperature + value, 0, 110);
            }
            else if (textPageTitle.text == GetLocalizedString(17))
            {
                _ender3.FanSpeed = Mathf.Clamp(_ender3.FanSpeed + value, 0, 255);
                _ender3.PrintFan.speed = _ender3.FanSpeed;
                _ender3.FanAudio.volume = (Mathf.InverseLerp(0, 1024, _ender3.FanSpeed));
            }
            else if (textPageTitle.text == GetLocalizedString(18))
            {
                _ender3.PrinterCordPosition.x =
                    Mathf.Clamp(_ender3.PrinterCordPosition.x + value, 0, _ender3.PrinterSizeInMm.x);
                _ender3.NormalPosition.x =
                    Mathf.InverseLerp(0, _ender3.PrinterSizeInMm.x, _ender3.PrinterCordPosition.x + value);
                _ender3.IsBusy = true;
                _ender3.FeedRate = 30;
            }
            else if (textPageTitle.text == GetLocalizedString(19))
            {
                _ender3.PrinterCordPosition.y =
                    Mathf.Clamp(_ender3.PrinterCordPosition.y + value, 0, _ender3.PrinterSizeInMm.y);
                _ender3.NormalPosition.y =
                    Mathf.InverseLerp(0, _ender3.PrinterSizeInMm.y, _ender3.PrinterCordPosition.y + value);
                _ender3.IsBusy = true;
                _ender3.FeedRate = 30;
            }
            else if (textPageTitle.text == GetLocalizedString(20))
            {
                _ender3.PrinterCordPosition.z =
                    Mathf.Clamp(_ender3.PrinterCordPosition.z + value, 0, _ender3.PrinterSizeInMm.z);
                _ender3.NormalPosition.z =
                    Mathf.InverseLerp(0, _ender3.PrinterSizeInMm.z, _ender3.PrinterCordPosition.z + value);
                _ender3.IsBusy = true;
            }

            DisplayValue();
            _ender3._requestNetworkUpdate();
        }
        private void _displayListMenu(String type)
        {
            AddPage(type);
            _ResetDisplay();
            
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

            imageDown.gameObject.SetActive(_options.Length > (4 + _listPagePosition));

            imageUp.gameObject.SetActive(_listPagePosition > 0);
        }
        
        #region uiCallbacks
                [UsedImplicitly]
        public void _confirm()
        {
            if (Networking.IsMaster)
            {
                if (textPageTitle.text == GetLocalizedString(3))
                {
                    _displayStatus();
                    if (!_ender3.IsVket)
                    {
                        _ender3.StartPrint();
                    }
                    else
                    {
                        Networking.SetOwner(Networking.LocalPlayer, gameObject);
                        _ender3.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(_ender3.StartPrint));
                    }
                }
                else if (textPageTitle.text == GetLocalizedString(4))
                {
                    if (!_ender3.IsVket)
                    {
                        _ender3.PrintFinished();
                    }
                    else
                    {
                        Networking.SetOwner(Networking.LocalPlayer, gameObject);
                        _ender3.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(_ender3.PrintFinished));
                    }

                    _displayStatus();
                }
                else if (textPageTitle.text == GetLocalizedString(5))
                {
                    if (!_ender3.IsVket)
                    {
                        _ender3.ResetPrinter();
                    }
                    else
                    {
                        Networking.SetOwner(Networking.LocalPlayer, gameObject);
                        _ender3.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(_ender3.ResetPrinter));
                    }

                    _displayStatus();
                    _displayStatus(); // I dont remember why i called this twice.
                }
                else if (textPageTitle.text == GetLocalizedString(2))
                {
                    _displayStatus();
                    _ender3.PrintGCode(gcodeInput.text);
                }
                else
                {
                    _back();
                }
            }
            else _back();

        }
        [UsedImplicitly]
        public void _add1()
        {
            UpdateValue(1);
        }
        [UsedImplicitly]
        public void _add10()
        {
            UpdateValue(10);
        }
        [UsedImplicitly]
        public void _add25()
        {
            UpdateValue(25);
        }
        [UsedImplicitly]
        public void _sub1()
        {
            UpdateValue(-1);
        }
        [UsedImplicitly]
        public void _sub10()
        {
            UpdateValue(-10);
        }
        [UsedImplicitly]
        public void _sub25()
        {
            UpdateValue(-25);
        }

        private string _previous;
        // ReSharper disable once MemberCanBePrivate.Global
        public void _back()
        {
            if (_pageDepth <= 1)
            {
                _displayStatus();
                _pageDepth = 0;
                return;
            }
            _pageDepth -= 2;
            _previous = _previousPage[_pageDepth];
            if (_previous == GetLocalizedString(9) || _previous == GetLocalizedString(12) || _previous == GetLocalizedString(25) ||
                _previous == GetLocalizedString(1) || _previous == GetLocalizedString(10 )|| _previous == GetLocalizedString(27) || _previous == GetLocalizedString(10) || _previous == GetLocalizedString(59))
                _displayListMenu(_previous);
        }
        [UsedImplicitly]
        public void _cancel()
        {
            _back();
        }
        
        [UsedImplicitly]
        public void _up()
        {

            if (_listPagePosition > 0)
            {
                _listPagePosition--;
            }

            _Beep();
            DisplayOptionsList();
        }
        [UsedImplicitly]
        public void _down()
        {
            _Beep();
            _listPagePosition++;
            DisplayOptionsList();
        }
        [UsedImplicitly]
        public void _displayListOption1()
        {
            ListMenuSelection(0);
        }
        [UsedImplicitly]
        public void _displayListOption2()
        {
            ListMenuSelection(1);
        }
        [UsedImplicitly]
        public void _displayListOption3()
        {
            ListMenuSelection(2);
        }
        [UsedImplicitly]
        public void _displayListOption4()
        {
            ListMenuSelection(3);
        }
        #endregion

        
        private string _previousLog = "";
        public void _Log(string message)
        {
            if (_debugLogging && message != _previousLog) Debug.Log("[<color=cyan>"+textPrinterName.text + "</color>]: " + message);
            _previousLog = message;
        }
        public void _SetLcdText(string text)
        {
            _lcdMessage = text;
            _Log(GetLocalizedString(35)+": " + text);
        }
        public void _SetDefaultLcdTxt()
        {
            _SetLcdText(GetLocalizedString(11));
        }
        private float _timeSinceLastSound = 0;
        public void _Beep()
        {
            _timeSinceLastSound = 0;
            if (_speaker == null) return;
            _speaker.enabled = true;
            _speaker.PlayOneShot(_speaker.clip);
            _Log(GetLocalizedString(34));
        }


        
        #region Language
        public String GetLocalizedString(int id)
        {
            if (_languageStrings == null)
            {
                _InitLanguage();
            }
            if (_languageStrings.Length == 0 || _languageStrings[_loadedLanguage] == null || _languageStrings[_loadedLanguage].Length == 0 || _languageStrings[_loadedLanguage].Length < id)
            {
                return "";
            }
            //_Log("GetLocalizedString(" + id + ") = " + _languageStrings[_loadedLanguage][id]);
            return _languageStrings[_loadedLanguage][id];
        }
        public void _InitLanguage()
        {
            _languageStrings = new String[languageFiles.Length][];
            for (int i = 0; i < languageFiles.Length; i++)
            {
                if (languageFiles[i] == null) continue;
                _languageStrings[i] = languageFiles[i].text.Split('\n');
            }
        }
        public void _SetLanguage(int id)
        {
            _loadedLanguage = id;
            _displayStatus();
        }
        private bool IsLanguage(String name)
        {
            foreach (TextAsset textAsset in languageFiles)
            {
                if (textAsset.name == name)
                {
                    return true;
                }
            }

            return false;
        }
        public void _SetLanguageUsingString(String name)
        {
            for (int i = 0; i < languageFiles.Length; i++)
            {
                if (languageFiles[i] == null) continue;
                if (languageFiles[i].name == name)
                {
                    _SetLanguage(i);
                    _displayStatus();
                    return;
                }
            }
        }

        private string[] _languageNames;
        private string[] GetLanguageNames()
        {
            _languageNames = new String[languageFiles.Length];
            for (int i = 0; i < languageFiles.Length; i++)
            {
                _languageNames[i] = languageFiles[i].name;
            }
            return _languageNames;
        }
        #endregion
    }
}

