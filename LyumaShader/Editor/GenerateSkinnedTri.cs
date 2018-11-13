using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class GenerateSkinnedTri : EditorWindow {

    Animator armatureObj;
    SkinnedMeshRenderer skinnedMesh;
    Transform headDefaultBone;
    Transform leftHandDefaultBone;
    Transform rightHandDefaultBone;
    Transform headBone;
    Transform leftHandBone;
    Transform rightHandBone;
    Transform [] bones;

    int triCount = 1;
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

    //[MenuItem ("Lyuma/GenerateSkinnedTri")]
    [MenuItem ("Lyuma/GenerateSkinnedTri")]
    static void Init ()
    {
        GenerateSkinnedTri win = (GenerateSkinnedTri)GetWindow (typeof (GenerateSkinnedTri));
        win.Show ();
    }

    void OnGUI() {
        armatureObj = (Animator)EditorGUILayout.ObjectField (new GUIContent ("Avatar Animator", "Avatar Animator object"), armatureObj, typeof (Animator), true);
        if (armatureObj != null && armatureObj.isHuman) {
            headDefaultBone = armatureObj.GetBoneTransform (HumanBodyBones.Head);
            leftHandDefaultBone = armatureObj.GetBoneTransform (HumanBodyBones.LeftHand);
            rightHandDefaultBone = armatureObj.GetBoneTransform (HumanBodyBones.RightHand);
        }
        /*
        updateSkinnedMeshRenderer((SkinnedMeshRenderer)EditorGUILayout.ObjectField (
            new GUIContent ("Body mesh", "A skinned mesh renderer attached to this Armature"),
            skinnedMesh, typeof (SkinnedMeshRenderer), true));
            */
        if (armatureObj != null && skinnedMesh == null) {
            SkinnedMeshRenderer [] renderers = armatureObj.GetComponentsInChildren<SkinnedMeshRenderer> ();
            Array.Sort (renderers, delegate (SkinnedMeshRenderer a, SkinnedMeshRenderer b) {
                return a.transform.parent == armatureObj.transform ? -1 : 1;
            });
            foreach (SkinnedMeshRenderer s in renderers) {
                IList<Transform> tbones = s.bones;
                if (tbones.Contains(headDefaultBone) &&
                        tbones.Contains(leftHandDefaultBone) &&
                        tbones.Contains(rightHandDefaultBone)) {
                    skinnedMesh = s;
                    break;
                }
            }
            if (leftHandBone == null && rightHandBone == null && headBone == null) {
                leftHandBone = leftHandDefaultBone;
                rightHandBone = rightHandDefaultBone;
                headBone = headDefaultBone;
            }
        }
        skinnedMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField (
            new GUIContent ("Body mesh", "A skinned mesh renderer"), skinnedMesh, typeof (SkinnedMeshRenderer), true);
        if (skinnedMesh != null) {
            bones = skinnedMesh.bones;
        }
        headBone = (Transform)EditorGUILayout.ObjectField (
            new GUIContent ("Head bone", "First object position (usually head or neck)"), headBone, typeof (Transform), true);
        leftHandBone = (Transform)EditorGUILayout.ObjectField (
            new GUIContent ("Left hand", "Second object position (usually left hand/wrist)"), leftHandBone, typeof (Transform), true);
        rightHandBone = (Transform)EditorGUILayout.ObjectField (
            new GUIContent ("Right hand", "Third object position (usually right hand/wrist)"), rightHandBone, typeof (Transform), true);


        triCount = EditorGUILayout.IntField (
            new GUIContent ("Triangle count", "Number of polygons to generate."), triCount);
        bool error = false;
        if (skinnedMesh == null) {
            EditorGUILayout.HelpBox ("You must select an existing Skinned Mesh Renderer to initialize bones array", MessageType.Error);
            error = true;
        }
        if (headBone == null || leftHandBone == null || rightHandBone == null) {
            EditorGUILayout.HelpBox ("This requires three transforms", MessageType.Error);
            error = true;
        }

        EditorGUILayout.Separator ();
        if (GUILayout.Button ("Clear")) {
            headBone = null;
            leftHandBone = null;
            rightHandBone = null;
            skinnedMesh = null;
            armatureObj = null;
        }
        if (error) return;

        if (GUILayout.Button ("Generate")) {
            doGenerate (false);
            //EditorUtility.DisplayDialog ("GenerateSkinnedTris", "Would start", "OK", "");
        }
        if (GUILayout.Button ("Generate BindPose")) {
            doGenerate (true);
            //EditorUtility.DisplayDialog ("GenerateSkinnedTris", "Would start", "OK", "");
        }
    }

    private void doGenerate(bool bindPose) {        
        Mesh sourceMesh = skinnedMesh.sharedMesh;
        Mesh newMesh = new Mesh ();
        string parentName = skinnedMesh.transform.parent.name;
        int size = 3;
        int numIndices = triCount * 3;
        var newVertices = new Vector3 [size];
        var newNormals = new Vector3 [size];
        var newTangents = new Vector4 [size];
        var newBoneWeights = new BoneWeight [size];
        // var newUV1 = new Vector4[size];
        // var newColors = new Color32 [size];
        var newIndices = new List<int []> ();
        var curIndices = new int [numIndices];
        newIndices.Add (curIndices);
        var newBones = skinnedMesh.bones;
        IList<Transform> boneColl = newBones;
        int[] boneIndexArray = new int [3];
        boneIndexArray [0] = boneColl.IndexOf (headBone);
        boneIndexArray [1] = boneColl.IndexOf (leftHandBone);
        boneIndexArray [2] = boneColl.IndexOf (rightHandBone);
        var newBindPoses = sourceMesh.bindposes;
        Matrix4x4 [] inverseBindPoses = new Matrix4x4 [newBindPoses.Length];
        for (int i = 0; i < inverseBindPoses.Length; i++) {
            inverseBindPoses [i] = newBindPoses [i].inverse;
        }
        for (int i = 0; i < size; i++) {
            int boneIndex = boneIndexArray[i];
            Transform t = newBones [boneIndex];
            int dstVert = i;
            Debug.Log ("Bone " + boneIndex + ": " + t.name, t);
            Vector3 newTang;
            // Generate Bone transform matrix
            newVertices [dstVert] = bindPose ? new Vector3 (0, 0, 0) : inverseBindPoses [boneIndex].MultiplyPoint (new Vector3 (0, 0, 0));
            newNormals [dstVert] = bindPose ? new Vector3 (0, 0, 1) : inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (0, 0, 1));
            newTang = bindPose ? new Vector3 (1, 0, 0) : inverseBindPoses [boneIndex].MultiplyVector (new Vector3 (1, 0, 0));
            newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
            newBoneWeights [dstVert] = new BoneWeight ();
            newBoneWeights [dstVert].boneIndex0 = boneIndex;
            newBoneWeights [dstVert].weight0 = 1.0f;
            /*byte [] simpleName = SimplifyBoneName (t.name);
            newColors [dstVert] = new Color32 ();
            newColors [dstVert].r = simpleName [0];
            newColors [dstVert].g = simpleName [1];
            newColors [dstVert].b = simpleName [2];
            newColors [dstVert].a = simpleName [3];
            if (i % NUM_BONE_MODES == 0) {
                curIndices [dstVert * 3] = i; // bind pose matrix
                curIndices [dstVert * 3 + 1] = i + (NUM_BONE_MODES >= 2 ? 1 : 0); // bone transform
                curIndices [dstVert * 3 + 2] = i + (NUM_BONE_MODES >= 3 ? 2 : 0); // unused - keep degenerate for Standard fallback
            }*/
            Debug.Log ("i:" + boneIndex + " v:" + dstVert + ": Adding vertex " + d (newVertices [dstVert]) + "/" + d (newNormals [dstVert]) + "/" + d (newTangents [dstVert]));
        }
        for (int i = 0; i < curIndices.Length; i++) {
            curIndices[i] = i % 3;
        }
        newMesh.vertices = newVertices;
        newMesh.normals = newNormals;
        newMesh.tangents = newTangents;
        newMesh.boneWeights = newBoneWeights;
        // newMesh.colors32 = newColors;
        // newMesh.SetUVs(0, new List<Vector4>(newUV1));
        newMesh.subMeshCount = 1;
        newMesh.SetTriangles (curIndices, 0);
        newMesh.bounds = sourceMesh.bounds;
        newMesh.bindposes = newBindPoses;
        newMesh.name = headBone.name + "_" + leftHandBone.name + "_" + rightHandBone.name;
        GameObject newGameObject = new GameObject (newMesh.name);
        newGameObject.transform.parent = skinnedMesh.transform.parent;
        newGameObject.transform.localPosition = skinnedMesh.transform.localPosition;
        SkinnedMeshRenderer newRenderer = newGameObject.AddComponent<SkinnedMeshRenderer> ();
        newRenderer.sharedMesh = newMesh;
        newRenderer.bones = newBones;
        newRenderer.rootBone = skinnedMesh.rootBone;
        Material [] newMaterials = new Material [1];
        newMaterials [0] = skinnedMesh.sharedMaterials [0];
        newRenderer.sharedMaterials = newMaterials;
        newRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        newRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        newRenderer.receiveShadows = false;
        newRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        newRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        newRenderer.updateWhenOffscreen = true;
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        int lastSlash = parentName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? parentName : parentName.Substring (lastSlash + 1);
        outFileName = outFileName.Split ('.') [0];
        string fileName = pathToGenerated + "/ZZskintri_" + outFileName + "_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (newRenderer.sharedMesh, fileName);
        AssetDatabase.SaveAssets ();
    }
}
