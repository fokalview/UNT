using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace Clarenza {
 public static class BuildAuditorium {
  public static void Build() {
   var source=EditorSceneManager.OpenScene("Assets/University Classroom/Demo.unity");
   var board=UnityEngine.Object.FindObjectsOfType<Renderer>().First(r=>r.name=="Blackboard");
   var boardCenter=board.bounds.center;
   var seats=UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r=>r.name.StartsWith("Chair")&&r.bounds.center.z<boardCenter.z-2).Select(r=>r.bounds.center+new Vector3(0,.75f,0)).ToArray();
   if(seats.Length<10)throw new Exception("Expected original auditorium seating");
   foreach(var go in source.GetRootGameObjects()) foreach(var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
   foreach(var c in UnityEngine.Object.FindObjectsOfType<Camera>())UnityEngine.Object.DestroyImmediate(c);
   foreach(var c in UnityEngine.Object.FindObjectsOfType<AudioListener>())UnityEngine.Object.DestroyImmediate(c);
   foreach(var c in UnityEngine.Object.FindObjectsOfType<RotateMoveCamera>())UnityEngine.Object.DestroyImmediate(c);
   var app=new GameObject("Clarenza Auditorium Application").AddComponent<AuditoriumApp>();
   app.boardCenter=boardCenter;app.seatPositions=seats;app.presenterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Clarenza/Surface.mat");
   app.teacherPrefab=TeacherAssets.Prepare();app.walkRoutes=TeacherRouteBuilder.Build();
   RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.55f,.63f);RenderSettings.ambientEquatorColor=new Color(.34f,.36f,.38f);RenderSettings.ambientGroundColor=new Color(.19f,.17f,.15f);
   QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=45;
   var key=new GameObject("Professor soft key").AddComponent<Light>();key.type=LightType.Spot;key.transform.position=boardCenter+new Vector3(-2,1,-5);key.transform.LookAt(boardCenter+new Vector3(-2,-1.8f,-1.5f));key.range=14;key.spotAngle=75;key.intensity=1.6f;key.color=new Color(1,.91f,.79f);key.shadows=LightShadows.Soft;
   var fill=new GameObject("Clarenza soft fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.2f;fill.transform.rotation=Quaternion.Euler(55,-20,0);
   EditorSceneManager.SaveScene(source,"Assets/Clarenza/Scenes/Auditorium.unity");
   var scenes=new[]{"Assets/Clarenza/Scenes/Auditorium.unity","Assets/Clarenza/Scenes/Classroom.unity"};
   EditorBuildSettings.scenes=scenes.Select(s=>new EditorBuildSettingsScene(s,true)).ToArray();
   PlayerSettings.companyName="U-Now Technology";PlayerSettings.productName="Clarenza Auditorium Pilot";
   PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
   PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_Standard_2_0);
   Directory.CreateDirectory("Build/Windows");
   Validate();
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName="Build/Windows/Clarenza.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
   if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
   Debug.Log("AUDITORIUM_BUILD_SUCCESS seats="+seats.Length);
  }
  public static void Validate() {
   var l=LessonImport.Local("One paragraph.\n\nAnother paragraph.","Test");LessonImport.Validate(l);
   if(l.sections.Length!=2||l.sections[1].sourceIds[0]!=2)throw new Exception("Source chunk mapping failed");
   bool rejected=false;try{LessonImport.Sources(" ");}catch{rejected=true;}if(!rejected)throw new Exception("Empty source accepted");
   l.sections[0].sourceIds=new[]{3};rejected=false;try{LessonImport.Validate(l);}catch{rejected=true;}if(!rejected)throw new Exception("Bad citation accepted");
   Debug.Log("AUDITORIUM_DATA_TESTS_PASSED");
   Directory.CreateDirectory("Temp");string testFile="Temp/import-test.pptx";
   using(var stream=File.Create(testFile))using(var zip=new System.IO.Compression.ZipArchive(stream,System.IO.Compression.ZipArchiveMode.Create)) {
    foreach(int n in new[]{10,2,1})using(var writer=new StreamWriter(zip.CreateEntry("ppt/slides/slide"+n+".xml").Open()))writer.Write("<a:p xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:r><a:t>Content "+n+"</a:t></a:r></a:p>");
   }
   string imported=LessonImport.Read(testFile);
   if(imported.IndexOf("Content 1")>imported.IndexOf("Content 2")||imported.IndexOf("Content 2")>imported.IndexOf("Content 10"))throw new Exception("PowerPoint ordering failed");
   File.Delete(testFile);Debug.Log("POWERPOINT_IMPORT_TEST_PASSED");
  }
  public static void Inspect() {
   EditorSceneManager.OpenScene("Assets/University Classroom/Demo.unity");
   foreach(var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
    if(r.name.IndexOf("Stair",StringComparison.OrdinalIgnoreCase)>=0 || r.name.IndexOf("Table",StringComparison.OrdinalIgnoreCase)>=0 || r.name.IndexOf("board",StringComparison.OrdinalIgnoreCase)>=0 || r.name.IndexOf("Chair",StringComparison.OrdinalIgnoreCase)>=0 || r.name.IndexOf("Structure",StringComparison.OrdinalIgnoreCase)>=0)
     Debug.Log("ROOM_OBJECT " + r.name + " center=" + r.bounds.center + " size=" + r.bounds.size);
   foreach(var c in UnityEngine.Object.FindObjectsOfType<Camera>()) Debug.Log("ROOM_CAMERA " + c.name + " pos=" + c.transform.position + " rot=" + c.transform.eulerAngles);
  }
 }
}


