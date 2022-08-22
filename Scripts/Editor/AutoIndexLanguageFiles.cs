using System;
using System.IO;
using System.Reflection;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;


namespace Codel1417.Editor
{
    [InitializeOnLoad]
    public class AutoIndexLanguageFiles : IVRCSDKBuildRequestedCallback 
    {
        public int callbackOrder { get; }
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            IndexLanguageFiles();
            return true;
        }
        static AutoIndexLanguageFiles()
        {
            EditorApplication.playModeStateChanged += IndexLanguageFiles;
        }
        public static void IndexLanguageFiles(PlayModeStateChange playModeStateChange )
        {
            if (playModeStateChange != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }
            IndexLanguageFiles();
        }
        public static void IndexLanguageFiles()
        {
            // get reference to Ender3 Display object
            DisplayManager[] displayObject = Object.FindObjectsOfType<DisplayManager>();
            if (displayObject.Length == 0)
            {
                return;
            }
            //make new arrays for the language files
            foreach (DisplayManager display in displayObject)
            {
                display.languageFiles = Array.Empty<TextAsset>();
            }
            MonoScript scriptPath = MonoScript.FromMonoBehaviour(displayObject[0]);
            string scriptPathString = AssetDatabase.GetAssetPath(scriptPath);
            if (scriptPathString == null)
            {
                return;
            }
            DirectoryInfo scriptDirectory = Directory.GetParent(scriptPathString);
            if (scriptDirectory == null)
            {
                return;
            }
            DirectoryInfo projectDirectory = scriptDirectory.Parent;
            if (projectDirectory == null)
            {
                return;
            }
            DirectoryInfo languageDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "Language"));
            
            FileInfo[] languageFiles = languageDirectory.GetFiles("*");
            foreach (FileInfo languageFile in languageFiles)
            {
                if (languageFile.Exists && !languageFile.Name.EndsWith(".meta"))
                {
                    foreach (DisplayManager display in displayObject)
                    {
                        //append to array
                        TextAsset[] textAssets = display.languageFiles;
                        Array.Resize(ref textAssets, textAssets.Length + 1);
                        Undo.RecordObject(display, "Added Language File");
                        string textAssetPath =
                            $"Assets{languageFile.FullName.Substring(Application.dataPath.Length).Replace("\\", "/")}";
                        TextAsset newTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(textAssetPath);
                        textAssets[textAssets.Length - 1] = newTextAsset;
                        display.languageFiles = textAssets;
                    }
                }
            }
            // Init the language files
            foreach (DisplayManager display in displayObject)
            {
                Undo.RecordObject(display, "Init Language");
                display._InitLanguage();
            }
        }
    }
}