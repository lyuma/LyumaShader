using UnityEngine;
using UnityEditor;
using System.Collections;
#if UNITY_EDITOR
 public class CreateBehaviorAnim : Editor
 {
     [UnityEditor.MenuItem("Behavior/create behavior anim")]
     public static void CreateAnim ()
     {
         AnimationClip clip = new AnimationClip();
         #pragma warning disable 0618
         AnimationUtility.SetEditorCurve(clip, "GameObject", typeof(Behaviour), "m_Enabled", AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 1.0f));
         #pragma warning restore 0618
         AssetDatabase.CreateAsset(clip, "Assets/behavior.anim");
     }    
 }
#endif