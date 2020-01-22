using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class RenderToTexture : ScriptableObject {
    [MenuItem ("CONTEXT/Camera/Render To Texture (ARGBHalf)")]
    static void RenderHALF (MenuCommand command)
    {
        RenderTex (command, 1);
    }
    [MenuItem ("CONTEXT/Camera/Render To Texture (ARGB32)")]
    static void Render32 (MenuCommand command)
    {
        RenderTex (command, 0);
    }
    [MenuItem ("CONTEXT/Camera/Render To Texture (R8)")]
    static void Render8 (MenuCommand command)
    {
        RenderTex (command, 2);
    }
    [MenuItem ("CONTEXT/Camera/Render To Texture (RGB565)")]
    static void Render565 (MenuCommand command)
    {
        RenderTex (command, 3);
    }
    static void RenderTex (MenuCommand command, int type)
    {
        Camera camera = command.context as Camera;
        RenderTexture oldTargTex = camera.targetTexture;
        RenderTexture tex = null;
        RenderTexture oldActive = RenderTexture.active;
        Texture2D outTexture = null;
        try {
            //tex = new RenderTexture (type == 1 ? (int)128 : (int)4096, type == 1 ? (int)256 : (int)1024, 0,
            //                         type == 1 ? RenderTextureFormat.ARGBHalf : (type == 2 ? RenderTextureFormat.ARGB32 : (type == 3 ? RenderTextureFormat.ARGB32 : RenderTextureFormat.ARGB32)));
            //camera.targetTexture = tex;
            tex = camera.targetTexture;
            camera.Render ();
            outTexture = new Texture2D (tex.width, (tex.height + 15) / 16 * 16,
                                        type == 1 ? TextureFormat.RGBAHalf : (type == 2 ? TextureFormat.Alpha8 : (type == 3 ? TextureFormat.RGB24 : TextureFormat.RGBA32)), false, true);
            oldActive = RenderTexture.active;
            RenderTexture.active = tex;
            if (type == 2) {
                Texture2D tmpTexture = new Texture2D (tex.width, (tex.height + 15) / 16 * 16, TextureFormat.RGBA32, false, true);
                tmpTexture.ReadPixels (new Rect (0, 0, tex.width, tex.height), 0, 0);
                outTexture.SetPixels (tmpTexture.GetPixels());
            } else {
                outTexture.ReadPixels (new Rect (0, 0, tex.width, tex.height), 0, 0);
            }
            outTexture.Apply ();
        } finally {
            RenderTexture.active = oldActive;
            if (tex != null) {
                //tex.Release ();
            }
            camera.targetTexture = oldTargTex;
        }
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        string outPath = pathToGenerated + "/ZZgentex_" + DateTime.UtcNow.ToString ("s").Replace (':', '_');
        if (type != 0 && type != 3) {
            AssetDatabase.CreateAsset (outTexture, outPath + ".asset");
            EditorGUIUtility.PingObject (outTexture);
        } else {
            File.WriteAllBytes (outPath + ".png", outTexture.EncodeToPNG ());
            DestroyImmediate (outTexture);
            AssetDatabase.Refresh ();
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath (outPath + ".png");
            importer.sRGBTexture = false;
            AssetDatabase.WriteImportSettingsIfDirty (outPath + ".png");
            AssetDatabase.ImportAsset (outPath + ".png", ImportAssetOptions.ForceUpdate);
            EditorGUIUtility.PingObject (AssetDatabase.LoadAssetAtPath (outPath + ".png", typeof (Texture2D)));
        }
    }
}
