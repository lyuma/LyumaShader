#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ConvertMeshToGPUSkinned : MonoBehaviour {
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

    public class WeightSum {
        public List<float>  weights;
        public List<int> indices;

        public WeightSum() {
            weights = new List<float>();
            indices = new List<int>();
        }

        public void addWeight(int index, float weight) {
            for (int i = 0; i < indices.Count; i++) {
                if (indices[i] == index) {
                    weights[i] += weight;
                    return;
                }
            }
            indices.Add(index);
            weights.Add(weight);
            Debug.Log("Weights full: " + index + "/" + weight);
        }
        public void addWeight(BoneWeight bw) {
            addWeight(bw.boneIndex0, bw.weight0);
            addWeight(bw.boneIndex1, bw.weight1);
            addWeight(bw.boneIndex2, bw.weight2);
            addWeight(bw.boneIndex3, bw.weight3);
        }

        public BoneWeight getPrimaryBoneWeight(int ignore1, int ignore2) {
            BoneWeight bw = new BoneWeight();
            for (int i = 0; i < indices.Count; i++) {
                if (weights[i] > bw.weight0 && indices[i] != ignore1 && indices[i] != ignore2) {
                    bw.weight0 = weights[i];
                    bw.boneIndex0 = indices[i];
                }
            }
            return bw;
        }
    }

    static long computeVertexHash(Vector3 v) {
        return (long)(v.x * 10000) + ((long)(v.y * 10000) * 10000) + ((long)(v.z * 10000) * 10000 * 10000);
    }

    //[MenuItem("GameObject/Create Mesh")]
    [MenuItem("CONTEXT/SkinnedMeshRenderer/Mesh to lite geom shader skinned")]
    public static void ConvertMeshToGPUSkinned_(MenuCommand command)
    {
        SkinnedMeshRenderer renderer = command.context as SkinnedMeshRenderer;
        Mesh sourceMesh = renderer.sharedMesh;
        for (int i = 0; i < renderer.bones.Length; i++) {
            Transform t = renderer.bones[i];
            Debug.Log("Bone " + i + ": " + t.name, t);
        }
        BoneWeight[] srcWeights = sourceMesh.boneWeights;
        for (int i = 0; i < srcWeights.Length && i < 20; i++) {
            BoneWeight bw = srcWeights[i];
            Debug.Log("weight[" + i + "]: <"
                      + bw.boneIndex0 + "=" + bw.weight0 + ","
                      + bw.boneIndex1 + "=" + bw.weight1 + ","
                      + bw.boneIndex2 + "=" + bw.weight2 + ","
                      + bw.boneIndex3 + "=" + bw.weight3 + ">", sourceMesh);
        }
        int size = sourceMesh.triangles.Length / 3;
        Vector2 [] srcUV = sourceMesh.uv;
        Vector2[] srcUV2 = sourceMesh.uv2;
        Vector3 [] srcVertices = sourceMesh.vertices;
        Color32[] srcColors = sourceMesh.colors32;
        Vector3 [] srcNormals = sourceMesh.normals;
        Vector4 [] srcTangents = sourceMesh.tangents;
        BoneWeight [] srcBoneWeights = sourceMesh.boneWeights;

        Dictionary<long, WeightSum> vertexHashToBones = new Dictionary<long, WeightSum>();
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            // Material not in dict, add it
            int[] curIndices = sourceMesh.GetTriangles(i);
            for (int curIdx = 0; curIdx < curIndices.Length; curIdx += 3) {
                int srcTriangle = curIdx;
                Dictionary<int, float> triWeights = new Dictionary<int, float> ();
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    long vhash = computeVertexHash (srcVertices [srcVert]);
                    for (int kk = 0; kk < 3; kk++) {
                        int targIndex = curIdx + kk;
                        int targVert = curIndices[targIndex];
                        WeightSum val = null;
                        if (!vertexHashToBones.TryGetValue(vhash, out val)) {
                            vertexHashToBones[vhash] = val = new WeightSum();
                        }
                        val.addWeight(srcBoneWeights[targVert]);
                    }
                }
            }
        }

        Mesh newMesh = new Mesh();
        var newVertices = new Vector3[size * 3];
        var newNormals = new Vector3[size * 3];
        var newTangents = new Vector4[size * 3];
        var newBoneWeights = new BoneWeight[size * 3];
        var newUV1 = new Vector4[size * 3];
        var newUV2 = new Vector4[size * 3];
        var newUV3 = new Vector4[size * 3];
        var newUV4 = new Vector4[size * 3];
        var newColors = new Color[size * 3];
        var newIndices = new List<int[]>();
        var newBones = renderer.bones;
        var newBindPoses = sourceMesh.bindposes;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [newBindPoses.Length];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = newBindPoses [i].inverse;
        }
        int j = 0;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            // Material not in dict, add it
            int[] curIndices = (int[])sourceMesh.GetTriangles(i).Clone();
            for (int curIdx = 0; curIdx < curIndices.Length; curIdx += 3, j += 3) {
                int srcTriangle = curIdx;
                int dstTriangle = curIdx;
                BoneWeight[] bws = new BoneWeight[3];
                WeightSum[] val = new WeightSum[3];
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[curIdx + k];
                    int dstVert = j + k;
                    long vhash = computeVertexHash(srcVertices[srcVert]);
                    WeightSum xval = null;
                    vertexHashToBones.TryGetValue (vhash, out xval);
                    val[k] = xval;
                }
                for (int k = 0; k < 3; k++) {
                    bws [k] = val[k].getPrimaryBoneWeight (-1, -1);
                }
                int indToReplace1 = bws[1].boneIndex0;
                int indToReplace2 = bws[2].boneIndex0;
                if (bws[1].boneIndex0 == bws[2].boneIndex0 || bws[0].boneIndex0 == bws[1].boneIndex0) {
                    float bestWeight = 0;
                    for (int k = 0; k < 3; k++) {
                        BoneWeight candidate = val[k].getPrimaryBoneWeight (indToReplace1, -1);
                        if (candidate.weight0 > bestWeight) { //  && candidate.weight0 >= 0.125
                            bws[1] = candidate;
                            bestWeight = candidate.weight0;
                        }
                    }
                }
                if (bws[0].boneIndex0 == bws[2].boneIndex0 || bws[1].boneIndex0 == bws[2].boneIndex0) {
                    float bestWeight = 0;
                    for (int k = 0; k < 3; k++) {
                        BoneWeight candidate = val[k].getPrimaryBoneWeight (indToReplace2, bws[1].boneIndex0);
                        if (candidate.weight0 > bestWeight) { //  && candidate.weight0 >= 0.125
                            bws[2] = candidate;
                            bestWeight = candidate.weight0;
                        }
                    }
                }
                if (bws[1].weight0 == 0) {
                    bws[1] = bws[0];
                }
                if (bws[2].weight0 == 0) {
                    bws[2] = bws[0];
                }
                //TODO: make sure only 2 bones show up here. if 3 we have a problem....
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    int dstVert = j + k;
                    newVertices [dstVert] = new Vector3 (0,0,0);//inverseBindPoses [sortedTriWeights [k].Key].MultiplyPoint (new Vector3 (0, 0, 0));
                    newNormals [dstVert] = new Vector3 (0, 0, 1);//inverseBindPoses [sortedTriWeights [k].Key].MultiplyVector (new Vector3 (0, 0, 1));
                    Vector3 newTang = new Vector3 (1, 0, 0);//inverseBindPoses[sortedTriWeights[k].Key].MultiplyVector(new Vector3(1, 0, 0));
                    newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
                    //debug: newVertices[dstVert] = (float)i * 0.0004f, 0.01 * curIdx / (float)curIndices.Length, (float)k * 0.0002f);
                    newBoneWeights [dstVert] = new BoneWeight ();
                    newBoneWeights [dstVert].boneIndex0 = bws [k].boneIndex0;
                    newBoneWeights [dstVert].weight0 = 1.0f;
                    //Debug.Log ("i:" + srcIndex + " v:" + srcVert + "->" + dstVert + ": Adding vertex " + newBindPoses [sortedTriWeights [k].Key] + " to vertex:" + d(newVertices [dstVert]) + " to normal:" + d(newNormals [dstVert]) + " to tangent:" + d(newTangents [dstVert]) + " to boneWeight:" + newBoneWeights [dstVert].boneIndex0);
                }
                // TODO: Figure out bones and weights for the 3 available bones

                float[] bweights = new float [3];
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices [srcIndex];
                    int dstVert = j + k;

                    BoneWeight bw = srcBoneWeights [srcVert];
                    float weightsum = 0;
                    for (int bwi = 0; bwi < 3; bwi++) {
                        bweights [bwi] = 0;
                        if (bwi >= 1 && bws [bwi].boneIndex0 == bws [0].boneIndex0) {
                            continue;
                        }
                        if (bwi == 2 && bws [bwi].boneIndex0 == bws [1].boneIndex0) {
                            continue;
                        }
                        if (bw.boneIndex0 == bws [bwi].boneIndex0) {
                            bweights [bwi] += bw.weight0;
                        }
                        if (bw.boneIndex1 == bws [bwi].boneIndex0) {
                            bweights [bwi] += bw.weight1;
                        }
                        if (bw.boneIndex2 == bws [bwi].boneIndex0) {
                            bweights [bwi] += bw.weight2;
                        }
                        if (bw.boneIndex3 == bws [bwi].boneIndex0) {
                            bweights [bwi] += bw.weight3;
                        }
                        weightsum += bweights [bwi];
                    }
                    Vector3 bweightvec = new Vector3 (bweights [0], bweights [1], bweights [2]);
                    bweightvec /= weightsum;
                    //bweightvec.x = 1 - bweightvec.y - bweightvec.z;

                    /*Matrix4x4 inverseBindPose = new Matrix4x4 ();
                    for (int bwi = 0; bwi < 16; bwi++) {
                        inverseBindPose [bwi] += inverseBindPoses [bws [0].boneIndex0][bwi] * bweightvec.x;
                        inverseBindPose [bwi] += inverseBindPoses [bws [1].boneIndex0][bwi] * bweightvec.y;
                        inverseBindPose [bwi] += inverseBindPoses [bws [2].boneIndex0][bwi] * bweightvec.z;
                    }
                    Vector3 srcPosition = inverseBindPose.MultiplyPoint (srcVertices [srcVert]);
                    Vector3 srcTangent = inverseBindPose.MultiplyPoint(new Vector3(
                        srcTangents[srcVert].x,
                        srcTangents[srcVert].y,
                        srcTangents[srcVert].z));
                    Vector3 srcNormal = inverseBindPose.MultiplyPoint(srcNormals[srcVert]);*/
                    Vector3 srcPosition = srcVertices[srcVert];
                    Vector4 srcTangent = srcTangents [srcVert];
                    Vector3 srcNormal = srcNormals [srcVert];

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
                    newUV2[dstVert] = new Vector4();
                    if (srcUV2.Length > srcVert) {
                        newUV2 [dstVert].x = srcUV2 [srcVert].x;
                        newUV2 [dstVert].y = srcUV2 [srcVert].y;
                    } else {
                        newUV2[dstVert].x = 0;
                        newUV2[dstVert].y = 0;
                    }
                    newUV2[dstVert].z = 0;// XXX;
                    newUV2[dstVert].w = 1;// XXX;
                    newUV3[dstVert] = new Vector4();
                    newUV3[dstVert].x = srcPosition.x;
                    newUV3[dstVert].y = srcPosition.y;
                    newUV3[dstVert].z = srcPosition.z;
                    newUV3[dstVert].w = srcTangent.z;
                    newUV4[dstVert] = new Vector4();
                    newUV4[dstVert].x = srcNormal.x;
                    newUV4[dstVert].y = srcNormal.y;
                    newUV4[dstVert].z = srcNormal.z;
                    newUV4[dstVert].w = (bws[k].boneIndex0 + 0.001f) * srcTangents[srcVert].w;// allow flipping binormal?
                    newColors[dstVert] = new Color();
                    newColors[dstVert].r = (float)(bweightvec.x);
                    newColors[dstVert].g = (float)(bweightvec.y);
                    newColors[dstVert].b = (float)(bweightvec.z);
                    if (srcColors.Length > srcVert) {
                        newColors [dstVert].a = (float)(((int)(srcColors [srcVert].r * 7)) |
                                               ((int)(srcColors [srcVert].g * 7) << 3) |
                                               ((int)(srcColors [srcVert].b * 7) << 6));
                    } else {
                        newColors [dstVert].a = 0;
                    }
                    curIndices[srcIndex] = dstVert;
                    //Debug.Log ("i:" + srcIndex + " v:" + srcVert + "->" + dstVert + ": Adding vertex " + d(srcPosition) + "/" + d(srcTangent) + "/" + d(srcNormal) + " to uv1:" + d(newUV1 [dstVert]) + " to uv2:" + d(newUV2 [dstVert]) + " to uv3:" + d(newUV3 [dstVert]) + " to uv4:" + d(newUV4 [dstVert]));
                }
            }
            newIndices.Add(curIndices);
        }
        if (newVertices.Length > 65535) {
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.boneWeights = newBoneWeights;
        newMesh.colors = newColors;
        newMesh.SetUVs(0, new List<Vector4>(newUV1));
        newMesh.SetUVs(1, new List<Vector4>(newUV2));
        newMesh.SetUVs(2, new List<Vector4>(newUV3));
        newMesh.SetUVs(3, new List<Vector4>(newUV4));
        newMesh.subMeshCount = sourceMesh.subMeshCount;
        for (int i = 0; i < sourceMesh.subMeshCount; i++)
        {
            // Material not in dict, add it
            newMesh.SetTriangles(newIndices[i], i);
        }
        newMesh.bounds = sourceMesh.bounds;
        newMesh.bindposes = newBindPoses;
        newMesh.name = sourceMesh.name + "_GPUSkinBaked";
        Undo.RecordObject (renderer, "Switched renderer to GPU skinned");
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
