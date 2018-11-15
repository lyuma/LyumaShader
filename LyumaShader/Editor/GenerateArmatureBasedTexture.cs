using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class GenerateArmatureBasedTexture : EditorWindow {
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

    public static Regex boneNameCleaner = new Regex ("(\\s+[0-9]*|\\.0[0-9][0-9])$");
    static string cleanBoneName(string boneName) {
        string ret = boneNameCleaner.Replace (boneName, "");
        Debug.Log ("Bone " + boneName + " => " + ret);
        return ret;
    }

    [MenuItem ("Lyuma/GenerateArmatureBasedTexture")]
    static void Init ()
    {
        GenerateArmatureBasedTexture win = (GenerateArmatureBasedTexture)GetWindow (typeof (GenerateArmatureBasedTexture));
        win.Show ();
    }

    Animator animatorObj;
    SkinnedMeshRenderer destSkinnedMesh;
    SkinnedMeshRenderer srcSkinnedMesh;

    int triCount;

    void OnGUI ()
    {
        animatorObj = (Animator)EditorGUILayout.ObjectField (new GUIContent ("Avatar Animator", "Avatar Animator object"),
                                                             animatorObj, typeof (Animator), true);
        if (animatorObj != null && animatorObj.isHuman) {
            //headDefaultBone = animatorObj.GetBoneTransform (HumanBodyBones.Head);
            //leftHandDefaultBone = animatorObj.GetBoneTransform (HumanBodyBones.LeftHand);
            //rightHandDefaultBone = animatorObj.GetBoneTransform (HumanBodyBones.RightHand);
        }
        if (animatorObj != null && destSkinnedMesh == null) {
            SkinnedMeshRenderer [] renderers = animatorObj.GetComponentsInChildren<SkinnedMeshRenderer> ();
            Array.Sort (renderers, delegate (SkinnedMeshRenderer a, SkinnedMeshRenderer b) {
                return a.transform.parent == animatorObj.transform ? -1 : 1;
            });
            foreach (SkinnedMeshRenderer s in renderers) {
                destSkinnedMesh = s;
                break;
                /*
                IList<Transform> tbones = s.bones;
                if (tbones.Contains (headDefaultBone) &&
                        tbones.Contains (leftHandDefaultBone) &&
                        tbones.Contains (rightHandDefaultBone)) {
                    skinnedMesh = s;
                    break;
                }*/
            }
        }
        destSkinnedMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField (
            new GUIContent ("Rigged mesh", "Destination mesh renderer (to get bone list)"), destSkinnedMesh, typeof (SkinnedMeshRenderer), true);

        srcSkinnedMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField (
            new GUIContent ("Source unrigged mesh", "Source mesh renderer (to get bone list)"), srcSkinnedMesh, typeof (SkinnedMeshRenderer), true);

        triCount = EditorGUILayout.IntField (
            new GUIContent ("Triangle count", "Number of polygons to generate."), triCount);
        bool error = false;
        if (destSkinnedMesh == null) {
            EditorGUILayout.HelpBox ("You must select a Skinned Mesh Renderer on the destination Humanoid skeleton", MessageType.Error);
            error = true;
        }
        if (srcSkinnedMesh == null) {
            EditorGUILayout.HelpBox ("You must select a Skinned Mesh Renderer on the source skeleton", MessageType.Error);
            error = true;
        }

        EditorGUILayout.Separator ();
        if (GUILayout.Button ("Clear")) {
            destSkinnedMesh = null;
            srcSkinnedMesh = null;
            animatorObj = null;
        }
        if (error) return;

        if (GUILayout.Button ("Generate")) {
            doGenerate ();
        }
    }

    private void generateBoneMapping(out int []srcToDest, out int []destToSrc) {
        Transform [] sourceBones = srcSkinnedMesh.bones;
        Transform [] destBones = destSkinnedMesh.bones;
        if (destBones [0] != destSkinnedMesh.rootBone) {
            throw new Exception ("Mismatched dest root bone! " + destSkinnedMesh.rootBone.name + " vs " + destBones[0].name);
        }
        if (sourceBones [0] != srcSkinnedMesh.rootBone) {
            throw new Exception ("Mismatched src root bone! " + srcSkinnedMesh.rootBone.name + " vs " + sourceBones [0].name);
        }
        int [] outBones = new int [sourceBones.Length];
        int [] outBonesRev = new int [destBones.Length];
        Dictionary<string, int> destBoneNameMap = new Dictionary<string, int>();
        Dictionary<Transform, int> srcBoneIdxMap = new Dictionary<Transform, int> ();
        for (int i = 0; i < destBones.Length; i++) {
            destBoneNameMap [cleanBoneName(destBones [i].name)] = i;
        }
        for (int i = 0; i < sourceBones.Length; i++) {
            srcBoneIdxMap [sourceBones[i]] = i;
        }
        for (int i = 0; i < sourceBones.Length; i++) {
            Transform curBone = sourceBones [i];
            int matchingBone = 0;
            while (curBone != null) {
                if (destBoneNameMap.ContainsKey(cleanBoneName(curBone.name))) {
                    matchingBone = destBoneNameMap [cleanBoneName(curBone.name)];
                    break;
                }
                curBone = curBone.parent;
            }
            outBones [i] = matchingBone;
            outBonesRev[matchingBone] = srcBoneIdxMap[curBone];
        }
        srcToDest = outBones;
        destToSrc = outBonesRev;
    }

    private void doGenerate ()
    {
        Mesh sourceMesh = srcSkinnedMesh.sharedMesh;
        Mesh newMesh = new Mesh ();
        Transform [] sourceBones = srcSkinnedMesh.bones;
        Transform [] destBones = destSkinnedMesh.bones;
        int numDestBones = destBones.Length;
        Transform sourceRelativeTransform = srcSkinnedMesh.transform.parent;
        Transform destRelativeTransform = animatorObj.transform;
        string parentName = srcSkinnedMesh.transform.parent.name;

        int [] boneIndexMapping;
        int [] boneIndexMappingRev;
        generateBoneMapping (out boneIndexMapping, out boneIndexMappingRev);
        Matrix4x4 [] srcToDestTransforms = new Matrix4x4 [numDestBones];
        Matrix4x4 [] destToSrcTransforms = new Matrix4x4 [numDestBones];
        for (int i = 0; i < numDestBones; i++) {
            int srcBoneIdx = boneIndexMappingRev [i];
            Transform sourceBone = sourceBones [srcBoneIdx];
            Transform destBone = destBones [i];
            srcToDestTransforms[i] = destBone.worldToLocalMatrix * sourceBone.localToWorldMatrix;
            destToSrcTransforms [i] = sourceBone.worldToLocalMatrix * destBone.localToWorldMatrix;
        }

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
        uint texPixelsPerElem = 6u;
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
                    elem.boneIndices = new Vector4 (
                        boneIndexMapping[w.boneIndex0],
                        boneIndexMapping[w.boneIndex1],
                        boneIndexMapping[w.boneIndex2],
                        boneIndexMapping[w.boneIndex3]);
                    //if (outIdx < 50) {
                    //    Debug.Log ("BoneIndexMapping: " + elem.boneIndices.x + "," + elem.boneIndices.y + "," + elem.boneIndices.z + "," + elem.boneIndices.w);
                    //}
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
        ComputeBuffer srcToDestBoneMatComputeBuffer = new ComputeBuffer ((int)numDestBones, 4 * 16);
        srcToDestBoneMatComputeBuffer.SetData (srcToDestTransforms);
        ComputeBuffer destToSrcBoneMatComputeBuffer = new ComputeBuffer ((int)numDestBones, 4 * 16);
        destToSrcBoneMatComputeBuffer.SetData (destToSrcTransforms);
        int kernelHandle = shader.FindKernel ("ConvertMeshTextureBoneWeights");

        RenderTexture tex = new RenderTexture ((int)texWidth, (int)(numIndicesRounded / texElemWidth), 0, RenderTextureFormat.ARGBHalf);
        tex.enableRandomWrite = true;
        tex.Create ();

        shader.SetBuffer (kernelHandle, "_InputBuffer", computeBuffer);
        //shader.SetFloats(kernelHandle, "_DataBuffer", )
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexWidth", tex.width);
        shader.SetInt ("_TexHeight", tex.height);
        shader.SetInt ("_TexYOffset", 8); // 4 pixels for src matrix, 4 pixels for dst matrix.
        shader.SetInt ("_BlendShapeIdx", 0);
        shader.Dispatch (kernelHandle, (int)texElemWidth, tex.height, 1);

        kernelHandle = shader.FindKernel ("DumpMatrices");
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", srcToDestBoneMatComputeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 0);
        shader.Dispatch (kernelHandle, (int)numDestBones, 1, 1);

        shader.SetBuffer (kernelHandle, "_MatrixBufIn", destToSrcBoneMatComputeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 4);
        shader.Dispatch (kernelHandle, (int)numDestBones, 1, 1);

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
        outFileName = outFileName.Replace ('.', '_');
        string outPath = pathToGenerated + "/ZZmeshtex_" + outFileName + "_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (outTexture, outPath);
        EditorGUIUtility.PingObject (outTexture);
    }

    [MenuItem ("Lyuma/PlayerSettings")]
    static void OpenPlayerSettings ()
    {
        EditorApplication.ExecuteMenuItem ("Edit/Project Settings/Player");
    }
}
