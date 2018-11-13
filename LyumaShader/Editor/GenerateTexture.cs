using UnityEngine;
using UnityEditor;
using System;
using System.IO;

public class GenerateTexture : EditorWindow {
    /*
    [MenuItem ("Lyuma/GenerateTexture")]
    static void Init ()
    {
        EditorUtility.DisplayDialog ("MyTool", "Do It in C# !", "OK", "");
        GenerateTexture win = (GenerateTexture)GetWindow (typeof (GenerateTexture));
        win.Show ();
    }

    void OnGUI ()
    {
    */

    struct InputBufferElement {
        public Vector4 vertex;
        public Vector4 normal;
        public Vector4 tangent;
        public Vector4 uvColor;
        public Vector4 boneIndices;
        public Vector4 boneWeights;
        public void initialize () {
            vertex = new Vector4 ();
            normal = new Vector4 ();
            tangent = new Vector4 ();
            uvColor = new Vector4 ();
            boneIndices = new Vector4 ();
            boneWeights = new Vector4 ();
        }
    }

    static int[] getVertexAffectedBlendShapes(Mesh sourceMesh) {
        int [] vertexAffectedBlendShapes = new int [sourceMesh.vertices.Length];
        int [] blendShapesAffectCount = new int [sourceMesh.blendShapeCount];
        {
            Vector3 [] deltaVertices = new Vector3 [sourceMesh.vertices.Length];
            Vector3 [] deltaNormals = new Vector3 [sourceMesh.vertices.Length];
            Vector3 [] deltaTangents = new Vector3 [sourceMesh.vertices.Length];
            for (int blendIdx = 0; blendIdx < sourceMesh.blendShapeCount; blendIdx++) {
                sourceMesh.GetBlendShapeFrameVertices (blendIdx, 0, deltaVertices, deltaNormals, deltaTangents);
                for (int i = 0; i < deltaVertices.Length; i++) {
#pragma warning disable RECS0018 // Comparison of floating point numbers with equality operator
                    if (deltaVertices [i].x != 0.0f || deltaVertices [i].y != 0.0f || deltaVertices [i].z != 0.0f ||
                        deltaNormals [i].x != 0.0f || deltaNormals [i].y != 0.0f || deltaNormals [i].z != 0.0f ||
                        deltaTangents [i].x != 0.0f || deltaTangents [i].y != 0.0f || deltaTangents [i].z != 0.0f) {
                        vertexAffectedBlendShapes [i] += 1;
                        blendShapesAffectCount [blendIdx] += 1;
                    }
#pragma warning restore RECS0018 // Comparison of floating point numbers with equality operator
                }
            }
        }
        for (int i = 0; i < blendShapesAffectCount.Length; i++) {
            Debug.Log ("Blend shape " + sourceMesh.GetBlendShapeName (i) + ": " + blendShapesAffectCount [i] + " vertices");
        }
        return vertexAffectedBlendShapes;
    } 

    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Generate blend shape texture")]
    public static void GenerateBlendShapeTextureOperation (MenuCommand command)
    {
        Mesh sourceMesh;
        SkinnedMeshRenderer smr = null;
        MeshRenderer mr = null;
        MeshFilter mf = null;
        string parentName = "";
        if (command.context is SkinnedMeshRenderer) {
            smr = command.context as SkinnedMeshRenderer;
            parentName = smr.transform.parent.name;
            sourceMesh = smr.sharedMesh;
        } else if (command.context is MeshRenderer) {
            mr = command.context as MeshRenderer;
            mf = mr.transform.GetComponent<MeshFilter> ();
            parentName = mr.transform.parent.name;
            sourceMesh = mf.sharedMesh;
        } else if (command.context is MeshFilter) {
            mf = command.context as MeshFilter;
            parentName = mf.transform.parent.name;
            sourceMesh = mf.sharedMesh;
        } else {
            throw new NotSupportedException ("Unknkown context type " + command.context.GetType ().FullName);
        }
        int size = sourceMesh.triangles.Length / 3;
        int [] vertexAffectedBlendShapes = getVertexAffectedBlendShapes (sourceMesh);
        uint numIndices = 0;
        for (int i = 0; i < sourceMesh.subMeshCount; i++) {
            // Material not in dict, add it
            int [] srcIndices = (int [])sourceMesh.GetTriangles (i % sourceMesh.subMeshCount);
            for (int jTri = 0; jTri < srcIndices.Length; jTri += 3) {
                int affectedBlend = 0;
                for (int jPt = 0; jPt < 3; jPt++) {
                    int j = jTri + jPt;
                    int idx = srcIndices [j];
                    affectedBlend += vertexAffectedBlendShapes [idx];
                }
                if (affectedBlend != 0) {
                    numIndices+=3;
                }
            }
        }
        uint texElemWidth = 64;
        uint texPixelsPerElem = 1; // Vertex only... To include normal and tangent do 3;
        uint texWidth = texElemWidth * texPixelsPerElem;
        uint numIndicesRounded = ((numIndices + texElemWidth - 1) / texElemWidth) * texElemWidth;
        InputBufferElement [] outDataBuf = new InputBufferElement [numIndicesRounded];
        for (int i = 0; i < numIndicesRounded; i++) {
            InputBufferElement elem = new InputBufferElement ();
            elem.initialize ();
            outDataBuf [i] = elem;
        }
        ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader> ("Assets/LyumaShader/Compute/GenerateTexture.compute");
        ComputeBuffer computeBuffer = new ComputeBuffer ((int)numIndicesRounded, 16 * 6);
        int kernelHandle = shader.FindKernel ("ConvertMeshTextureVertexOnly");

        RenderTexture tex = new RenderTexture ((int)texWidth, (int)(numIndicesRounded / texElemWidth) + 1, 0, RenderTextureFormat.ARGBHalf);
        tex.enableRandomWrite = true;
        tex.Create ();

        Texture2D outTexture = new Texture2D (tex.width, (tex.height + 15) / 16 * 16, TextureFormat.RGBAHalf, false, true);
        Texture2DArray outTextureArray = new Texture2DArray (tex.width, (tex.height + 15) / 16 * 16,
                                                             sourceMesh.blendShapeCount, TextureFormat.RGBAHalf, false, true);
        outTextureArray.wrapMode = TextureWrapMode.Clamp;
        shader.SetBuffer (kernelHandle, "_InputBuffer", computeBuffer);
        //shader.SetFloats(kernelHandle, "_DataBuffer", )
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexWidth", tex.width);
        shader.SetInt ("_TexHeight", tex.height);
        shader.SetInt ("_BlendShapeIdx", 0);
        Vector3 [] deltaVertices = new Vector3 [sourceMesh.vertices.Length];
        Vector3 [] deltaNormals = new Vector3 [sourceMesh.vertices.Length];
        Vector3 [] deltaTangents = new Vector3 [sourceMesh.vertices.Length];
        for (int blendIdx = 0; blendIdx < sourceMesh.blendShapeCount; blendIdx++) {
            shader.SetInt ("_BlendShapeIdx", blendIdx);
            if (sourceMesh.GetBlendShapeFrameCount(blendIdx) != 1) {
                throw new Exception ("Blend shape named " + sourceMesh.GetBlendShapeName (blendIdx) + " has multiple frames. Unimplemented.");
            }
            sourceMesh.GetBlendShapeFrameVertices (blendIdx, 0, deltaVertices, deltaNormals, deltaTangents);
            int outIdx = 0;
            for (int i = 0; i < sourceMesh.subMeshCount; i++) {
                // Material not in dict, add it
                int [] srcIndices = (int [])sourceMesh.GetTriangles (i % sourceMesh.subMeshCount);
                int wantBlend = i / sourceMesh.subMeshCount;
                for (int jTri = 0; jTri < srcIndices.Length; jTri += 3) {
                    int affectedBlend = 0;
                    for (int jPt = 0; jPt < 3; jPt++) {
                        int j = jTri + jPt;
                        int idx = srcIndices [j];
                        affectedBlend += vertexAffectedBlendShapes [idx];
                    }
                    if ((affectedBlend == 0) == (wantBlend == 0)) {
                        continue;
                    }
                    for (int jPt = 0; jPt < 3; jPt++) {
                        int j = jTri + jPt;
                        int idx = srcIndices [j];
                        outDataBuf [i].vertex = new Vector3(deltaVertices [idx].x * 1000f, deltaVertices [idx].y * 1000f, deltaVertices [idx].z * 1000f);
                        outDataBuf [i].normal = deltaNormals [idx];
                        outDataBuf [i].tangent = deltaTangents [idx];
                        outIdx++;
                    }
                }
            }
            computeBuffer.SetData (outDataBuf);
            shader.Dispatch (kernelHandle, (int)texElemWidth, tex.height, 1);

            RenderTexture oldActive = RenderTexture.active;
            RenderTexture.active = tex;
            outTexture.ReadPixels (new Rect (0, 0, tex.width, tex.height), 0, 0);
            outTexture.Apply ();
            RenderTexture.active = oldActive;

            Graphics.CopyTexture (outTexture, 0, 0, outTextureArray, blendIdx, 0);
        }
        tex.Release ();
        computeBuffer.Release ();

        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        string tmpName = (parentName + "_" + sourceMesh.name);
        int lastSlash = tmpName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? tmpName : tmpName.Substring (lastSlash + 1);
        outFileName = outFileName.Split ('.') [0];
        string outPath = pathToGenerated + "/ZZblendmeshtex_" + outFileName + "_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (outTextureArray, outPath);
        EditorGUIUtility.PingObject (outTextureArray);
    }

