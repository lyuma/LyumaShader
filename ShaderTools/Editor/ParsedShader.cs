//#define DEBUG_PARSER 1

using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

public class ParsedShader {
    public Shader shader;
    public string filePath;
    public string surfaceTempFilePath;
    public string [] shaderLines;
    public string shaderName;

    const string zipPath = "builtin_shaders-2019.4.28f1.zip";
    ZipArchive zipArchive = openBuiltinShaderZip();
    static ZipArchive openBuiltinShaderZip() {
        string [] zipAssets = AssetDatabase.FindAssets("builtin_shaders");
        string path = "";
        foreach (string guid in zipAssets) {
            path = AssetDatabase.GUIDToAssetPath (guid);
            if (path.EndsWith(".zip")) {
                break;
            }
        }
        return ZipFile.OpenRead( path);
    }
    public static string[] getBuiltinShaderSource(ZipArchive zipArchive, string shaderName, out string shaderPath)
    {
        foreach (ZipArchiveEntry entry in zipArchive.Entries)
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
        shaderPath = "";
        return new string[]{};
    }
    public string[] getBuiltinShaderSource(string shaderName, out string shaderPath) {
        return getBuiltinShaderSource(zipArchive, shaderName, out shaderPath);
    }

    public static string getZipCgincSource(ZipArchive zipArchive, string cgincPath) {
        foreach (ZipArchiveEntry entry in zipArchive.Entries) {
            if (entry.FullName.Equals("CGIncludes/" + cgincPath, StringComparison.OrdinalIgnoreCase)) {
                using (StreamReader s = new StreamReader(entry.Open())) {
                    string fileContents = s.ReadToEnd();
                    return fileContents;
                }
            }
        }
        return "";
    }
    public string getCgincSource(string fileContext, ref string fileName) {
        if (fileContext.Contains('/')) {
            string fsPath = Path.Combine(Path.GetDirectoryName(fileContext), fileName);
            if (File.Exists(fsPath)) {
                fileName = fsPath;
                return File.ReadAllText(fsPath);
            }
        }
        string ret = getZipCgincSource(zipArchive, fileName);
        if (ret.Length == 0) {
            Debug.LogError("Failed to find include " + fileName + " from " + fileContext);
        }
        return ret;
    }


    public class Block {
        public ParsedShader shader;
        public enum Type {
            None = -1,
            ShaderName = 0,
            Properties = 1,
            CGInclude = 2,
            CGProgram = 3,
            GrabPass = 4,
            Pass = 5
        }
        public Type type;
        public int beginLineNum = -1;
        public int beginSkip = -1;
        public int endLineNum = -1;
        public int endSkip = -1;

        public Block (ParsedShader shader, Type type, int beginLine, int beginSkip, int endLine, int endSkip) {
            this.shader = shader;
            this.type = type;
            this.beginLineNum = beginLine;
            this.beginSkip = beginSkip;
            this.endLineNum = endLine;
            this.endSkip = endSkip;
        }
    }

    public class CGBlock : Block {

        class CGProgramOutputCollector : tinycpp.Preproc.OutputInterface {
            StringBuilder outputCode = new StringBuilder();
            bool wasNewline;
            ParsedShader parsedShader;
            public CGProgramOutputCollector(ParsedShader ps) {
                parsedShader = ps;
            }
            public void Emit(string s, string file, int line, int column) {
                if (wasNewline && s.Trim() == "" && s.EndsWith("\n")) {
                    return;
                }
                if (s.Trim() != "" || s.EndsWith("\n")) {
                    wasNewline = (s.EndsWith("\n"));
                }
                outputCode.Append(s);
            }
            public void EmitError(string msg) {
                Debug.LogError(msg);
            }
            public void EmitWarning(string msg) {
                Debug.LogWarning(msg);
            }
            public string IncludeFile(string fileContext, ref string filename) {
                Debug.Log("Found a pound include " + fileContext + "," + filename);
                return parsedShader.getCgincSource(fileContext, ref filename);
            }

