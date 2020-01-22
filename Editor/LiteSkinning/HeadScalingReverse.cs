//ConvertToGPU
//Script

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//using HierarchyDict = System.Collections.Generic.Dictionary<string, UnityEngine.Transform>;
//using BoneTransformDict = System.Collections.Generic.Dictionary<string, Tuple<UnityEngine.Transform, string>>;


//#if DO_STUFF_CONVERT_MESH_TO
public class HeadScalingReverse : MonoBehaviour {
    public static string d(Vector2 vector) {
        return "<" + vector.x + "," + vector.y + ">";
    }
    public static string d (Vector3 vector)
    {
        return "<" + vector.x + "," + vector.y + "," + vector.z + ">";
    }
    public static string d (Vector4 vector)
    {
        return "<" + vector.x + "," + vector.y + "," + vector.z + "," + vector.w + ">";
    }

    const bool SIMPLIFIED_TRIS = true;

    public static bool isHeadBone(BoneWeight weight, HashSet<int> headBones) {
        float headWeight = 0;
        if (headBones.Contains(weight.boneIndex0)) {
            headWeight += weight.weight0;
        }
        if (headBones.Contains(weight.boneIndex1)) {
            headWeight += weight.weight1;
        }
        if (headBones.Contains(weight.boneIndex2)) {
            headWeight += weight.weight2;
        }
        if (headBones.Contains(weight.boneIndex3)) {
            headWeight += weight.weight3;
        }
        if (headWeight > 0.25) {
            return true;
        }
        return false;
    }

    //[MenuItem("GameObject/Create Mesh")]
    [MenuItem("CONTEXT/SkinnedMeshRenderer/Head Scaling Reverse : LMTx")]
    public static void HeadScalingReverse_(MenuCommand command)
    {
        SkinnedMeshRenderer renderer = command.context as SkinnedMeshRenderer;
        Mesh sourceMesh = renderer.sharedMesh;
        Dictionary<Transform, int> boneToIndex = new Dictionary<Transform, int>();
        Animator parentAnim = renderer.transform.GetComponentInParent<Animator>();
        for (int i = 0; i < renderer.bones.Length; i++) {
            Transform t = renderer.bones[i];
            boneToIndex.Add(t, i);
        }

        Transform head = parentAnim.GetBoneTransform(HumanBodyBones.Head);
        int headBone = boneToIndex[head];
        HashSet<int> headBones = new HashSet<int>();
        for (int i = 0; i < renderer.bones.Length; i++) {
            Transform t = renderer.bones[i];
            Transform parT = t;
            while (parT != null) {
                if (parT == head) {
                    headBones.Add(i);
                    break;
                }
                parT = parT.parent;
            }
        }

        Vector2 [] srcUV = sourceMesh.uv;
        List<Vector4> srcUV2 = new List<Vector4> ();
        sourceMesh.GetUVs(1, srcUV2);
        Vector3 [] srcVertices = sourceMesh.vertices;
        Color[] srcColors = sourceMesh.colors;
        Vector3 [] srcNormals = sourceMesh.normals;
        Vector4 [] srcTangents = sourceMesh.tangents;
        BoneWeight [] srcBoneWeights = sourceMesh.boneWeights;

        int size = sourceMesh.vertices.Length;
        int pristineVertexSize = size;
        int headVerticesToDuplicate = 0;
        int [] duplicateVertices = new int[sourceMesh.vertices.Length];
        for (int i = 0; i < sourceMesh.vertices.Length; i++) {
            if (isHeadBone(srcBoneWeights[i], headBones)) {
                duplicateVertices[i] = size;
                size++;
                headVerticesToDuplicate++;
            } else {
                duplicateVertices[i] = -1;
            }
        }

        var newBones = renderer.bones;
        var newBindPoses = sourceMesh.bindposes;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [newBindPoses.Length];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = newBindPoses [i].inverse;
        }

