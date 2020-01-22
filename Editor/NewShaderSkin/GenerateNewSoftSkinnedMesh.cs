//GenerateBoneMesh
//Script

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//using HierarchyDict = System.Collections.Generic.Dictionary<string, UnityEngine.Transform>;
//using BoneTransformDict = System.Collections.Generic.Dictionary<string, Tuple<UnityEngine.Transform, string>>;

public class GenerateNewSoftSkinnedMesh : MonoBehaviour {
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

    static Dictionary<HumanBodyBones, int> standardBodyBoneIndices = new Dictionary<HumanBodyBones, int>
    {
        {HumanBodyBones.Hips, 0}, // required
        {HumanBodyBones.Spine, 1}, // required
        {HumanBodyBones.Chest, 2},
        {HumanBodyBones.UpperChest, 3},
        {HumanBodyBones.Neck, 4},
        {HumanBodyBones.Head, 5}, // required
        {HumanBodyBones.Jaw, 6},
        // root: 7
        // parent: 8
        // mesh: 9
        {HumanBodyBones.LeftEye, 10},
        {HumanBodyBones.RightEye, 11},
        {HumanBodyBones.LeftUpperLeg, 12},
        {HumanBodyBones.RightUpperLeg, 13},
        {HumanBodyBones.LeftLowerLeg, 14},
        {HumanBodyBones.RightLowerLeg, 15},
        {HumanBodyBones.LeftFoot, 16},
        {HumanBodyBones.RightFoot, 17},
        {HumanBodyBones.LeftToes, 18},
        {HumanBodyBones.RightToes, 19},
        {HumanBodyBones.LeftShoulder, 20},
        {HumanBodyBones.RightShoulder, 21},
        {HumanBodyBones.LeftUpperArm, 22},
        {HumanBodyBones.RightUpperArm, 23},
        {HumanBodyBones.LeftLowerArm, 24},
        {HumanBodyBones.RightLowerArm, 25},
        {HumanBodyBones.LeftHand, 26},
        {HumanBodyBones.RightHand, 27},
        {HumanBodyBones.LeftThumbProximal, 28},
        {HumanBodyBones.RightThumbProximal, 29},
        {HumanBodyBones.LeftThumbIntermediate, 30},
        {HumanBodyBones.RightThumbIntermediate, 31},
        {HumanBodyBones.LeftThumbDistal, 32},
        {HumanBodyBones.RightThumbDistal, 33},
        {HumanBodyBones.LeftIndexProximal, 34},
        {HumanBodyBones.RightIndexProximal, 35},
        {HumanBodyBones.LeftIndexIntermediate, 36},
        {HumanBodyBones.RightIndexIntermediate, 37},
        {HumanBodyBones.LeftIndexDistal, 38},
        {HumanBodyBones.RightIndexDistal, 39},
        {HumanBodyBones.LeftMiddleProximal, 40},
        {HumanBodyBones.RightMiddleProximal, 41},
        {HumanBodyBones.LeftMiddleIntermediate, 42},
        {HumanBodyBones.RightMiddleIntermediate, 43},
        {HumanBodyBones.LeftMiddleDistal, 44},
        {HumanBodyBones.RightMiddleDistal, 45},
        {HumanBodyBones.LeftRingProximal, 46},
        {HumanBodyBones.RightRingProximal, 47},
        {HumanBodyBones.LeftRingIntermediate, 48},
        {HumanBodyBones.RightRingIntermediate, 49},
        {HumanBodyBones.LeftRingDistal, 50},
        {HumanBodyBones.RightRingDistal, 51},
        {HumanBodyBones.LeftLittleProximal, 52},
        {HumanBodyBones.RightLittleProximal, 53},
        {HumanBodyBones.LeftLittleIntermediate, 54},
        {HumanBodyBones.RightLittleIntermediate, 55},
        {HumanBodyBones.LeftLittleDistal, 56},
        {HumanBodyBones.RightLittleDistal, 57},
    };
    static Dictionary<string, int> specialBoneNames = new Dictionary<string, int> {
        {"Ear_L", 58},
        {"Ear_R", 59},
        {"Breast_L", 60},
        {"Breast_R", 61},
        {"Wing_L", 62},
        {"Wing_R", 63},
        {"Hair", 64},
        {"Tail", 65},
        {"Skirt", 66},
        {"Hat", 67},
    };
    static Dictionary<int, int> fallbackBoneIds = new Dictionary<int, int> {
        {2, 1}, // chest -> spine
        {3, 2}, // upper chest
        {4, 5}, // neck -> head
        {6, 5}, // jaw -> head
        {10, 5}, // eyes -> head
        {11, 5},
        {18, 16}, // left toes -> left foot
        {19, 17}, // right toes -> right foot
        {20, 22}, // left shoulder -> left arm
        {21, 23}, // right shoulder -> right arm
        {28, 26}, // thumb
        {29, 27},
        {30, 28},
        {31, 29},
        {32, 30},
        {33, 31},
        {34, 26}, // index
        {35, 27},
        {36, 34},
        {37, 35},
        {38, 36},
        {39, 37},
        {40, 26}, // middle
        {41, 27},
        {42, 40},
        {43, 41},
        {44, 42},
        {45, 43},
        {46, 26}, // ring
        {47, 27},
        {48, 46},
        {49, 47},
        {50, 48},
        {51, 49},
        {52, 26}, // little
        {53, 27},
        {54, 52},
        {55, 53},
        {56, 54},
        {57, 55},
        {58, 5},
        {59, 5},
        {60, 3},
        {61, 3},
        {62, 2},
        {63, 2},
        {64, 5},
        {65, 0},
        {66, 0},
        {67, 5},
    };

