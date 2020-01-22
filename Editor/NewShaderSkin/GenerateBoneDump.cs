//GenerateBoneMesh
//Script

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//using HierarchyDict = System.Collections.Generic.Dictionary<string, UnityEngine.Transform>;
//using BoneTransformDict = System.Collections.Generic.Dictionary<string, Tuple<UnityEngine.Transform, string>>;

public class GenerateSoftSkinnedMesh : MonoBehaviour {
    public static string d (Vector2 vector)
    {
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
    private static byte [] SimplifyBoneName (string name)
    {
        byte [] outb = new byte [4];
        if (name == null || name.Length == 0) {
            return outb;
        }
        outb[0] = (byte) name[0]; // often L or R for left/right
        int nextCapital = 1;
        if (name.Length > 1 && (name[name.Length - 1] == 'L' || name[name.Length - 1] == 'R')) {
            outb [0] = (byte)name [name.Length - 1];
            nextCapital = 0;
        }
        // find any capital letter after position 1.
        for (; nextCapital < name.Length && !char.IsUpper(name[nextCapital]) && nextCapital > 0 && name[nextCapital - 1] != ' '; nextCapital++) {}
        if (nextCapital == name.Length) {
            nextCapital = 1;
        }
        for (int i = 0; i < 3 && nextCapital < name.Length; i++, nextCapital++) {
            outb [i + 1] = (byte)name[nextCapital];
        }
        Debug.Log ("simplify name: " + name + " -> " + (char)outb [0] + (char)outb [1] + (char)outb [2] + (char)outb [3]);
        return outb;
    }

    static int getSibling(string xname, Dictionary<string, int> bonesByName) {
        // xname = name of bone to lookup.
        int lastUnderscore = xname.LastIndexOf ('_');
        if (lastUnderscore == -1) {
            return -1;
        }
        int secondLastUnderscore = xname.LastIndexOf ('_', lastUnderscore - 1);
        if (secondLastUnderscore == -1) {
            return -1;
        }
        string number1str = xname.Substring (secondLastUnderscore + 1, lastUnderscore - secondLastUnderscore - 1);
        string number2str = xname.Substring (lastUnderscore + 1);
        int number1, number2;
        if (!int.TryParse (number1str, out number1) || !int.TryParse(number2str, out number2)) {
            return -1;
        }
        string basepart = xname.Substring (0, secondLastUnderscore + 1);
        string teststr = basepart + number1 + "_" + (number2 + 1);
        if (bonesByName.ContainsKey(teststr)) {
            Debug.Log ("Found bone " + teststr + " for bone " + xname);
            return bonesByName [teststr];
        }
        teststr = basepart + number1 + "_" + "0";
        if (bonesByName.ContainsKey (teststr)) {
            Debug.Log ("Found rewind bone " + teststr + " for bone " + xname);
            return bonesByName [teststr];
        }
        return -1;
    }

    //[MenuItem("GameObject/Create Soft Skinned")]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Generate soft skinned")]
    public static void GenerateSoftSkinned_ (MenuCommand command)
    {
        int USE_BLEND_SHAPES = 1; // 1 == same material; 2 = separate materials.
        int CLOTH_SUPPORT = 0; // requires NUM_BONE_MODES=1
        int NUM_BONE_MODES = 1; //3;
        SkinnedMeshRenderer renderer = command.context as SkinnedMeshRenderer;
        Mesh sourceMesh = renderer.sharedMesh;
        int size1 = renderer.bones.Length * NUM_BONE_MODES;
        int size2 = sourceMesh.blendShapeCount;
        Dictionary<Transform, int> srcBoneIdxMap = new Dictionary<Transform, int> ();
        Dictionary<string, int> bonesByName = new Dictionary<string, int> ();
        for (int i = 0; i < renderer.bones.Length; i++) {
            srcBoneIdxMap [renderer.bones [i]] = i;
            bonesByName [renderer.bones [i].name] = i;
        }
        int rootBoneIndex = srcBoneIdxMap[renderer.rootBone]; //Array.FindIndex (renderer.bones, b => b == renderer.rootBone);

        int size = USE_BLEND_SHAPES >= 1 ? size1 + size2 : size1;

        Mesh newMesh = new Mesh ();
        var newVertices = new Vector3 [size];
        var newNormals = new Vector3 [size];
        var newTangents = new Vector4 [size];
        var newBoneWeights = new BoneWeight [size];
        var newUV1 = new Vector4[size];
        var newColors = new Color [size];
        var curIndices = new int [USE_BLEND_SHAPES == 1 ? (renderer.bones.Length + sourceMesh.blendShapeCount) * 3 : renderer.bones.Length * 3];
        var blendIndices = curIndices;
        var newBones = renderer.bones;
        var newBindPoses = sourceMesh.bindposes;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [newBindPoses.Length];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = newBindPoses [i].inverse;
        }
        for (int i = 0; i < size1; i++) {
            int boneIndex = i / NUM_BONE_MODES;
            Transform t = renderer.bones [boneIndex];
            int dstVert = i;
            Debug.Log ("Bone " + boneIndex + ": " + t.name, t);
            Vector3 newTang;
            if (CLOTH_SUPPORT == 0) {
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
            } else {
                if (i % NUM_BONE_MODES == 0) {
                    // Generate Bone transform matrix
                    newVertices [dstVert] = inverseBindPoses [boneIndex].MultiplyPoint (new Vector3 (0, 0, 0));
                    newNormals [dstVert] = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, 0, 1));
                    newTang = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, -1, 0));
                } else if (i % NUM_BONE_MODES == 1) {
                    // Generate Bone transform matrix
                    newVertices [dstVert] = inverseBindPoses [boneIndex].MultiplyPoint (new Vector3 (0.1f, 0.2f, 0));
                    newNormals [dstVert] = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, 0, 1));
                    newTang = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, -1, 0));
                } else {
                    // Generate Bone transform matrix
                    newVertices [dstVert] = inverseBindPoses [boneIndex].MultiplyPoint (new Vector3 (0, 0.2f, 0));
                    newNormals [dstVert] = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, 0, 1));
                    newTang = inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, -1, 0));
                }
            }
            newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
            newBoneWeights [dstVert] = new BoneWeight ();
            newBoneWeights [dstVert].boneIndex0 = boneIndex;
            newBoneWeights [dstVert].weight0 = 1.0f;
            byte [] simpleName = SimplifyBoneName (t.name);
            newColors [dstVert] = new Color ();
            newColors [dstVert].r = simpleName [0];
            newColors [dstVert].g = simpleName [1];
            newColors [dstVert].b = simpleName [2];
            newColors [dstVert].a = simpleName [3];
            newUV1 [dstVert] = new Vector4 (0, boneIndex, t == renderer.rootBone ? -1 : srcBoneIdxMap[t.parent], 1);
            if (i % NUM_BONE_MODES == 0) {
                curIndices [boneIndex * 3] = i; // bind pose matrix
                if (CLOTH_SUPPORT == 1) {
                    /*
                    int firstIndex = 0;
                    int secondIndex = 0;
                    if (t != renderer.rootBone) {
                        firstIndex = srcBoneIdxMap [t.parent];
                    }
                    if (firstIndex == secondIndex) {
                        secondIndex = 2;
                    }
                    int siblingIndex = getSibling (t.name, bonesByName);
                    if (siblingIndex != -1) {
                        secondIndex = siblingIndex;
                    }
                    curIndices [boneIndex * 3 + 1] = firstIndex; // parent
                    curIndices [boneIndex * 3 + 2] = secondIndex; // grandparent or sibling
                    */
                    int firstIndex = boneIndex * 3 + 1;
                    int secondIndex = boneIndex * 3 + 2;
                    int siblingIndex = getSibling (t.name, bonesByName);
                    if (t != renderer.rootBone && siblingIndex != -1) {
                        secondIndex = srcBoneIdxMap [t.parent] * 3;
                        firstIndex = siblingIndex * 3;
                    }
                    curIndices [boneIndex * 3 + 1] = secondIndex; // parent
                    curIndices [boneIndex * 3 + 2] = firstIndex; // grandparent or sibling
                } else {
                    curIndices [boneIndex * 3 + 1] = i + (NUM_BONE_MODES >= 2 ? 1 : 0); // bone transform
                    curIndices [boneIndex * 3 + 2] = i + (NUM_BONE_MODES >= 3 ? 2 : 0); // unused - keep degenerate for Standard fallback
                }
            }
            Debug.Log ("i:" + boneIndex + " v:" + dstVert + ": Adding vertex " + d(newVertices [dstVert]) + "/" + d(newNormals [dstVert]) + "/" + d(newTangents [dstVert]));
        }
        if (USE_BLEND_SHAPES >= 1) {
            int baseTri = renderer.bones.Length;
            if (USE_BLEND_SHAPES == 2) {
                blendIndices = new int [sourceMesh.blendShapeCount * 3];
                baseTri = 0;
            }
            for (int ishape = 0; ishape < size2; ishape++) {
                string name = sourceMesh.GetBlendShapeName (ishape);
                int dstVert = ishape + size1;
                int dstTri = ishape + baseTri;
                Debug.Log ("Morph " + ishape + ": " + name);
                Vector3 newTang;
                newVertices [dstVert] = new Vector3 (0, 0, 0);
                newNormals [dstVert] = new Vector3 (0, 0, 1);
                newTang = new Vector3 (1, 0, 0);
                newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
                newBoneWeights [dstVert] = new BoneWeight ();
                newBoneWeights [dstVert].boneIndex0 = rootBoneIndex;
                newBoneWeights [dstVert].weight0 = 1.0f;
                byte [] simpleName = SimplifyBoneName (name);
                newColors [dstVert] = new Color ();
                newColors [dstVert].r = simpleName [0];
                newColors [dstVert].g = simpleName [1];
                newColors [dstVert].b = simpleName [2];
                newColors [dstVert].a = simpleName [3];
                newUV1 [dstVert] = new Vector4 (1, ishape, 0, 1);
                blendIndices [dstTri * 3] = dstVert;
                blendIndices [dstTri * 3 + 1] = dstVert;
                blendIndices [dstTri * 3 + 2] = dstVert;
                Debug.Log ("i:" + ishape + " v:" + dstVert + ": Adding vertex " + d (newVertices [dstVert]) + "/" + d (newNormals [dstVert]) + "/" + d (newTangents [dstVert]));
            }
        }
        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.boneWeights = newBoneWeights;
        newMesh.colors = newColors;
        newMesh.SetUVs(0, new List<Vector4>(newUV1));
        if (USE_BLEND_SHAPES == 2) {
            newMesh.subMeshCount = 2;
            newMesh.SetTriangles (curIndices, 0);
            newMesh.SetTriangles (blendIndices, 1);
        } else{
            newMesh.subMeshCount = 1;
            newMesh.SetTriangles (curIndices, 0);
        }
        newMesh.bounds = sourceMesh.bounds;
        newMesh.bindposes = newBindPoses;
        newMesh.name = sourceMesh.name + "_BoneMesh";
        if (USE_BLEND_SHAPES >= 1) {
            Vector3 [] deltaVertices = new Vector3 [size];
            Vector3 [] deltaNormals = new Vector3 [size];
            Vector3 [] deltaTangents = new Vector3 [size];
            for (int ishape = 0; ishape < size2; ishape++) {
                deltaVertices [size1 + ishape].x = 1.0f;
                newMesh.AddBlendShapeFrame (sourceMesh.GetBlendShapeName (ishape), 1.0f, deltaVertices, deltaNormals, deltaTangents);
                deltaVertices [size1 + ishape].x = 0.0f;
            }
        }
        //newMesh.RecalculateBounds ();
        //newMesh.RecalculateNormals ();
        //newMesh.RecalculateTangents ();
        GameObject newGameObject = new GameObject ("BoneMesh");
        newGameObject.transform.parent = renderer.transform.parent;
        newGameObject.transform.localPosition = renderer.transform.localPosition;
        SkinnedMeshRenderer newRenderer = newGameObject.AddComponent<SkinnedMeshRenderer> ();
        newRenderer.updateWhenOffscreen = false;
        newRenderer.sharedMesh = newMesh;
        newRenderer.bones = renderer.bones;
        newRenderer.rootBone = renderer.rootBone;
        Material [] newMaterials = new Material [1];
        newMaterials[0] = renderer.sharedMaterials[0];
        newRenderer.sharedMaterials = newMaterials;
        newRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        newRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        newRenderer.receiveShadows = false;
        newRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        newRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        newRenderer.updateWhenOffscreen = true;
        AssetDatabase.CreateAsset(newRenderer.sharedMesh, "Assets/ZZbonemesh_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + sourceMesh.name + "_boneMesh.asset");
        AssetDatabase.SaveAssets ();
    }
}
#endif