        int[][] outIndices = new int[sourceMesh.subMeshCount][];
        int[] pristineIndicesLength = new int[sourceMesh.subMeshCount];
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            int numNonPristineTris = 0;
            // Material not in dict, add it
            int[] curIndices = (int[])sourceMesh.GetTriangles(i).Clone();
            int origLength = curIndices.Length;
            pristineIndicesLength[i] = origLength;
            for (int curIdx = 0; curIdx < curIndices.Length; curIdx += 3) {
                int srcTriangle = curIdx;
                bool pristine = false;
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    if (duplicateVertices[srcVert] == -1) {
                        pristine = true;
                    }
                }
                if (!pristine) {
                    numNonPristineTris++;
                }
            }
            { // NON_PRISTINE_COPY
                Array.Resize(ref curIndices, origLength + 3 * numNonPristineTris);
                int outIdx = origLength;
                for (int curIdx = 0; curIdx < origLength; curIdx += 3) {
                    int srcTriangle = curIdx;
                    bool pristine = false;
                    for (int k = 0; k < 3; k++) {
                        int srcIndex = curIdx + k;
                        int srcVert = curIndices[srcIndex];
                        if (duplicateVertices[srcVert] == -1) {
                            pristine = true;
                        }
                    }
                    if (!pristine) {
                        for (int k = 0; k < 3; k++) {
                            int srcIndex = curIdx + k;
                            int srcVert = curIndices[srcIndex];
                            curIndices[outIdx + k] = duplicateVertices[srcVert];
                        }
                        outIdx += 3;
                    }
                }
            }
            outIndices[i] = curIndices;
        }
        Mesh newMesh = new Mesh();
        var newVertices = new Vector3[size];
        var newNormals = new Vector3[size];
        var newTangents = new Vector4[size];
        var newBoneWeights = new BoneWeight[size];
        var newUV1 = new Vector4[size];
        Vector4[] newUV2 = null;
        if (srcUV2.Count > 0) {
            newUV2 = new Vector4[size];
        }
        var newUV3 = new Vector4[size];
        var newUV4 = new Vector4[size];
        Color[] newColors = null;
        if (srcColors.Length > 0) {
            newColors = new Color[size];
        }
        for (int i = 0; i < sourceMesh.vertices.Length; i++) {
            int outi = duplicateVertices[i];
            if (duplicateVertices[i] != -1) { // && NON_PRISTINE_COPY
                newVertices [outi] = inverseBindPoses [headBone].MultiplyPoint (new Vector3 (0, -50, 0));
                newNormals [outi] = inverseBindPoses [headBone].MultiplyVector (new Vector3 (0, 0, 1));
                Vector3 newTang = inverseBindPoses[headBone].MultiplyVector(new Vector3(1, 0, 0));
                newTangents [outi] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
                newBoneWeights [outi] = new BoneWeight ();
                newBoneWeights [outi].boneIndex0 = headBone;
                newBoneWeights [outi].weight0 = 1.0f;

                Vector3 srcPosition = srcVertices[i];
                Vector3 srcTangent = new Vector3(srcTangents [i].x, srcTangents [i].y, srcTangents [i].z);
                Vector3 srcNormal = srcNormals [i];
                srcPosition = sourceMesh.bindposes[headBone].MultiplyPoint(srcPosition);
                srcTangent = sourceMesh.bindposes[headBone].MultiplyVector(srcTangent);
                srcNormal = sourceMesh.bindposes[headBone].MultiplyVector(srcNormals [i]);
                newUV1 [outi] = new Vector4 ();
                if (srcUV.Length > i) {
                    newUV1[outi].x = srcUV[i].x;
                    newUV1[outi].y = srcUV[i].y;
                } else {
                    newUV1[outi].x = 0;
                    newUV1[outi].y = 0;
                }
                newUV1 [outi].z = srcTangent.x;
                newUV1 [outi].w = srcTangent.y;
                if (i < srcUV2.Count) {
                    newUV2[outi] = srcUV2[i];
                }
                newUV3[outi] = new Vector4();
                newUV3[outi].x = srcPosition.x;
                newUV3[outi].y = srcPosition.y;
                newUV3[outi].z = srcPosition.z;
                newUV3[outi].w = srcTangent.z;
                newUV4[outi] = new Vector4();
                newUV4[outi].x = srcNormal.x;
                newUV4[outi].y = srcNormal.y;
                newUV4[outi].z = srcNormal.z;
                newUV4[outi].w = srcTangents [i].w;// allow flipping binormal?
                if (srcColors.Length > i) {
                    newColors[outi] = srcColors[i];
                }

                newUV4[i] = new Vector4(10,10,10,10); // original copy should be hidden.
            }
            {
                newVertices[i] = srcVertices[i];
                newNormals[i] = srcNormals[i];
                newTangents[i] = srcTangents[i];
                newBoneWeights[i] = srcBoneWeights[i];
                newUV1[i].x = srcUV[i].x;
                newUV1[i].y = srcUV[i].y;
                if (srcColors.Length > i) {
                    newColors[i] = srcColors[i];
                }
                if (i < srcUV2.Count) {
                    newUV2[i] = srcUV2[i];
                }
            }
        }

        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.boneWeights = newBoneWeights;
        if (newColors != null) {
            newMesh.colors = newColors;
        }
        newMesh.SetUVs(0, new List<Vector4>(newUV1));
        if (newUV2 != null) {
            newMesh.SetUVs(1, new List<Vector4>(newUV2));
        }
        newMesh.SetUVs(2, new List<Vector4>(newUV3));
        newMesh.SetUVs(3, new List<Vector4>(newUV4));
        newMesh.subMeshCount = sourceMesh.subMeshCount;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            // Material not in dict, add it
            newMesh.SetTriangles(outIndices[i], i);
        }
        for (int i = 0; i < sourceMesh.blendShapeCount; i++) {
            var blendShapeName = sourceMesh.GetBlendShapeName (i);
            var blendShapeFrameCount = sourceMesh.GetBlendShapeFrameCount (i);
            for (int frameIndex = 0; frameIndex < blendShapeFrameCount; frameIndex++) {
                float weight = sourceMesh.GetBlendShapeFrameWeight (i, frameIndex);
                Vector3 [] deltaVertices = new Vector3 [pristineVertexSize];
                Vector3 [] deltaNormals = new Vector3 [pristineVertexSize];
                Vector3 [] deltaTangents = new Vector3 [pristineVertexSize];
                sourceMesh.GetBlendShapeFrameVertices (i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                Array.Resize(ref deltaVertices, size);
                Array.Resize(ref deltaNormals, size);
                Array.Resize(ref deltaTangents, size);
                newMesh.AddBlendShapeFrame (blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
            }
        }
        newMesh.bounds = sourceMesh.bounds;
        newMesh.bindposes = newBindPoses;
        newMesh.name = sourceMesh.name + "_GPUSkinBaked";
        Undo.RecordObject (renderer, "Embedded bone informaion");
        renderer.sharedMesh = newMesh;
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        string fileName = pathToGenerated + "/ZZskinbaked_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (renderer.sharedMesh, fileName);
        AssetDatabase.SaveAssets ();
        //AssetDatabase.CreateAsset(newMesh, "Assets/tmpMesh.asset");
    }
}

#endif