    // Ids for which the parent id is different from the fallback bone id.
    // For the rest, you must use the id of the Transform's parent, if it exists.
    static Dictionary<int, int> parentBoneIds = new Dictionary<int, int> {
        {0, ROOT_BONE_INDEX},
        {ROOT_BONE_INDEX, ROOT_BONE_INDEX},
        {MESH_BONE_INDEX, ROOT_BONE_INDEX},
        {PARENT_BONE_INDEX, ROOT_BONE_INDEX},
        {1, 0},
        {4, 3},
        {5, 4},
        {20, 3},
        {21, 3},
        {22, 20},
        {23, 21}
    };

    const int FIRST_USER_BONE = 72;

    const int HIPS_BONE_INDEX = 0;
    const int ROOT_BONE_INDEX = 7;
    const int PARENT_BONE_INDEX = 8;
    const int MESH_BONE_INDEX = 9;
        

    static void buildBoneToTargetIndexMap(Dictionary<string, int> referenceBoneMapOrNull, Animator anim, SkinnedMeshRenderer smr, Dictionary<Transform, int> outVirtualBoneMap, List<Transform> outVirtualBoneList, Dictionary<Transform, int> outMeshBoneMap, List<Transform> outMeshBones, List<Matrix4x4> outMeshBindposes) {
        while (outVirtualBoneList.Count < FIRST_USER_BONE) {
            outVirtualBoneList.Add(null);
        }
        Dictionary<Transform, Matrix4x4> bindPoseMap = new Dictionary<Transform, Matrix4x4>();
        Transform[] bones = smr.bones;
        for (int i = 0; i < smr.bones.Length; i++) {
            if (i < smr.sharedMesh.bindposes.Length) {
                bindPoseMap[smr.bones[i]] = smr.sharedMesh.bindposes[i];
            }
        }
        foreach (KeyValuePair<HumanBodyBones, int> ent in standardBodyBoneIndices) {
            Transform trans = anim.GetBoneTransform(ent.Key);
            if (trans != null) {
                outVirtualBoneMap[trans] = ent.Value;
                while (outVirtualBoneList.Count <= ent.Value) {
                    outVirtualBoneList.Add(null);
                }
                outVirtualBoneList[ent.Value] = trans;
            }
        }
        if (smr.rootBone != null) {
            if (!outVirtualBoneMap.ContainsKey(smr.rootBone)) {
                outVirtualBoneMap[smr.rootBone] = ROOT_BONE_INDEX;
            }
            outVirtualBoneList[ROOT_BONE_INDEX] = smr.rootBone; // Allowed to be duplicate
        }
        if (!outVirtualBoneMap.ContainsKey(smr.transform)) {
            outVirtualBoneMap[smr.transform] = MESH_BONE_INDEX;
        }
        outVirtualBoneList[MESH_BONE_INDEX] = smr.transform;
        if (!outVirtualBoneMap.ContainsKey(anim.transform)) {
            outVirtualBoneMap[anim.transform] = PARENT_BONE_INDEX;
        }
        outVirtualBoneList[PARENT_BONE_INDEX] = anim.transform;
        int nextUserIndex = FIRST_USER_BONE;
        foreach (Transform t in bones) {
            string name = t.name;
            if (outVirtualBoneMap.ContainsKey(t)) {
                continue;
            }
            int val = -1;
            if (referenceBoneMapOrNull != null && referenceBoneMapOrNull.ContainsKey(name)) {
                val = referenceBoneMapOrNull[name];
            } else if (specialBoneNames.ContainsKey(name)) {
                val = specialBoneNames[name];
            }
            while (val == -1 || (val < outVirtualBoneList.Count && outVirtualBoneList[val] != null)) {
                val = nextUserIndex++;
            }
            outVirtualBoneMap[t] = val;
            while (outVirtualBoneList.Count <= val) {
                outVirtualBoneList.Add(null);
            }
            outVirtualBoneList[val] = t;
        }
        Transform fallbackBone = outVirtualBoneList[0];
        if (fallbackBone == null) {
            fallbackBone = outVirtualBoneList[ROOT_BONE_INDEX];
            if (fallbackBone == null) {
                fallbackBone = outVirtualBoneList[PARENT_BONE_INDEX];
            }
        }
        for (int i = 0; i < FIRST_USER_BONE; i++) {
            if (outVirtualBoneList[i] == null) {
                int fallback = 0;
                if (fallbackBoneIds.ContainsKey(i)) {
                    fallback = fallbackBoneIds[i];
                }
                outVirtualBoneList[i] = outVirtualBoneList[fallback];
                if (outVirtualBoneList[i] == null) {
                    outVirtualBoneList[i] = fallbackBone;
                }
            }
        }
        HashSet<Transform> meshSeenTransforms = new HashSet<Transform>();
        for (int i = 0; i < outVirtualBoneList.Count; i++) {
            Transform t = outVirtualBoneList[i];
            if (!meshSeenTransforms.Contains(t)) {
                outMeshBoneMap[t] = outMeshBones.Count;
                outMeshBones.Add(t);
                if (bindPoseMap.ContainsKey(t)) {
                    outMeshBindposes.Add(bindPoseMap[t]);
                } else {
                    outMeshBindposes.Add(Matrix4x4.identity);
                }
                meshSeenTransforms.Add(t);
            }
        }
    }

