using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace Clarenza {
 public static class TeacherAssets {
  public static GameObject Prepare(){
   string source="Assets/StudioNewPunch/SuitPack/SuitedMan/Prefabs/BlueSuitFree01.prefab";
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
   if(prefab==null)throw new Exception("Teacher prefab did not import");
   foreach(var dep in AssetDatabase.GetDependencies(source,true))Debug.Log("TEACHER_DEPENDENCY "+dep);
   var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
   try{
    foreach(var t in copy.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    var animator=copy.GetComponentInChildren<Animator>();
    if(animator==null||animator.avatar==null||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Teacher humanoid avatar invalid");
    var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Huge Mocap Library/mocap animations/140/140_07.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
    if(clip==null||!clip.humanMotion)throw new Exception("Idle animation is not humanoid");
    var idle=UnityEngine.Object.Instantiate(clip);idle.name="Professor idle";
    var settings=AnimationUtility.GetAnimationClipSettings(idle);settings.loopTime=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;settings.keepOriginalOrientation=true;settings.loopBlendPositionXZ=true;settings.loopBlendPositionY=true;settings.loopBlendOrientation=true;AnimationUtility.SetAnimationClipSettings(idle,settings);
    string animPath="Assets/Clarenza/ProfessorIdleAlternate.anim",controllerPath="Assets/Clarenza/Professor.controller";
    if(AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath)==null)AssetDatabase.CreateAsset(idle,animPath);else UnityEngine.Object.DestroyImmediate(idle);
    var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
    if(ctrl==null){ctrl=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var state=ctrl.layers[0].stateMachine.AddState("Idle");state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);ctrl.layers[0].stateMachine.defaultState=state;}
    ctrl.layers[0].stateMachine.defaultState.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
    animator.enabled=false;animator.speed=1;animator.runtimeAnimatorController=ctrl;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    foreach(var renderer in copy.GetComponentsInChildren<Renderer>())foreach(var m in renderer.sharedMaterials)if(m==null||m.shader==null)throw new Exception("Missing teacher material");
    var result=PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Clarenza/Professor.prefab");Debug.Log("TEACHER_ASSETS_VALIDATED idle="+clip.length+"s");return result;
   }finally{UnityEngine.Object.DestroyImmediate(copy);}
  }
 }
}



