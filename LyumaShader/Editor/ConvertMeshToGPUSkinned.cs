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

namespace utils {
    public class MeshCombiner {
        #region Operations
        //! Combine mesh.
        /*!
            \return combined mesh instance.
        */
        public static GameObject Combine (List<SkinnedMeshRenderer> SkinnedRenderers)
        {
            // Generated GO
            GameObject final_mesh_go = new GameObject ("Mesh");
            // Dummy parent holder
            GameObject dummy_parent = new GameObject ("DummyParent");

            // All available bones
            /*var all_bones = new BoneTransformDict();
            // Traverse through all skinned mesh renderers
            foreach(var renderer in SkinnedRenderers)
            {
                var renderer_bones = renderer.bones;
                foreach (var bone in renderer_bones)
                {
                    // Bone doesn't exist, add it
                    if (!all_bones.ContainsKey(bone.name))
                        all_bones[bone.name] = new utils.Tuple<Transform, string>(bone, bone.parent.name);
                }
            }*/

            var combineInstanceArrays = new Dictionary<Material, List<CombineInstance>> ();
            var bone_weights = new Dictionary<Mesh, BoneWeight []> ();
            // Map between bone name and index
            var added_bones = new Dictionary<string, int> ();
            // List of child objects holding the skinned mesh renderers to be
            // destroyed when finished
            var child_objects_to_destroy = new List<GameObject> ();

            int bone_index = 0;
            foreach (var renderer in SkinnedRenderers) {
                child_objects_to_destroy.Add (renderer.transform.parent.gameObject);

                var renderer_bones = renderer.bones;
                // Add all bones as first and save the indices of them
                foreach (var bone in renderer_bones) {
                    // Bone not yet added
                    if (!added_bones.ContainsKey (bone.name))
                        added_bones [bone.name] = bone_index++;
                }
                // Adjust bone weights indices based on real indices of bones
                var bone_weights_list = new BoneWeight [renderer.sharedMesh.boneWeights.Length];
                var renderer_bone_weights = renderer.sharedMesh.boneWeights;
                for (int i = 0; i < renderer_bone_weights.Length; ++i) {

                    BoneWeight current_bone_weight = renderer_bone_weights [i];

                    current_bone_weight.boneIndex0 = added_bones [renderer_bones [current_bone_weight.boneIndex0].name];
                    current_bone_weight.boneIndex2 = added_bones [renderer_bones [current_bone_weight.boneIndex2].name];
                    current_bone_weight.boneIndex3 = added_bones [renderer_bones [current_bone_weight.boneIndex3].name];
                    current_bone_weight.boneIndex1 = added_bones [renderer_bones [current_bone_weight.boneIndex1].name];

                    bone_weights_list [i] = current_bone_weight;
                }
                bone_weights [renderer.sharedMesh] = bone_weights_list;

                // Handle bad input
                if (renderer.sharedMaterials.Length != renderer.sharedMesh.subMeshCount) {
                    Debug.LogError ("Mismatch between material count and submesh count. Is this the correct MeshRenderer?");
                    continue;
                }

                // Prepare stuff for mesh combination with same materials
                for (int i = 0; i < renderer.sharedMesh.subMeshCount; i++) {
                    // Material not in dict, add it
                    if (!combineInstanceArrays.ContainsKey (renderer.sharedMaterials [i]))
                        combineInstanceArrays [renderer.sharedMaterials [i]] = new List<CombineInstance> ();
                    var actual_mat_list = combineInstanceArrays [renderer.sharedMaterials [i]];
                    // Add new instance
                    var combine_instance = new CombineInstance ();
                    combine_instance.transform = renderer.transform.localToWorldMatrix;
                    combine_instance.subMeshIndex = i;
                    combine_instance.mesh = renderer.sharedMesh;

                    actual_mat_list.Add (combine_instance);
                }
                // No need to use it anymore
                renderer.enabled = false;
            }
            /*
            var bones_hierarchy = new HierarchyDict();
            // Recreate bone structure
            foreach (var bone in all_bones)
            {
                // Bone not processed, process it
                if (!bones_hierarchy.ContainsKey(bone.Key))
                    AddParent(bone.Key, bones_hierarchy, all_bones, dummy_parent);
            }
 */

            // Create bone array from preprocessed dict
            var bones = new Transform [added_bones.Count];
            //foreach (var bone in added_bones)
            //    bones[bone.Value] = bones_hierarchy[bone.Key];
            // Get the root bone
            Transform root_bone = bones [0];

            while (root_bone.parent != null) {
                // Get parent
                //if (bones_hierarchy.ContainsKey(root_bone.parent.name))
                root_bone = root_bone.parent;
                //else
                //    break;
            }


            // Create skinned mesh renderer GO
            GameObject combined_mesh_go = new GameObject ("Combined");
            combined_mesh_go.transform.parent = final_mesh_go.transform;
            combined_mesh_go.transform.localPosition = Vector3.zero;

            // Fill bind poses
            var bind_poses = new Matrix4x4 [bones.Length];
            for (int i = 0; i < bones.Length; ++i)
                bind_poses [i] = bones [i].worldToLocalMatrix * combined_mesh_go.transform.localToWorldMatrix;

            // Need to move it to new GO
            root_bone.parent = final_mesh_go.transform;

            // Combine meshes into one
            var combined_new_mesh = new Mesh ();
            var combined_vertices = new List<Vector3> ();
            var combined_uvs = new List<Vector2> ();
            var combined_indices = new List<int []> ();
            var combined_bone_weights = new List<BoneWeight> ();
            var combined_materials = new Material [combineInstanceArrays.Count];

            var vertex_offset_map = new Dictionary<Mesh, int> ();

            int vertex_index_offset = 0;
            int current_material_index = 0;

            foreach (var combine_instance in combineInstanceArrays) {
                combined_materials [current_material_index++] = combine_instance.Key;
                var submesh_indices = new List<int> ();
                // Process meshes for each material
                foreach (var combine in combine_instance.Value) {
                    // Update vertex offset for current mesh
                    if (!vertex_offset_map.ContainsKey (combine.mesh)) {
                        // Add vertices for mesh
                        combined_vertices.AddRange (combine.mesh.vertices);
                        // Set uvs
                        combined_uvs.AddRange (combine.mesh.uv);
                        // Add weights
                        combined_bone_weights.AddRange (bone_weights [combine.mesh]);

                        vertex_offset_map [combine.mesh] = vertex_index_offset;
                        vertex_index_offset += combine.mesh.vertexCount;
                    }
                    int vertex_current_offset = vertex_offset_map [combine.mesh];

                    var indices = combine.mesh.GetTriangles (combine.subMeshIndex);
                    // Need to "shift" indices
                    for (int k = 0; k < indices.Length; ++k)
                        indices [k] += vertex_current_offset;

                    submesh_indices.AddRange (indices);
                }
                // Push indices for given submesh
                combined_indices.Add (submesh_indices.ToArray ());
            }

            combined_new_mesh.vertices = combined_vertices.ToArray ();
            combined_new_mesh.uv = combined_uvs.ToArray ();
            combined_new_mesh.boneWeights = combined_bone_weights.ToArray ();

            combined_new_mesh.subMeshCount = combined_materials.Length;
            for (int i = 0; i < combined_indices.Count; ++i)
                combined_new_mesh.SetTriangles (combined_indices [i], i);

            // Create mesh renderer
            SkinnedMeshRenderer combined_skin_mesh_renderer = combined_mesh_go.AddComponent<SkinnedMeshRenderer> ();
            combined_skin_mesh_renderer.sharedMesh = combined_new_mesh;
            combined_skin_mesh_renderer.bones = bones;
            combined_skin_mesh_renderer.rootBone = root_bone;
            combined_skin_mesh_renderer.sharedMesh.bindposes = bind_poses;

            combined_skin_mesh_renderer.sharedMesh.RecalculateNormals ();
            combined_skin_mesh_renderer.sharedMesh.RecalculateBounds ();
            combined_skin_mesh_renderer.sharedMaterials = combined_materials;

            // Destroy children
            foreach (var child in child_objects_to_destroy)
                GameObject.DestroyImmediate (child);
            // Destroy dummy parent
            GameObject.DestroyImmediate (dummy_parent);

            return final_mesh_go;
        }