    static public Dictionary<string, int> buildBoneNameToTransformMap(Animator anim, SkinnedMeshRenderer smr) {
        Dictionary<Transform, int> outVirtualBoneIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outVirtualBoneList = new List<Transform>();
        Dictionary<Transform, int> outMeshIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outMeshBoneList = new List<Transform>();
        List<Matrix4x4> outMeshBindposes = new List<Matrix4x4>();
        buildBoneToTargetIndexMap(null, anim, smr, outVirtualBoneIdxMap, outVirtualBoneList, outMeshIdxMap, outMeshBoneList, outMeshBindposes);
        Dictionary<string, int> referenceBoneNamesToIndex = new Dictionary<string, int>();
        foreach (KeyValuePair<Transform, int> ent in outVirtualBoneIdxMap) {
            referenceBoneNamesToIndex[ent.Key.name] = ent.Value;
        }
        return referenceBoneNamesToIndex;
    }

    static public Dictionary<int, int> buildBoneIndexMap(Animator anim, SkinnedMeshRenderer smr, Dictionary<string, int> referenceBoneMapOrNull) {
        Dictionary<Transform, int> outVirtualBoneIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outVirtualBoneList = new List<Transform>();
        Dictionary<Transform, int> outMeshIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outMeshBoneList = new List<Transform>();
        List<Matrix4x4> outMeshBindposes = new List<Matrix4x4>();
        buildBoneToTargetIndexMap(referenceBoneMapOrNull, anim, smr, outVirtualBoneIdxMap, outVirtualBoneList, outMeshIdxMap, outMeshBoneList, outMeshBindposes);
        Dictionary<int, int> map = new Dictionary<int, int>();
        for (int i = 0; i < smr.bones.Length; i++) {
            map[i] = outVirtualBoneIdxMap[smr.bones[i]];
        }
        return map;
    }

