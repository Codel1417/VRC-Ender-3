using UnityEngine;
using UnityEngine.UI;
using System;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
using System.Globalization;
using System.Diagnostics;

public class Ender3 : UdonSharpBehaviour
{
    const float ambientTemperature = 20f;
    const float feedrateLimiter = 30; //30 default
    private float fanSpeed;
    private float feedRate;
    private float currentBedTemperature;
    private float targetBedTemperature;
    private float currentHotendTemperature;
    private float targetHotendTemperature;

    [Header("Objects")]
    public Transform ZAxis;
    public Transform YAxis;
    public Transform XAxis;
    public Transform printFan;
    public Transform nozzle;
    public TrailRenderer trailRenderer;
    public GameObject _emptyMeshFilter;
    private string[] gcodeFile;
    private int gcodeFilePosition = 0;

    [HideInInspector]
    [UdonSynced]
    public int networkFilePosition = 0;

    [Header("GCode")]
    public TextAsset gcodeTextFile;
    [Header("Position Data")]
    [Tooltip("This is the 0 position of the object that moves on the X Axis")]
    public Vector3 xOffset = new Vector3(-0.03462034f, -0.04733565f, 0.007330472f);
    [Tooltip("This is the 0 position of the object that moves on the Y Axis")]
    public Vector3 yOffset = new Vector3(-0.07813719f,0.03400593f,0.06497317f);
    [Tooltip("This is the 0 position of the object that moves on the Z (Vertical) Axis")]
    public Vector3 zOffset = new Vector3(-0.1708187f,-0.0317542f,0.004123815f);
    [Tooltip("This alligns the trail with the nozzle of the printer")]
    public Vector3 trailOffset = new Vector3(-0.09f,0f,0.09f);
    [Tooltip("The furthest position each object can move on each axis")]
    public Vector3 maxPosition = new Vector3(0.2009f,-0.20f,-0.24f);
    private Vector3 printerSizeInMM = new Vector3(235,235,250);
    private Vector3 normalPosition;
    private Vector3 velocity;
    private Vector3 currentPosition;
    [Header("Status")]
    private bool isBusy = false;
    private bool isPrinting = false;
    private bool isFileLoaded = false;
    private bool isPaused = false;
    private bool isWaitingHotend, isWaitingBed = false;
    private bool isManualProgress = false;
    private float printProgress = 0;
    [Tooltip("Hides the baked meshes for performance")]
    private bool isMeshHidden = false;

    [Header("Colors")]
    public Color backgroundColor;
    public Color foregroundColor;
    public Color plasticColor;
    [Header("Display")]
    public GameObject statusPage;
    private string lcdMessage;