            public string GetOutputCode() {
                return outputCode.ToString();
            }
        }
        public string vertFunction;
        public int vertFunctionPragmaLine = -1;
        public string geomFunction;
        public int geomFunctionPragmaLine = -1;
        public string fragFunction;
        public string domainFunction;
        public string hullFunction;
        public string surfFunction;
        public string surfVertFunction;
        public string vertReturnType;
        public string vertInputType;
        public bool originalSurfaceShader;
        public int shaderTarget;
        public Dictionary<int, string> pragmas = new Dictionary<int, string>();
        public CGBlock (ParsedShader shader, Type type, int beginLine, int endLine, string cgProgramSource) : base(shader, type, beginLine, 0, endLine, 0)
        {
            if (type != Type.CGProgram) {
                return;
            }
            //for (int i = beginLine; i < endLine; i++) {
            Regex re = new Regex ("^\\s*(vertex|fragment|geometry|surface|domain|hull|target)\\s*(\\S+)\\s*.*(\\bvertex:(\\S+))?\\s*.*$");
            foreach (var pragmaLine in new PragmaIterator(shader.shaderLines.Skip(beginLine).Take(endLine - beginLine), beginLine) ) {
                Match m = re.Match (pragmaLine.Key);
                #if DEBUG_PARSER
                    Debug.Log ("Found #pragma " + pragmaLine.Key + ": match " + m.Groups [1].Value + "," + m.Groups [2].Value + "," + m.Groups [3].Value + "," + m.Groups [4].Value);
                #endif
                if (m != null && m.Success) {
                    string funcType = m.Groups [1].Value;
                    if (funcType.Equals ("surface")) {
                        surfFunction = m.Groups [2].Value;
                        if (m.Groups[4] != null) {
                            surfVertFunction = m.Groups [4].Value;
                        }
                        originalSurfaceShader = true;
                    } else if (funcType.Equals("vertex")) {
                        vertFunction = m.Groups [2].Value;
                        vertFunctionPragmaLine = pragmaLine.Value;
                    } else if (funcType.Equals ("fragment")) {
                        fragFunction = m.Groups [2].Value;
                    } else if (funcType.Equals ("geometry")) {
                        geomFunction = m.Groups [2].Value;
                        geomFunctionPragmaLine = pragmaLine.Value;
                        if (shaderTarget <= 0) {
                            shaderTarget = -40;
                        }
                    } else if (funcType.Equals ("domain")) {
                        domainFunction = m.Groups [2].Value;
                        if (shaderTarget <= 0) {
                            shaderTarget = -50;
                        }
                    } else if (funcType.Equals ("hull")) {
                        hullFunction = m.Groups [2].Value;
                        if (shaderTarget <= 0) {
                            shaderTarget = -50;
                        }
                    } else if (funcType.Equals ("target")) {
                        shaderTarget = Mathf.RoundToInt(float.Parse(m.Groups [2].Value) * 10.0f);
                    }
                }
                pragmas.Add (pragmaLine.Value, pragmaLine.Key);
            }
            if (shaderTarget < 0) {
                shaderTarget = -shaderTarget;
            }
            if (shaderTarget == 0) {
                Debug.Log("Note: shader " + this.shader.shaderName + " using old shader target " + (shaderTarget/10) + "." + (shaderTarget%10));
                shaderTarget = 20;
            }
            tinycpp.Preproc pp = new tinycpp.Preproc();
            CGProgramOutputCollector cgpo = new CGProgramOutputCollector(shader);
            pp.set_output_interface(cgpo);
            pp.cpp_add_define("SHADER_API_D3D11 1");
            pp.cpp_add_define("SHADER_TARGET " + shaderTarget);
            pp.cpp_add_define("SHADER_TARGET_SURFACE_ANALYSIS 1"); // maybe?
            pp.cpp_add_define("UNITY_VERSION " + 2018420);
            pp.cpp_add_define("UNITY_PASS_SHADOWCASTER 1"); // FIXME: this is wrong. WE need to get the LightMode from tags. so we should parse tags, too.
            pp.cpp_add_define("UNITY_INSTANCING_ENABLED 1");
            //pp.cpp_add_define("UNITY_STEREO_INSTANCING_ENABLED 1");
            pp.parse_file(shader.filePath, cgProgramSource);
            string code = cgpo.GetOutputCode();
            File.WriteAllText("output_code.txt", code);
            
            if (surfFunction != null) {
                // TODO: for non-surface shaders, find the vertFUnction and pick out the return type.
                vertReturnType = "v2f_" + surfFunction;
                vertInputType = "appdata_full";
                vertFunction = "vert_" + surfFunction; // will be autogenerated.
            } else {
                Regex vertRe = new Regex ("\\b(\\S+)\\b\\s+" + vertFunction + "\\s*\\(\\s*(\\S*)\\b");
                foreach (string lin in new CommentFreeIterator(shader.shaderLines.Skip (beginLine).Take (endLine - beginLine))) {
                    if (lin.IndexOf("// Surface shader code generated based on:", StringComparison.CurrentCulture) != -1) {
                        originalSurfaceShader = true;
                        /*vertReturnType = "v2f_surf";
                        vertInputType = "appdata_full";
                        break;*/
                    }
                    /*
                    int vertIndex = lin.IndexOf (" " + vertFunction + " ", StringComparison.CurrentCulture);
                    if (vertIndex != -1) {
                        vertReturnType = lin.Substring (0, vertIndex).Trim ();
                        int paren = lin.IndexOf ("(", vertIndex, StringComparison.CurrentCulture);
                        if (paren != -1) {
                            int space = lin.IndexOf (" ", paren);
                            if (space != -1) {
                                vertInputType = lin.Substring (paren + 1, space - paren - 1).Trim();
                            }
                        }
                    }
                    */
                    Match m = vertRe.Match (lin);
                    if (m != null && m.Success) {
                        vertReturnType = m.Groups [1].Value;
                        vertInputType = m.Groups [2].Value;
                    }
                }
            }
        }
        public string getVertReturnType () {
            return vertReturnType;
        }
        public bool isSurface() {
            return surfFunction != null;
        }
        public bool hasGeometry() {
            return geomFunction != null;
        }
    }

