using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Clarenza
{
    public static class BuildMvp
    {
        public static void Run()
        {
            Validate();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var classroom = new GameObject("Clarenza Lesson").AddComponent<Classroom>();
            string materialPath = "Assets/Clarenza/Surface.mat";
            var surface = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (surface == null) {
                surface = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(surface, materialPath);
            }
            classroom.surfaceMaterial = surface;
            Directory.CreateDirectory("Assets/Clarenza/Scenes");
            string path = "Assets/Clarenza/Scenes/Classroom.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new [] { new EditorBuildSettingsScene(path, true) };
            PlayerSettings.companyName = "U-Now Technology";
            PlayerSettings.productName = "Clarenza Circuit Studio";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory("Build/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new [] { path }, locationPathName = "Build/Windows/Clarenza.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            Debug.Log("CLARENZA_BUILD_SUCCESS");
        }
        public static void Validate()
        {
            for (int mask=0;mask<8;mask++)
            {
                var c = new CircuitState { battery=(mask&1)!=0, lamp=(mask&2)!=0, closed=(mask&4)!=0 };
                if(c.Powered != (mask==7)) throw new Exception("Circuit truth table failed");
            }
            var state = new CircuitState(); state.Observe();
            if(state.Complete) throw new Exception("Premature completion");
            state.battery=state.lamp=state.closed=true; state.Observe();
            if(state.Complete || !state.observedLight) throw new Exception("Lighting progression failed");
            state.closed=false; state.Observe();
            if(!state.Complete) throw new Exception("Opening progression failed");
            state.Reset(); if(state.Complete || state.Powered) throw new Exception("Reset failed");
            Debug.Log("CLARENZA_TESTS_PASSED");
        }
    }
}
