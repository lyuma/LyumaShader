using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

public class AssetizeMeshes : EditorWindow {

	[MenuItem ("CONTEXT/Mesh/Assetize Meshes")]
	[MenuItem ("CONTEXT/MeshFilter/Assetize Meshes")]
	[MenuItem ("CONTEXT/MeshRenderer/Assetize Meshes")]
	[MenuItem ("CONTEXT/SkinnedMeshRenderer/Assetize Meshes")]
	public static void RunAssetizeMeshes (MenuCommand command)
    {
        Mesh sourceMesh;
        SkinnedMeshRenderer smr = null;
        MeshRenderer mr = null;
        MeshFilter mf = null;
		Transform trans = null;
		string parentName = "";
        if (command.context is SkinnedMeshRenderer) {
            smr = command.context as SkinnedMeshRenderer;
			trans = smr.transform;
            sourceMesh = smr.sharedMesh;
        } else if (command.context is MeshRenderer) {
            mr = command.context as MeshRenderer;
            mf = mr.transform.GetComponent<MeshFilter> ();
			trans = mr.transform;
            sourceMesh = mf.sharedMesh;
        } else if (command.context is MeshFilter) {
            mf = command.context as MeshFilter;
			trans = mf.transform;
            sourceMesh = mf.sharedMesh;
		} else if (command.context is Mesh) {
			sourceMesh = command.context as Mesh;
        } else {
            EditorUtility.DisplayDialog ("MergeUVs", "Unknkown context type " + command.context.GetType ().FullName, "OK", "");
            throw new NotSupportedException ("Unknkown context type " + command.context.GetType ().FullName);
        }
		if (trans != null) {
			// Get name of top-most object this mesh is attached to
			while (trans.parent != null) {
				trans = trans.parent;
			}
			parentName = trans.name + "_";
		}
		parentName += sourceMesh.name;
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
        Vector3 [] srcVertices = sourceMesh.vertices;
		Color [] srcColors = sourceMesh.colors; // Forces to half precision?
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
            newMesh.boneWeights = srcBoneWeights;
        }
        if (srcColors != null && srcColors.Length > 0) {
            newMesh.colors = srcColors;
        }
		if (srcUV.Count > 0) {
			newMesh.SetUVs (0, srcUV);
		}
		if (srcUV2.Count > 0) {
			newMesh.SetUVs (1, srcUV2);
		}
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
		int lastSlash = parentName.LastIndexOf ('/');
		string outFileName = lastSlash == -1 ? parentName : parentName.Substring (lastSlash + 1);
		outFileName = outFileName.Split ('.') [0];
		string fileName = pathToGenerated + "/" + outFileName + "_assetized_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
		if (smr == null && mf == null) {
			EditorGUIUtility.PingObject (meshAfterUpdate);
		}
    }
}