    public Text textStatus;
    public Text textHotendTargetTemperature, textHotendCurrentTemperature, textBedCurrentTemperature, textBedTargetTemperature, textFanSpeed, textFeedRate, textPrinterName, textXPos, textYPos, textZPos, textTime,TextPageTitle, textCredits, textCancel, TextConfirmation;
    public Slider textPrintProgress;
    public Image imageHotend, imageBed, ImageFan, imageBackground, imageMiddleBar, imageProgressBar, imageProgressBarFill, imageBackButton, imageConfirmationButton, imageCancelButton, imageSD;
    private float timeMin;
    public InputField gcodeInput;
    public Image imageGcodeConfirmation;
    [HideInInspector]
    [UdonSynced]
    public float printStartTime;
    private MeshFilter[] meshObjects = new MeshFilter[10000];
    private int meshObjectCount = 0;
    private int displayInputChoice = 0; //0 = default, 1 = confirm, 2 = cancel
    private string[] previousPage = new string[5];
    private int pageDepth = 0;
    private VRCPlayerApi playerApi;
    private bool extrudeCheck = false;
    private Stopwatch stopWatch = new Stopwatch();
    const int maxNetworkGap = 1000;
    private Material lineMaterial;
    private int catchUpTimeout = 5000;
    [Header("Audio")]
    public AudioSource fanAudio;
    public AudioSource speaker;
    const string versionInfo = "V0.2 by CodeL1417";
    public override void OnDeserialization(){
        if (Networking.GetOwner(this.gameObject) == playerApi){
            return; //only sync to non owners
        }
        stopWatch.Start();
        var gap = networkFilePosition - gcodeFilePosition;
        if (gap > maxNetworkGap){
            UnityEngine.Debug.Log("[ENDER 3] Catching Up. " + gap + " lines behind");
            isWaitingBed = false;
            isWaitingHotend = false;
            currentBedTemperature = targetBedTemperature;
            currentHotendTemperature = targetHotendTemperature;
            while (gcodeFilePosition < networkFilePosition){
                //custom move function without interpolation
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
        catchUpTimeout = 40;
        lcdMessage = versionInfo;
        stopWatch.Stop();
        stopWatch.Reset();
        }
    }
    void Start(){
        lineMaterial = trailRenderer.sharedMaterial;
        playerApi = Networking.LocalPlayer;
        normalPosition = new Vector3(0.5f,0.5f,0.3f);
        currentPosition = new Vector3(0.5f,0.5f,0.3f);
        currentHotendTemperature = ambientTemperature;
        currentBedTemperature = ambientTemperature;
        move();
        _displayStatus();
        isPrinting = true;


        //debug
        currentBedTemperature = 60;
        targetBedTemperature = 60;
        currentHotendTemperature = 200;
        targetHotendTemperature = 200;
    }
    public void _ToggleMesh(){
        speaker.Play();
        isMeshHidden = !isMeshHidden;
        hideMeshes();
    }
    void Update()
    {
        periodically();
        fan();
        heaters();
        move();
        if (isPrinting){
            if (isFileLoaded){
                if (!isBusy && !isPaused && !isWaitingHotend && !isWaitingBed){
                    if (gcodeFilePosition + 1 < gcodeFile.Length && isFileLoaded){
                        parseGcode(gcodeFile[gcodeFilePosition]);
                        gcodeFilePosition++;
                    }
                    else {
                        lcdMessage = "Finished";
                        generateMesh();
                        reset();
                    }

                }
            }
            else {
                readFile(gcodeTextFile);
                isPrinting = true;
                lcdMessage = "Printing";
                printStartTime = Time.time;
                gcodeFilePosition = 0;
                cleanupMesh();
            }
        }
        else
        {
            isFileLoaded = false;
            G28();
        }
        if (Networking.GetOwner(this.gameObject) == playerApi){
            networkFilePosition = gcodeFilePosition;
        }
    }
    private void linePerformanceOption(){
        if (!isMeshHidden){
            lineMaterial.SetFloat("Width",Mathf.InverseLerp(10,0, Vector3.Distance(playerApi.GetPosition(),transform.position)) * 0.0004f * transform.localScale.x);
        }
    }
    private void hideMeshes(){
        for (int i = 0; i < meshObjectCount; i++){
            if (Utilities.IsValid(meshObjects[i])){
                meshObjects[i].gameObject.SetActive(!isMeshHidden);
            }
        }

    }
    private void periodically(){
        timeMin = timeMin + Time.deltaTime;
        {
            if (timeMin >= 1f){
                timeMin = timeMin - 1;
                updateColor();
                display();
                #if !UNITY_EDITOR
                    linePerformanceOption();
                #endif
            }
        }
    }
    //clears them display
    private void resetDisplay(){
        statusPage.SetActive(false);
        TextPageTitle.gameObject.SetActive(false);
        textCredits.gameObject.SetActive(false);
        imageBackButton.gameObject.SetActive(false);
        imageConfirmationButton.gameObject.SetActive(false);
        imageCancelButton.gameObject.SetActive(false);
        gcodeInput.gameObject.SetActive(false);
        imageGcodeConfirmation.gameObject.SetActive(false);
        speaker.Play();
    }
    public void _displayStatus(){
        resetDisplay();
        statusPage.SetActive(true);
    }
    public void _displayGcodeInput(){
        addPage("gcode");
        resetDisplay();
        gcodeInput.gameObject.SetActive(true);
        imageGcodeConfirmation.gameObject.SetActive(true);
        TextPageTitle.gameObject.SetActive(true);
        TextPageTitle.text = "Enter Gcode";
        imageBackButton.gameObject.SetActive(true);
    }
    public void _confirmGcode(){
        _displayStatus();
        gcodeFile = gcodeInput.text.Split('\n');
        isPrinting = true;
        isFileLoaded = true;
    }
    public void _confirm(){
        _back();
        displayInputChoice = 1;
    }
    public void _cancel(){
        _back();
        displayInputChoice = 2;
    }
    public void _back(){
        var previous = previousPage[pageDepth];
        pageDepth--;
        if (String.IsNullOrEmpty(previous) || pageDepth == 0){
            pageDepth = 0;
            _displayStatus();
        }
        else if (previous == "gcode"){
            _displayGcodeInput();
        }

    }
    private void addPage(string page){
        pageDepth++;
        previousPage[pageDepth] = page;
    }
    public void _displayCredits(){
        resetDisplay();
        TextPageTitle.gameObject.SetActive(true);
        TextPageTitle.text = "Credits";
        imageBackButton.gameObject.SetActive(true);
        textCredits.gameObject.SetActive(true);

    }
    public void _displayConfirmation(String title){
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
    }
    private void reset(){
        isPrinting = false;
        targetHotendTemperature = 0;
        targetBedTemperature = 0;
        fanSpeed = 0;
        isManualProgress = false;
        _displayStatus();
    }
    private void cleanupMesh(){
        trailRenderer.Clear();
        for (int i = 0; i < meshObjectCount; i++){
            if (Utilities.IsValid(meshObjects[i])){
                UnityEngine.Object.Destroy(meshObjects[i]);
            }
        }
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
    private void display(){
        printProgressUpdate();
        textTime.text = TimeStringGen();
        textHotendTargetTemperature.text = Mathf.Floor(targetHotendTemperature) + "°";
        textHotendCurrentTemperature.text = Mathf.Floor(currentHotendTemperature) + "°";
        textBedTargetTemperature.text = Mathf.Floor(targetBedTemperature) + "°";
        textBedCurrentTemperature.text = Mathf.Floor(currentBedTemperature) + "°";
        textFanSpeed.text = Mathf.InverseLerp(0,255,fanSpeed) * 100 + "%";
        textStatus.text = lcdMessage;
        textXPos.text = "X" + Mathf.Lerp(0f, printerSizeInMM.x,currentPosition.x).ToString("F1", CultureInfo.InvariantCulture);
        textYPos.text = "Y" + Mathf.Lerp(0f, printerSizeInMM.y,currentPosition.y).ToString("F1", CultureInfo.InvariantCulture);
        textZPos.text = "Z" + Mathf.Lerp(0f, printerSizeInMM.z,currentPosition.z).ToString("F1", CultureInfo.InvariantCulture);
    }
    private void addVertToTrail(bool isExtrude){
        if (trailRenderer.positionCount < 10000){
            if (isExtrude){
                //Cold Extrusion Prevention
                if (currentHotendTemperature  > 160f){
                    trailRenderer.AddPosition(new Vector3(nozzle.position.x, nozzle.position.y, Mathf.Lerp(transform.TransformPoint(yOffset).z,transform.TransformPoint(maxPosition).z, currentPosition.y)));
                }
                else {
                    isExtrude = false;
                    lcdMessage = "Cold Extrusion Prevented";
                }
            }
            else {
                trailRenderer.AddPosition(new Vector3(nozzle.position.x,nozzle.position.y -20, nozzle.position.z));
            }
        }
        else {
            generateMesh();
            addVertToTrail(isExtrude);
        }
    }
    private void generateMesh(){
            var mesh = new Mesh();
            trailRenderer.BakeMesh(mesh);
            mesh.UploadMeshData(false);
            meshObjects[meshObjectCount] = VRCInstantiate(_emptyMeshFilter).GetComponent<MeshFilter>();
            meshObjects[meshObjectCount].mesh = mesh;
            trailRenderer.Clear();
            meshObjectCount++;
    }
    private void moveTrail(){
        //Y axis scaling incorrect.
        lineMaterial.SetVector("_PositionOffset",new Vector3(YAxis.position.x + trailOffset.x ,trailOffset.y ,YAxis.position.z + trailOffset.z));
    }
    private void move(){
        currentPosition.x =  Mathf.SmoothDamp(currentPosition.x, normalPosition.x, ref velocity.x, 0f, Mathf.Clamp(feedRate,0,500));
        currentPosition.y =  Mathf.SmoothDamp(currentPosition.y, normalPosition.y, ref velocity.z, 0f, Mathf.Clamp(feedRate,0,500));
        currentPosition.z =  Mathf.SmoothDamp(currentPosition.z, normalPosition.z, ref velocity.z, 0f, Mathf.Clamp(feedRate,0,5));

        XAxis.localPosition = new Vector3(Mathf.Lerp(xOffset.x, maxPosition.x, currentPosition.x), xOffset.y, xOffset.z);
        YAxis.localPosition = new Vector3(yOffset.x,Mathf.Lerp(yOffset.y,maxPosition.y, currentPosition.y),yOffset.z);
        ZAxis.localPosition = new Vector3(zOffset.x,zOffset.y,Mathf.Lerp(zOffset.z,maxPosition.z,currentPosition.z));
        if (Mathf.Approximately(currentPosition.x,normalPosition.x) && Mathf.Approximately(currentPosition.y,normalPosition.y) && Mathf.Approximately(currentPosition.z,normalPosition.z)){
            isBusy = false;
            if (extrudeCheck){
                addVertToTrail(extrudeCheck);
            }
        }
        moveTrail();
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
        currentBedTemperature =  Mathf.MoveTowards(currentBedTemperature,ambientTemperature, 0.1f * Time.deltaTime);
        currentHotendTemperature =  Mathf.MoveTowards(currentHotendTemperature,ambientTemperature, 0.5f * Time.deltaTime);
        
        if (targetBedTemperature > ambientTemperature){
            currentBedTemperature = Mathf.MoveTowards(currentBedTemperature,targetBedTemperature,0.5f * Time.deltaTime);
        }
        if (targetHotendTemperature > ambientTemperature){
            currentHotendTemperature = Mathf.MoveTowards(currentHotendTemperature,targetHotendTemperature, 2f * Time.deltaTime);
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
    private void fan(){
        if (fanSpeed > 0){
            printFan.Rotate(Vector3.up, fanSpeed / 8,  Space.Self);
        }
        if (fanAudio.isPlaying){
            if (fanSpeed == 0){
                fanAudio.Stop();
            }
            else {
                fanAudio.volume = (Mathf.InverseLerp(0,1024,fanSpeed));
            }
        }
        else if (fanSpeed > 0) {
            fanAudio.Play();
        }
    }
    private void readFile(TextAsset text){
        gcodeFile = text.text.Split('\n');
        isFileLoaded = true;
    }
    private void parseGcode(string gcode){
        if (string.IsNullOrEmpty(gcode)){
            return;
        }
        if (gcode[0] == ';'){ //ignore comments
            return;
        }
        if (gcode.Contains(";")){         //strip comments from gcode line
            gcode = gcode.Substring(0, gcode.IndexOf(';') - 1);
        }
        var words = gcode.Split(' ');
        if (words.Length == 0){
            return;
        }
        else if(words[0].Length == 0){
            return;
        }
        isBusy = true;
        switch (words[0].Trim()){
            case "G0":
            case "G1":
                G0(words);
                break;
            case "G28":
                G28();
                break;
            case "M73":
                M73(words);
                break;
            case "M104":
                M104(words);
                break;
            case "M106":
                M106(words);
                break;
            case "M107":
                M107();
                break;
            case "M109":
                M109(words);
                break;
            case "M117":
                M117(gcode);
                break;
            case "M118":
                M118(gcode);
                break;
            case "M140":
                M140(words);
                break;
            case "M190":
                M190(words);
                break;
            //ignored gcode;
            case "G29": //bed leveling
            case "M82": //absolue positioning is the only positioning mode
            case "M105": //report temperature to serial
            case "M84": //disable steppers
            case "G90": //absolue positioning
            case "G92": //set position
            case " ": //space
                break;
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
                    normalPosition.x = Mathf.InverseLerp(0, printerSizeInMM.x, Convert.ToSingle(currentGcodeLineSection));
                    break;
                case 'Y':
                    normalPosition.y = Mathf.InverseLerp(0, printerSizeInMM.y,Convert.ToSingle(currentGcodeLineSection));
                    break;
                case 'Z':
                    normalPosition.z = Mathf.InverseLerp(0, printerSizeInMM.z,Convert.ToSingle(currentGcodeLineSection));
                    break;
                case 'F':
                    feedRate = Convert.ToSingle(currentGcodeLineSection,System.Globalization.CultureInfo.InvariantCulture) / feedrateLimiter;
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
        feedRate = 15;
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
                    fanSpeed = Mathf.Clamp(Convert.ToSingle(currentGcodeLineSection,System.Globalization.CultureInfo.InvariantCulture),0f,255f);
                    break;
            }
        }
    }
    private void M107(){
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
}