    public Block shaderNameBlock;
    public Block propertiesBlock;
    public List<Block> passes = new List<Block>();
    public List<CGBlock> cgBlocks = new List<CGBlock>();

    public ParsedShader(Shader shader) {
        this.shader = shader;
        this.filePath = getPath (shader);
        Parse ();
    }

    public ParsedShader (Shader shader, string realFilePath)
    {
        this.shader = shader;
        this.filePath = realFilePath;
        Parse ();
    }

    private enum ParseState {
        ShaderName = 0,
        Properties = 1,
        PropertiesBlock = 2,
        SubShader = 3,
        PassBlock = 4,
        SubShaderCG = 6,
        PassCG = 7
    }

    void Parse() {
        shaderLines = File.ReadAllLines (filePath);
        ParseState state = ParseState.ShaderName;
        ParseState lastState = ParseState.PassBlock;
        int braceLevel = 0;
        int lineNum = -1;
        int beginBraceLineNum = -1;
        int beginBraceSkip = -1;
        int beginCGLineNum = -1;
        bool isOpenQuote = false;
        // bool CisOpenQuote = false;
        Block.Type passType = Block.Type.None;
        Block.Type cgType = Block.Type.None;
        Regex programCGRegex = new Regex ("\\b(CG|HLSL)PROGRAM\\b|\\b(CG|HLSL)INCLUDE\\b");
        Regex passCGRegex = new Regex ("\\bGrabPass\\b|\\bPass\\b|\\b(CG|HLSL)PROGRAM\\b|\\b(CG|HLSL)INCLUDE\\b");
        foreach (string xline in new CommentFreeIterator(shaderLines)) {
            string line = xline;
            lineNum++;
            int lineSkip = 0;
            /*
            while (true) {
                //Debug.Log ("Looking for comment " + lineNum);
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
                if (openQuote != -1) {
                    CisOpenQuote = true;
                    lineSkip = openQuote + 1;
                    //Debug.Log("C-Open quote start " + lineSkip);
                    continue;
                }
            }
            lineSkip = 0;
            */
            bool fallThrough = true;

            while (fallThrough) {
                if (state != lastState) {
                    #if DEBUG_PARSER
                        Debug.Log ("Line " + lineNum + ": state changed to " + state); }
                    #endif
                    lastState = state;
                }
                Debug.Log ("Looking for state " + state + " on line " + lineNum);
                fallThrough = false;
                lineSkip = 0; // ???
                switch (state) {
                case ParseState.ShaderName: {
                        int shaderOff = line.IndexOf ("Shader", lineSkip, StringComparison.CurrentCulture);
                        if (shaderOff != -1) {
                            int firstQuote = line.IndexOf ('\"', shaderOff);
                            int secondQuote = line.IndexOf ('\"', firstQuote + 1);
                            if (firstQuote != -1 && secondQuote != -1) {
                                shaderName = line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                                shaderNameBlock = new Block (this, Block.Type.ShaderName, lineNum, firstQuote + 1, lineNum, secondQuote);
                                fallThrough = true;
                                state = ParseState.Properties;
                            }
                        }
                    }
                    break;
                case ParseState.Properties: {
                        // Find beginning of Properties block
                        int shaderOff = line.IndexOf ("Properties", lineSkip, StringComparison.CurrentCulture);
                        if (shaderOff != -1) {
                            state = ParseState.PropertiesBlock;
                            passType = Block.Type.Properties;
                            lineSkip = shaderOff;
                            fallThrough = true;
                        }
                    }
                    break;
                case ParseState.PropertiesBlock:
                case ParseState.PassBlock: {
                        // Find end of Properties block
                        int i = 0;
                        while (lineSkip < line.Length && i < 10000) {
                            i++;
                            int openQuote = line.IndexOf ("\"", lineSkip, StringComparison.CurrentCulture);
                            if (isOpenQuote) {
                                if (openQuote == -1) {
                                    Debug.Log("Open quote ignore " + lineSkip);
                                    break;
                                } else {
                                    lineSkip = openQuote + 1;
                                    bool esc = false;
                                    int xi = lineSkip - 1;
                                    while (xi > 0 && line[xi] == '\\') {
                                        esc = !esc;
                                        xi--;
                                    }
                                    if (!esc) {
                                        isOpenQuote = false;
                                    }
                                }
                                Debug.Log("Open quote end " + lineSkip);
                                continue;
                            }
                            int openBrace = line.IndexOf ("{", lineSkip, StringComparison.CurrentCulture);
                            int closeBrace = line.IndexOf ("}", lineSkip, StringComparison.CurrentCulture);
                            if (openQuote != -1 && (openQuote < openBrace || openBrace == -1) && (openQuote < closeBrace || closeBrace == -1)) {
                                isOpenQuote = true;
                                lineSkip = openQuote + 1;
                                Debug.Log("Open quote start " + lineSkip);
                                continue;
                            }
                            Match m = null;
                            if (state == ParseState.PassBlock) {
                                m = programCGRegex.Match (line, lineSkip);
                            }
                            Debug.Log ("Looking for braces state " + state + " on line " + lineNum + "/" + lineSkip + " {}" + braceLevel + " open:" + openBrace + "/ close:" + closeBrace + " m.index " + (m == null ? -2 : m.Index));
                            if (m != null && m.Success && (closeBrace == -1 || m.Index < closeBrace) && (openBrace == -1 || m.Index < openBrace)) {
                                string match = m.Value;
                                #if DEBUG_PARSER
                                    Debug.Log ("Found " + match + " in Pass block line " + lineNum);
                                #endif
                                cgType = match.Equals ("HLSLINCLUDE") || match.Equals ("CGINCLUDE") ? Block.Type.CGInclude : Block.Type.CGProgram;
                                state = ParseState.PassCG;
                                fallThrough = false;
                                lineSkip = line.Length;
                                beginCGLineNum = lineNum + 1;
                                break;
                            } else if (closeBrace != -1 && (openBrace > closeBrace || openBrace == -1)) {
                                lineSkip = closeBrace + 1;
                                braceLevel--;
                                if (braceLevel == 0) {
                                    Block b = new Block (this, passType, beginBraceLineNum, beginBraceSkip, lineNum, closeBrace);
                                    if (state == ParseState.PropertiesBlock) {
                                        propertiesBlock = b;
                                    } else if (state == ParseState.PassBlock) {
                                        passes.Add (b);
                                    }
                                    state = ParseState.SubShader;
                                    fallThrough = true;
                                    break;
                                }
                            } else if (openBrace != -1 && (openBrace < closeBrace || closeBrace == -1)) {
                                if (braceLevel == 0) {
                                    beginBraceLineNum = lineNum;
                                    beginBraceSkip = openBrace + 1;
                                }
                                braceLevel++;
                                lineSkip = openBrace + 1;
                            } else {
                                break;
                            }
                        }
                        if (i >= 9999) {
                            throw new Exception ("Loop overflow " + i + "in braces search " + lineNum + "/" + lineSkip + ":" + braceLevel);
                        }
                    }
                    break;
                case ParseState.SubShader: {
                        Match m = null;
                        m = passCGRegex.Match (line, lineSkip);
                        if (m != null && m.Success) {
                            string match = m.Value;
                            if (match.Equals ("HLSLINCLUDE") || match.Equals ("HLSLPROGRAM") || match.Equals ("CGINCLUDE") || match.Equals ("CGPROGRAM")) {
                                cgType = match.Equals ("HLSLINCLUDE") || match.Equals ("CGINCLUDE") ? Block.Type.CGInclude : Block.Type.CGProgram;
                                #if DEBUG_PARSER
                                    Debug.Log ("Found " + match + " in SubShader line " + lineNum + ": " + cgType);
                                #endif
                                state = ParseState.SubShaderCG;
                                fallThrough = true;
                                beginCGLineNum = lineNum + 1;
                                break;
                            } else if (match.Equals ("GrabPass") || match.Equals ("Pass")) {
                                state = ParseState.PassBlock;
                                fallThrough = true;
                                passType = match.Equals ("Pass") ? Block.Type.Pass : Block.Type.GrabPass;
                                break;
                            }
                        }
                    }
                    break;
                case ParseState.SubShaderCG:
                case ParseState.PassCG:
                    int endCG = line.IndexOf ("ENDCG", lineSkip, StringComparison.CurrentCulture);
                    if (endCG != -1) {
                        #if DEBUG_PARSER
                            Debug.Log ("Ending cg:" + cgType + " lines " + beginCGLineNum + "-" + lineNum);
                        #endif
                        string buf = "";
                        if (cgType == Block.Type.CGProgram) {
                            int whichBlock = 0;
                            for (int i = 0; i < beginCGLineNum; i++) {
                                // if (i == cgBlocks[whichBlock].beginLineNum) {
                                //     buf += shaderLines[i].Substring(cgBlocks[whichBlock].beginSkip) + "\n";
                                // } else
                                if (whichBlock >= cgBlocks.Count) {
                                    buf += "\n";
                                } else if (i >= cgBlocks[whichBlock].beginLineNum && i < cgBlocks[whichBlock].endLineNum) {
                                    buf += shaderLines[i] + "\n";
                                // } else if (i == cgBlocks[whichBlock].endLineNum) {
                                //     buf += shaderLines[i].Substring(0, cgBlocks[whichBlock].endSkip) + "\n";
                                //     whichBlock += 1;
                                } else {
                                    buf += "\n";
                                }
                            }
                            for (int i = beginCGLineNum; i < lineNum; i++) {
                                buf += shaderLines[i] + "\n";
                            }
                        }
                        CGBlock b = new CGBlock (this, cgType, beginCGLineNum, lineNum, buf);
                        passes.Add (b);
                        cgBlocks.Add (b);
                        state = (state == ParseState.SubShaderCG) ? ParseState.SubShader : ParseState.PassBlock;
                    }
                    // Look for modified tag, or end of shader, or custom editor.
                    break;
                }
            }
        }
        foreach (Block b in passes) {
            CGBlock cgb = b as CGBlock;
            if (cgb != null || b.type == Block.Type.CGInclude||b.type == Block.Type.CGProgram) {
                Debug.Log ("Shader has a " + b.type + " on lines " + b.beginLineNum + "-" + b.endLineNum +
                            " with vert:" + cgb.vertFunction + " geom:" + cgb.geomFunction + " surf:" + cgb.surfFunction +
                            " | vert accepts input " + cgb.vertInputType + " output " + cgb.vertReturnType);
            } else {
                Debug.Log ("Shader has " + b.type + " block on lines " + b.beginLineNum + "-" + b.endLineNum);
            }
        }
    }
    
    public class CommentFreeIterator : IEnumerable<string> {
        private IEnumerable<string> sourceLines;
        public static string parserRemoveComments (string line, ref int comment)
        {
            int lineSkip = 0;
            bool CisOpenQuote = false;


            while (true) {
                //Debug.Log ("Looking for comment " + lineNum);
                int openQuote = line.IndexOf ("\"", lineSkip, StringComparison.CurrentCulture);
                if (CisOpenQuote) {
                    if (openQuote == -1) {
                        //Debug.Log("C-Open quote ignore " + lineSkip);
                        break;
                    } else {
                        lineSkip = openQuote + 1;
                        bool esc = false;
                        int i = lineSkip - 1;
                        while (i > 0 && line[i] == '\\') {
                            esc = !esc;
                            i--;
                        }
                        if (!esc) {
                            CisOpenQuote = false;
                        }
                    }
                    //Debug.Log("C-Open quote end " + lineSkip);
                    continue;
                }
                //Debug.Log ("Looking for comment " + lineSkip);
                int commentIdx;
                if (comment == 1) {
                    commentIdx = line.IndexOf ("*/", lineSkip, StringComparison.CurrentCulture);
                    if (commentIdx != -1) {
                        line = new String (' ', (commentIdx + 2)) + line.Substring (commentIdx + 2);
                        lineSkip = commentIdx + 2;
                        comment = 0;
                    } else {
                        line = "";
                        break;
                    }
                }
                commentIdx = line.IndexOf ("//", lineSkip, StringComparison.CurrentCulture);
                int commentIdx2 = line.IndexOf ("/*", lineSkip, StringComparison.CurrentCulture);
                if (commentIdx2 != -1 && (commentIdx == -1 || commentIdx > commentIdx2)) {
                    commentIdx = -1;
                }
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
            return line;
        }

        public CommentFreeIterator (IEnumerable<string> sourceLines)
        {
            this.sourceLines = sourceLines;
        }
        public IEnumerator<string> GetEnumerator ()
        {
            int comment = 0;
            foreach (string xline in sourceLines) {
                string line = parserRemoveComments (xline, ref comment);
                yield return line;
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () { return GetEnumerator (); }
    }

    public class PragmaIterator : IEnumerable<KeyValuePair<string, int>> {
        private IEnumerable<string> sourceLines;
        int startLine;
        public PragmaIterator (IEnumerable<string> sourceLines, int startLine)
        {
            this.sourceLines = sourceLines;
            this.startLine = startLine;
        }
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator ()
        {
            Regex re = new Regex ("^\\s*#\\s*pragma\\s+(.*)$");
            //Regex re = new Regex ("^\\s*#\\s*pragma\\s+geometry\\s+\(\\S*\)\\s*$");
            int ln = startLine - 1;
            foreach (string xline in sourceLines) {
                string line = xline;
                ln++;
                /*if (ln < startLine + 10) { Debug.Log ("Check line " + ln +"/" + line); }
                line = line.Trim ();
                if (line.StartsWith("#", StringComparison.CurrentCulture)) {
                    Debug.Log ("Check pragma " + ln + "/" + line);
                }*/
                if (re.IsMatch (line)) {
                    //Debug.Log ("Matched pragma " + line);
                    yield return new KeyValuePair<string, int> (re.Replace (line, match => match.Groups [1].Value), ln);
                }
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () { return GetEnumerator (); }
    }

    public bool hasSurfaceShader ()
    {
        foreach (CGBlock b in cgBlocks) {
            if (b.isSurface()) {
                return true;
            }
        }
        return false;
    }

    public static string getPath (Shader shader)
    {
        if (shader == null) {
            return null;
        }
        string path = AssetDatabase.GetAssetPath (shader);
        if (path.StartsWith ("Resources/unity_builtin_extra", StringComparison.CurrentCulture) && "Standard".Equals (shader.name)) {
            string [] tmpassets = AssetDatabase.FindAssets ("StandardSimple");
            foreach (string guid in tmpassets) {
                path = AssetDatabase.GUIDToAssetPath (guid);
                if (path.IndexOf (".shader", StringComparison.CurrentCulture) != -1) {
                    break;
                }
            }
        }
        // TODO: same for Legacy Shaders/Diffuse etc.
        return path;
    }

    public static string getTempPath (Shader shader, out string error)
    {
        string assetPath = getPath (shader);
        if (assetPath == null) {
            error = "No shader selected.";
            return null;
        }
        error = null;
        if (!assetPath.StartsWith ("Assets/", StringComparison.CurrentCulture)) {
            error = "Asset " + shader + " at path " + assetPath + " is not inside the Assets folder.";
            //EditorUtility.DisplayDialog ("GeomShaderGenerator", error, "OK", "");
            return null;
        }
        if (!File.Exists (assetPath)) {
            error = "Asset " + shader + " at path " + assetPath + " file does not exist.";
            return null;
        }
        string relativeAssetPath = assetPath.Substring (7);
        // TODO: Test in Unity 2017. This is likely to break in unity updates.
        // However, no public API is exposed to give us access to this file.
        string tempPath = "Temp/GeneratedFromSurface-" + shader.name.Replace ("/", "-") + ".shader";
        if (!File.Exists (tempPath)) {
            error = "Generated surface shader " + tempPath + " does not exist.\n\nPlease find the Surface shader line and click \"Show generated code\" before converting here.";
            return null;
        }
        return tempPath;
    }

    [MenuItem ("CONTEXT/Shader/TestShaderParser")]
    static void TestShaderParser (MenuCommand command)
    {
        Shader s = command.context as Shader;
        new ParsedShader(s);
    }

}
