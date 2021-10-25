using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.IO.Compression;

public class ShaderEditor : ScriptableObject {
    const bool USE_INCLUDE = false;
    const string zipPath = "builtin_shaders-2019.4.28f1.zip";
    static string[] getBuiltinShaderSource(string shaderName, out string shaderPath)
    {
        string [] zipAssets = AssetDatabase.FindAssets("builtin_shaders");
        string path = "";
        foreach (string guid in zipAssets) {
            path = AssetDatabase.GUIDToAssetPath (guid);
            if (path.EndsWith(".zip")) {
                break;
            }
        }
        using (ZipArchive archive = ZipFile.OpenRead( path))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)) {
                    using (StreamReader s = new StreamReader(entry.Open())) {
                        string fileContents = s.ReadToEnd();
                        if (fileContents.IndexOf("Shader \"" + shaderName + "\"") != -1) {
                            shaderPath = Path.GetFileName(entry.FullName);
                            return fileContents.Split(new char[]{'\n'});
                        }
                    }
                }
            }
        }
        shaderPath = "";
        return new string[]{};
    }

    public class ShaderState {
        public string shaderName;
        public string path;
        public string shaderSuffix;
        public string [] shaderData;
        public int beginPropertiesLineNum = -1;
        public int beginPropertiesSkip = -1;
        public int endPropertiesLineNum = -1;
        public int endPropertiesSkip = -1;
        public bool foundCgInclude = false;
        public bool foundNoCgInclude = false;
        public int cgIncludeLineNum = -1;
        public int cgIncludeSkip = -1;
        public int editShaderNameLineNum = -1;
        public int editShaderNameSkip = -1;

    }
    interface IShaderOperation {
        string GetSuffix();
        bool ModifyShaderLines(ShaderState ss);
    }

    public class Waifu2DOperation : IShaderOperation {
        public string GetSuffix() {
            return "_2d";
        }
        public bool ModifyShaderLines(ShaderState ss) {
            if (ss.editShaderNameLineNum == -1) {
                EditorUtility.DisplayDialog ("Waifu2d", "In " + ss.shaderName + ": failed to find Shader \"...\" block.", "OK", "");
                // Failed to parse shader;
                return false;
            }
            if (ss.endPropertiesLineNum == -1) {
                EditorUtility.DisplayDialog ("Waifu2d", "In " + ss.shaderName + ": failed to find end of Properties block.", "OK", "");
                // Failed to parse shader;
                return false;
            }
            if (ss.cgIncludeLineNum == -1) {
                EditorUtility.DisplayDialog ("Waifu2d", "In " + ss.shaderName + ": failed to find CGINCLUDE or appropriate insertion point.", "OK", "");
                // Failed to parse shader;
                return false;
            }

            string [] shader2dassets = AssetDatabase.FindAssets ("Waifu2d.cginc");
            string includePath = "LyumaShader/Waifu2d/Waifu2d.cginc";
            foreach (string guid in shader2dassets) {
                Debug.Log ("testI: " + AssetDatabase.GUIDToAssetPath (guid));
                includePath = AssetDatabase.GUIDToAssetPath (guid);
                if (!includePath.Contains ("Waifu2d.cginc")) {
                    continue;
                }
                if (!includePath.StartsWith ("Assets/", StringComparison.CurrentCulture)) {
                    EditorUtility.DisplayDialog ("Waifu2d", "This script at path " + includePath + " must be in Assets!", "OK", "");
                    return false;
                }
                includePath = includePath.Substring (7);
                break;
            }
            Debug.Log("Including code from " + includePath);
            string cgincCode = File.ReadAllText("Assets/" + includePath);
            int numSlashes = 0;
            if (!ss.path.StartsWith ("Assets/", StringComparison.CurrentCulture)) {
                EditorUtility.DisplayDialog ("Waifu2d", "Shader " + ss.shaderName + " at path " + ss.path + " must be in Assets!", "OK", "");
                return false;
            }
            string includePrefix = "";
            Debug.Log("path is " + ss.path);
            foreach (char c in ss.path.Substring (7)) {
                if (c == '/') {
                    numSlashes++;
                    includePrefix += "../";
                }
            }
            includePath = includePrefix + includePath;
            if (ss.foundCgInclude) {
                string cgIncludeLine = ss.shaderData [ss.cgIncludeLineNum];
                string cgIncludeAdd = "//Waifu2d Generated\n#define LYUMA2D_HOTPATCH\n";
                if (USE_INCLUDE) {
                    cgIncludeAdd += "#include \"" + includePath + "\"\n";
                } else {
                    cgIncludeAdd += cgincCode.Replace("\r\n", "\n");
                }
                ss.shaderData [ss.cgIncludeLineNum] = cgIncludeAdd + cgIncludeLine;
            } else {
                string cgIncludeLine = ss.shaderData [ss.cgIncludeLineNum];
                string cgIncludeAdd = "\nCGINCLUDE\n//Waifu2d Generated Block\n#define LYUMA2D_HOTPATCH\n";
                if (USE_INCLUDE) {
                    cgIncludeAdd += "# include \"" + includePath + "\"\n";
                } else {
                    cgIncludeAdd += cgincCode.Replace("\r\n", "\n");
                }
                cgIncludeAdd += "ENDCG\n";
                ss.shaderData [ss.cgIncludeLineNum] = cgIncludeLine.Substring (0, ss.cgIncludeSkip) + cgIncludeAdd + cgIncludeLine.Substring (ss.cgIncludeSkip);
            }

            string epLine = ss.shaderData [ss.beginPropertiesLineNum];
            string propertiesAdd = "\n" +
                "        // Waifu2d Properties::\n" +
                "        _2d_coef (\"Twodimensionalness\", Range(0, 1)) = 0.99\n" +
                "        _facing_coef (\"Face in Profile\", Range (-1, 1)) = 0.0\n" +
                "        _lock2daxis_coef (\"Lock 2d Axis\", Range (0, 1)) = " + ("1.0") + "\n" +
                "        _zcorrect_coef (\"Squash Z (good=.975; 0=3d; 1=z-fight)\", Float) = " + ("0.975") + "\n";
            epLine = epLine.Substring (0, ss.beginPropertiesSkip) + propertiesAdd + epLine.Substring (ss.beginPropertiesSkip);
            ss.shaderData [ss.beginPropertiesLineNum] = epLine;

            string shaderLine = ss.shaderData [ss.editShaderNameLineNum];
            shaderLine = shaderLine.Substring (0, ss.editShaderNameSkip) + ss.shaderSuffix + shaderLine.Substring (ss.editShaderNameSkip);
            ss.shaderData [ss.editShaderNameLineNum] = shaderLine;
            string prepend = "// AUTOGENERATED by LyumaShader Waifu2DGenerator at " + DateTime.UtcNow.ToString ("s") + "!\n";
            prepend += ("// Original source file: " + ss.path + "\n");
            prepend += ("// This shader will not update automatically. Please regenerate if you change the original.\n");
            prepend += ("// WARNING: this shader uses relative includes. Unity might not recompile if Waifu2d.cginc changes.\n");
            prepend += ("// If editing Waifu2d.cginc, force a recompile by adding a space in here or regenerating.\n");
            ss.shaderData[0] = prepend + ss.shaderData[0];
            for (int i = 0; i < ss.shaderData.Length; i++) {
                if (ss.shaderData [i].IndexOf ("CustomEditor", StringComparison.CurrentCulture) != -1) {
                    ss.shaderData [i] = ("//" + ss.shaderData [i]);
                }
            }
            return true;
        }
    }
    [MenuItem ("CONTEXT/Material/Generate 2d waifu TEST (Lyuma Waifu2d)")]
    static void Waifu2dMaterial (MenuCommand command)
    {
        Material m = command.context as Material;
        Shader newShader = ModifyShader (m.shader, new Waifu2DOperation());
        if (newShader != null) {
            m.shader = newShader;
        }
    }
       
    static Shader ModifyShader (Shader s, IShaderOperation shOp)
    {
        string shaderName = s.name;
        string path = AssetDatabase.GetAssetPath (s);
        Debug.Log ("Starting to work on shader " + shaderName);
        Debug.Log ("Original path: " + path);
        string[] shaderLines;
        if (path.StartsWith ("Resources/unity_builtin_extra", StringComparison.CurrentCulture)) {
            string zipPath = "";
            shaderLines = getBuiltinShaderSource(shaderName, out zipPath);
            string pathToGenerated = "Assets" + "/Generated";
            if (!Directory.Exists (pathToGenerated)) {
                Directory.CreateDirectory (pathToGenerated);
            }
            path = pathToGenerated + "/" + zipPath;
        } else {
            shaderLines = File.ReadAllLines(path);
        }
        return ModifyShaderAtPath (path, shaderName, shaderLines, shOp);
    }

    static Shader ModifyShaderAtPath(string path, string shaderName, string[] shaderLines, IShaderOperation shOp) {
        ShaderState ss = new ShaderState();
        ss.shaderName = shaderName;
        ss.path = path;
        ss.shaderData = shaderLines;
        ss.shaderSuffix = shOp.GetSuffix();
        int state = 0;
        int comment = 0;
        int braceLevel = 0;
        int lineNum = -1;
        bool isOpenQuote = false;
        bool CisOpenQuote = false;
        foreach (string xline in ss.shaderData) {
            string line = xline;
            if (path.IndexOf (ss.shaderSuffix + ".shader", StringComparison.CurrentCulture) != -1 && shaderName.EndsWith (ss.shaderSuffix, StringComparison.CurrentCulture)) {
                String origPath = path.Replace (ss.shaderSuffix + ".shader", ".shader");
                String origShaderName = shaderName.Replace (ss.shaderSuffix, "");
                if (File.Exists(origPath)) {
                    if (EditorUtility.DisplayDialog ("Lyuma ShaderModifier", "Detected an existing shader: Regenrate from " + origShaderName + "?", "Regenerate", "Cancel")) {
                        if (path.Equals(origPath) || shaderName.Equals(origShaderName)) {
                            EditorUtility.DisplayDialog ("Lyuma ShaderModifier", "Unable to find name of original shader for " + shaderName, "OK", "");
                            return null;
                        }
                        Shader origShader = Resources.Load<Shader>(origPath);
                        if (origShader == null) {
                            origShader = Shader.Find(origShaderName);
                        }
                        return ModifyShader(origShader, shOp);
                    } else {
                        return null;
                    }
                }
            }
            lineNum++;
            int lineSkip = 0;
            while (true) {
                //Debug.Log ("Looking for comment " + lineNum);
                int commentIdx;
                if (comment == 1) {
                    commentIdx = line.IndexOf ("*/", lineSkip, StringComparison.CurrentCulture);
                    if (commentIdx != -1) {
                        lineSkip = commentIdx + 2;
                        comment = 0;
                    } else {
                        line = "";
                        break;
                    }
                }
                int openQuote = line.IndexOf ("\"", lineSkip, StringComparison.CurrentCulture);
                if (CisOpenQuote) {
                    if (openQuote == -1) {
                        //Debug.Log("C-Open quote ignore " + lineSkip);
                        break;
                    } else {
                        lineSkip = openQuote + 1;
                        CisOpenQuote = false;
                    }
                    //Debug.Log("C-Open quote end " + lineSkip);
                    continue;
                }
                commentIdx = line.IndexOf ("//", lineSkip, StringComparison.CurrentCulture);
                int commentIdx2 = line.IndexOf ("/*", lineSkip, StringComparison.CurrentCulture);
                if (openQuote != -1 && (openQuote < commentIdx || commentIdx == -1) && (openQuote < commentIdx2 || commentIdx2 == -1)) {
                    CisOpenQuote = true;
                    lineSkip = openQuote + 1;
                    //Debug.Log("C-Open quote start " + lineSkip);
                    continue;
                }
                if (commentIdx != -1) {
                    line = line.Substring (0, commentIdx);
                    break;
                }
                commentIdx = commentIdx2;
                if (commentIdx != -1) {
                    int endCommentIdx = line.IndexOf ("*/", lineSkip, StringComparison.CurrentCulture);
                    if (endCommentIdx != -1) {
                        line = line.Substring (0, commentIdx) + new String (' ', (endCommentIdx + 2 - commentIdx)) + line.Substring (endCommentIdx + 2);
                        lineSkip = endCommentIdx + 2;
                    } else {
                        line = line.Substring (0, commentIdx);
                        comment = 1;
                        break;
                    }
                } else {
                    break;
                }
            }
            lineSkip = 0;
            bool fallThrough = true;
            while (fallThrough) {
                //Debug.Log ("Looking for state " + state + " on line " + lineNum);
                fallThrough = false;
                switch (state) {
                case 0: {
                        int shaderOff = line.IndexOf ("Shader", lineSkip, StringComparison.CurrentCulture);
                        if (shaderOff != -1) {
                            int firstQuote = line.IndexOf ('\"', shaderOff);
                            int secondQuote = line.IndexOf ('\"', firstQuote + 1);
                            if (firstQuote != -1 && secondQuote != -1) {
                                ss.editShaderNameLineNum = lineNum;
                                ss.editShaderNameSkip = secondQuote;
                                state = 1;
                            }
                        }
                    }
                    break;
                case 1: {
                        // Find beginning of Properties block
                        int shaderOff = line.IndexOf ("Properties", lineSkip, StringComparison.CurrentCulture);
                        if (shaderOff != -1) {
                            state = 2;
                            lineSkip = shaderOff;
                            fallThrough = true;
                        }
                    }
                    break;
                case 2: {
                        // Find end of Properties block
                        while (lineSkip < line.Length) {
                            int openQuote = line.IndexOf ("\"", lineSkip, StringComparison.CurrentCulture);
                            if (isOpenQuote) {
                                if (openQuote == -1) {
                                    //Debug.Log("Open quote ignore " + lineSkip);
                                    break;
                                } else {
                                    lineSkip = openQuote + 1;
                                    isOpenQuote = false;
                                }
                                //Debug.Log("Open quote end " + lineSkip);
                                continue;
                            }
                            int openBrace = line.IndexOf ("{", lineSkip, StringComparison.CurrentCulture);
                            int closeBrace = line.IndexOf ("}", lineSkip, StringComparison.CurrentCulture);
                            if (openQuote != -1 && (openQuote < openBrace || openBrace == -1) && (openQuote < closeBrace || closeBrace == -1)) {
                                isOpenQuote = true;
                                lineSkip = openQuote + 1;
                                //Debug.Log("Open quote start " + lineSkip);
                                continue;
                            }
                            //Debug.Log ("Looking for braces state " + state + " on line " + lineNum + "/" + lineSkip + " {}" + braceLevel + " open:" + openBrace + "/ close:" + closeBrace + "/ quote:" + openQuote);
                            if (closeBrace != -1 && (openBrace > closeBrace || openBrace == -1)) {
                                braceLevel--;
                                if (braceLevel == 0) {
                                    ss.endPropertiesLineNum = lineNum;
                                    ss.endPropertiesSkip = closeBrace;
                                    state = 3;
                                    fallThrough = true;
                                }
                                lineSkip = closeBrace + 1;
                            } else if (openBrace != -1 && (openBrace < closeBrace || closeBrace == -1)) {
                                if (braceLevel == 0) {
                                    ss.beginPropertiesLineNum = lineNum;
                                    ss.beginPropertiesSkip = openBrace + 1;
                                }
                                braceLevel++;
                                lineSkip = openBrace + 1;
                            } else {
                                break;
                            }
                        }
                    }
                    break;
                case 3: {
                        // Find beginning of CGINCLUDE block, or beginning of a Pass or CGPROGRAM
                        int cgInclude = line.IndexOf ("CGINCLUDE", lineSkip, StringComparison.CurrentCulture);
                        int cgProgram = line.IndexOf ("CGPROGRAM", lineSkip, StringComparison.CurrentCulture);
                        int passBlock = line.IndexOf ("GrabPass", lineSkip, StringComparison.CurrentCulture);
                        int grabPassBlock = line.IndexOf ("Pass", lineSkip, StringComparison.CurrentCulture);
                        if (cgInclude != -1) {
                            ss.foundCgInclude = true;
                        } else if (cgProgram != -1) {
                            ss.foundNoCgInclude = true;
                        } else if (grabPassBlock != -1) {
                            ss.foundNoCgInclude = true;
                        } else if (passBlock != -1) {
                            if (passBlock == lineSkip || char.IsWhiteSpace (line [passBlock - 1])) {
                                if (passBlock + 4 == line.Length || char.IsWhiteSpace (line [passBlock + 4])) {
                                    ss.foundNoCgInclude = true;
                                }
                            }
                        }
                        if (ss.foundCgInclude) {
                            state = 4;
                            ss.cgIncludeLineNum = lineNum + 1;
                            ss.cgIncludeSkip = 0;
                        } else if (ss.foundNoCgInclude) {
                            state = 4;
                            ss.cgIncludeLineNum = lineNum;
                            ss.cgIncludeSkip = lineSkip;
                        }
                    }
                    break;
                case 4:
                    // Look for modified tag, or end of shader, or custom editor.
                    break;
                }
            }
            if (state == 5) {
                break;
            }
        }
        Debug.Log ("Done with hard work");
        if (!shOp.ModifyShaderLines(ss)) {
            return null;
        }

        String dest = ss.path.Replace (".shader", ss.shaderSuffix + ".txt");
        String finalDest = ss.path.Replace (".shader", ss.shaderSuffix + ".shader");
        if (dest.Equals (ss.path)) {
            EditorUtility.DisplayDialog ("Lyuma ShaderModifier", "Shader " + ss.shaderName + " at path " + ss.path + " does not have .shader!", "OK", "");
            return null;
        }
        Debug.Log ("Writing shader " + dest);
        Debug.Log ("Shader name" + ss.shaderName + ss.shaderSuffix);
        Debug.Log ("Original path " + ss.path + " name " + ss.shaderName);
        StreamWriter writer = new StreamWriter (dest, false);
        writer.NewLine = "\n";

        for (int i = 0; i < ss.shaderData.Length; i++) {
            writer.WriteLine (ss.shaderData [i]);
        }
        writer.Close ();
        FileUtil.ReplaceFile (dest, finalDest);
        try {
            FileUtil.DeleteFileOrDirectory (dest);
        } catch (Exception e) {
        }
        //FileUtil.MoveFileOrDirectory (dest, finalDest);
        AssetDatabase.ImportAsset (finalDest);
        return (Shader)AssetDatabase.LoadAssetAtPath (finalDest, typeof (Shader));
    }
}