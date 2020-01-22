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
public class SimplifiedGPUSkinning : MonoBehaviour {
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

    static long computeVertexHash(Vector3 v) {
        return (long)(v.x * 10000) + ((long)(v.y * 10000) * 10000) + ((long)(v.z * 10000) * 10000 * 10000);
    }

    class BodyFeatures {

        public const float SKIRT_BONE = 2;
        public const float TAIL_BONE = 3;
        public const float FIRST_NONHIPS = 4;
        public const float HAIR_BONE = FIRST_NONHIPS;
        public const float LEFT_EAR = 5;
        public const float RIGHT_EAR = 6;
        int rootSkirt = -1;
        int rootTail = -1;
        int rootHair = -1;
        int leftEar = -1;
        int rightEar = -1;
        HashSet<int> skirts = new HashSet<int>();
        HashSet<int> tails = new HashSet<int>();
        HashSet<int> hairs = new HashSet<int>();

        HashSet<int> specials = new HashSet<int>();

        public Dictionary<Transform, int> boneToIndex = new Dictionary<Transform, int>();

        public Animator parentAnim;

        public BodyFeatures(SkinnedMeshRenderer renderer) {
            parentAnim = renderer.transform.GetComponentInParent<Animator>();
            for (int i = 0; i < renderer.bones.Length; i++) {
                Transform t = renderer.bones[i];
                boneToIndex.Add(t, i);
                String lowerName = t.name.ToLower();
                if (lowerName.Contains("skirt")) {
                    if (skirts.Count == 0) {
                        rootSkirt = i;
                    }
                    skirts.Add(i);
                    specials.Add(i);
                    Debug.Log("Bone " + i + ": " + t.name + " SKIRT", t);
                } else if (lowerName.Contains("tail")) {
                    if (tails.Count == 0) {
                        rootTail = i;
                    }
                    tails.Add(i);
                    specials.Add(i);
                    Debug.Log("Bone " + i + ": " + t.name + " TAIL", t);
                } else if (lowerName.Contains("hair")) {
                    if (hairs.Count == 0) {
                        rootHair = i;
                    }
                    hairs.Add(i);
                    specials.Add(i);
                    Debug.Log("Bone " + i + ": " + t.name + " HAIR", t);
                } else if (lowerName.Contains("ear")) {
                    if (lowerName.Contains('l') & leftEar == -1) {
                        leftEar = i;
                        Debug.Log("Bone " + i + ": " + t.name + " LEFTEAR", t);
                    } else {
                        rightEar = i;
                        Debug.Log("Bone " + i + ": " + t.name + " RIGHTEAR", t);
                    }
                } else {
                    Debug.Log("Bone " + i + ": " + t.name, t);
                }
            }
            if (leftEar != -1) {
                specials.Add(leftEar);
            }
            if (rightEar != -1) {
                specials.Add(rightEar);
            }
        }

