using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
public class LyumaMeshTools : EditorWindow {

    [MenuItem ("CONTEXT/Mesh/\u2014\u2014LyumaMeshTools\u2014\u2014", true)]
    [MenuItem ("CONTEXT/MeshFilter/\u2014\u2014LyumaMeshTools\u2014\u2014", true)]
    [MenuItem ("CONTEXT/MeshRenderer/\u2014\u2014LyumaMeshTools\u2014\u2014", true)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/\u2014\u2014LyumaMeshTools\u2014\u2014", true)]
    public static bool LyumaMeshToolsVal (MenuCommand command) {
        return false;
    }

    [MenuItem ("CONTEXT/Mesh/\u2014\u2014LyumaMeshTools\u2014\u2014", false, 130)]
    [MenuItem ("CONTEXT/MeshFilter/\u2014\u2014LyumaMeshTools\u2014\u2014", false, 130)]
    [MenuItem ("CONTEXT/MeshRenderer/\u2014\u2014LyumaMeshTools\u2014\u2014", false, 130)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/\u2014\u2014LyumaMeshTools\u2014\u2014", false, 130)]
    public static void LyumaMeshToolsAct (MenuCommand command) {}


    [MenuItem ("CONTEXT/Mesh/Merge UV and UV2 : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Merge UV and UV2 : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshRenderer/Merge UV and UV2 : LMT", false, 131)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Merge UV and UV2 : LMT", false, 131)]
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
		} else if (command.context is Mesh) {
			sourceMesh = command.context as Mesh;
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
        Color [] srcColors = sourceMesh.colors; // FIXME: Should use colors?
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
            newMesh.colors = srcColors;
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
	[MenuItem ("CONTEXT/Mesh/Assetize Meshes : LMT", false, 131)]
	[MenuItem ("CONTEXT/MeshFilter/Assetize Meshes : LMT", false, 131)]
	[MenuItem ("CONTEXT/MeshRenderer/Assetize Meshes : LMT", false, 131)]
	[MenuItem ("CONTEXT/SkinnedMeshRenderer/Assetize Meshes : LMT", false, 131)]
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
        List<Vector4> srcUV = new List<Vector4>();
        List<Vector4> srcUV2 = new List<Vector4>();
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

    [MenuItem ("CONTEXT/Mesh/Add Shadow : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Add Shadow : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshRenderer/Add Shadow : LMT", false, 131)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Add Shadow : LMT", false, 131)]
    public static void RunAddShadow (MenuCommand command)
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
        List<Vector4> srcUV = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV2 = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV3 = new List<Vector4> ();
        List<Vector4> srcUV4 = new List<Vector4> ();
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
        newMesh.subMeshCount = sourceMesh.subMeshCount + 1;
        List<int> allIndices = new List<int> ();
        for (int i = 0; i < sourceMesh.subMeshCount; i++) {
            var curIndices = sourceMesh.GetIndices (i);
            if (sourceMesh.GetTopology (i) == MeshTopology.Triangles) {
                allIndices.AddRange (curIndices);
            }
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology (i), i);
        }
        newMesh.SetIndices (allIndices.ToArray(), MeshTopology.Triangles, sourceMesh.subMeshCount);
        newMesh.bounds = sourceMesh.bounds;
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = sourceMesh.bindposes;
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
        newMesh.name = sourceMesh.name + "_shadow";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to shadow");
            smr.sharedMesh = newMesh;
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to shadow");
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
        string fileName = pathToGenerated + "/" + outFileName + "_shadow_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
        if (smr == null && mf == null) {
            EditorGUIUtility.PingObject (meshAfterUpdate);
        }
    }

    [MenuItem ("CONTEXT/Mesh/Combine same material : LMT [Requires renderer]", true)]
    [MenuItem ("CONTEXT/MeshFilter/Combine same material : LMT [Requires renderer]", true)]
    public static bool RunCombineSameMaterialVal (MenuCommand command) {
        return false;
    }

    [MenuItem ("CONTEXT/Mesh/Combine same material : LMT [Requires renderer]", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Combine same material : LMT [Requires renderer]", false, 131)]
    [MenuItem ("CONTEXT/MeshRenderer/Combine same material : LMT", false, 131)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Combine same material : LMT", false, 131)]
    public static void RunCombineSameMaterial (MenuCommand command)
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
        } else {
            EditorUtility.DisplayDialog ("CombineSameMaterial", "Must have a renderer", "OK", "");
            throw new NotSupportedException ("Unknkown context type " + command.context.GetType ().FullName);
        }
        if (trans != null) {
            // Get name of top-most object this mesh is attached to
            while (trans.parent != null) {
                trans = trans.parent;
            }
            parentName = trans.name + "_";
        }
        Dictionary<Material, int> materialToIndex = new Dictionary<Material, int> ();
        List<Material> finalMaterials = new List<Material>();
        Dictionary<Material, List<int>> materialToSubmesh = new Dictionary<Material, List<int>> ();
        int subi = 0;
        foreach (Material mat in mr == null ? smr.sharedMaterials : mr.sharedMaterials) {
            if (!materialToIndex.ContainsKey(mat)) {
                materialToIndex [mat] = finalMaterials.Count;
                finalMaterials.Add (mat);
                materialToSubmesh.Add (mat, new List<int> ());
            }
            materialToSubmesh [mat].Add (subi);
            subi++;
        }
        parentName += sourceMesh.name;
        Mesh newMesh = new Mesh ();
        int size = sourceMesh.vertices.Length;
        List<Vector4> srcUV = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV2 = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV3 = new List<Vector4> ();
        List<Vector4> srcUV4 = new List<Vector4> ();
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
        newMesh.subMeshCount = finalMaterials.Count;
        for (int i = 0; i < newMesh.subMeshCount; i++) {
            List<int> curIndices = new List<int> ();
            MeshTopology topo = sourceMesh.GetTopology(materialToSubmesh[finalMaterials[i]][0]);
            foreach (int thisIndex in materialToSubmesh [finalMaterials[i]]) {
                if (sourceMesh.GetTopology (thisIndex) == topo) {
                    curIndices.AddRange (sourceMesh.GetIndices (thisIndex));
                }
            }
            newMesh.SetIndices (curIndices.ToArray<int>(), topo, i);
        }
        newMesh.bounds = sourceMesh.bounds;
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = sourceMesh.bindposes;
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
        newMesh.name = sourceMesh.name + "_combinemat";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to shadow");
            smr.sharedMesh = newMesh;
            smr.sharedMaterials = finalMaterials.ToArray<Material>();
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to combinemat");
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
        string fileName = pathToGenerated + "/" + outFileName + "_combinemat_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
        if (smr == null && mf == null) {
            EditorGUIUtility.PingObject (meshAfterUpdate);
        }
    }

    static float uvOffset = 0;

    [MenuItem ("CONTEXT/Mesh/Make Skinned Parent+Child : LMT", true)]
    [MenuItem ("CONTEXT/MeshRenderer/Make Skinned : LMT [use MeshFilter]", true)]
    public static bool RunMakeSkinnedVal (MenuCommand command) {
        return false;
    }

    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Skin to child : LMT", false, 131)]
    [MenuItem ("CONTEXT/Mesh/Make Skinned Parent+Child : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Make Skinned Parent+Child : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshRenderer/Make Skinned : LMT [use MeshFilter]", false, 131)]
    public static void RunMakeSkinned (MenuCommand command)
    {
        SkinnedMeshRenderer smr;
        if (command.context is MeshFilter) {
            MeshFilter mf = command.context as MeshFilter;
            GameObject go = mf.gameObject;

            if (go.GetComponent<MeshRenderer>() == null) {
                EditorUtility.DisplayDialog ("LyumaMeshTools", "object must have a MeshRenderer.", "OK", "");
                return;
            }
            //Undo.RecordObject (go, "Switched SkinnedMeshRenderer to bone");
            smr = Undo.AddComponent<SkinnedMeshRenderer>(go);
            smr.rootBone = go.transform.parent;
        } else {
            smr = command.context as SkinnedMeshRenderer;
        }
        Transform trans = smr.transform;
        Mesh sourceMesh = smr.sharedMesh;
        string parentName = "";
        Transform child = null;
        if (trans.childCount == 1) {
            child = trans.GetChild (0);
        } else {
            EditorUtility.DisplayDialog ("LyumaMeshTools", "Skinned mesh Must have a single child.", "OK", "");
            return;
        }
        if (smr.rootBone == null) {
            EditorUtility.DisplayDialog ("LyumaMeshTools", "Skinned mesh Must have a root bone assigned.", "OK", "");
            return;
        }
        if (smr.rootBone.transform == smr.transform) {
            if (!EditorUtility.DisplayDialog ("LyumaMeshTools", "Skinned mesh should have a different transform set as root bone.", "Continue", "Cancel")) {
                return;
            }
        }
        parentName = trans.name + "_";
        bool staticNormal = trans.name.Contains("NONORMAL");
        bool relativeNormal = trans.name.Contains("NORMALREL");
        bool staticPosition = trans.name.Contains("NOPOSITION");
        bool relativePosition = trans.name.Contains("POSITIONREL");
        Mesh newMesh = new Mesh ();
        int size = sourceMesh.vertices.Length;
        List<Vector4> srcUV = new List<Vector4> ();
        List<Vector4> srcUV2 = new List<Vector4> ();
        List<Vector4> srcUV3 = new List<Vector4> ();
        List<Vector4> srcUV4 = new List<Vector4> ();
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
        Matrix4x4 invchildmatrix4 = smr.transform.worldToLocalMatrix * child.localToWorldMatrix;
        for (int i = 0; i < srcVertices.Length; i++) {
            if (staticPosition) {
                srcVertices [i] = new Vector3(0, 0, 0);
            }
            if (!relativePosition) {
                srcVertices [i] = invchildmatrix4.MultiplyPoint3x4 (srcVertices [i]);
            }
        }
        newMesh.vertices = srcVertices;
        if (srcNormals != null && srcNormals.Length > 0) {
            for (int i = 0; i < srcNormals.Length; i++) {
                if (staticNormal) {
                    srcNormals [i] = new Vector3(0, 0, 1);
                }
                if (!relativeNormal) {
                    srcNormals [i] = invchildmatrix4.MultiplyVector (srcNormals [i]);
                }
            }
            newMesh.normals = srcNormals;
        }
        if (srcTangents != null && srcTangents.Length > 0) {
            for (int i = 0; i < srcTangents.Length; i++) {
                if (staticNormal) {
                    srcTangents [i] = new Vector4 (1, 0, 0, 1);
                }
                if (!relativeNormal) {
                    Vector3 mulout = invchildmatrix4.MultiplyVector ((Vector3)srcTangents [i]);
                    srcTangents [i] = new Vector4(mulout.x, mulout.y, mulout.z, srcTangents[i].w);
                }
            }
            newMesh.tangents = srcTangents;
        }
        {
            BoneWeight [] newBoneWeights = new BoneWeight [srcVertices.Length];
            for (int i = 0; i < srcVertices.Length; i++) {
                BoneWeight bw = new BoneWeight ();
                if (child == null) {
                    bw.boneIndex1 = 0;
                    bw.weight1 = 1.0f;
                } else {
                    bw.boneIndex0 = 1;
                    bw.weight0 = 0.99999f;
                    bw.boneIndex1 = 0;
                    bw.weight1 = 0.00001f;
                }
                newBoneWeights [i] = bw;
            }
            newMesh.boneWeights = newBoneWeights;
        }
        if (srcColors != null && srcColors.Length > 0) {
            newMesh.colors = srcColors;
        }
        if (srcUV.Count > 0) {
            bool resetCount = EditorUtility.DisplayDialog ("Make Skinned", "Reset uv.z value? (Currently " + uvOffset + ")", "No UV / Reset to 0", "Use " + uvOffset);
            if (resetCount) {
                uvOffset = 0;
            } else {
                for (int i = 0; i < srcUV.Count; i++) {
                    Vector4 origI = srcUV [i];
                    origI.z = uvOffset;
                    srcUV [i] = origI;
                }
                newMesh.SetUVs (0, srcUV);
            }
            uvOffset++;
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
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology (i), i);
        }
        newMesh.bounds = sourceMesh.bounds;
        if (child == null) {
            Matrix4x4 rootmatrix = smr.rootBone.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            newMesh.bindposes = new Matrix4x4 [] { rootmatrix };
        } else {
            Matrix4x4 childmatrix4 = child.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            Matrix4x4 rootmatrix = smr.rootBone.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            newMesh.bindposes = new Matrix4x4[] { rootmatrix, childmatrix4 };
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
        newMesh.name = sourceMesh.name + "_uvmerged";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to bone");
            if (child == null) {
                smr.bones = new Transform [] { smr.transform.parent }; //smr.transform };
            } else {
                smr.bones = new Transform [] { smr.transform.parent, child }; //smr.transform };
            }
            smr.sharedMesh = newMesh;
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        int lastSlash = parentName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? parentName : parentName.Substring (lastSlash + 1);
        outFileName = outFileName.Split ('.') [0];
        string fileName = pathToGenerated + "/" + outFileName + "_boned_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
        //if (smr == null && mf == null) {
        //    EditorGUIUtility.PingObject (meshAfterUpdate);
        //}
    }

    [MenuItem ("CONTEXT/MeshRenderer/Keyframe Blend shapes : LMT [Requires skin]", true)]
    [MenuItem ("CONTEXT/MeshFilter/Keyframe Blend shapes : LMT [Requires skin]", true)]
    public static bool RunKeyframeBlendShapesVal (MenuCommand command) {
        return false;
    }

    [MenuItem ("CONTEXT/MeshRenderer/Keyframe Blend shapes : LMT [Requires skin]", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Keyframe Blend shapes : LMT [Requires skin]", false, 131)]
    [MenuItem ("CONTEXT/Mesh/Keyframe Blend shapes : LMT", false, 131)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Keyframe Blend shapes : LMT", false, 131)]
    public static void RunKeyframeBlendShapes (MenuCommand command)
    {
        Mesh sourceMesh;
        SkinnedMeshRenderer smr = null;
        Transform trans = null;
        string parentName = "";
        if (command.context is SkinnedMeshRenderer) {
            smr = command.context as SkinnedMeshRenderer;
            trans = smr.transform;
            sourceMesh = smr.sharedMesh;
        } else if (command.context is Mesh) {
            sourceMesh = command.context as Mesh;
        } else {
            EditorUtility.DisplayDialog ("RunKeyframeBlendShapes", "Unknkown context type " + command.context.GetType ().FullName, "OK", "");
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
        List<Vector4> srcUV = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV2 = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV3 = new List<Vector4> ();
        List<Vector4> srcUV4 = new List<Vector4> ();
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
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology (i), i);
        }
        newMesh.bounds = sourceMesh.bounds;
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = sourceMesh.bindposes;
        }
        List<string> blendShapeIndices = new List<string>();
        Dictionary<string, SortedDictionary<float, Vector2Int>> outBlendShapes = new Dictionary<string, SortedDictionary<float, Vector2Int>> ();
        char [] separators = {'_',' '};
        for (int i = 0; i < sourceMesh.blendShapeCount; i++) {
            var blendShapeName = sourceMesh.GetBlendShapeName (i);
            int outIndex = -1;
            if (blendShapeName.StartsWith("#", StringComparison.CurrentCulture)) {
                int firstUs = blendShapeName.IndexOfAny (separators);
                if (firstUs == -1) {
                    Debug.Log ("Failed to parse blend shape " + blendShapeName);
                } else {
                    if (!int.TryParse (blendShapeName.Substring (1, firstUs - 1), out outIndex)) {
                        Debug.Log ("Failed to parse blend shape " + blendShapeName);
                    } else {
                        blendShapeName = blendShapeName.Substring (firstUs + 1);
                    }
                }
            }
            int lastUs = blendShapeName.LastIndexOfAny (separators);
            float fullpct = 100;
            if (lastUs != -1 && blendShapeName.EndsWith("%", StringComparison.CurrentCulture)) {
                if (!float.TryParse(blendShapeName.Substring(lastUs + 1, blendShapeName.Length - lastUs - 2), out fullpct)) {
                    Debug.Log ("Failed to parse percent " + blendShapeName);
                } else {
                    blendShapeName = blendShapeName.Substring (0, lastUs);
                }
            }
            var blendShapeFrameCount = sourceMesh.GetBlendShapeFrameCount (i);
            if (!outBlendShapes.ContainsKey(blendShapeName)) {
                outBlendShapes.Add (blendShapeName, new SortedDictionary<float, Vector2Int> ());
                int thisIndex = blendShapeIndices.Count;
                blendShapeIndices.Add (blendShapeName);
                if (outIndex != -1 && outIndex < blendShapeIndices.Count) {
                    Debug.Log ("Blend shape " + blendShapeName + " index " + outIndex + " swapped with " + thisIndex);
                    string tmp = blendShapeIndices[outIndex];
                    blendShapeIndices [outIndex] = blendShapeIndices [thisIndex];
                    blendShapeIndices [thisIndex] = tmp;
                } else {
                    Debug.Log ("Blend shape " + blendShapeName + " index " + thisIndex);
                }
            }
            for (int frameIndex = 0; frameIndex < blendShapeFrameCount; frameIndex++) {
                float weight = sourceMesh.GetBlendShapeFrameWeight (i, frameIndex);
                Debug.Log ("Blend shape " + blendShapeName + " weight " + (weight * fullpct / 100) + " source:" + i + "/" + frameIndex);
                outBlendShapes [blendShapeName].Add (weight * fullpct / 100, new Vector2Int (i, frameIndex));
            }
        }
        for (int i = 0; i < blendShapeIndices.Count; i++) {
            var blendShapeName = blendShapeIndices[i];
            //var blendShapeFrameCount = outBlendShapes[blendShapeName].Count;
            foreach (KeyValuePair<float, Vector2Int> entry in outBlendShapes [blendShapeName]) {
            //for (int frameIndex = 0; frameIndex < blendShapeFrameCount; frameIndex++) {
                float weight = entry.Key;
                Vector3 [] deltaVertices = new Vector3 [size];
                Vector3 [] deltaNormals = new Vector3 [size];
                Vector3 [] deltaTangents = new Vector3 [size];
                sourceMesh.GetBlendShapeFrameVertices (entry.Value.x, entry.Value.y, deltaVertices, deltaNormals, deltaTangents);
                newMesh.AddBlendShapeFrame (blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
            }
        }
        newMesh.name = sourceMesh.name + "_blendmerge";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to blendmerge");
            smr.sharedMesh = newMesh;
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        string pathToGenerated = "Assets" + "/Generated";
        if (!Directory.Exists (pathToGenerated)) {
            Directory.CreateDirectory (pathToGenerated);
        }
        int lastSlash = parentName.LastIndexOf ('/');
        string outFileName = lastSlash == -1 ? parentName : parentName.Substring (lastSlash + 1);
        outFileName = outFileName.Split ('.') [0];
        string fileName = pathToGenerated + "/" + outFileName + "_blendmerge_" + DateTime.UtcNow.ToString ("s").Replace (':', '_') + ".asset";
        AssetDatabase.CreateAsset (meshAfterUpdate, fileName);
        AssetDatabase.SaveAssets ();
        if (smr == null) {
            EditorGUIUtility.PingObject (meshAfterUpdate);
        }
    }

    [MenuItem ("CONTEXT/Mesh/Remove First Mat : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Remove First Mat : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshRenderer/Remove First Mat : LMT", false, 131)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Remove First Mat : LMT", false, 131)]
    public static void RunRemoveFirstMat (MenuCommand command)
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
        List<Vector4> srcUV = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV2 = new List<Vector4> (); // will discard zw
        List<Vector4> srcUV3 = new List<Vector4> ();
        List<Vector4> srcUV4 = new List<Vector4> ();
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
        newMesh.subMeshCount = sourceMesh.subMeshCount - 1;
        for (int i = 1; i < sourceMesh.subMeshCount; i++) {
            var curIndices = sourceMesh.GetIndices (i);
            newMesh.SetIndices (curIndices, sourceMesh.GetTopology (i), i - 1);
        }
        newMesh.bounds = sourceMesh.bounds;
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = sourceMesh.bindposes;
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
        newMesh.name = sourceMesh.name + "_shadow";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to Remove First Mat");
            smr.sharedMesh = newMesh;
            smr.sharedMaterials = smr.sharedMaterials.Skip (1).ToArray();
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to Remove First Mat");
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


    [MenuItem ("CONTEXT/Mesh/Combine Mesh Siblings : LMT [Requires GameObject]", true)]
    public static bool RunCombineSiblingMeshesVal (MenuCommand command) {
        return false;
    }

    [MenuItem ("CONTEXT/Mesh/Combine Mesh Siblings : LMT [Requires GameObject]", false, 131)]
    [MenuItem ("CONTEXT/MeshFilter/Combine Mesh Siblings : LMT", false, 131)]
    [MenuItem ("CONTEXT/MeshRenderer/Combine Mesh Siblings : LMT", false, 131)]
    [MenuItem ("CONTEXT/SkinnedMeshRenderer/Combine Mesh Siblings : LMT", false, 131)]
    public static void RunCombineSiblingMeshes (MenuCommand command)
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

        List<Matrix4x4> bindposes = sourceMesh.bindposes.ToList();
        List<Transform> boneTransforms = new List<Transform> ();
        Dictionary<Transform, int> boneTransToIndex = new Dictionary<Transform, int> ();
        SkinnedMeshRenderer [] renderers = null;

        List<Mesh> siblingMeshes = new List<Mesh> ();
        List<Material> addMaterials = new List<Material> ();
        List<int> addSubMeshCount = new List<int>();
        List<int> baseVertex = new List<int> ();
        int addVertices = 0;
        int addSubMeshes = 0;
        UnityEngine.Rendering.IndexFormat indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        if (smr != null) {
            renderers = trans.parent.GetComponentsInChildren<SkinnedMeshRenderer> (false);
            int x = Array.IndexOf (renderers, smr);
            SkinnedMeshRenderer tmp = renderers [0];
            renderers [0] = smr;
            renderers [x] = tmp;
            bindposes = new List<Matrix4x4> ();
            foreach (SkinnedMeshRenderer s in renderers) {
                //if (s.rootBone != smr.rootBone) {
                 //   continue;
                //}
                int i = 0;
                foreach (Transform t in s.bones) {
                    if (!boneTransToIndex.ContainsKey(t)) {
                        boneTransToIndex.Add (t, boneTransforms.Count);
                        bindposes.Add (s.sharedMesh.bindposes [i]);
                        boneTransforms.Add (t);
                    }
                    i++;
                }
                int materialsToCopy = Math.Min (s.sharedMaterials.Length, s.sharedMesh.subMeshCount);
                Debug.Log ("mesh " + s.sharedMesh.name + " : boneweight length=" + s.sharedMesh.boneWeights.Length + "/ bindpose length=" + s.sharedMesh.bindposes.Length + " / bones length=" + s.bones.Length + " / root bone=" + s.rootBone.name);
                addSubMeshCount.Add (materialsToCopy);
                addSubMeshes += materialsToCopy;
                for (i = 0; i < materialsToCopy; i++) {
                    addMaterials.Add (s.sharedMaterials[i]);
                }
                siblingMeshes.Add(s.sharedMesh);
                baseVertex.Add (addVertices);
                if (s.sharedMesh.vertices.Length > 65535) {
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                addVertices += s.sharedMesh.vertices.Length;
            }
        }
        if (mf != null) {
            MeshFilter [] filters = trans.parent.GetComponentsInChildren<MeshFilter> (false);
            int x = Array.IndexOf (filters, mf);
            MeshFilter tmp = filters [0];
            filters [0] = mf;
            filters [x] = tmp;
            foreach (MeshFilter s in filters) {
                MeshRenderer thismr = s.GetComponent<MeshRenderer> ();
                if (mr != null && thismr != null) {
                    int materialsToCopy = Math.Min (thismr.sharedMaterials.Length, s.sharedMesh.subMeshCount);
                    addSubMeshCount.Add (materialsToCopy);
                    addSubMeshes += materialsToCopy;
                    for (int i = 0; i < materialsToCopy; i++) {
                        addMaterials.Add (thismr.sharedMaterials [i]);
                    }
                } else if (mr != null && thismr == null) {
                    continue;
                } else {
                    addSubMeshCount.Add (s.sharedMesh.subMeshCount);
                    addSubMeshes += s.sharedMesh.subMeshCount;
                }
                siblingMeshes.Add (s.sharedMesh);
                baseVertex.Add (addVertices);
                if (s.sharedMesh.vertices.Length > 65535) {
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                addVertices += s.sharedMesh.vertices.Length;
            }
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
        Matrix4x4 [] srcBindposes = bindposes.ToArray ();
        int size = sourceMesh.vertices.Length;
        List<Vector4> finalUV = new List<Vector4> ();
        List<Vector4> finalUV2 = new List<Vector4> ();
        List<Vector4> finalUV3 = new List<Vector4> ();
        List<Vector4> finalUV4 = new List<Vector4> ();
        List<Vector3> finalVertices = new List<Vector3> ();
        List<Color> finalColors = new List<Color> ();
        List<Vector3> finalNormals = new List<Vector3> (); 
        List<Vector4> finalTangents = new List<Vector4> ();
        List<BoneWeight> finalBoneWeights = new List<BoneWeight> ();
        int subi = 0;
        foreach (Mesh thisMesh in siblingMeshes) {
            List<Vector4> srcUV = new List<Vector4> ();
            List<Vector4> srcUV2 = new List<Vector4> ();
            List<Vector4> srcUV3 = new List<Vector4> ();
            List<Vector4> srcUV4 = new List<Vector4> ();
            List<Vector3> srcVertices = new List<Vector3> ();
            List<Color> srcColors = new List<Color> ();
            List<Vector3> srcNormals = new List<Vector3> ();
            List<Vector4> srcTangents = new List<Vector4> ();
            List<BoneWeight> srcBoneWeights = new List<BoneWeight> ();
            thisMesh.GetUVs (0, srcUV);
            thisMesh.GetUVs (1, srcUV2);
            thisMesh.GetUVs (2, srcUV3);
            thisMesh.GetUVs (3, srcUV4);
            thisMesh.GetVertices (srcVertices);
            thisMesh.GetColors (srcColors);
            thisMesh.GetNormals (srcNormals);
            thisMesh.GetTangents (srcTangents);
            thisMesh.GetBoneWeights (srcBoneWeights);
            if (srcUV.Count > 0) {
                finalUV.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV.Count));
                finalUV.AddRange (srcUV);
            }
            if (srcUV2.Count > 0) {
                finalUV2.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV2.Count));
                finalUV2.AddRange (srcUV2);
            }
            if (srcUV3.Count > 0) {
                finalUV3.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV3.Count));
                finalUV3.AddRange (srcUV3);
            }
            if (srcUV4.Count > 0) {
                finalUV4.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV4.Count));
                finalUV4.AddRange (srcUV4);
            }
            if (srcColors.Count > 0) {
                finalColors.AddRange (Enumerable.Repeat (new Color (0, 0, 0, 1), finalVertices.Count - finalColors.Count));
                finalColors.AddRange (srcColors);
            }
            if (srcNormals.Count > 0) {
                finalNormals.AddRange (Enumerable.Repeat (new Vector3 (0, 0, 1), finalVertices.Count - finalNormals.Count));
                finalNormals.AddRange (srcNormals);
            }
            if (srcTangents.Count > 0) {
                finalTangents.AddRange (Enumerable.Repeat(new Vector4(1,0,0,1), finalVertices.Count - finalTangents.Count));
                finalTangents.AddRange (srcTangents);
            }
            if (srcBoneWeights.Count > 0) {
                finalBoneWeights.AddRange (Enumerable.Repeat (new BoneWeight(), finalVertices.Count - finalBoneWeights.Count));
                Transform [] thisbones = renderers [subi].bones;
                int[] indexMapping = new int [thisbones.Length];
                for (int i = 0; i < thisbones.Length; i++) {
                    indexMapping [i] = boneTransToIndex [thisbones [i]];
                }
                for (int i = 0; i < srcBoneWeights.Count; i++) {
                    BoneWeight src = srcBoneWeights [i];
                    BoneWeight newbw = new BoneWeight ();
                    newbw.weight0 = src.weight0;
                    newbw.weight1 = src.weight1;
                    newbw.weight2 = src.weight2;
                    newbw.weight3 = src.weight3;
                    newbw.boneIndex0 = indexMapping[src.boneIndex0];
                    newbw.boneIndex1 = indexMapping [src.boneIndex1];
                    newbw.boneIndex2 = indexMapping [src.boneIndex2];
                    newbw.boneIndex3 = indexMapping [src.boneIndex3];
                    srcBoneWeights [i] = newbw;
                }
                finalBoneWeights.AddRange (srcBoneWeights);
            }
            finalVertices.AddRange (srcVertices);
            subi++;
        }
        if (finalUV.Count > 0) {
            finalUV.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV.Count));
        }
        if (finalUV2.Count > 0) {
            finalUV2.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV2.Count));
        }
        if (finalUV3.Count > 0) {
            finalUV3.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV3.Count));
        }
        if (finalUV4.Count > 0) {
            finalUV4.AddRange (Enumerable.Repeat (new Vector4 (0, 0, 0, 0), finalVertices.Count - finalUV4.Count));
        }
        if (finalColors.Count > 0) {
            finalColors.AddRange (Enumerable.Repeat (new Color (0, 0, 0, 1), finalVertices.Count - finalColors.Count));
        }
        if (finalNormals.Count > 0) {
            finalNormals.AddRange (Enumerable.Repeat (new Vector3 (0, 0, 1), finalVertices.Count - finalNormals.Count));
        }
        if (finalTangents.Count > 0) {
            finalTangents.AddRange (Enumerable.Repeat (new Vector4 (1, 0, 0, 1), finalVertices.Count - finalTangents.Count));
        }
        if (finalBoneWeights.Count > 0) {
            finalBoneWeights.AddRange (Enumerable.Repeat (new BoneWeight (), finalVertices.Count - finalBoneWeights.Count));
        }
        if (srcBindposes != null && srcBindposes.Length > 0) {
            newMesh.bindposes = srcBindposes;
        }
        newMesh.SetVertices(finalVertices);
        if (finalNormals != null && finalNormals.Count > 0) {
            newMesh.SetNormals(finalNormals);
        }
        if (finalTangents != null && finalTangents.Count > 0) {
            newMesh.SetTangents(finalTangents);
        }
        if (finalBoneWeights != null && finalBoneWeights.Count > 0) {
            newMesh.boneWeights = finalBoneWeights.ToArray<BoneWeight>();
        }
        if (finalColors != null && finalColors.Count > 0) {
            newMesh.SetColors(finalColors);
        }
        if (finalUV.Count > 0) {
            newMesh.SetUVs (0, finalUV);
        }
        if (finalUV2.Count > 0) {
            newMesh.SetUVs (1, finalUV2);
        }
        if (finalUV3.Count > 0) {
            newMesh.SetUVs (2, finalUV3);
        }
        if (finalUV4.Count > 0) {
            newMesh.SetUVs (3, finalUV4);
        }

        newMesh.subMeshCount = addSubMeshes;
        newMesh.bounds = sourceMesh.bounds;
        newMesh.indexFormat = indexFormat;
        int whichSubMesh = 0;
        for (subi = 0; subi < siblingMeshes.Count; subi++) {
            for (int i = 0; i < addSubMeshCount[subi]; i++) {
                var curIndices = siblingMeshes[subi].GetIndices (i);
                newMesh.SetIndices (curIndices, siblingMeshes [subi].GetTopology (i), whichSubMesh, false, baseVertex[subi]);
                whichSubMesh++;
            }
            newMesh.bounds = new Bounds(sourceMesh.bounds.center, sourceMesh.bounds.extents);
            newMesh.bounds.Encapsulate (siblingMeshes [subi].bounds);
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
                Vector3 [] newDeltaVertices = new Vector3 [finalVertices.Count];
                Vector3 [] newDeltaNormals = new Vector3 [finalVertices.Count];
                Vector3 [] newDeltaTangents = new Vector3 [finalVertices.Count];
                Array.Copy (deltaVertices, newDeltaVertices, size);
                Array.Copy (deltaNormals, newDeltaNormals, size);
                Array.Copy (deltaTangents, newDeltaTangents, size);
                newMesh.AddBlendShapeFrame (blendShapeName, weight, newDeltaVertices, newDeltaNormals, newDeltaTangents);
            }
        }
        newMesh.name = sourceMesh.name + "_merged";
        Mesh meshAfterUpdate = newMesh;
        if (smr != null) {
            Undo.RecordObject (smr, "Switched SkinnedMeshRenderer to merged");
            smr.sharedMesh = newMesh;
            smr.sharedMaterials = addMaterials.ToArray<Material> ();
            smr.bones = boneTransforms.ToArray<Transform> ();
            meshAfterUpdate = smr.sharedMesh;
            // No need to change smr.bones: should use same bone indices and blendshapes.
        }
        if (mf != null) {
            Undo.RecordObject (mf, "Switched MeshFilter to merged");
            mf.sharedMesh = newMesh;
            if (mr != null) {
                mr.sharedMaterials = addMaterials.ToArray<Material> ();
            }
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
#endif