    public class VisemeData {
        public string name;
        public float[] weights;
    }
    public class VisemeSource {
        public string[] names;
        public float weight;
    }
    public static readonly VisemeSource[][] VISEME_SOURCES = new VisemeSource[][]{
        new VisemeSource[]{new VisemeSource{names=new string[]{"vrc.v_aa","あ"}, weight=1.0f}},
        new VisemeSource[]{new VisemeSource{names=new string[]{"vrc.v_ou","お"}, weight=1.0f}},
        new VisemeSource[]{new VisemeSource{names=new string[]{"vrc.v_ch","い"}, weight=1.0f}},
        new VisemeSource[]{new VisemeSource{names=new string[]{"ウィンク","ウィンク左","vrc.blink_left"}, weight=1.0f}},
        new VisemeSource[]{new VisemeSource{names=new string[]{"ウィンク右","vrc.blink_right"}, weight=1.0f}}
    };
    // weights array indices correspond to indices in VISEME_SOURCES:
    public static readonly VisemeData[] VISEME_WEIGHTS = new VisemeData[]{
        new VisemeData{name="vrc.blink_left", weights=new float[]{0.0f,0.0f,0.0f,1.0f,0.0f}},
        new VisemeData{name="vrc.blink_right", weights=new float[]{0.0f,0.0f,0.0f,0.0f,1.0f}},
        new VisemeData{name="vrc.lowerlid_left", weights=new float[]{0.0f,0.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.lowerlid_right", weights=new float[]{0.0f,0.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_aa", weights=new float[]{1.0f,0.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_ch", weights=new float[]{0.0f,0.0f,1.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_dd", weights=new float[]{0.3f,0.0f,0.7f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_e", weights=new float[]{0.0f,0.3f,0.7f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_ff", weights=new float[]{0.2f,0.0f,0.4f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_ih", weights=new float[]{0.5f,0.0f,0.2f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_kk", weights=new float[]{0.7f,0.0f,0.4f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_nn", weights=new float[]{0.2f,0.0f,0.7f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_oh", weights=new float[]{0.2f,0.8f,0.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_ou", weights=new float[]{0.0f,1.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_pp", weights=new float[]{0.0004f,0.0004f,0.0f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_rr", weights=new float[]{0.0f,0.3f,0.5f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_sil", weights=new float[]{0.0002f,0.0f,0.0002f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_ss", weights=new float[]{0.0f,0.0f,0.8f,0.0f,0.0f}},
        new VisemeData{name="vrc.v_th", weights=new float[]{0.4f,0.15f,0.0f,0.0f,0.0f}},
        new VisemeData{name="ah", weights=new float[]{1.0f,0.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="oh", weights=new float[]{0.0f,1.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="ch", weights=new float[]{0.0f,0.0f,1.0f,0.0f,0.0f}},
        new VisemeData{name="Ah", weights=new float[]{1.0f,0.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="Your", weights=new float[]{0.0f,1.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="There", weights=new float[]{0.0f,0.0f,1.0f,0.0f,0.0f}},
        new VisemeData{name="あ", weights=new float[]{1.0f,0.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="お", weights=new float[]{0.0f,1.0f,0.0f,0.0f,0.0f}},
        new VisemeData{name="い", weights=new float[]{0.0f,0.0f,1.0f,0.0f,0.0f}},
        new VisemeData{name="ウィンク左", weights=new float[]{0.0f,0.0f,0.0f,1.0f,0.0f}},
        new VisemeData{name="ウィンク右", weights=new float[]{0.0f,0.0f,0.0f,0.0f,1.0f}},
        new VisemeData{name="ウィンク", weights=new float[]{0.0f,0.0f,0.0f,1.0f,0.0f}},
        // TODO: add mmd dance world names too
    };

    //[MenuItem("GameObject/Create Soft Skinned")]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Generate new quad soft skinned", false, 142)]
    public static void GenerateNewSoftSkinned_ (MenuCommand command)
    {
        SkinnedMeshRenderer renderer = command.context as SkinnedMeshRenderer;
        Animator anim = null;
        Transform parentTransform = renderer.transform;
        while (anim == null || !anim.isHuman) {
            parentTransform = parentTransform.parent;
            anim = parentTransform.GetComponent<Animator>();
        }
        doGenerate(anim, renderer, null);
    }

    public static void doGenerate(Animator anim, SkinnedMeshRenderer renderer, Dictionary<string, int> referenceBoneMapOrNull) {
        int USE_BLEND_SHAPES = 1; // 1 == same material; 2 = separate materials.
        int NUM_BONE_MODES = 4; //3;
        Mesh sourceMesh = renderer.sharedMesh;
        Dictionary<Transform, int> srcBoneIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outVirtualBoneList = new List<Transform>();
        Dictionary<Transform, int> outMeshIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outMeshBoneList = new List<Transform>();
        List<Matrix4x4> outMeshBindposes = new List<Matrix4x4>();
        buildBoneToTargetIndexMap(referenceBoneMapOrNull, anim, renderer, srcBoneIdxMap, outVirtualBoneList, outMeshIdxMap, outMeshBoneList, outMeshBindposes);
        int rootBoneIndex;
        rootBoneIndex = outMeshIdxMap[outVirtualBoneList[ROOT_BONE_INDEX]];
        int blendShapeCount = (USE_BLEND_SHAPES == 1 ? VISEME_SOURCES.Length + 1: 1); // extra: root bone
        int size1 = (outVirtualBoneList.Count) * NUM_BONE_MODES;
        int size2 = blendShapeCount * NUM_BONE_MODES;

        int size = USE_BLEND_SHAPES >= 1 ? size1 + size2 : size1;

        Mesh newMesh = new Mesh ();
        var newVertices = new Vector3 [size];
        var newNormals = new Vector3 [size];
        var newTangents = new Vector4 [size];
        var newBoneWeights = new BoneWeight [size];
        var newUV1 = new Vector4[size];
        var newColors = new Color [size];
        var curIndices = new int [(outVirtualBoneList.Count) * 6 + (USE_BLEND_SHAPES == 1 ? blendShapeCount * 6 : 0)];
        var blendIndices = curIndices;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [outMeshBindposes.Count];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = outMeshBindposes [i].inverse;
        }
        for (int i = 0; i < size1; i++) {
            float uvy = i % 2 == 0 ? 0.0f : 1.0f;
            float uvx = i % 4 < 2 ? 0.0f : 1.0f;
            int boneIndex = i / NUM_BONE_MODES;
            int meshBoneIndex = outMeshIdxMap[outVirtualBoneList[boneIndex]];
            Transform t = outVirtualBoneList [boneIndex];
            int dstVert = i;
            Debug.Log ("Bone " + boneIndex + ": " + meshBoneIndex + ": " + t.name, t);
            Vector3 newTang;
            newVertices [dstVert] = inverseBindPoses [meshBoneIndex].MultiplyPoint (new Vector3 (0, 0, 0));
            newNormals [dstVert] = inverseBindPoses [meshBoneIndex].MultiplyVector (new Vector3 (0, 0, 1));
            newTang = inverseBindPoses [meshBoneIndex].MultiplyVector (new Vector3 (1, 0, 0));
            newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
            newBoneWeights [dstVert] = new BoneWeight ();
            newBoneWeights [dstVert].boneIndex0 = meshBoneIndex;
            newBoneWeights [dstVert].weight0 = 1.0f;
            byte [] simpleName = SimplifyBoneName (t.name);
            newColors [dstVert] = new Color ();
            newColors [dstVert].r = simpleName [0];
            newColors [dstVert].g = simpleName [1];
            newColors [dstVert].b = simpleName [2];
            newColors [dstVert].a = simpleName [3];
            int uvw = 1 + boneIndex;
            newUV1 [dstVert] = new Vector4 (uvx, uvy, 0, uvw);
            if (i % NUM_BONE_MODES == 0) {
                curIndices [boneIndex * 6] = i;
                curIndices [boneIndex * 6 + 1] = i + 1;
                curIndices [boneIndex * 6 + 2] = i + 2;
                curIndices [boneIndex * 6 + 3] = i + 2;
                curIndices [boneIndex * 6 + 4] = i + 1;
                curIndices [boneIndex * 6 + 5] = i + 3;
            }
            Debug.Log ("i:" + boneIndex + " v:" + dstVert + ": Adding vertex " + d(newVertices [dstVert]) + "/" + d(newNormals [dstVert]) + "/" + d(newTangents [dstVert]));
        }
        {
            int baseTri = outVirtualBoneList.Count;
            if (USE_BLEND_SHAPES == 2) {
                blendIndices = new int [blendShapeCount * 6];
                baseTri = 0;
            }
            for (int ishape = 0; ishape < blendShapeCount; ishape++) {
                string name = ishape == 0 ? "root" : sourceMesh.GetBlendShapeName (ishape - 1);
                int dstVertBase = ishape * NUM_BONE_MODES + size1;
                int dstTri = ishape + baseTri;
                for (int vert = 0; vert < NUM_BONE_MODES; vert++) {
                    float uvy = vert % 2 == 0 ? 0.0f : 1.0f;
                    float uvx = vert < 2 ? 0.0f : 1.0f;
                    int dstVert = dstVertBase + vert;
                    Debug.Log ("Morph " + ishape + ": " + name);
                    Vector3 newTang;
                    newVertices [dstVert] = inverseBindPoses [rootBoneIndex].MultiplyPoint (new Vector3 (0, 0, 0));
                    newNormals [dstVert] = inverseBindPoses [rootBoneIndex].MultiplyVector (new Vector3 (0, 0, 1));
                    newTang = inverseBindPoses [rootBoneIndex].MultiplyVector (new Vector3 (1, 0, 0));
                    newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
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
                    newUV1 [dstVert] = new Vector4 (uvx, uvy, 1, ishape);
                }
                blendIndices [dstTri * 6] = dstVertBase;
                blendIndices [dstTri * 6 + 1] = dstVertBase + 1;
                blendIndices [dstTri * 6 + 2] = dstVertBase + 2;
                blendIndices [dstTri * 6 + 3] = dstVertBase + 2;
                blendIndices [dstTri * 6 + 4] = dstVertBase + 1;
                blendIndices [dstTri * 6 + 5] = dstVertBase + 3;
                Debug.Log ("i:" + ishape + " v:" + dstVertBase + ": Adding vertex " + d (newVertices [dstVertBase]) + "/" + d (newNormals [dstVertBase]) + "/" + d (newTangents [dstVertBase]) + "/" + d (newUV1 [dstVertBase]));
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
        newMesh.bindposes = outMeshBindposes.ToArray();
        newMesh.name = sourceMesh.name + "_BoneMesh";
        if (USE_BLEND_SHAPES >= 1) {
            Vector3 [] deltaVertices = new Vector3 [size];
            Vector3 [] deltaNormals = new Vector3 [size];
            Vector3 [] deltaTangents = new Vector3 [size];
            for (int destshape = 0; destshape < VISEME_WEIGHTS.Length; destshape++) {
                for (int ishape = 1; ishape < blendShapeCount; ishape++) {
                    float thisweight = VISEME_WEIGHTS[destshape].weights[ishape - 1];
                    if (thisweight != 0.0f) {
                        for (int vert = 0; vert < NUM_BONE_MODES; vert++) {
                            deltaVertices [size1 + ishape * NUM_BONE_MODES + vert].x = thisweight;
                        }
                    }
                }
                newMesh.AddBlendShapeFrame (VISEME_WEIGHTS[destshape].name, 100.0f, deltaVertices, deltaNormals, deltaTangents);
                for (int ishape = 1; ishape < blendShapeCount; ishape++) {
                    float thisweight = VISEME_WEIGHTS[destshape].weights[ishape - 1];
                    if (thisweight != 0.0f) {
                        for (int vert = 0; vert < NUM_BONE_MODES; vert++) {
                            deltaVertices [size1 + ishape * NUM_BONE_MODES + vert].x = 0.0f;
                        }
                    }
                }
            }
        }
        //newMesh.RecalculateBounds ();
        //newMesh.RecalculateNormals ();
        //newMesh.RecalculateTangents ();
        Undo.RecordObject (renderer, "Switched SkinnedMeshRenderer to Quad");
        renderer.updateWhenOffscreen = false;
        renderer.sharedMesh = newMesh;
        renderer.bones = outMeshBoneList.ToArray();
        //renderer.rootBone = renderer.rootBone;
        Material [] newMaterials = new Material [1];
        newMaterials[0] = renderer.sharedMaterials[0];
        renderer.sharedMaterials = newMaterials;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.receiveShadows = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.updateWhenOffscreen = true;
        AssetDatabase.CreateAsset(renderer.sharedMesh, "Assets/ZZbonemesh_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + sourceMesh.name + "_boneMesh.asset");
        AssetDatabase.SaveAssets ();
    }

    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Weights to Color, Shapes to UV234", false, 142)]
    public static void WeightsToColorShapeToUV234_Menu (MenuCommand command)
    {
        SkinnedMeshRenderer smr = command.context as SkinnedMeshRenderer;
        Animator anim = null;
        Transform parentTransform = smr.transform;
        while (anim == null || !anim.isHuman) {
            parentTransform = parentTransform.parent;
            anim = parentTransform.GetComponent<Animator>();
        }
        Dictionary<int, int> indexMap = buildBoneIndexMap(anim, smr, null);
        WeightsToColorShapeToUV234(smr, indexMap);
    }
    public static void WeightsToColorShapeToUV234 (UnityEngine.Object context, Dictionary<int, int> indexMap) {
        Mesh sourceMesh;
        SkinnedMeshRenderer smr = null;
        MeshRenderer mr = null;
        MeshFilter mf = null;
        Transform trans = null;
        string parentName = "";
        int rootBoneIndex = 0;
        if (context is SkinnedMeshRenderer) {
            smr = context as SkinnedMeshRenderer;
            trans = smr.transform;
            sourceMesh = smr.sharedMesh;
            for (int i = 0; i < smr.bones.Length; i++) {
                if (smr.bones[i] == smr.rootBone) {
                    rootBoneIndex = i;
                }
            }
        } else if (context is MeshRenderer) {
            mr = context as MeshRenderer;
            mf = mr.transform.GetComponent<MeshFilter> ();
            trans = mr.transform;
            sourceMesh = mf.sharedMesh;
        } else if (context is MeshFilter) {
            mf = context as MeshFilter;
            trans = mf.transform;
            sourceMesh = mf.sharedMesh;
        } else if (context is Mesh) {
            sourceMesh = context as Mesh;
        } else {
            EditorUtility.DisplayDialog ("MergeUVs", "Unknkown context type " + context.GetType ().FullName, "OK", "");
            throw new NotSupportedException ("Unknkown context type " + context.GetType ().FullName);
        }
        if (trans != null) {
            // Get name of top-most object this mesh is attached to
            while (trans.parent != null) {
                trans = trans.parent;
            }
            parentName = trans.name + "_";
        }
        parentName += sourceMesh.name;
        if (rootBoneIndex >= sourceMesh.bindposes.Length) {
            rootBoneIndex = sourceMesh.bindposes.Length - 1;
        }
        Mesh newMesh = new Mesh ();
        int size = sourceMesh.vertices.Length;
        List<Vector4> srcUV = new List<Vector4> ();
        Color [] newColors = new Color[size];
        sourceMesh.GetUVs (0, srcUV);
        Vector3 [] srcVertices = sourceMesh.vertices;
        Vector3 [] srcNormals = sourceMesh.normals;
        Vector4 [] srcTangents = sourceMesh.tangents;
        Matrix4x4 [] srcBindposes = sourceMesh.bindposes;
        BoneWeight [] srcBoneWeights = sourceMesh.boneWeights;
        newMesh.vertices = srcVertices;
        if (srcNormals != null && srcNormals.Length > 0) {
            newMesh.normals = srcNormals;
        }
        if (srcTangents != null && srcTangents.Length > 0) {
            newMesh.tangents = srcTangents;
        }
        if (srcBoneWeights != null && srcBoneWeights.Length > 0) {
            BoneWeight defaultBw = new BoneWeight();
            defaultBw.boneIndex0 = rootBoneIndex;
            defaultBw.weight0 = 1.0f;
            for (int i = 0; i < srcBoneWeights.Length; i++) {
                BoneWeight bw = srcBoneWeights[i];
                newColors[i] = new Color(
                    bw.weight0 <= 0.0 ? 0.0f : (indexMap[bw.boneIndex0] + 0.9999f * bw.weight0),
                    bw.weight1 <= 0.0 ? 0.0f : (indexMap[bw.boneIndex1] + 0.9999f * bw.weight1),
                    bw.weight2 <= 0.0 ? 0.0f : (indexMap[bw.boneIndex2] + 0.9999f * bw.weight2),
                    bw.weight3 <= 0.0 ? 0.0f : (indexMap[bw.boneIndex3] + 0.9999f * bw.weight3)
                );
                srcBoneWeights[i] = defaultBw;
            }
            newMesh.colors = newColors;
            newMesh.boneWeights = srcBoneWeights;
        }
        {
            Vector3 [] tempDeltaVertices = new Vector3 [size];
            Vector3 [] tempDeltaNormals = new Vector3 [size];
            Vector3 [] tempDeltaTangents = new Vector3 [size];
            Dictionary<string, int> blendShapeIndices = new Dictionary<string, int>();
            int[] blendShapeFrameCounts = new int[sourceMesh.blendShapeCount];
            for (int i = 0; i < sourceMesh.blendShapeCount; i++) {
                var blendShapeName = sourceMesh.GetBlendShapeName (i);
                blendShapeIndices[blendShapeName] = i;
                blendShapeFrameCounts[i] = sourceMesh.GetBlendShapeFrameCount (i);
            }
            List<Vector4> uv2 = new List<Vector4>();
            List<Vector4> uv3 = new List<Vector4>();
            List<Vector4> uv4 = new List<Vector4>();
            for (int i = 0; i < size; i++) {
                uv2.Add(new Vector4());
                uv3.Add(new Vector4());
                uv4.Add(new Vector4());
            }
            for (int ishape = 0; ishape < VISEME_SOURCES.Length; ishape++) {
                Vector3 [] fullDeltaVertices = new Vector3 [size];
                for (int i = 0; i < VISEME_SOURCES[ishape].Length; i++) {
                    int srcBlendShape = -1;
                    for (int nameidx = 0; nameidx < VISEME_SOURCES[ishape][i].names.Length; nameidx++) {
                        if (blendShapeIndices.ContainsKey(VISEME_SOURCES[ishape][i].names[nameidx])) {
                            srcBlendShape = blendShapeIndices[VISEME_SOURCES[ishape][i].names[nameidx]];
                            break;
                        }
                    }
                    if (srcBlendShape == -1) {
                        continue;
                    }
                    float srcWeight = VISEME_SOURCES[ishape][i].weight;
                    sourceMesh.GetBlendShapeFrameVertices (srcBlendShape, blendShapeFrameCounts[srcBlendShape] - 1, tempDeltaVertices, tempDeltaNormals, tempDeltaTangents);
                    for (int vertIdx = 0; vertIdx < size; vertIdx++) {
                        fullDeltaVertices[vertIdx].x += srcWeight * tempDeltaVertices[vertIdx].x;
                        fullDeltaVertices[vertIdx].y += srcWeight * tempDeltaVertices[vertIdx].y;
                        fullDeltaVertices[vertIdx].z += srcWeight * tempDeltaVertices[vertIdx].z;
                    }
                }
                for (int vertIdx = 0; vertIdx < size; vertIdx++) {
                    if (Vector3.Dot(fullDeltaVertices[vertIdx],fullDeltaVertices[vertIdx]) == 0.0f) {
                        continue;
                    }
                    if (uv2[vertIdx].w == 0) {
                        uv2[vertIdx] = new Vector4(
                            fullDeltaVertices[vertIdx].x,
                            fullDeltaVertices[vertIdx].y,
                            fullDeltaVertices[vertIdx].z,
                            ishape + 1);
                    } else if (uv3[vertIdx].w == 0) {
                        uv3[vertIdx] = new Vector4(
                            fullDeltaVertices[vertIdx].x,
                            fullDeltaVertices[vertIdx].y,
                            fullDeltaVertices[vertIdx].z,
                            ishape + 1);
                    } else if (uv4[vertIdx].w == 0) {
                        uv4[vertIdx] = new Vector4(
                            fullDeltaVertices[vertIdx].x,
                            fullDeltaVertices[vertIdx].y,
                            fullDeltaVertices[vertIdx].z,
                            ishape + 1);
                    }
                }
            }
            if (sourceMesh.blendShapeCount > 0) {
                newMesh.SetUVs (1, uv2);
                newMesh.SetUVs (2, uv3);
                newMesh.SetUVs (3, uv4);
            }
        }
        if (srcUV.Count > 0) {
            newMesh.SetUVs (0, srcUV);
        }
        newMesh.subMeshCount = sourceMesh.subMeshCount;
        for (int i = 0; i < sourceMesh.subMeshCount; i++) {
            var curIndices = sourceMesh.GetIndices (i);
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology (i), i);
        }
        newMesh.bounds = sourceMesh.bounds;
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = new Matrix4x4[] { Matrix4x4.identity };
        }
        for (int i = 0; i < sourceMesh.blendShapeCount; i++) {
            var blendShapeName = sourceMesh.GetBlendShapeName (i);
            var blendShapeFrameCount = sourceMesh.GetBlendShapeFrameCount (i);
            for (int frameIndex = 0; frameIndex < blendShapeFrameCount; frameIndex++) {
                float weight = sourceMesh.GetBlendShapeFrameWeight (i, frameIndex);
                Vector3 [] deltaVertices = new Vector3 [size];
                Vector3 [] deltaNormals = new Vector3 [size];
                Vector3 [] deltaTangents = new Vector3 [size];
                sourceMesh.GetBlendShapeFrameVertices (i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                newMesh.AddBlendShapeFrame (blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
            }
        }
        newMesh.name = sourceMesh.name + "_weightuv";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to Weight UV34");
            smr.bones = new Transform[] {smr.rootBone};//renderer.bones;
            smr.sharedMesh = newMesh;
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to Weight UV34");
            mf.sharedMesh = newMesh;
            meshAfterUpdate = mf.sharedMesh;
        }
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        int lastSlash = parentName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? parentName : parentName.Substring (lastSlash + 1);
        outFileName = outFileName.Split ('.') [0];
        string fileName = pathToGenerated + "/" + outFileName + "_test_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
        if (smr == null && mf == null) {
            EditorGUIUtility.PingObject (meshAfterUpdate);
        }
    }

    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Generate bone info texture", false, 142)]
    public static void GenerateBindPoseTexture_ (MenuCommand command)
    {
        SkinnedMeshRenderer smr = command.context as SkinnedMeshRenderer;
        Transform trans = smr.transform;
        Animator anim = null;
        Transform parentTransform = smr.transform;
        while (anim == null || !anim.isHuman) {
            parentTransform = parentTransform.parent;
            anim = parentTransform.GetComponent<Animator>();
        }
        GenerateBindPoseTexture(anim, smr, null, null);
    }
    public static void GenerateBindPoseTexture (Animator anim, SkinnedMeshRenderer smr, Dictionary<string, int> referenceBoneMapOrNull, SkinnedMeshRenderer referenceSmr) {
        string parentName = "";
        if (smr != null) {
            Transform trans = smr.transform;
            // Get name of top-most object this mesh is attached to
            while (trans.parent != null) {
                trans = trans.parent;
            }
            parentName = trans.name;
        }
        Dictionary<Transform, int> outVirtualIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outVirtualBoneList = new List<Transform>();
        Dictionary<Transform, int> outMeshIdxMap = new Dictionary<Transform, int> ();
        List<Transform> outMeshBoneList = new List<Transform>();
        List<Matrix4x4> outMeshBindposes = new List<Matrix4x4>();
        buildBoneToTargetIndexMap(referenceBoneMapOrNull, anim, smr, outVirtualIdxMap, outVirtualBoneList, outMeshIdxMap, outMeshBoneList, outMeshBindposes);
        int rootBoneIndex;
        rootBoneIndex = outMeshIdxMap[outVirtualBoneList[ROOT_BONE_INDEX]];
        Transform rootBone = outMeshBoneList[rootBoneIndex];
        int numBones = outVirtualBoneList.Count;
        Matrix4x4 [] boneToRootTransform = new Matrix4x4[numBones];
        Matrix4x4 [] rootToBoneTransform = new Matrix4x4[numBones];
        Matrix4x4 [] bindposes = new Matrix4x4[numBones];
        Matrix4x4 [] invBindposes = new Matrix4x4[numBones];
        Matrix4x4 [] relBindposes = new Matrix4x4[numBones];
        Matrix4x4 [] relInvBindposes = new Matrix4x4[numBones];
        Matrix4x4 [] parentIds16 = new Matrix4x4[numBones];
        // localQuat, float4(localPos.xyz, localScale), globalQuat, float4(globalPos.xyz, globalScale);
        Matrix4x4 [] localGlobalQPS = new Matrix4x4[numBones];
        for (int i = 0; i < numBones; i++) {
            Transform sourceBone = outVirtualBoneList[i];
            if (outMeshIdxMap.ContainsKey(sourceBone)) {
                bindposes[i] = outMeshBindposes[outMeshIdxMap[sourceBone]];
            } else {
                bindposes[i] = Matrix4x4.identity;
            }
            invBindposes[i] = bindposes[i].inverse;
            boneToRootTransform[i] = rootBone.worldToLocalMatrix * sourceBone.localToWorldMatrix;
            rootToBoneTransform[i] = sourceBone.worldToLocalMatrix * rootBone.localToWorldMatrix;
            if (referenceBoneMapOrNull != null && referenceBoneMapOrNull.ContainsKey(sourceBone.name) ) {
                Transform destBone = referenceSmr.bones[referenceBoneMapOrNull[sourceBone.name]];
                relBindposes[i] = destBone.worldToLocalMatrix * sourceBone.localToWorldMatrix * bindposes[i];
                relInvBindposes [i] = sourceBone.worldToLocalMatrix * destBone.localToWorldMatrix * invBindposes[i];
            } else {
                relBindposes[i] = bindposes[i];
                relInvBindposes[i] = invBindposes[i];
            }

            int boneId = i;
            int firstParent = -1;
            Matrix4x4 parentMat = new Matrix4x4();
            for (int pid = 0; pid < 16; pid++) {
                if (parentBoneIds.ContainsKey(boneId)) {
                    boneId = parentBoneIds[boneId];
                } else if (fallbackBoneIds.ContainsKey(boneId)) {
                    boneId = fallbackBoneIds[boneId];
                } else {
                    Transform t = outVirtualBoneList[boneId];
                    while (t != null) {
                        t = t.parent;
                        if (t != null && outVirtualIdxMap.ContainsKey(t)) {
                            boneId = outVirtualIdxMap[t];
                            break;
                        }
                    }
                    if (t == null) {
                        boneId = ROOT_BONE_INDEX;
                    }
                }
                if (firstParent == -1) {
                    firstParent = boneId;
                }
                parentMat[pid % 4, pid / 4] = (float)boneId;
            }
            parentIds16[i] = parentMat;
            Transform parentBone = outVirtualBoneList[firstParent];
            Matrix4x4 localTransform = parentBone.worldToLocalMatrix * sourceBone.localToWorldMatrix;
            Vector3 localScale = localTransform.lossyScale;
            Vector3 relPosition = sourceBone.transform.position - parentBone.transform.position;
            Quaternion localRot = Quaternion.identity; //localTransform.rotation;
            localGlobalQPS[i].SetColumn(0, new Vector4(localRot.x, localRot.y, localRot.z, localRot.w));
            localGlobalQPS[i].SetColumn(1, new Vector4(
                relPosition.x, relPosition.y, relPosition.z, //localTransform.m03, localTransform.m13, localTransform.m23,
                (localScale.x + localScale.y + localScale.z)/3));
            Vector3 globalScale = boneToRootTransform[i].lossyScale;
            Quaternion globalRot = boneToRootTransform[i].rotation;
            localGlobalQPS[i].SetColumn(2, new Vector4(globalRot.x, globalRot.y, globalRot.z, globalRot.w));
            localGlobalQPS[i].SetColumn(3, new Vector4(
                boneToRootTransform[i].m03, boneToRootTransform[i].m13, boneToRootTransform[i].m23,
                (globalScale.x + globalScale.y + globalScale.z)/3));

            boneToRootTransform[i][3,3] = (float)firstParent;
            rootToBoneTransform[i][3,3] = (float)firstParent;
        }
        int texWidth = ((numBones + 15) / 16) * 16;
        int texHeight = 32;

        ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader> ("Assets/LyumaShader/Compute/GenerateTexture.compute");
        ComputeBuffer bindposeBuffer = new ComputeBuffer ((int)numBones, 4 * 16);
        int kernelHandle = shader.FindKernel ("DumpMatrices");

        RenderTexture tex = new RenderTexture ((int)texWidth, (int)texHeight, 0, RenderTextureFormat.ARGBHalf);
        tex.enableRandomWrite = true;
        tex.Create ();

        bindposeBuffer.SetData (relBindposes);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 0); // 4 pixels for src matrix, 4 pixels for dst matrix.
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (relInvBindposes);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 4); // 4 pixels for src matrix, 4 pixels for dst matrix.
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (bindposes);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 8);
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (invBindposes);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 12);
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (parentIds16);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 16);
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (localGlobalQPS);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 20);
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (rootToBoneTransform);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 24);
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        bindposeBuffer.SetData (boneToRootTransform);
        shader.SetBuffer (kernelHandle, "_MatrixBufIn", bindposeBuffer);
        shader.SetTexture (kernelHandle, "_Result", tex);
        shader.SetInt ("_TexYOffset", 28);
        shader.Dispatch (kernelHandle, (int)numBones, 1, 1);

        Texture2D outTexture = new Texture2D (tex.width, (tex.height + 15) / 16 * 16, TextureFormat.RGBAHalf, false, true);
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture.active = tex;
        outTexture.ReadPixels (new Rect (0, 0, tex.width, tex.height), 0, 0);
        outTexture.Apply ();
        RenderTexture.active = oldActive;
        tex.Release ();
        bindposeBuffer.Release ();

        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        string tmpName = (parentName);
        int lastSlash = tmpName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? tmpName : tmpName.Substring (lastSlash + 1);
        outFileName = outFileName.Replace ('.', '_');
        string outPath = pathToGenerated + "/ZZbindposetex_" + outFileName + "_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (outTexture, outPath);
        EditorGUIUtility.PingObject (outTexture);
    }

}
#endif