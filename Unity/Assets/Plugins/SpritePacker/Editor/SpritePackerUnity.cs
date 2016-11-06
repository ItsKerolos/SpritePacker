//-----------------------------------------------------------------------
//    SpritePackerUnity.cs: SpritePacker Unity
//-----------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SpritePackerUnity : EditorWindow
{
    #if UNITY_EDITOR_WIN
    [MenuItem("Window/SpritePacker")]
    private static void Init()
    {
        GetWindow(typeof(SpritePackerUnity), true, "SpritePacker", true);
    }
    #endif

    public string[] Scales = new string[]
    {
         "0.25",
         "0.5",
         "1",
    };

    int scaleIndex = 2;

    void OnGUI()
    {
        scaleIndex = EditorGUILayout.Popup("Scale", scaleIndex, Scales);

        if (GUILayout.Button("Export"))
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, "");

            if (string.IsNullOrEmpty(folderPath))
                return;

            string savePath = EditorUtility.SaveFilePanelInProject("Save Sprite", System.IO.Path.GetFileName(folderPath), "png", "");

            if (string.IsNullOrEmpty(savePath))
                return;

            Export(folderPath, savePath, float.Parse(Scales[scaleIndex]));
        }
    }

    public static void Export(string folderPath, string savePath, float scale)
    {
        if(!System.IO.File.Exists(Application.dataPath + "/Plugins/SpritePacker/Editor/SpritePacker.exe"))
        {
            Debug.LogError("(SpritePacker) SpritePacker.exe not found");
            return;
        }

        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = Application.dataPath + "/Plugins/SpritePacker/Editor/SpritePacker.exe";
        startInfo.Arguments = "-\"" + folderPath + "\" -\"" + Application.dataPath.Replace("Assets", "") + savePath + "\" -" + scale;
        startInfo.RedirectStandardOutput = true;
        startInfo.UseShellExecute = false;

        System.Diagnostics.Process process = new System.Diagnostics.Process();
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();

        string output = process.StandardOutput.ReadToEnd();

        if (output.StartsWith("Error: "))
        {
            if (System.IO.File.Exists(savePath))
                AssetDatabase.DeleteAsset(savePath);

            EditorUtility.DisplayDialog("SpritePacker", output, "Ok");
            AssetDatabase.Refresh();
            return;
        }

        AssetDatabase.Refresh();

        string[] spriteInfo = output.Split(new string[] { "&&" }, System.StringSplitOptions.RemoveEmptyEntries);
        var spriteSheet = (TextureImporter)AssetImporter.GetAtPath(savePath);

        if (spriteSheet == null)
        {
            EditorUtility.DisplayDialog("SpritePacker", "Error: SpriteSheet is null?!", "Ok");
            return;
        }

        int currentSize = int.Parse(spriteInfo[0]);
        int columnCount = int.Parse(spriteInfo[1]);

        spriteSheet.maxTextureSize = currentSize;
        spriteSheet.textureFormat = TextureImporterFormat.AutomaticTruecolor;
        spriteSheet.spriteImportMode = SpriteImportMode.Multiple;

        int itemIndex = 0;
        List<int> highestYs = new List<int>();
        int highestY = 0;

        List<SpriteMetaData> images = new List<SpriteMetaData>();

        for (int i = 2; i < spriteInfo.Length; i++)
        {
            string[] dataArray = spriteInfo[i].Split(';');
            SpriteInfo info = new SpriteInfo(int.Parse(dataArray[1].Split(',')[0]), int.Parse(dataArray[1].Split(',')[1]));
            
            if (itemIndex < columnCount)
            {
                itemIndex += 1;
                if (highestY < info.height)
                    highestY = info.height;
            }
            else
            {
                itemIndex = 1;
                highestYs.Add(highestY);
                highestY = info.height;
            }
        }

        itemIndex = 0;
        highestYs.Add(highestY);
        highestY = 0;

        int x = 0;
        int y = currentSize;
        int lastX = 0;
        int columnIndex = 0;
        int padding = 2;

        for (int i = 2; i < spriteInfo.Length; i++)
        {
            string[] dataArray = spriteInfo[i].Split(';');
            SpriteMetaData data = new SpriteMetaData();
            data.name = dataArray[0];
            SpriteInfo info = new SpriteInfo(int.Parse(dataArray[1].Split(',')[0]), int.Parse(dataArray[1].Split(',')[1]));

            if (itemIndex < columnCount)
            {
                itemIndex += 1;

                if (itemIndex == 1)
                {
                    x = padding;
                    y -= highestYs[columnIndex] + padding;
                }
                else
                {
                    x += lastX + padding;
                }
            }
            else
            {
                itemIndex = 1;
                columnIndex += 1;
                lastX = 0;

                x = padding;
                y -= highestYs[columnIndex] + padding;
            }

            int diff = highestYs[columnIndex] - info.height;
            data.rect = new Rect(x, y + diff, info.width, info.height);
            lastX = info.width;

            images.Add(data);
        }

        spriteSheet.spritesheet = images.ToArray();
        EditorUtility.SetDirty(spriteSheet);
        spriteSheet.SaveAndReimport();
    }
}

public class SpriteInfo
{
    public int width;
    public int height;

    public SpriteInfo(int width, int height)
    {
        this.width = width;
        this.height = height;
    }
}
