#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
 
public class CreateMesh : MonoBehaviour {  
    [MenuItem("GameObject/Create Mesh")]
    static void CreateMesh_()
    {
        int size = 512 * 512;
       
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {new Vector3(0, 0, 0)};
        mesh.triangles =  new int[size*3];
        mesh.bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(20, 20, 20));
        AssetDatabase.CreateAsset(mesh, "Assets/Particles/512x512.asset");
        /*
        int size = 32 * 32;
       
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {new Vector3(0, 0, 0)};
        mesh.triangles =  new int[size*3];
        mesh.bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(20, 20, 20));
        AssetDatabase.CreateAsset(mesh, "Assets/Arcade/32x32.asset");
         */
    }
}
#endif
