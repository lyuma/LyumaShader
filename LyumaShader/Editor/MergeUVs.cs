using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

public class MergeUVs : EditorWindow {

    [MenuItem ("CONTEXT/MeshFilter/Merge UV and UV2")]
    [MenuItem ("CONTEXT/MeshRenderer/Merge UV and UV2")]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Merge UV and UV2")]
    public static void RunMergeUVs (MenuCommand command)
    {
        Mesh sourceMesh;
        SkinnedMeshRenderer smr = null;
        MeshRenderer mr = null;
        MeshFilter mf = null;
        if (command.context is SkinnedMeshRenderer) {
            smr = command.context as SkinnedMeshRenderer;
            sourceMesh = smr.sharedMesh;
        } else if (command.context is MeshRenderer) {
            mr = command.context as MeshRenderer;
            mf = mr.transform.GetComponent<MeshFilter> ();
            sourceMesh = mf.sharedMesh;
        } else if (command.context is MeshFilter) {
            mf = command.context as MeshFilter;
            sourceMesh = mf.sharedMesh;
        } else {
            EditorUtility.DisplayDialog ("MergeUVs", "Unknkown context type " + command.context.GetType ().FullName, "OK", "");
            throw new NotSupportedException ("Unknkown context type " + command.context.GetType ().FullName);
        }
        Mesh newMesh = new Mesh ();
        int size = sourceMesh.vertices.Length;
        List<Vector2> srcUV = new List<Vector2>(); // will discard zw
        List<Vector2> srcUV2 = new List<Vector2>(); // will discard zw
        List<Vector4> srcUV3 = new List<Vector4>();
        List<Vector4> srcUV4 = new List<Vector4>();
        sourceMesh.GetUVs (0, srcUV);
        sourceMesh.GetUVs (1, srcUV2);
        sourceMesh.GetUVs (2, srcUV3);
        sourceMesh.GetUVs (3, srcUV4);
        if (srcUV.Count == 0) {
            EditorUtility.DisplayDialog ("MergeUVs", "Source mesh has no UV!", "OK", "");
            return;
        }
        if (srcUV2.Count == 0) {
            EditorUtility.DisplayDialog ("MergeUVs", "Source mesh has no UV2!", "OK", "");
            return;
        }
        Vector3 [] srcVertices = sourceMesh.vertices;
        Color32 [] srcColors = sourceMesh.colors32; // FIXME: Should use colors?
        Vector3 [] srcNormals = sourceMesh.normals;
        Vector4 [] srcTangents = sourceMesh.tangents;
        Matrix4x4 [] srcBindposes = sourceMesh.bindposes;
        BoneWeight [] srcBoneWeights = sourceMesh.boneWeights;
        var newUV1 = new Vector4[size];
        for (int i = 0; i < size; i++) {
            Vector2 uv1 = srcUV [i];
            Vector2 uv2 = srcUV2 [i];
            newUV1 [i] = new Vector4 (uv1.x, uv1.y, uv2.x, uv2.y);
        }
        newMesh.vertices = srcVertices;
        if (srcNormals != null && srcNormals.Length > 0) {
            newMesh.normals = srcNormals;
        }
        if (srcTangents != null && srcTangents.Length > 0) {
            newMesh.tangents = srcTangents;
        }
        if (srcBoneWeights != null && srcBoneWeights.Length > 0) {
            newMesh.boneWeights = srcBoneWeights;
        }
        if (srcColors != null && srcColors.Length > 0) {
            newMesh.colors32 = srcColors;
        }
        newMesh.SetUVs(0, new List<Vector4>(newUV1));
        if (srcUV3.Count > 0) {
            newMesh.SetUVs (2, srcUV3);
        }
        if (srcUV4.Count > 0) {
            newMesh.SetUVs (3, srcUV4);
        }
        newMesh.subMeshCount = sourceMesh.subMeshCount;
        for (int i = 0; i < sourceMesh.subMeshCount; i++) {
            var curIndices = sourceMesh.GetIndices (i);
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology(i), i);
        }
        newMesh.bounds = sourceMesh.bounds;
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = sourceMesh.bindposes;
        }
        for (int i = 0; i < sourceMesh.blendShapeCount; i++) {
            var blendShapeName = sourceMesh.GetBlendShapeName (i);
            var blendShapeFrameCount = sourceMesh.GetBlendShapeFrameCount (i);
            for (int frameIndex = 0; frameIndex < blendShapeFrameCount; frameIndex++) {
                float weight = sourceMesh.GetBlendShapeFrameWeight(i, frameIndex);
                Vector3 [] deltaVertices = new Vector3 [size];
                Vector3 [] deltaNormals = new Vector3 [size];
                Vector3 [] deltaTangents = new Vector3 [size];
                sourceMesh.GetBlendShapeFrameVertices (i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                newMesh.AddBlendShapeFrame (blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
            }
        }
        newMesh.name = sourceMesh.name + "_uvmerged";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to merged UVs");
            smr.sharedMesh = newMesh;
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to merged UVs");
            mf.sharedMesh = newMesh;
            meshAfterUpdate = mf.sharedMesh;
        }
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        string fileName = pathToGenerated + "/ZZmergeuvs_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
    }
}
