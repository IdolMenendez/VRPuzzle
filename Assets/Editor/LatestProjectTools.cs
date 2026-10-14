using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Operates directly on the delivered scenes; no historical scene builder is needed.
public static class LatestProjectTools
{
    public const string Garden = "Assets/Scenes/GardenCottage_20260824_V02_ProviderSemanticReplacement.unity";
    public const string Evidence = "Artifacts/Checkpoints/171_RepositoryCleanup_20260908";
    static string Output => Path.GetFullPath(
        Environment.GetEnvironmentVariable("PPVR_EVIDENCE_DIR") ?? Evidence);
    static string[] Runtime => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    static string[] Scenes => Runtime.Concat(new[] { Garden }).ToArray();

    [MenuItem("Personally Puzzled XR/Current/Export Dependencies")]
    public static void ExportDependencies()
    {
        Directory.CreateDirectory(Output);
        var roots = new HashSet<string>(Scenes);
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || AssetDatabase.IsValidFolder(path)) continue;
            if (path.Contains("/Resources/") || path.Contains("/StreamingAssets/") ||
                path.StartsWith("Assets/Settings/") || path.StartsWith("Assets/XR/") ||
                path.StartsWith("Assets/XRI/") || path.Contains("/Plugins/")) roots.Add(path);
        }
        foreach (string file in Directory.GetFiles("ProjectSettings"))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"guid:\s*([a-f0-9]{32})"))
            {
                string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (!string.IsNullOrEmpty(path)) roots.Add(path);
            }
        string[] deps = AssetDatabase.GetDependencies(roots.ToArray(), true).OrderBy(p => p).ToArray();
        File.WriteAllLines(Path.Combine(Output, "unity-dependencies.txt"), deps);
        File.WriteAllLines(Path.Combine(Output, "scene-dependencies.txt"), AssetDatabase.GetDependencies(Scenes, true));
        File.WriteAllLines(Path.Combine(Output, "retained-scenes.txt"), Scenes);
        Debug.Log("[Current] DEPENDENCIES EXPORTED " + deps.Length);
    }

    [MenuItem("Personally Puzzled XR/Current/Capture Before Cleanup")]
    public static void CaptureBefore() => CaptureAll("Before");

    [MenuItem("Personally Puzzled XR/Current/Capture Final")]
    public static void CaptureFinal() => CaptureAll("Final");

    static void CaptureAll(string phase)
    {
        Directory.CreateDirectory(Path.Combine(Output, phase));
        foreach (string path in Scenes.Where(p => !p.Contains("Bootstrap")))
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Camera[] cameras = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).ToArray();
            Camera camera = cameras.FirstOrDefault(c => c.name.Contains("ReferenceCaptureCamera")) ?? cameras.FirstOrDefault(c => c.enabled) ?? cameras.FirstOrDefault();
            if (!camera) throw new InvalidOperationException("Capture camera missing: " + path);
            bool garden = path == Garden;
            string stem = garden ? "GardenCottage_V76" : Path.GetFileNameWithoutExtension(path);
            Render(camera, Path.Combine(Output, phase, stem + "_FRONT.png"), garden ? 1450 : 1920, garden ? 1498 : 1080, 0);
            Render(camera, Path.Combine(Output, phase, stem + "_LEFT.png"), garden ? 1450 : 1920, garden ? 1498 : 1080, -.032f);
        }
        Debug.Log("[Current] CAPTURE PASSED " + Path.Combine(Output, phase));
    }

    static void Render(Camera camera, string path, int width, int height, float offset)
    {
        Vector3 position = camera.transform.position;
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position += camera.transform.right * offset;
            camera.targetTexture = target;
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Debug.Log("[Current] SCREENSHOT " + path);
        }
        finally
        {
            camera.transform.position = position;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [MenuItem("Personally Puzzled XR/Current/Validate")]
    public static void Validate()
    {
        if (Runtime.Length != 10 || Runtime.Select(Path.GetDirectoryName).Distinct().Count() != 1 ||
            Runtime.Any(p => !p.Contains("RuntimeV80_ZeroLatencyAssembly") && !p.Contains("RuntimeV82_ResourceOptimization")) ||
            !Runtime[0].Contains("Runtime_Bootstrap_"))
            throw new InvalidOperationException("Expected ten enabled scenes from one delivered runtime version, Bootstrap first.");
        var findings = new List<string>();
        foreach (string path in Scenes)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    findings.Add(path + " missing script: " + transform.name);
                foreach (Component component in transform.GetComponents<Component>().Where(c => c))
                {
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            findings.Add(path + " missing reference: " + transform.name + "/" + property.propertyPath);
                }
                foreach (Renderer renderer in transform.GetComponents<Renderer>())
                {
                    // Disabled legacy FX intentionally have no material. Dangling
                    // references are still checked above, even when inactive.
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    foreach (Material material in renderer.sharedMaterials)
                        if (!material || !material.shader || material.shader.name.Contains("InternalErrorShader"))
                            findings.Add(path + " missing material/shader: " + transform.name);
                }
            }
            Debug.Log("[Current] SCENE CHECKED " + path);
        }
        Directory.CreateDirectory(Output);
        File.WriteAllLines(Path.Combine(Output, "reference-findings.txt"), findings);
        if (findings.Count != 0) throw new InvalidOperationException("Missing scene references: " + findings.Count);
        if (Runtime[0].Contains("RuntimeV82_ResourceOptimization")) ResourceOptimizationV82.ValidateFontCoverage();
        Debug.Log("[Current] VALIDATION PASSED scenes=11 missing-scripts=0 missing-references=0 missing-active-materials=0");
    }

    [MenuItem("Personally Puzzled XR/Current/Run Interaction Smoke")]
    public static void RunInteractionSmoke()
    {
        Environment.SetEnvironmentVariable(PinchIntegritySmokeV46.EnvironmentVariable, "1");
        EditorSceneManager.OpenScene(Runtime[0], OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Personally Puzzled XR/Current/Build PICO APK")]
    public static void BuildPico()
    {
        CurrentAndroidToolchain.Apply();
        RestorePicoRuntimeConfiguration();
        PersonallyPuzzledPlayModeWindow.ForceManualReleaseDefault();
        Validate();
        Directory.CreateDirectory("Builds");
        string apkPath = Environment.GetEnvironmentVariable("PPVR_APK_PATH") ??
            "Builds/PersonallyPuzzledVR-PICO-OptimizedV82.apk";
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Runtime,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PICO build failed.");
        long apkBytes = new FileInfo(apkPath).Length;
        Debug.Log("[Current] PICO BUILD PASSED apkBytes=" + apkBytes);
    }

    static void RestorePicoRuntimeConfiguration()
    {
        // PICO may recreate this Resources asset with defaults while a clean
        // Library is importing. Reapply the delivered V80 hand/OpenXR profile
        // after import and before validation or player serialization.
        var settings = AssetDatabase.LoadAssetAtPath<PXR_ProjectSetting>(
            "Assets/Resources/PXR_ProjectSetting.asset");
        if (!settings) throw new InvalidOperationException("PICO project settings missing.");
        settings.handTracking = true;
        settings.adaptiveHand = true;
        settings.highFrequencyHand = true;
        settings.handTrackingSupportType = (HandTrackingSupport)1;
        settings.mrSafeguard = true;
        settings.portalInited = true;
        settings.portalFirstSelected = 2;
        settings.isSpatialAdapter = true;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        var loaded = PXR_ProjectSetting.GetProjectConfig();
        if (!loaded.handTracking || !loaded.adaptiveHand || !loaded.highFrequencyHand ||
            (int)loaded.handTrackingSupportType != 1 || !loaded.isSpatialAdapter)
            throw new InvalidOperationException("PICO hand/OpenXR profile did not persist.");
        Debug.Log("[Current] PICO CONFIGURATION PASSED hands/adaptive/high-frequency/OpenXR enabled");
    }
}