        /*
        static void AddParent(string BoneName, HierarchyDict BoneHierarchy, BoneTransformDict AllBones, GameObject DummyParent)
        {
            Transform actual_bone = null;
            // Must be bone
            if (AllBones.ContainsKey(BoneName))
            {
                var bone_tuple = AllBones[BoneName];
                // Add parent recursively if not added
                if (!BoneHierarchy.ContainsKey(bone_tuple._2))
                {
                    AddParent(bone_tuple._2, BoneHierarchy, AllBones, DummyParent);
                    // Unparent all children of parents
                    Unparent(BoneHierarchy[bone_tuple._2], DummyParent);
                }
 
 
                bone_tuple._1.parent = BoneHierarchy[bone_tuple._2];
                actual_bone = bone_tuple._1;
            }
 
            BoneHierarchy[BoneName] = actual_bone;
        }
 
        static void Unparent(Transform Parent, GameObject DummyParent)
        {
            if (Parent != null)
            {
                var unparent_list = new List<Transform>();
 
                foreach (Transform child in Parent.transform)
                    unparent_list.Add(child);
 
                foreach (var child in unparent_list)
                    child.parent = DummyParent.transform;
            }
        }
        */
        #endregion
    }
}

//#if DO_STUFF_CONVERT_MESH_TO
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

        public BoneWeight getPrimaryBoneWeight() {
            BoneWeight bw = new BoneWeight();
            for (int i = 0; i < indices.Count; i++) {
                if (weights[i] > bw.weight0) {
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
    [MenuItem("CONTEXT/SkinnedMeshRenderer/Convert mesh to GPU skinned")]
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
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[curIdx + k];
                    int dstVert = j + k;
                    long vhash = computeVertexHash(srcVertices[srcVert]);
                    WeightSum val = null;
                    vertexHashToBones.TryGetValue (vhash, out val);
                    bws [k] = val.getPrimaryBoneWeight ();
                }
                //TODO: make sure only 2 bones show up here. if 3 we have a problem....
                for (int k = 0; k < 3; k++) {
                    int srcIndex = curIdx + k;
                    int srcVert = curIndices[srcIndex];
                    int dstVert = j + k;
                    newVertices [dstVert] = new Vector3 ((float)i * 0.04f, curIdx / (float)curIndices.Length, (float)k * 0.02f);//inverseBindPoses [sortedTriWeights [k].Key].MultiplyPoint (new Vector3 (0, 0, 0));
                    newNormals [dstVert] = new Vector3 (0, 0, 1);//inverseBindPoses [sortedTriWeights [k].Key].MultiplyVector (new Vector3 (0, 0, 1));
                    Vector3 newTang = new Vector3 (1, 0, 0);//inverseBindPoses[sortedTriWeights[k].Key].MultiplyVector(new Vector3(1, 0, 0));
                    newTangents [dstVert] = new Vector4 (newTang.x, newTang.y, newTang.z, 1);
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
                    for (int bwi = 0; bwi < 3; bwi++) {
                        bweights [bwi] = 0;
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
                    }
                    Vector3 bweightvec = new Vector3 (bweights [0], bweights [1], bweights [2]);
                    if (bweightvec.x <= 0 && bweightvec.y <= 0 && bweightvec.z <= 0) {
                        bweightvec.x = 1;
                    }
                    bweightvec.Normalize ();

                    /*Matrix4x4 inverseBindPose = new Matrix4x4 ();
                    for (int bwi = 0; bwi < 16; bwi++) {
                        inverseBindPose [bwi] += inverseBindPoses [sortedTriWeights [0].Key] * bweightvec.x;
                        inverseBindPose [bwi] += inverseBindPoses [sortedTriWeights [1].Key] * bweightvec.y;
                        inverseBindPose [bwi] += inverseBindPoses [sortedTriWeights [2].Key] * bweightvec.z;
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
                    newUV4[dstVert].w = bws[k].boneIndex0 * srcTangent.w;// allow flipping binormal?
                    newColors[dstVert] = new Color();
                    newColors[dstVert].r = (float)(255 * bweightvec.x);
                    newColors[dstVert].g = (float)(255 * bweightvec.y);
                    newColors[dstVert].b = (float)(255 * bweightvec.z);
                    if (srcColors.Length > srcVert) {
                        newColors [dstVert].a = (float)(((int)srcColors [srcVert].r) |
                                               ((int)srcColors [srcVert].g << 8) |
                                               ((int)srcColors [srcVert].b << 16));
                    } else {
                        newColors [dstVert].a = 0;
                    }
                    curIndices[srcIndex] = dstVert;
                    //Debug.Log ("i:" + srcIndex + " v:" + srcVert + "->" + dstVert + ": Adding vertex " + d(srcPosition) + "/" + d(srcTangent) + "/" + d(srcNormal) + " to uv1:" + d(newUV1 [dstVert]) + " to uv2:" + d(newUV2 [dstVert]) + " to uv3:" + d(newUV3 [dstVert]) + " to uv4:" + d(newUV4 [dstVert]));
                }
            }
            newIndices.Add(curIndices);
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

public class Equipmentizer : MonoBehaviour
{
    //[Tooltip("Here goes the SkinnedMeshRenderer you want to target")]
    //public GameObject target;

    //void Start()
    [MenuItem("CONTEXT/SkinnedMeshRenderer/Equipmentizer")]
    public static void Equipmentizer_(MenuCommand command)
    {
        SkinnedMeshRenderer myRenderer = command.context as SkinnedMeshRenderer;
        GameObject gameObject = myRenderer.gameObject.transform.parent.gameObject;
        SkinnedMeshRenderer targetRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
        Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
        foreach (Transform bone in targetRenderer.bones) boneMap[bone.gameObject.name] = bone;
        Transform[] newBones = new Transform[myRenderer.bones.Length];
        for (int i = 0; i < myRenderer.bones.Length; ++i)
        {
            Debug.Log("bone " + i + ": " + myRenderer.bones[i]);
            GameObject bone = myRenderer.bones[i].gameObject;
            if (!boneMap.TryGetValue(bone.name, out newBones[i]))
            {
                Debug.Log("Unable to map bone \"" + bone.name + "\" to target skeleton.");
                //return;
            }
        }
        myRenderer.bones = newBones;
    }
}
//#endif
#endif