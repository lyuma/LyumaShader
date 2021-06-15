using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace LyumaShader {
    public class Waifu2dGenerator : ScriptableObject {
        // Use a #include instead of pasting in code.
        const bool USE_INCLUDE = false;

        //[MenuItem ("Tools/Lyuma Waifu2d")]
        //MenuItem("GameObject/Create Mesh")
        [MenuItem ("CONTEXT/Material/Make 2d (Lyuma Waifu2d)")]
        static void Waifu2dMaterial (MenuCommand command)
        {
            Material m = command.context as Material;
            Shader newShader = Waifu2d (m.shader, false);
            if (newShader != null) {
                Undo.RecordObject(m, "Waifu2d: Switch Shader to 2d");
                m.shader = newShader;
            }
        }

        [MenuItem ("CONTEXT/Shader/Generate 2d waifu (Lyuma Waifu2d)")]
        static void Waifu2dShader (MenuCommand command)
        {
            Shader s = command.context as Shader;
            Shader newS = Waifu2d (s, false);
            EditorGUIUtility.PingObject (newS);
        }

        [MenuItem ("CONTEXT/Material/Revert to 3d (Lyuma Waifu2d)")]
        static void WaifuRevert2dMaterial (MenuCommand command)
        {
            Material m = command.context as Material;
            string path = AssetDatabase.GetAssetPath (m.shader);
            if (path.StartsWith ("Resources/unity_builtin_extra", StringComparison.CurrentCulture)) {
                return;
            }
            string shaderName = m.shader.name;
            string [] shaderData = File.ReadAllLines (path);
            string origPath = "";
            foreach (string xline in shaderData) {
                string line = xline;
                if (line.IndexOf("Original source file:") != -1 && origPath.Length == 0) {
                    origPath = line.Substring(line.IndexOf(":") + 1).Trim();
                }
                if (line.IndexOf ("Waifu2d Generated", StringComparison.CurrentCulture) != -1) {
                    if (origPath.Length == 0) {
                        origPath = path.Replace ("_vr2d.shader", ".shader").Replace ("_2d.shader", ".shader");
                    }
                    String origShaderName = shaderName.Replace ("_vr2d", "").Replace("_2d", "");
                    Shader origShader = AssetDatabase.LoadAssetAtPath<Shader>(origPath);
                    if (origShader == null) {
                        origShader = Shader.Find(origShaderName);
                    }
                    if (origShader == null) {
                        EditorUtility.DisplayDialog ("Waifu2d", "Cannot find the original shader " + origShaderName + " at " + origPath, "OK", "");
                    } else {
                        Undo.RecordObject(m, "Waifu2d: Revert Shader to 3d");
                        Debug.Log("original shader " + origShaderName + " at " + origPath, origShader);
                        m.shader = origShader;
                    }
                    break;
                }
            }
        }

        static Shader Waifu2d (Shader s, bool vr2d)
        {
            string shaderName = s.name;
            string path = AssetDatabase.GetAssetPath (s);
            Debug.Log ("Starting to work on shader " + shaderName);
            Debug.Log ("Original path: " + path);
            if (path.StartsWith ("Resources/unity_builtin_extra", StringComparison.CurrentCulture) && "Standard".Equals (s.name)) {
                string [] tmpassets = AssetDatabase.FindAssets ("StandardSimple");
                foreach (string guid in tmpassets) {
                    path = AssetDatabase.GUIDToAssetPath (guid);
                    if (path.IndexOf (".shader", StringComparison.CurrentCulture) != -1) {
                        break;
                    }
                }
            }
            return Waifu2dPath (path, shaderName, vr2d, vr2d ? "_vr2d" : "_2d");
        }

        static Shader Waifu2dPath(string path, string shaderName, bool vr2d, string shaderSuffix) {
            string [] shaderData = File.ReadAllLines (path);
            int state = 0;
            int comment = 0;
            int braceLevel = 0;
            int lineNum = -1;
            int beginPropertiesLineNum = -1;
            int beginPropertiesSkip = -1;
            int endPropertiesLineNum = -1;
            int endPropertiesSkip = -1;
            bool foundCgInclude = false;
            bool foundNoCgInclude = false;
            int cgIncludeLineNum = -1;
            int cgIncludeSkip = -1;
            int editShaderNameLineNum = -1;
            int editShaderNameSkip = -1;
            bool isOpenQuote = false;
            bool CisOpenQuote = false;
            foreach (string xline in shaderData) {
                string line = xline;
                if (line.IndexOf ("Waifu2d Generated", StringComparison.CurrentCulture) != -1) {
                    String origPath = path.Replace ("_vr2d.shader", ".shader").Replace ("_2d.shader", ".shader");
                    String origShaderName = shaderName.Replace ("_vr2d", "").Replace("_2d", "");
                    if (EditorUtility.DisplayDialog ("Waifu2d", "Detected an existing Waifu2d comment: Regenrate from " + origShaderName + "?", "Regenerate", "Cancel")) {
                        if (path.Equals(origPath) || shaderName.Equals(origShaderName)) {
                            EditorUtility.DisplayDialog ("Waifu2d", "Unable to find name of original shader for " + shaderName, "OK", "");
                            return null;
                        }
                        return Waifu2dPath (origPath, origShaderName, vr2d, shaderSuffix);
                    } else {
                        return null;
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
                                    editShaderNameLineNum = lineNum;
                                    editShaderNameSkip = secondQuote;
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
                                        endPropertiesLineNum = lineNum;
                                        endPropertiesSkip = closeBrace;
                                        state = 3;
                                        fallThrough = true;
                                    }
                                    lineSkip = closeBrace + 1;
                                } else if (openBrace != -1 && (openBrace < closeBrace || closeBrace == -1)) {
                                    if (braceLevel == 0) {
                                        beginPropertiesLineNum = lineNum;
                                        beginPropertiesSkip = openBrace + 1;
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
                                foundCgInclude = true;
                            } else if (cgProgram != -1) {
                                foundNoCgInclude = true;
                            } else if (grabPassBlock != -1) {
                                foundNoCgInclude = true;
                            } else if (passBlock != -1) {
                                if (passBlock == lineSkip || char.IsWhiteSpace (line [passBlock - 1])) {
                                    if (passBlock + 4 == line.Length || char.IsWhiteSpace (line [passBlock + 4])) {
                                        foundNoCgInclude = true;
                                    }
                                }
                            }
                            if (foundCgInclude) {
                                state = 4;
                                cgIncludeLineNum = lineNum + 1;
                                cgIncludeSkip = 0;
                            } else if (foundNoCgInclude) {
                                state = 4;
                                cgIncludeLineNum = lineNum;
                                cgIncludeSkip = lineSkip;
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
            if (editShaderNameLineNum == -1) {
                EditorUtility.DisplayDialog ("Waifu2d", "In " + shaderName + ": failed to find Shader \"...\" block.", "OK", "");
                // Failed to parse shader;
                return null;
            }
            if (endPropertiesLineNum == -1) {
                EditorUtility.DisplayDialog ("Waifu2d", "In " + shaderName + ": failed to find end of Properties block.", "OK", "");
                // Failed to parse shader;
                return null;
            }
            if (cgIncludeLineNum == -1) {
                EditorUtility.DisplayDialog ("Waifu2d", "In " + shaderName + ": failed to find CGINCLUDE or appropriate insertion point.", "OK", "");
                // Failed to parse shader;
                return null;
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
                    return null;
                }
                includePath = includePath.Substring (7);
                break;
            }
            Debug.Log("Including code from " + includePath);
            string cgincCode = File.ReadAllText("Assets/" + includePath);
            int numSlashes = 0;
            if (!path.StartsWith ("Assets/", StringComparison.CurrentCulture)) {
                EditorUtility.DisplayDialog ("Waifu2d", "Shader " + shaderName + " at path " + path + " must be in Assets!", "OK", "");
                return null;
            }
            string includePrefix = "";
            Debug.Log("path is " + path);
            foreach (char c in path.Substring (7)) {
                if (c == '/') {
                    numSlashes++;
                    includePrefix += "../";
                }
            }
            includePath = includePrefix + includePath;
            if (foundCgInclude) {
                string cgIncludeLine = shaderData [cgIncludeLineNum];
                string cgIncludeAdd = "//Waifu2d Generated\n#define LYUMA2D_HOTPATCH\n";
                if (vr2d) {
                    cgIncludeAdd += "#define VR_ONLY_2D 1\n";
                }
                if (USE_INCLUDE) {
                    cgIncludeAdd += "#include \"" + includePath + "\"\n";
                } else {
                    cgIncludeAdd += cgincCode.Replace("\r\n", "\n");
                }
                shaderData [cgIncludeLineNum] = cgIncludeAdd + cgIncludeLine;
            } else {
                string cgIncludeLine = shaderData [cgIncludeLineNum];
                string cgIncludeAdd = "\nCGINCLUDE\n//Waifu2d Generated Block\n#define LYUMA2D_HOTPATCH\n";
                if (vr2d) {
                    cgIncludeAdd += "#define VR_ONLY_2D 1\n";
                }
                if (USE_INCLUDE) {
                    cgIncludeAdd += "# include \"" + includePath + "\"\n";
                } else {
                    cgIncludeAdd += cgincCode.Replace("\r\n", "\n");
                }
                cgIncludeAdd += "ENDCG\n";
                shaderData [cgIncludeLineNum] = cgIncludeLine.Substring (0, cgIncludeSkip) + cgIncludeAdd + cgIncludeLine.Substring (cgIncludeSkip);
            }

            string epLine = shaderData [beginPropertiesLineNum];
            string propertiesAdd = "\n" +
                "        // Waifu2d Properties::\n" +
                "        _2d_coef (\"Twodimensionalness\", Range(0, 1)) = 0.99\n" +
                "        _facing_coef (\"Face in Profile\", Range (-1, 1)) = 0.0\n" +
                "        _lock2daxis_coef (\"Lock 2d Axis\", Range (0, 1)) = " + (vr2d ? "0.0" : "1.0") + "\n" +
                "        _zcorrect_coef (\"Squash Z (good=.975; 0=3d; 1=z-fight)\", Float) = " + (vr2d ? "0.0" : "0.975") + "\n";
            epLine = epLine.Substring (0, beginPropertiesSkip) + propertiesAdd + epLine.Substring (beginPropertiesSkip);
            shaderData [beginPropertiesLineNum] = epLine;

            string shaderLine = shaderData [editShaderNameLineNum];
            shaderLine = shaderLine.Substring (0, editShaderNameSkip) + shaderSuffix + shaderLine.Substring (editShaderNameSkip);
            shaderData [editShaderNameLineNum] = shaderLine;

            String dest = path.Replace (".shader", shaderSuffix + ".txt");
            String finalDest = path.Replace (".shader", shaderSuffix + ".shader");
            if (dest.Equals (path)) {
                EditorUtility.DisplayDialog ("Waifu2d", "Shader " + shaderName + " at path " + path + " does not have .shader!", "OK", "");
                return null;
            }
            Debug.Log ("Writing shader " + dest);
            Debug.Log ("Shader name" + shaderName + shaderSuffix);
            Debug.Log ("Original path " + path + " name " + shaderName);
            StreamWriter writer = new StreamWriter (dest, false);
            writer.NewLine = "\n";
            writer.WriteLine ("// AUTOGENERATED by LyumaShader Waifu2DGenerator at " + DateTime.UtcNow.ToString ("s") + "!");
            writer.WriteLine ("// Original source file: " + path);
            writer.WriteLine ("// This shader will not update automatically. Please regenerate if you change the original.");
            writer.WriteLine ("// WARNING: this shader uses relative includes. Unity might not recompile if Waifu2d.cginc changes.");
            writer.WriteLine ("// If editing Waifu2d.cginc, force a recompile by adding a space in here or regenerating.");
            for (int i = 0; i < shaderData.Length; i++) {
                if (shaderData [i].IndexOf ("CustomEditor", StringComparison.CurrentCulture) != -1) {
                    writer.WriteLine ("//" + shaderData [i]);
                } else {
                    writer.WriteLine (shaderData [i]);
                }
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
}
