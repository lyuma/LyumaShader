//GenerateBoneMesh
//Script

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//using HierarchyDict = System.Collections.Generic.Dictionary<string, UnityEngine.Transform>;
//using BoneTransformDict = System.Collections.Generic.Dictionary<string, Tuple<UnityEngine.Transform, string>>;

public class GenerateBoneTransformRenderer : MonoBehaviour {
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
        for (; nextCapital < name.Length && !char.IsUpper(name[nextCapital]) && name[nextCapital - 1] != ' '; nextCapital++) {}
        if (nextCapital == name.Length) {
            nextCapital = 1;
        }
        for (int i = 0; i < 3 && nextCapital < name.Length; i++, nextCapital++) {
            outb [i + 1] = (byte)name[nextCapital];
        }
        Debug.Log ("simplify name: " + name + " -> " + (char)outb [0] + (char)outb [1] + (char)outb [2] + (char)outb [3]);
        return outb;
    }
    //[MenuItem("GameObject/Create Mesh")]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Generate bone transform mesh")]
    public static void GenerateBoneTransformRenderer_ (MenuCommand command)
    {
        int NUM_BONE_MODES = 3;
        SkinnedMeshRenderer renderer = command.context as SkinnedMeshRenderer;
        Mesh sourceMesh = renderer.sharedMesh;
        int size = renderer.bones.Length * NUM_BONE_MODES;

        Mesh newMesh = new Mesh ();
        var newVertices = new Vector3 [size];
        var newNormals = new Vector3 [size];
        var newTangents = new Vector4 [size];
        var newBoneWeights = new BoneWeight [size];
        // var newUV1 = new Vector4[size];
        var newColors = new Color32 [size];
        var newIndices = new List<int []> ();
        var curIndices = new int [size];
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
                newTang = inverseBindPoses[boneIndex].MultiplyVector(new Vector3(1, 0, 0));
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
            Debug.Log ("i:" + boneIndex + " v:" + dstVert + ": Adding vertex " + d(newVertices [dstVert]) + "/" + d(newNormals [dstVert]) + "/" + d(newTangents [dstVert]));
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
        newMaterials[0] = renderer.sharedMaterials[0];
        newRenderer.sharedMaterials = newMaterials;
        newRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        newRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        newRenderer.receiveShadows = false;
        newRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        newRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        newRenderer.updateWhenOffscreen = true;
        AssetDatabase.CreateAsset(newRenderer.sharedMesh, "Assets/" + sourceMesh.name + "_boneMesh.asset");
        AssetDatabase.SaveAssets ();
    }
}
#endif