        const bool SIMPLIFIED_TRIS = true;

// TODO: Compatibility: duplicate faces and attach them to vertices with outrageous UV3/UV4
// then cull those faces if we are using our shader.
        public void selectBone(Vector3[] positions, BoneWeight[] inWeight, int[] whichBone, float[] parentWeight, out float boneType) {
            boneType = 1;
            /*
                        float 
            BoneWeight weight = inWeight[0]; // ignore other vertices for now?
            if (!needsSpecialWeighting(weight)) {
                weight = inWeight[1];
                if (!needsSpecialWeighting(weight)) {
                    weight = inWeight[2];
                }
            }
 */
            bool found = false;
            for (int k = 0; k < 3; k++) {
                BoneWeight weight = inWeight[k];
                int outBone;
                float specialWeight = specialWeightingAmount(weight, out outBone);
                parentWeight[k] = 1 - specialWeight;
                if (!found && specialWeight > 0) {
                    found = true;
                    //Vector3 posNormalized = positions[k].normalized;
                    Vector3 posXzNormalized = new Vector3(positions[k].x, 0, positions[k].z);
                    posXzNormalized.Normalize();

                    float leftRightness = posXzNormalized.x;
                    float frontness = posXzNormalized.z;

                    // logic here
                    if (skirts.Contains(outBone)) {
                        boneType = SKIRT_BONE;
                        whichBone[0] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.LeftUpperLeg)];
                        whichBone[1] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.RightUpperLeg)];
                        if (rootTail != -1 && frontness < 0 && leftRightness < 0.5 && leftRightness > -0.5) {
                            whichBone[2] = rootTail;
                        } else if (leftRightness < -0.5) {
                            whichBone[2] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.LeftHand)];
                        } else if (leftRightness > 0.5) {
                            whichBone[2] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.RightHand)];
                        } else {
                            whichBone[2] = rootSkirt;
                        }
                    } else if (tails.Contains(outBone)) {
                        whichBone[0] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.LeftHand)];
                        whichBone[1] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.RightHand)];
                        whichBone[2] = rootTail;
                        boneType = TAIL_BONE;
                    } else if (hairs.Contains(outBone)) {
                        whichBone[0] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.LeftLowerArm)];
                        whichBone[1] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.RightLowerArm)];
                        whichBone[2] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.Head)];
                        boneType = HAIR_BONE;
                    } else if (leftEar == outBone) {
                        whichBone[0] = leftEar;
                        whichBone[1] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.LeftHand)];
                        whichBone[2] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.Head)];
                        boneType = LEFT_EAR;
                    } else if (rightEar == outBone) {
                        whichBone[0] = rightEar;
                        whichBone[1] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.RightHand)];
                        whichBone[2] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.Head)];
                        boneType = RIGHT_EAR;
                    }
                    if (SIMPLIFIED_TRIS) {
                        if (boneType == HAIR_BONE) {
                            if (leftRightness < 0) {
                                whichBone[1] = whichBone[0];
                            }
                            whichBone[0] = boneToIndex[parentAnim.GetBoneTransform(HumanBodyBones.Head)];
                        } else if (boneType == TAIL_BONE) {
                            whichBone[0] = whichBone[2]; // tail root and right hand only
                        }
                        whichBone[2] = whichBone[0];
                    }
                }
            }
        }

        public bool needsSpecialWeighting(int boneIndex) {
            return specials.Contains(boneIndex);
        }
        public float specialWeightingAmount(BoneWeight weight, out int whichBone) {
            float totalWeight = 0;
            whichBone = 0;
            if (weight.weight3 > 0.0) {
                if (needsSpecialWeighting(weight.boneIndex3)) {
                    totalWeight += weight.weight3;
                    whichBone = weight.boneIndex3;
                }
            }
            if (weight.weight2 > 0.0) {
                if (needsSpecialWeighting(weight.boneIndex2)) {
                    totalWeight += weight.weight2;
                    whichBone = weight.boneIndex2;
                }
            }
            if (weight.weight1 > 0.0) {
                if (needsSpecialWeighting(weight.boneIndex1)) {
                    totalWeight += weight.weight1;
                    whichBone = weight.boneIndex1;
                }
            }
            if (weight.weight0 > 0.0) {
                if (needsSpecialWeighting(weight.boneIndex0)) {
                    totalWeight += weight.weight0;
                    whichBone = weight.boneIndex0;
                }
            }
            return totalWeight;
        }
        public bool needsSpecialWeighting(BoneWeight weight) {
            int outBone;
            return specialWeightingAmount(weight, out outBone) > 0;
        }
    }

    //[MenuItem("GameObject/Create Mesh")]
    [MenuItem("CONTEXT/SkinnedMeshRenderer/Simplified GPU Skinning : LMTx")]
    public static void SimplifiedGPUSkinning_(MenuCommand command)
    {
        SkinnedMeshRenderer renderer = command.context as SkinnedMeshRenderer;
        Mesh sourceMesh = renderer.sharedMesh;

        BodyFeatures bodyFeatures = new BodyFeatures(renderer);
        //Transform head = parentAnim.GetBoneTransform(HumanBodyBones.Head);

        /*BoneWeight[] srcWeights = sourceMesh.boneWeights;
        for (int i = 0; i < srcWeights.Length && i < 20; i++) {
            BoneWeight bw = srcWeights[i];
            Debug.Log("weight[" + i + "]: <"
                      + bw.boneIndex0 + "=" + bw.weight0 + ","
                      + bw.boneIndex1 + "=" + bw.weight1 + ","
                      + bw.boneIndex2 + "=" + bw.weight2 + ","
                      + bw.boneIndex3 + "=" + bw.weight3 + ">", sourceMesh);
        }*/
        Vector2 [] srcUV = sourceMesh.uv;
        List<Vector4> srcUV2 = new List<Vector4> ();
        sourceMesh.GetUVs(1, srcUV2);
        Vector3 [] srcVertices = sourceMesh.vertices;
        Color[] srcColors = sourceMesh.colors;
        Vector3 [] srcNormals = sourceMesh.normals;
        Vector4 [] srcTangents = sourceMesh.tangents;
        BoneWeight [] srcBoneWeights = sourceMesh.boneWeights;

        bool NON_PRISTINE_COPY = true;

        int size = 0;
        int [] pristineVertexMapping = new int[sourceMesh.vertices.Length];
        for (int i = 0; i < sourceMesh.vertices.Length; i++) {
            if (bodyFeatures.needsSpecialWeighting(srcBoneWeights[i])) {
                pristineVertexMapping[i] = -1;
                if (NON_PRISTINE_COPY) {
                    size++;
                }
            } else {
                pristineVertexMapping[i] = size;
                size++;
            }
        }
        int pristineVertexSize = size;
        int numNonPristineTris = 0;
        int[][] outIndices = new int[sourceMesh.subMeshCount][];
        int[] pristineIndicesLength = new int[sourceMesh.subMeshCount];
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            // Material not in dict, add it
            int[] curIndices = (int[])sourceMesh.GetTriangles(i).Clone();
            int origLength = curIndices.Length;
            pristineIndicesLength[i] = origLength;
            for (int curIdx = 0; curIdx < curIndices.Length; curIdx += 3) {
                int srcTriangle = curIdx;
                bool pristine = true;
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    if (pristineVertexMapping[srcVert] == -1) {
                        pristine = false;
                    }
                }
                if (!pristine) {
                    size += 3;
                    numNonPristineTris++;
                }
            }
            if (NON_PRISTINE_COPY) {
                Array.Resize(ref curIndices, origLength + 3 * numNonPristineTris);
                int outIdx = origLength;
                for (int curIdx = 0; curIdx < origLength; curIdx += 3) {
                    int srcTriangle = curIdx;
                    bool pristine = true;
                    for (int k = 0; k < 3; k++) {
                        int srcIndex = curIdx + k;
                        int srcVert = curIndices[srcIndex];
                        if (pristineVertexMapping[srcVert] == -1) {
                            pristine = false;
                        }
                    }
                    if (!pristine) {
                        for (int k = 0; k < 3; k++) {
                            int srcIndex = curIdx + k;
                            int srcVert = curIndices[srcIndex];
                            curIndices[outIdx + k] = srcVert;
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
        var newColors = new Color[size];
        for (int i = 0; i < sourceMesh.vertices.Length; i++) {
            int outi = pristineVertexMapping[i];
            if (NON_PRISTINE_COPY && outi == -1) {
                outi = i;
                newUV4[outi] = new Vector4(10,10,10,10);
            }
            if (outi != -1) {
                newVertices[outi] = srcVertices[i];
                newNormals[outi] = srcNormals[i];
                newTangents[outi] = srcTangents[i];
                newBoneWeights[outi] = srcBoneWeights[i];
                newUV1[outi].x = srcUV[i].x;
                newUV1[outi].y = srcUV[i].y;
                if (srcColors.Length > i) {
                    newColors[outi] = srcColors[i];
                }
                if (i < srcUV2.Count) {
                    newUV2[outi] = srcUV2[i];
                }
            }
        }

        var newIndices = new List<int[]>();
        var newBones = renderer.bones;
        var newBindPoses = sourceMesh.bindposes;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [newBindPoses.Length];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = newBindPoses [i].inverse;
        }
        int[] whichBone = new int[3];
        float[] parentWeight = new float[3];
        BoneWeight[] bws = new BoneWeight[3];
        Vector3[] positions = new Vector3[3];
        int j = pristineVertexSize;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            // Material not in dict, add it
            int[] curIndices = outIndices[i];
            for (int curIdx = 0; curIdx < pristineIndicesLength[i]; curIdx += 3) {
                bool pristine = true;
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    bws[k] = srcBoneWeights[srcVert];
                    positions[k] = srcVertices[srcVert];
                    if (pristineVertexMapping[srcVert] == -1) {
                        pristine = false;
                    } else  {
                    }
                }
                if (pristine) {
                    for (int k = 0; k < 3; k++) {
                        int srcIndex = curIdx + k;
                        int srcVert = curIndices[srcIndex];
                        curIndices[srcIndex] = pristineVertexMapping[srcVert];
                    }
                    // original vertex data already copied
                    // indices already copied.
                    continue;
                }
                /*else {
                    curIndices[curIdx] = 0;
                    curIndices[curIdx + 1] = 0;
                    curIndices[curIdx + 2] = 0;
                    continue;
                }*/
                /////whichBone
                float boneType;
                bodyFeatures.selectBone(positions, bws, whichBone, parentWeight, out boneType);
                //TODO: make sure only 2 bones show up here. if 3 we have a problem....
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    int dstVert = j + k;
                    newVertices [dstVert] = inverseBindPoses [whichBone[k]].MultiplyPoint (new Vector3 (0, 0, 0));
                    newNormals [dstVert] = inverseBindPoses [whichBone[k]].MultiplyVector (new Vector3 (0, 0, 1));
                    Vector3 newTang = inverseBindPoses[whichBone[k]].MultiplyVector(new Vector3(1, 0, 0));
                    newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
                    newBoneWeights [dstVert] = new BoneWeight ();
                    newBoneWeights [dstVert].boneIndex0 = whichBone[k];
                    newBoneWeights [dstVert].weight0 = 1.0f;
                    //Debug.Log ("i:" + srcIndex + " v:" + srcVert + "->" + dstVert + ": Adding vertex " + newBindPoses [sortedTriWeights [k].Key] + " to vertex:" + d(newVertices [dstVert]) + " to normal:" + d(newNormals [dstVert]) + " to tangent:" + d(newTangents [dstVert]) + " to boneWeight:" + newBoneWeights [dstVert].boneIndex0);
                }
                // TODO: Figure out bones and weights for the 3 available bones

                float[] bweights = new float [3];
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices [srcIndex];
                    int dstVert = j + k;
                    Vector3 srcPosition = srcVertices[srcVert];
                    Vector3 srcTangent = new Vector3(srcTangents [srcVert].x, srcTangents [srcVert].y, srcTangents [srcVert].z);
                    Vector3 srcNormal = srcNormals [srcVert];
                    int baseBone = whichBone[2];
                    if (boneType < BodyFeatures.FIRST_NONHIPS) {
                        baseBone = bodyFeatures.boneToIndex[bodyFeatures.parentAnim.GetBoneTransform(HumanBodyBones.Hips)];
                    }
                    srcPosition = sourceMesh.bindposes[baseBone].MultiplyPoint(srcPosition);
                    srcTangent = sourceMesh.bindposes[baseBone].MultiplyVector(srcTangent);
                    srcNormal = sourceMesh.bindposes[baseBone].MultiplyVector(srcNormals [srcVert]);

                    newUV1 [dstVert] = new Vector4 ();
                    if (srcUV.Length > srcVert) {
                        newUV1[dstVert].x = srcUV[srcVert].x;
                        newUV1[dstVert].y = srcUV[srcVert].y;
                    } else {
                        newUV1[dstVert].x = 0;
                        newUV1[dstVert].y = 0;
                    }
                    newUV1 [dstVert].z = srcTangent.x;
                    newUV1 [dstVert].w = srcTangent.y;
                    if (srcVert < srcUV2.Count) {
                        newUV2[dstVert] = srcUV2[srcVert];
                    }
                    newUV3[dstVert] = new Vector4();
                    newUV3[dstVert].x = srcPosition.x;
                    newUV3[dstVert].y = srcPosition.y;
                    newUV3[dstVert].z = srcPosition.z;
                    newUV3[dstVert].w = srcTangent.z;
                    newUV4[dstVert] = new Vector4();
                    newUV4[dstVert].x = srcNormal.x;
                    newUV4[dstVert].y = srcNormal.y;
                    newUV4[dstVert].z = srcNormal.z;
                    newUV4[dstVert].w = boneType * srcTangents [srcVert].w;// allow flipping binormal?
                    newColors[dstVert] = new Color();
                    if (srcColors.Length > srcVert) {
                        newColors[dstVert].r = (float)(srcColors[srcVert].r);
                        newColors[dstVert].g = (float)(srcColors[srcVert].g);
                        newColors[dstVert].b = (float)(srcColors[srcVert].b);
                    }
                    newColors [dstVert].a = (float)parentWeight[k];
                    curIndices[srcIndex] = dstVert;
                    //Debug.Log ("i:" + srcIndex + " v:" + srcVert + "->" + dstVert + ": Adding vertex " + d(srcPosition) + "/" + d(srcTangent) + "/" + d(srcNormal) + " to uv1:" + d(newUV1 [dstVert]) + " to uv2:" + d(newUV2 [dstVert]) + " to uv3:" + d(newUV3 [dstVert]) + " to uv4:" + d(newUV4 [dstVert]));
                }
                j += 3;
            }
            newIndices.Add(curIndices);
        }
        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.boneWeights = newBoneWeights;
        newMesh.colors = newColors;
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
            newMesh.SetTriangles(newIndices[i], i);
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