    [MenuItem ("CONTEXT/MeshFilter/Generate mesh texture")]
    [MenuItem ("CONTEXT/MeshRenderer/Generate mesh texture")]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Generate mesh texture")]
    public static void GenerateTextureOperation (MenuCommand command)
    {
        bool ENABLE_BONE_WEIGHTS = true;
        Mesh sourceMesh;
        SkinnedMeshRenderer smr = null;
        MeshRenderer mr = null;
        MeshFilter mf = null;
        string parentName = "";
        if (command.context is SkinnedMeshRenderer) {
            smr = command.context as SkinnedMeshRenderer;
            parentName = smr.transform.parent.name;
            sourceMesh = smr.sharedMesh;
        } else if (command.context is MeshRenderer) {
            mr = command.context as MeshRenderer;
            mf = mr.transform.GetComponent<MeshFilter> ();
            parentName = mr.transform.parent.name;
            sourceMesh = mf.sharedMesh;
        } else if (command.context is MeshFilter) {
            mf = command.context as MeshFilter;
            parentName = mf.transform.parent.name;
            sourceMesh = mf.sharedMesh;
        } else {
            throw new NotSupportedException ("Unknkown context type " + command.context.GetType ().FullName);
        }
        //BoneWeight [] srcWeights = sourceMesh.boneWeights;
        //for (int i = 0; i < srcWeights.Length && i < 20; i++) {
        //    BoneWeight bw = srcWeights [i];
        //    Debug.Log ("weight[" + i + "]: <"
        //              + bw.boneIndex0 + "=" + bw.weight0 + ","
        //              + bw.boneIndex1 + "=" + bw.weight1 + ","
        //              + bw.boneIndex2 + "=" + bw.weight2 + ","
        //              + bw.boneIndex3 + "=" + bw.weight3 + ">", sourceMesh);
        //}
        int size = sourceMesh.triangles.Length / 3;
        Vector2 [] srcUV = sourceMesh.uv;
        Vector2 [] srcUV2 = sourceMesh.uv2;
        Vector3 [] srcVertices = sourceMesh.vertices;
        Color32 [] srcColors = sourceMesh.colors32;
        Vector3 [] srcNormals = sourceMesh.normals;
        Vector4 [] srcTangents = sourceMesh.tangents;
        BoneWeight [] srcBoneWeights = sourceMesh.boneWeights;
        uint numIndices = 0;
        for (int i = 0; i < sourceMesh.subMeshCount; i++) {
            numIndices += sourceMesh.GetIndexCount (i);
        }
        uint texElemWidth = 64;
        uint texPixelsPerElem = ENABLE_BONE_WEIGHTS ? 6u : 4u;
        uint texWidth = texElemWidth * texPixelsPerElem;
        uint numIndicesRounded = ((numIndices + texElemWidth - 1) / texElemWidth) * texElemWidth;
        InputBufferElement[] outDataBuf = new InputBufferElement [numIndicesRounded];
        int outIdx = 0;
        int [] vertexAffectedBlendShapes = getVertexAffectedBlendShapes (sourceMesh);
        for (int i = 0; i < 2 * sourceMesh.subMeshCount; i++) {
            // Material not in dict, add it
            int [] srcIndices = (int [])sourceMesh.GetTriangles (i % sourceMesh.subMeshCount);
            int wantBlend = i / sourceMesh.subMeshCount;
            for (int jTri = 0; jTri < srcIndices.Length; jTri += 3) {
                int affectedBlend = 0;
                for (int jPt = 0; jPt < 3; jPt++) {
                    int j = jTri + jPt;
                    int idx = srcIndices [j];
                    affectedBlend += vertexAffectedBlendShapes [idx];
                }
                if ((affectedBlend == 0) == (wantBlend == 0)) {
                    continue;
                }
                for (int jPt = 0; jPt < 3; jPt++) {
                    int j = jTri + jPt;
                    int idx = srcIndices [j];

                    InputBufferElement elem = new InputBufferElement ();
                    Vector3 v = srcVertices [idx];
                    elem.vertex = new Vector4 (v.x, v.y, v.z, 1.0f);
                    v = srcNormals [idx];
                    elem.normal = new Vector4 (v.x, v.y, v.z, 1.0f);
                    Vector4 v4 = srcTangents [idx];
                    elem.tangent = new Vector4 (v4.x, v4.y, v4.z, v4.w);
                    Vector4 uvColor = new Vector4 ();
                    uvColor.x = srcUV != null && srcUV.Length > idx ? srcUV [idx].x : srcColors != null && srcColors.Length > idx ? srcColors [idx].r : 0f;
                    uvColor.y = srcUV != null && srcUV.Length > idx ? srcUV [idx].y : srcColors != null && srcColors.Length > idx ? srcColors [idx].g : 0f;
                    uvColor.z = srcUV2 != null && srcUV2.Length > idx ? srcUV2 [idx].x : srcColors != null && srcColors.Length > idx ? srcColors [idx].b : 0f;
                    uvColor.w = srcUV2 != null && srcUV2.Length > idx ? srcUV2 [idx].y : srcColors != null && srcColors.Length > idx ? srcColors [idx].a : 0f;
                    elem.uvColor = uvColor;
                    BoneWeight w = srcBoneWeights [idx];
                    elem.boneIndices = new Vector4 (w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3);
                    elem.boneWeights = new Vector4 (w.weight0, w.weight1, w.weight2, w.weight3);
                    outDataBuf [outIdx] = elem;
                    outIdx++;
                }
            }
            Debug.Log ("subMesh " + i + ": outIdx = " + outIdx);
        }
        for (; outIdx < numIndicesRounded; outIdx++) {
            InputBufferElement elem = new InputBufferElement ();
            elem.initialize ();
            outDataBuf [outIdx] = elem;
            /*for (int j = 0; j < 16; j++) {
                outDataBuf [outIdx * 16 + j] = 0;
            }*/
        }
        ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader> ("Assets/LyumaShader/Compute/GenerateTexture.compute");
        ComputeBuffer computeBuffer = new ComputeBuffer ((int)numIndicesRounded, 16 * 6);
        computeBuffer.SetData (outDataBuf);
        int kernelHandle = shader.FindKernel (ENABLE_BONE_WEIGHTS ? "ConvertMeshTextureBoneWeights" : "ConvertMeshTexture");

        RenderTexture tex = new RenderTexture ((int)texWidth, (int)(numIndicesRounded / texElemWidth), 0, RenderTextureFormat.ARGBHalf);
        tex.enableRandomWrite = true;
        tex.Create ();

        shader.SetBuffer (kernelHandle, "_InputBuffer", computeBuffer);
        //shader.SetFloats(kernelHandle, "_DataBuffer", )
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexWidth", tex.width);
        shader.SetInt ("_TexHeight", tex.height);
        shader.SetInt ("_BlendShapeIdx", 0);
        shader.Dispatch (kernelHandle, (int)texElemWidth, tex.height, 1);

        Texture2D outTexture = new Texture2D (tex.width, (tex.height + 15) / 16 * 16, TextureFormat.RGBAHalf, false, true);
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture.active = tex;
        outTexture.ReadPixels (new Rect (0, 0, tex.width, tex.height), 0, 0);
        outTexture.Apply ();
        RenderTexture.active = oldActive;
        tex.Release ();
        computeBuffer.Release ();

        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        string tmpName = (parentName + "_" + sourceMesh.name);
        int lastSlash = tmpName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? tmpName : tmpName.Substring (lastSlash + 1);
        outFileName = outFileName.Split ('.') [0];
        string outPath = pathToGenerated + "/ZZmeshtex_" + outFileName + "_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (outTexture, outPath);
        EditorGUIUtility.PingObject (outTexture);

        //for (int i = 0; i < srcVertices.Length; i++) {
        //d
        //}
        /*
        Mesh newMesh = new Mesh ();
        var newVertices = new Vector3 [size];
        var newNormals = new Vector3 [size];
        var newTangents = new Vector4 [size];
        var newBoneWeights = new BoneWeight [size];
        // var newUV1 = new Vector4[size];
        var newColors = new Color32 [size];
        var newIndices = new List<int []> ();
        var curIndices = new int [size * 3];
        newIndices.Add (curIndices);
        var newBones = renderer.bones;
        var newBindPoses = sourceMesh.bindposes;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [newBindPoses.Length];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = newBindPoses [i].inverse;
        }
        for (int i = 0; i < size; i++) {
            int boneIndex = i / NUM_BONE_MODES;
            Transform t = renderer.bones [boneIndex];
            int dstVert = i;
            Debug.Log ("Bone " + boneIndex + ": " + t.name, t);
            Vector3 newTang;
            if (i % NUM_BONE_MODES == 0) {
                // Generate Bind pose matrix
                newVertices [dstVert] = new Vector3 (0, 0, 0);
                newNormals [dstVert] = new Vector3 (0, 0, 1);
                newTang = new Vector3 (1, 0, 0);
            } else if (i % NUM_BONE_MODES == 1) {
                // Generate Bone transform matrix
                newVertices [dstVert] = inverseBindPoses [boneIndex].MultiplyPoint (new Vector3 (0, 0, 0));
                newNormals [dstVert] = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, 0, 1));
                newTang = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (1, 0, 0));
            } else {
                // causes bones to be visible without any shader applied: for testing.
                newVertices [dstVert] = new Vector3 (-.0005f, -.001f, .0001f);
                newNormals [dstVert] = new Vector3 (0, 0, 1);
                newTang = new Vector3 (1, 0, 0);
            }
            newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
            newBoneWeights [dstVert] = new BoneWeight ();
            newBoneWeights [dstVert].boneIndex0 = boneIndex;
            newBoneWeights [dstVert].weight0 = 1.0f;
            byte [] simpleName = SimplifyBoneName (t.name);
            newColors [dstVert] = new Color32 ();
            newColors [dstVert].r = simpleName [0];
            newColors [dstVert].g = simpleName [1];
            newColors [dstVert].b = simpleName [2];
            newColors [dstVert].a = simpleName [3];
            if (i % NUM_BONE_MODES == 0) {
                curIndices [dstVert * 3] = i; // bind pose matrix
                curIndices [dstVert * 3 + 1] = i + (NUM_BONE_MODES >= 2 ? 1 : 0); // bone transform
                curIndices [dstVert * 3 + 2] = i + (NUM_BONE_MODES >= 3 ? 2 : 0); // unused - keep degenerate for Standard fallback
            }
            Debug.Log ("i:" + boneIndex + " v:" + dstVert + ": Adding vertex " + d (newVertices [dstVert]) + "/" + d (newNormals [dstVert]) + "/" + d (newTangents [dstVert]));
        }
        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.boneWeights = newBoneWeights;
        newMesh.colors32 = newColors;
        // newMesh.SetUVs(0, new List<Vector4>(newUV1));
        newMesh.subMeshCount = 1;
        newMesh.SetTriangles (curIndices, 0);
        newMesh.bounds = sourceMesh.bounds;
        newMesh.bindposes = newBindPoses;
        newMesh.name = sourceMesh.name + "_BoneMesh";
        GameObject newGameObject = new GameObject ("BoneMesh");
        newGameObject.transform.parent = renderer.transform.parent;
        newGameObject.transform.localPosition = renderer.transform.localPosition;
        SkinnedMeshRenderer newRenderer = newGameObject.AddComponent<SkinnedMeshRenderer> ();
        newRenderer.sharedMesh = newMesh;
        newRenderer.bones = renderer.bones;
        newRenderer.rootBone = renderer.rootBone;
        Material [] newMaterials = new Material [1];
        newMaterials [0] = renderer.sharedMaterials [0];
        newRenderer.sharedMaterials = newMaterials;
        newRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        newRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        newRenderer.receiveShadows = false;
        newRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        newRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        newRenderer.updateWhenOffscreen = true;
        AssetDatabase.CreateAsset (newRenderer.sharedMesh, "Assets/" + sourceMesh.name + "_boneMesh.asset");
        AssetDatabase.SaveAssets ();*/
    }

    [MenuItem ("Lyuma/PlayerSettings")]
    static void OpenPlayerSettings ()
    {
        EditorApplication.ExecuteMenuItem ("Edit/Project Settings/Player");
    }
}
