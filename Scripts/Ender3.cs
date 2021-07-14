using UnityEngine;
using UnityEngine.UI;
using System;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;
using System.Globalization;
using System.Diagnostics;
using UdonToolkit;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
#endif
[CustomName("Ender VR")]
[HelpMessage("A fully functional 3d printer in VRChat")]
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class Ender3 : UdonSharpBehaviour
{
    [Header("GCode")]
    [HelpBox("Each SD Card has an ID. Set which files belong to each card. 0 is the default sd card loaded on the printer.")]
    [ListView("GCode Files")]
    public TextAsset[] GCode;
    [ListView("GCode Files")]
    public string[] ModelName;
    [ListView("GCode Files")]
    public int[] CardID;
    [SectionHeader("Speed")]
    [HideLabel]
    [Tooltip("Slows down the print to more realistic speeds")]
    [Range(50f,1000f)]
    [UdonSynced]
    public float feedrateLimiter = 100f; 

    [SectionHeader("Colors")]
    [ColorUsage(false)]
    public Color plasticColor = new Color(255,0,0);
    [Horizontal("Display Colors", true)]
    [ColorUsage(false)]
    public Color backgroundColor = new Color(0,0,255);
    [Horizontal("Display Colors", false)]
    [ColorUsage(false)]
    public Color foregroundColor = new Color(255,255,255);

    [SectionHeader("Other Options")]
    [Tooltip("Temperature in C")]
    public float ambientTemperature = 20f;
    [Range(0,1)]
    public float audioVolume = 1f;
    [Tooltip("Automatically print file 0 on sd card 0")]
    public bool AutoStartPrint = false;

    [HelpBox("Demo Mode disables print controls and autostarts slot 0. Meshes are not generated. This is NOT RECOMMENDED due to performance")]
    public bool demoMode = false;
    [HideIf("@!demoMode")]
    public int demoStartPosition = 0;

    [FoldoutGroup("Objects")]
    [SectionHeader("Printer Objects")]
    [Tooltip("Vertical Axis")]
    public Transform ZAxis;
    [FoldoutGroup("Objects")]
    [Tooltip("Forword/Back Axis")]
    public Transform YAxis;
    [FoldoutGroup("Objects")]
    [Tooltip("Left/Right Axis")]
    public Transform XAxis;
    [FoldoutGroup("Objects")]
    public Animator printFan;
    [FoldoutGroup("Objects")]
    [Tooltip("The point the hotend nozzle is located")]
    public Transform nozzle;
    [FoldoutGroup("Objects")]
    public TrailRenderer trailRenderer;
    [FoldoutGroup("Objects")]
    public GameObject _emptyMeshFilter;
    [FoldoutGroup("Objects")]
    public BoxCollider _pickupObject;
    private string[] gcodeFile = new string[1];
    [UdonSynced]
    [HideInInspector]
    public int loadedSdCard = 0;
    [UdonSynced]
    [HideInInspector]
    public int gcodeFileSelected = 0;
    private int gcodeFilePosition = 0;
    [UdonSynced]
    [HideInInspector]
    public int networkFilePosition = 0;
    private Vector3 xOffset = new Vector3(-0.03462034f, -0.04733565f, 0.007330472f);
    private Vector3 yOffset = new Vector3(-0.07813719f,0.03400593f,0.06497317f);
    private Vector3 zOffset = new Vector3(-0.1708187f,-0.0317542f,0.0055f);
    private Vector3 maxPosition = new Vector3(0.2f,-0.2f,-0.24f);
    private Vector3 printerSizeInMM = new Vector3(235,235,250);
    private Vector3 normalPosition;
    private Vector3 calcVelocity, currentPosition, printerCordPosition;
    private Vector3 velocity;
    private bool isBusy = false;
    [UdonSynced]
    [HideInInspector]
    public bool isPrinting = false;
    private bool isFileLoaded = false;
    [UdonSynced]
    [HideInInspector]
    public bool isPaused = false;
    private bool isWaitingHotend, isWaitingBed = false;
    private bool isManualProgress = false;
    private float printProgress = 0;
    private bool isMeshHidden = false;
    private float fanSpeed;
    private float feedRate, currentBedTemperature, targetBedTemperature,currentHotendTemperature, targetHotendTemperature;
    [FoldoutGroup("Objects")]
    [SectionHeader("Display")]
    public GameObject statusPage;
    [FoldoutGroup("Objects")]
    public Text textPage, textValue, textAdd1, textAdd10, textAdd25, textRemove1, textRemove10, textRemove25, textOption1, textOption2, textOption3, textOption4, textStatus, textHotendTargetTemperature, textHotendCurrentTemperature, textBedCurrentTemperature, textBedTargetTemperature, textFanSpeed, textFeedRate, textPrinterName, textXPos, textYPos, textZPos, textTime,TextPageTitle, textCancel, TextConfirmation;
    [FoldoutGroup("Objects")]
    public Slider textPrintProgress;
    [FoldoutGroup("Objects")]
    public Image imageUp, imageDown, imageGcodeConfirmation, imageHotend, imageBed, ImageFan, imageBackground, imageMiddleBar, imageProgressBar, imageProgressBarFill, imageBackButton, imageConfirmationButton, imageCancelButton, imageSD;
    private float timeMin;
    [FoldoutGroup("Objects")]
    public InputField gcodeInput;
    [HideInInspector]
    public float printStartTime;
    private MeshFilter[] meshObjects = new MeshFilter[1000];
    private int meshObjectCount = 0;
    private string[] previousPage = new string[10];
    private int pageDepth = 0;
    private string lcdMessage = "";
    private bool extrudeCheck = false;
    private Stopwatch stopWatch = new Stopwatch();
    [FoldoutGroup("Objects")]
    public Material lineMaterial;
    private float catchUpTimeout = 5000f;
    [FoldoutGroup("Objects")]
    [SectionHeader("Audio")]
    public  AudioSource fanAudio;
    [FoldoutGroup("Objects")]
    public AudioSource speaker, xMotorAudio, yMotorAudio, zMotorAudio;
    const string versionInfo = "V0.4 by CodeL1417";
    private float printerScale;
    private string[] options;
    //Where does the top of the 4 displayed options begin
    private int listPagePosition = 0;
    private int lastSyncByteCount = 0;
    private bool lastSyncSuccessful = true;
    private int totalVertices = 0;
    private bool isRelativeMovement = false;

    void Start(){
        printerScale = transform.localScale.x;
        normalPosition = new Vector3(0.5f,0.5f,0.3f);
        currentPosition = new Vector3(0.5f,0.5f,0.3f);
        currentHotendTemperature = ambientTemperature;
        currentBedTemperature = ambientTemperature;
        move();
        _displayStatus();
        lineMaterial.SetFloat("_MaxSegmentLength",0.1f * printerScale);
        lineMaterial.SetFloat("_Width",0.00035f * printerScale);
        M107(); //sets default fanspeed of 0
        lcdMessage = versionInfo;
        printerCordPosition.x = Mathf.Lerp(0f, printerSizeInMM.x,currentPosition.x);
        printerCordPosition.y = Mathf.Lerp(0f, printerSizeInMM.y,currentPosition.y);
        printerCordPosition.z = Mathf.Lerp(0f, printerSizeInMM.z,currentPosition.z);
        updateAudioVolume();
        isPrinting = AutoStartPrint;
        readFile(GCode[0]); //load default file
        if (demoMode){
            isPrinting = true;
            currentBedTemperature = 60;
            targetBedTemperature = 60;
            currentHotendTemperature = 200;
            targetHotendTemperature = 200;
            networkFilePosition= demoStartPosition;
            reSync();
        }
    }
    public override void OnPreSerialization(){
        if (Networking.IsOwner(this.gameObject)){
            networkFilePosition = gcodeFilePosition;
        }
    }
    public void printFinished(){
        isPrinting = false;
        targetHotendTemperature = 0;
        targetBedTemperature = 0;
        M107();
        isManualProgress = false;
        extrudeCheck = false;
        lcdMessage = "Finished";
        if (!demoMode){
            generateMesh();
            for (int i = 0; i < meshObjectCount; i++){
                if (Utilities.IsValid(meshObjects[i])){
                    meshObjects[i].transform.SetParent(_pickupObject.transform);
                }
            }
            _pickupObject.enabled = true;
        }
    }
    private void reSync(){
        var gap = networkFilePosition - gcodeFilePosition;
        if (gap > 500){
            isPrinting = true;
            stopWatch.Start();
            lcdMessage = "Syncing";
            isWaitingBed = false;
            isWaitingHotend = false;
            currentBedTemperature = targetBedTemperature;
            currentHotendTemperature = targetHotendTemperature;
            while (gcodeFilePosition < networkFilePosition){
                if (isBusy){
                    fastMove();
                }
                else {
                    parseGcode(gcodeFile[gcodeFilePosition]);
                    gcodeFilePosition++;
                }
                if (stopWatch.ElapsedMilliseconds > catchUpTimeout) {
                    break;
                }
            }
        catchUpTimeout = 20;
        if (gap < 500) {
            lcdMessage = versionInfo;
        }
        stopWatch.Stop();
        stopWatch.Reset();
        }
    }
    private void _ToggleMesh(){
        speaker.Play();
        if (!demoMode){
            return;
        }
        isMeshHidden = !isMeshHidden;
        for (int i = 0; i < meshObjectCount; i++){
            if (Utilities.IsValid(meshObjects[i])){
                meshObjects[i].gameObject.SetActive(!isMeshHidden);
            }
        }    
    }
    void FixedUpdate()
    {
        reSync();
        heaters();
        move();
        motorSounds();
        if (isPrinting){
            if (isFileLoaded){
                if (!isBusy && !isPaused && !isWaitingHotend && !isWaitingBed){
                    if (gcodeFilePosition + 1 < gcodeFile.Length && isFileLoaded){
                        parseGcode(gcodeFile[gcodeFilePosition]);
                        gcodeFilePosition++;
                    }
                    else {
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "printFinished");
                    }
                }
            }
            else {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "startPrint");
            }
        }
        periodically();
    }
    private void periodically(){
        timeMin = timeMin + Time.fixedDeltaTime;
        if (timeMin >= 1f){
            timeMin = timeMin - 1;
            display();
            RequestSerialization(); //Manual Sync go burr
        }
        else if (pageDepth != 0) {
            display();
        }
    }
    public void startPrint(){
        readFile(GCode[gcodeFileSelected]);
        isPrinting = true;
        isPaused = false;
        extrudeCheck = false;
        lcdMessage = "Printing";
        printStartTime = Time.time;
        gcodeFilePosition = 0;
        networkFilePosition = 0;
        cleanupMesh();
        RequestSerialization();
    }
    //clears them display
    private void resetDisplay(){
        updateColor();
        statusPage.SetActive(false);
        TextPageTitle.gameObject.SetActive(false);
        imageBackButton.gameObject.SetActive(false);
        imageConfirmationButton.gameObject.SetActive(false);
        imageCancelButton.gameObject.SetActive(false);
        gcodeInput.gameObject.SetActive(false);
        imageGcodeConfirmation.gameObject.SetActive(false);
        textOption1.text = "";
        textOption2.text = "";
        textOption3.text = "";
        textOption4.text = "";
        TextPageTitle.text = "";
        textPage.text = "";
        textOption1.gameObject.SetActive(false);
        textOption2.gameObject.SetActive(false);
        textOption3.gameObject.SetActive(false);
        textOption4.gameObject.SetActive(false);
        imageUp.gameObject.SetActive(false);
        imageDown.gameObject.SetActive(false);
        speaker.Play();
        textAdd1.gameObject.SetActive(false);
        textAdd10.gameObject.SetActive(false);
        textAdd25.gameObject.SetActive(false);
        textRemove1.gameObject.SetActive(false);
        textRemove10.gameObject.SetActive(false);
        textRemove25.gameObject.SetActive(false);
        textValue.gameObject.SetActive(false);
        textPage.gameObject.SetActive(false);
    }
    private void _displayStatus(){
        resetDisplay();
        pageDepth = 0;
        statusPage.SetActive(true);
    }
    public void _displayMainMenu(){
        _displayListMenu("Main Menu");
    }
    private void _displayGcodeInput(){
        addPage("Gcode Input");
        resetDisplay();
        gcodeInput.gameObject.SetActive(true);
        imageGcodeConfirmation.gameObject.SetActive(true);
        TextPageTitle.gameObject.SetActive(true);
        TextPageTitle.text = "Enter Gcode";
        imageBackButton.gameObject.SetActive(true);
    }
    public void _confirm(){
        if (Networking.IsMaster && !demoMode){
            switch (TextPageTitle.text) {
                case "Start Print?": _displayStatus(); SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All,"startPrint"); break;
                case "Cancel Print?": SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All,"printFinished"); _displayStatus(); break;
                case "Reset Printer?": SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All,"resetPrinter"); _displayStatus(); _displayStatus(); break;
                case "Enter Gcode": _displayStatus(); gcodeFile = gcodeInput.text.Split('\n'); isPrinting = true; isFileLoaded = true; gcodeFilePosition = 0; networkFilePosition = 0; break;
                default: _back(); break;
            }
        }
        else _back();

    }
    public void _cancel(){
        _back();
    }
    public void _back(){
        if (pageDepth <= 1){
            _displayStatus();
            pageDepth = 0;
            return;
        }
        var previous = previousPage[pageDepth - 2];
        pageDepth = pageDepth - 2;
        switch (previous){
            case "Gcode Input":
            case "Printer Control":
            case "Options":
            case "Main Menu":
            case "Debug":
            case "Files": _displayListMenu(previous); break;
            default: break;
        }
    }
    private void addPage(string page){
        previousPage[pageDepth] = page;
        pageDepth++;
    }
    private void displayValueOption(String title){
        resetDisplay();
        addPage(title);
        TextPageTitle.text = title;
        textAdd1.gameObject.SetActive(true);
        textAdd10.gameObject.SetActive(true);
        textAdd25.gameObject.SetActive(true);
        textRemove1.gameObject.SetActive(true);
        textRemove10.gameObject.SetActive(true);
        textRemove25.gameObject.SetActive(true);
        textValue.gameObject.SetActive(true);
        TextPageTitle.gameObject.SetActive(true);
        imageBackButton.gameObject.SetActive(true);
        displayValue();
    }
    private void _displayConfirmation(String title){
        addPage(title);
        resetDisplay();
        TextPageTitle.gameObject.SetActive(true);
        TextPageTitle.text = title;
        imageBackButton.gameObject.SetActive(true);
        imageConfirmationButton.gameObject.SetActive(true);
        imageCancelButton.gameObject.SetActive(true);  
        }
    private void updateColor(){
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
        ImageFan.color = foregroundColor;
        imageMiddleBar.color = foregroundColor;
        textTime.color = foregroundColor;
        imageProgressBar.color = foregroundColor;
        imageProgressBarFill.color = foregroundColor;
        imageConfirmationButton.color = foregroundColor;
        imageGcodeConfirmation.color = foregroundColor;
        imageCancelButton.color = foregroundColor;
        TextPageTitle.color = foregroundColor;
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
    }
    private void resetPrinter(){
        if (demoMode) {
            return;
        }
        isPrinting = false;
        targetHotendTemperature = 0;
        targetBedTemperature = 0;
        M107();
        isManualProgress = false;
        gcodeFilePosition = 0;
        networkFilePosition = 0;
        _displayStatus();
        gcodeFile = new string[0];
        cleanupMesh();
        isPaused = false;
        isBusy = false;
        isWaitingBed = false;
        isWaitingHotend = false;
        isFileLoaded = false;
        extrudeCheck = false;
        lcdMessage = versionInfo;
        totalVertices = 0;
        RequestSerialization();
    }
    private void cleanupMesh(){
        if (demoMode){
            return;
        }
        totalVertices = 0;
        trailRenderer.Clear();
        for (int i = 0; i < meshObjectCount; i++){
            if (Utilities.IsValid(meshObjects[i])){
                UnityEngine.Object.Destroy(meshObjects[i]);
            }
        }
        //force drop to return pickup to start position
        var pickup = (VRCPickup)_pickupObject.GetComponent(typeof(VRCPickup));
        pickup.Drop();
        var objectSync = (VRCObjectSync)_pickupObject.GetComponent(typeof(VRCObjectSync));
        objectSync.Respawn();
        _pickupObject.enabled = false;
    }
    private void generateListMenuItems(){
        var fileiD = new int[100];
        var fileLength = 0;
        var pageTitle = TextPageTitle.text;
        if (pageTitle == "Files") {
            for (int i = 0; i < GCode.Length; i++){
                if (CardID[i] == loadedSdCard) {
                    fileiD[fileLength] = i;
                    fileLength++;
                }
            }
            options = new string[fileLength];
            for (int i = 0; i < fileLength; i++) {
                options[i] = ModelName[fileiD[i]];
            }
        }
        else if (pageTitle == "Options"){
            options = new string[]{"Speed","Audio Volume","Toggle Mesh", "Reset Printer"};
        }
        else if (pageTitle == "Printer Control"){ //lock during print, provide different menu
            if (isPrinting) {
                options = new string[]{"Pause Print","Cancel Print"};
            }
            else {
                options = new string[]{"Bed Temp","Hotend Temp","Fan Speed","Cooldown", "Auto Home","Move X Axis","Move Y Axis","Move Z Axis"};
            }
        }
        else if (pageTitle == "Main Menu"){
            options = new string[]{"Printer Control","SD Card","Gcode Input","Options","Credits", "Debug"};
        }
        else if (pageTitle == "Debug") {
            options = new string[] {"Position", "Status", "GCode", "Mesh", "Network"};
        }
    }
    private void listMenuSelection(int id){
        string SelectedName = "";
        switch (id){
            case 0:
                SelectedName = textOption1.text;
                break;
            case 1:
                SelectedName = textOption2.text;
                break;
            case 2:
                SelectedName = textOption3.text;
                break;
            case 3:
                SelectedName = textOption4.text;
                break;
        }
        if (SelectedName == ""){
            return;
        }
        if (TextPageTitle.text == "Files") {
            for (int i = 0; i< GCode.Length; i++){
                if (ModelName[i] == SelectedName){
                    gcodeFileSelected = i;
                    _displayConfirmation("Start Print?");
                    break;
                }
            }
        }
        else {
            switch (SelectedName) {
                case "Speed":
                case "Audio Volume":
                case "Hotend Temp":
                case "Bed Temp":
                case "Fan Speed":
                case "Move X Axis":
                case "Move Y Axis":
                case "Move Z Axis": displayValueOption(SelectedName); break;
                case "Plastic Color": //TODO: add color selection system.
                case "Auto Home": speaker.Play(); G28(); break;
                case "Pause Print": speaker.Play(); isPaused = !isPaused; break;
                case "Cancel Print": _displayConfirmation("Cancel Print?"); break;
                case "Printer Control": _displayListMenu("Printer Control"); break;
                case "SD Card": _displayListMenu("Files"); break;
                case "Options": _displayListMenu("Options"); break;
                case "Debug": _displayListMenu("Debug"); break;
                case "Gcode Input": _displayGcodeInput(); break;
                case "Cooldown": speaker.Play(); targetBedTemperature = 0; targetHotendTemperature = 0; _displayStatus(); break;
                case "Reset Printer": _displayConfirmation("Reset Printer?"); break;
                case "Toggle Mesh": speaker.Play(); _ToggleMesh(); break;
                case "Position":
                case "GCode":
                case "Mesh":
                case "Network":
                case "Credits": 
                case "Status": displayTextPage(SelectedName); break;
            }
            RequestSerialization();
        }
        
    }
    private void displayTextPage(string title){
        resetDisplay();
        TextPageTitle.text = title;
        addPage(title);
        textPage.gameObject.SetActive(true);
        TextPageTitle.gameObject.SetActive(true);
        imageBackButton.gameObject.SetActive(true);
        display();
    }
    public void _sdInsert(){
        _displayStatus();
        speaker.Play();
        lcdMessage = "SD Card Inserted";
        RequestSerialization();
    }
    public void _up(){

        if (listPagePosition > 0) {
            listPagePosition--;
        }
        speaker.Play();
        displayOptionsList();
    }
    public void _down(){
        speaker.Play();
        listPagePosition++;
        displayOptionsList();
    }
    private void _displayListMenu(String type){
        addPage(type);
        resetDisplay();
        TextPageTitle.text = type;
        listPagePosition = 0;
        textOption1.gameObject.SetActive(true);
        textOption2.gameObject.SetActive(true);
        textOption3.gameObject.SetActive(true);
        textOption4.gameObject.SetActive(true);
        imageBackButton.gameObject.SetActive(true);
        TextPageTitle.gameObject.SetActive(true);
        generateListMenuItems();
        displayOptionsList();
    }
    private void displayOptionsList() {
        if (options.Length > (0 + listPagePosition)) {
            textOption1.text = options[0 + listPagePosition];
        }
        if (options.Length > (1 + listPagePosition)) {
            textOption2.text = options[1 + listPagePosition];
        }
        if (options.Length > (2 + listPagePosition)) {
            textOption3.text = options[2 + listPagePosition];
        }
        if (options.Length > (3 + listPagePosition)) {
            textOption4.text = options[3 + listPagePosition];
        }
        if (options.Length > (4 + listPagePosition)) {
            imageDown.gameObject.SetActive(true);
        }
        else {
            imageDown.gameObject.SetActive(false);
        }
        if (listPagePosition > 0) {
            imageUp.gameObject.SetActive(true);
        }
        else {
            imageUp.gameObject.SetActive(false);
        }
    }
    public void _displayListOption1(){
        listMenuSelection(0);
    }
    public void _displayListOption2(){
        listMenuSelection(1);
    }
    public void _displayListOption3(){
        listMenuSelection(2);
    }
    public void _displayListOption4(){
        listMenuSelection(3);
    }
    private void motorSounds(){
        var absVelocity = new Vector3(Mathf.Abs(calcVelocity.x),Mathf.Abs(calcVelocity.y),Mathf.Abs(calcVelocity.z));
        xMotorAudio.pitch = Mathf.Clamp(0.15f,0f,absVelocity.x);
        yMotorAudio.pitch = Mathf.Clamp(0.15f,0f,absVelocity.y);
        zMotorAudio.pitch = Mathf.Clamp(0.15f,0f,absVelocity.z);
    }
    private void updateAudioVolume(){
        xMotorAudio.volume = audioVolume / 2;
        yMotorAudio.volume = audioVolume / 2;
        zMotorAudio.volume = audioVolume / 2;
        fanAudio.volume = (Mathf.InverseLerp(0,512,fanSpeed)) * audioVolume;
        speaker.volume = audioVolume;
    }
    private String TimeStringGen(){
        var timeElapsed = Time.time + printStartTime;
        var minutes = Mathf.Floor(timeElapsed / 60f);
        var hours = Mathf.Floor(minutes / 60);
        var seconds = timeElapsed - (minutes * 60);
        if (!isPrinting){
            return "00:00";
        }
        if (hours > 0){
            return String.Format("{0:00}", hours) + ":" + String.Format("{0:00}", minutes / 60);
        }
        else return String.Format("{0:00}", minutes) + ":" + String.Format("{0:00}", seconds);
    }
    private void printProgressUpdate(){
        if (isFileLoaded){
            if (!isManualProgress){
                textPrintProgress.maxValue = gcodeFile.Length;
                textPrintProgress.value = gcodeFilePosition;
            }
            else {
                textPrintProgress.maxValue = 100;
                textPrintProgress.value = printProgress;
            }      
        }
    }
    public override void OnPostSerialization(VRC.Udon.Common.SerializationResult result){
        lastSyncByteCount =  result.byteCount;
        lastSyncSuccessful = result.success;
    }
    private void display(){
        if (pageDepth == 0) {
            printProgressUpdate();
            textTime.text = TimeStringGen();
            textHotendTargetTemperature.text = Mathf.Floor(targetHotendTemperature) + "°";
            textHotendCurrentTemperature.text = Mathf.Floor(currentHotendTemperature) + "°";
            textBedTargetTemperature.text = Mathf.Floor(targetBedTemperature) + "°";
            textBedCurrentTemperature.text = Mathf.Floor(currentBedTemperature) + "°";
            textFanSpeed.text = Mathf.InverseLerp(0,255,fanSpeed) * 100 + "%";
            textStatus.text = lcdMessage;
            textXPos.text = "X" + printerCordPosition.x.ToString("F1", CultureInfo.InvariantCulture);
            textYPos.text = "Y" + printerCordPosition.y.ToString("F1", CultureInfo.InvariantCulture);
            textZPos.text = "Z" + printerCordPosition.z.ToString("F1", CultureInfo.InvariantCulture);
        }
        switch (TextPageTitle.text){
            case "Position": textPage.text = "PrintPos: " + printerCordPosition + "\nNormalPos: " + normalPosition + "\nCurrentPos: " + currentPosition + "\nVelocity: " + calcVelocity + "\nFeedRate: " + feedRate + "\nisRelativeG0: " + isRelativeMovement ; break;
            case "Status": textPage.text = "isPrinting: " + isPrinting + "\nisBusy: " + isBusy + "\nisPaused: " + isPaused + "\nisWaitingHotend: " + isWaitingHotend + "\nisWaitingBed: " + isWaitingBed + "\nisManualProgress: " + isManualProgress + "\nisExtrude: " + extrudeCheck; break;
            case "GCode": 
                var gcodeNum = Mathf.Clamp(gcodeFilePosition - 1,0, gcodeFile.Length);
                textPage.text = "FilePosition: " + gcodeFilePosition + "\nNetFilePosition: " + networkFilePosition + "\nSDCard: " + loadedSdCard + "\nFileLines: " + gcodeFile.Length + "\nFileID: " + gcodeFileSelected + "\n> " + gcodeFile[gcodeNum]; 
                break;
            case "Mesh": textPage.text = "Mesh Count: " + meshObjectCount + "\nTrail Size: " + trailRenderer.positionCount + "\nisMeshHidden: " + isMeshHidden + "\nTrailOffset: " + lineMaterial.GetVector("_PositionOffset") + "\nMesh vertices: " + totalVertices ;  break;
            case "Network": textPage.text = "isClogged: " + Networking.IsClogged + "\nisInstanceOwner: " + Networking.IsInstanceOwner + "\nisMaster: " + Networking.IsMaster + "\nisNetworkSettled: " + Networking.IsNetworkSettled + "\nisLastSyncSucessful: " + lastSyncSuccessful + "\nlastSyncBytes: " + lastSyncByteCount + "\nOwner: " + Networking.GetOwner(this.gameObject).displayName; break; //breaks in Unity
            case "Credits": textPage.text = "-Code by Codel1417, Lyuma \n-Shader by Lyuma, phi16, Xiexe\n-UdonSharp By Merlin"; break;
        }
    }
    private void addVertToTrail(bool isExtrude){
        if (trailRenderer.positionCount < 10000){
            if (isExtrude){
                //Cold Extrusion Prevention
                if (currentHotendTemperature  > 160f){
                    var nozzleLocal = transform.InverseTransformPoint(nozzle.position);
                    var point = new Vector3(nozzleLocal.x, nozzleLocal.y, Mathf.Lerp(yOffset.y ,maxPosition.y, currentPosition.y));
                    //The Nozzle isnt actually moving in the Y axis (Z World). We have to move the trail position to simulate the movement of the bed.
                    trailRenderer.AddPosition(transform.TransformPoint(point));
                    totalVertices = totalVertices + 2; //technically the shader adds 4 per vert 

                }
                else {
                    lcdMessage = "Cold Extrusion Prevented";
                }
            }
            else {
                totalVertices = totalVertices + 2; //technically the shader adds 4 per vert 
                trailRenderer.AddPosition(new Vector3(nozzle.position.x,nozzle.position.y - (20f * printerScale), nozzle.position.z));
            }
        }
        else {
            if (!demoMode){
                generateMesh();
            }
            addVertToTrail(isExtrude);
        }
    }
    private void generateMesh(){
            var mesh = new Mesh();
            trailRenderer.BakeMesh(mesh);
            meshObjects[meshObjectCount] = VRCInstantiate(_emptyMeshFilter).GetComponent<MeshFilter>();
            meshObjects[meshObjectCount].mesh = mesh;
            meshObjects[meshObjectCount].gameObject.SetActive(!isMeshHidden);
            trailRenderer.Clear();
            meshObjectCount++;
    }
    private void displayValue(){
        switch (TextPageTitle.text){
            case "Speed": textValue.text = Convert.ToString(feedrateLimiter) + "%"; return;
            case "Audio Volume": textValue.text = Convert.ToString(audioVolume * 100) + "%"; break;
            case "Hotend Temp": textValue.text = Convert.ToString(targetHotendTemperature) + "°C"; return;
            case "Bed Temp": textValue.text = Convert.ToString(targetBedTemperature) + "°C"; return;
            case "Fan Speed": textValue.text = Convert.ToString(fanSpeed); return;
            case "Move X Axis": textValue.text = Convert.ToString(printerCordPosition.x) + "mm"; break;
            case "Move Y Axis": textValue.text = Convert.ToString(printerCordPosition.y) + "mm"; break;
            case "Move Z Axis": textValue.text = Convert.ToString(printerCordPosition.z) + "mm"; break;
        }
    }
    private void updateValue(int value) {
        speaker.Play();
        switch (TextPageTitle.text){
            case "Speed": feedrateLimiter = Mathf.Clamp(feedrateLimiter + value,50f,1000f);break;
            case "Audio Volume": audioVolume = Mathf.Clamp(audioVolume + (value * 0.01f) ,0f,1f); updateAudioVolume(); break;
            case "Hotend Temp": targetHotendTemperature = Mathf.Clamp(targetHotendTemperature + value,0,260);break;
            case "Bed Temp": targetBedTemperature = Mathf.Clamp(targetBedTemperature + value,0,110);break;
            case "Fan Speed": fanSpeed = Mathf.Clamp(fanSpeed + value,0,255); printFan.speed = fanSpeed; fanAudio.volume = (Mathf.InverseLerp(0,1024,fanSpeed)); break;
            case "Move X Axis": printerCordPosition.x =  Mathf.Clamp(printerCordPosition.x + value,0,printerSizeInMM.x); normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, printerCordPosition.x + value); isBusy = true; feedRate = 30; break;
            case "Move Y Axis": printerCordPosition.y =  Mathf.Clamp(printerCordPosition.y + value,0,printerSizeInMM.y); normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y, printerCordPosition.y + value); isBusy = true; feedRate = 30; break;
            case "Move Z Axis": printerCordPosition.z =  Mathf.Clamp(printerCordPosition.z + value,0,printerSizeInMM.z); normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z, printerCordPosition.z + value); isBusy = true; break;
        }
        displayValue();
        RequestSerialization();
    }
    public void _add1(){
        updateValue(1);
    }
    public void _add10(){
        updateValue(10);
    }
    public void _add25(){
        updateValue(25);
    }
    public void _sub1(){
        updateValue(-1);
    }
    public void _sub10(){
        updateValue(-10);
    }
    public void _sub25(){
        updateValue(-25);
    }
    private void move(){
        var previousPosition = currentPosition;
        currentPosition.x =  Mathf.SmoothDamp(currentPosition.x, normalPosition.x, ref velocity.x, 0f, Mathf.Clamp(feedRate,0,500));
        currentPosition.y =  Mathf.SmoothDamp(currentPosition.y, normalPosition.y, ref velocity.z, 0f, Mathf.Clamp(feedRate,0,500));
        currentPosition.z =  Mathf.SmoothDamp(currentPosition.z, normalPosition.z, ref velocity.z, 0f, Mathf.Clamp(feedRate,0,5));

        calcVelocity = (previousPosition - currentPosition) * 50; //used for sound

        XAxis.localPosition = new Vector3(Mathf.Lerp(xOffset.x, maxPosition.x, currentPosition.x), xOffset.y, xOffset.z);
        YAxis.localPosition = new Vector3(yOffset.x,Mathf.Lerp(yOffset.y,maxPosition.y, currentPosition.y),yOffset.z);
        ZAxis.localPosition = new Vector3(zOffset.x,zOffset.y,Mathf.Lerp(zOffset.z,maxPosition.z,currentPosition.z));
        if (Mathf.Approximately(currentPosition.x,normalPosition.x) && Mathf.Approximately(currentPosition.y,normalPosition.y) && Mathf.Approximately(currentPosition.z,normalPosition.z)){
            isBusy = false;
            if (extrudeCheck){
                addVertToTrail(extrudeCheck);
            }
        }
        lineMaterial.SetVector("_PositionOffset",transform.TransformVector(new Vector3(0,0,(-YAxis.localPosition.y) + 0.089f)));
    }
    private void fastMove(){
        currentPosition = normalPosition;
        XAxis.localPosition = new Vector3(Mathf.Lerp(xOffset.x, maxPosition.x, normalPosition.x), xOffset.y, xOffset.z);
        YAxis.localPosition = new Vector3(yOffset.x,Mathf.Lerp(yOffset.y,maxPosition.y, normalPosition.y),yOffset.z);
        ZAxis.localPosition = new Vector3(zOffset.x,zOffset.y,Mathf.Lerp(zOffset.z,maxPosition.z,normalPosition.z));
        isBusy = false;
        if (extrudeCheck){
            addVertToTrail(extrudeCheck);
        }
    }
    private void heaters(){
        //ambient cooldown
        currentBedTemperature =  Mathf.MoveTowards(currentBedTemperature,ambientTemperature, 0.1f * Time.deltaTime * (feedrateLimiter / 100));
        currentHotendTemperature =  Mathf.MoveTowards(currentHotendTemperature,ambientTemperature, 0.5f * Time.deltaTime * (feedrateLimiter / 100));
        
        if (targetBedTemperature > ambientTemperature){
            currentBedTemperature = Mathf.MoveTowards(currentBedTemperature,targetBedTemperature,0.5f * Time.deltaTime * (feedrateLimiter / 100));
        }
        if (targetHotendTemperature > ambientTemperature){
            currentHotendTemperature = Mathf.MoveTowards(currentHotendTemperature,targetHotendTemperature, 2f * Time.deltaTime * (feedrateLimiter / 100));
        }
        if (Mathf.Approximately(currentBedTemperature,targetBedTemperature)){
            if (isWaitingBed){
                isBusy = false;
                lcdMessage = versionInfo;
            }
            isWaitingBed = false;
        }
        if (Mathf.Approximately(currentHotendTemperature,targetHotendTemperature)){
            if (isWaitingHotend){
                isBusy = false;
                lcdMessage = versionInfo;
            }
            isWaitingHotend = false;
        }
    }
    private void readFile(TextAsset text){
        gcodeFile = text.text.Split('\n');
        isFileLoaded = true;
    }
    private void parseGcode(string gcode){
        if (string.IsNullOrEmpty(gcode) || gcode[0] == ';'){
            return;
        }
        if (gcode.Contains(";")){         //strip comments from gcode line
            gcode = gcode.Substring(0, gcode.IndexOf(';') - 1);
        }
        var words = gcode.Split(' ');
        if (words.Length == 0){ //if empty line
            return;
        }
        else if(words[0].Length <= 1){ //if too short to be a command
            return;
        }
        isBusy = true;
        switch (words[0].Trim()){
            case "G0":
            case "G1": G0(words); break;
            case "G28": G28(); break;
            case "M25": M25(); break;
            case "M73": M73(words); break;
            case "M104": M104(words); break;
            case "M106": M106(words); break;
            case "M107": M107(); break;
            case "M109": M109(words); break;
            case "M117": M117(gcode); break;
            case "M118": M118(gcode); break;
            case "M140": M140(words); break;
            case "M190": M190(words); break;
            case "G29": break; //bed leveling 
            case "M82": isRelativeMovement = true; break;
            case "M105": break; //report temperature to serial
            case "M84":  break; //disable steppers
            case "G90": isRelativeMovement = false; break;
            case "G92": break; //set position.
            default:
                UnityEngine.Debug.LogWarning("[ENDER 3] Unknown GCODE: " + gcode);
                break;
        }
    }

    //G0/G1 is a linear move;
    private void G0(string[] words) {
        extrudeCheck = false;
        for (int i = 1; i < words.Length; i++){
            string currentGcodeLineSection;

            if(words[i].Length > 0){
                currentGcodeLineSection = words[i].Substring(1);
            }
            else continue;
            switch (words[i][0]){
                case 'X':
                    var value = Convert.ToSingle(currentGcodeLineSection);
                    if (isRelativeMovement){
                        printerCordPosition.x = Mathf.Clamp(printerCordPosition.x + value, 0f, printerSizeInMM.x);
                        normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, printerCordPosition.x);
                    }
                    else {
                        printerCordPosition.x = value;
                        normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, value);
                    }

                    break;
                case 'Y':
                    var value1 = Convert.ToSingle(currentGcodeLineSection);
                    if (isRelativeMovement){
                        printerCordPosition.y = Mathf.Clamp(printerCordPosition.y + value1, 0f, printerSizeInMM.y);
                        normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y,printerCordPosition.y);

                    }
                    else {
                        printerCordPosition.y = value1;
                        normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y,value1);
                    }
                    break;
                case 'Z':
                    var value2 = Convert.ToSingle(currentGcodeLineSection);
                    if (isRelativeMovement){
                        printerCordPosition.z = Mathf.Clamp(printerCordPosition.z + value2, 0f, printerSizeInMM.z);
                        normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z,printerCordPosition.z);

                    }
                    else {
                        printerCordPosition.z = value2;
                        normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z,value2);
                    }
                    break;
                case 'F':
                    feedRate = Convert.ToSingle(currentGcodeLineSection,System.Globalization.CultureInfo.InvariantCulture)  * (feedrateLimiter * 0.0001f);
                    break;
                case 'E':
                    extrudeCheck = true;
                    break;
            }
        }
        addVertToTrail(extrudeCheck);
    }
    private void G28(){
        normalPosition = Vector3.zero;
        printerCordPosition = Vector3.zero;
        feedRate = 15;
    }
    private void M25(){
        isPaused = !isPaused;
    }
    private void M73(String[] words){
        isManualProgress = true;
        for (int i = 1; i < words.Length; i++){
            string currentGcodeLineSection;

            if(words[i].Length > 0){
                currentGcodeLineSection = words[i].Substring(1);
            }
            else continue;
            switch (words[i][0]){
                case 'P':
                    printProgress = Mathf.Clamp(Convert.ToSingle(currentGcodeLineSection,System.Globalization.CultureInfo.InvariantCulture),0f,255f);
                    break;
            }
        }
    }
    private void M104(string[] words){
        for (int i = 1; i < words.Length; i++){
            string currentGcodeLineSection;

            if(words[i].Length > 0){
                currentGcodeLineSection = words[i].Substring(1);
            }
            else continue;
            switch (words[i][0]){
                case 'S':
                    targetHotendTemperature = Mathf.Clamp(Convert.ToInt32(currentGcodeLineSection),0,260);
                    break;
            }
        }
    }
    private void M106(string[] words){
        for (int i = 1; i < words.Length; i++){
            string currentGcodeLineSection;

            if(words[i].Length > 0){
                currentGcodeLineSection = words[i].Substring(1);
            }
            else continue;
            switch (words[i][0]){
                case 'S':
                    var speed = Mathf.Clamp(Convert.ToSingle(currentGcodeLineSection,System.Globalization.CultureInfo.InvariantCulture),0f,255f);
                    fanAudio.volume = (Mathf.InverseLerp(0,512,speed)) * audioVolume;
                    printFan.speed = speed;
                    fanSpeed = speed;
                    break;
            }
        }
    }
    private void M107(){
        printFan.speed = 0;
        fanAudio.volume = 0;
        fanSpeed = 0;
    }
    private void M109(string[] words){
        isWaitingHotend = true;
        isBusy = true;
        lcdMessage = "Heating Hotend";
        M104(words);
    }
    private void M117(string gcode){
        lcdMessage = gcode.Substring(4);
    }
    private void M118(string print){
        UnityEngine.Debug.Log("[ENDER 3] " + print.Substring(4));
    }
    private void M140(string[] words){
        for (int i = 1; i < words.Length; i++){
            string currentGcodeLineSection;

            if(words[i].Length > 0){
                currentGcodeLineSection = words[i].Substring(1);
            }
            else continue;
            switch (words[i][0]){
                case 'S':
                    targetBedTemperature = Mathf.Clamp(Convert.ToInt32(currentGcodeLineSection),0,110);
                    break;
            }
        }
    }
    private void M190(string[] words){
        isWaitingBed = true;
        isBusy = true;
        lcdMessage ="Heating Bed";
        M140(words);
    }

    #if !COMPILER_UDONSHARP && UNITY_EDITOR
    //TODO move to own class
        public void ExportMesh() {
            if (meshObjectCount > 0) {
                CombineInstance[] combine = new CombineInstance[meshObjectCount];
                for (int i = 0; i < meshObjectCount; i++){
                    combine[i].mesh = meshObjects[i].sharedMesh;
                }
                Mesh combMesh = new Mesh();
                combMesh.CombineMeshes(combine);
                AssetDatabase.CreateAsset(combMesh, "Assets/Ender 3/generatedMeshes/"+ ModelName[gcodeFileSelected] + ".asset");
                UnityEngine.Debug.Log("[ENDER 3] Mesh Verts: " + combMesh.vertexCount);
            }
        }
    #endif
    #if UNITY_EDITOR
        public void testNetwork(){
            networkFilePosition = 100000;
        }
    #endif
}