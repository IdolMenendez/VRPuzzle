using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

public static class AndroidSizeOptimization
{
    public const string Evidence = "Artifacts/Checkpoints/173_AndroidOptimization_20260910";
    const string IconPath = "Assets/Branding/V81/PersonallyPuzzledIcon.png";

    public static void Apply()
    {
        Directory.CreateDirectory(Evidence + "/Backup");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path);
        var roots = scenes.Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/"))).ToArray();
        var paths = AssetDatabase.GetDependencies(roots, true).OrderBy(p => p);
        var lines = new List<string> { "path\twidth\theight\tprevious_format\tandroid_max\tandroid_format" };
        foreach (string path in paths)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            // Only material maps: preserve UI, fonts, lightmaps and lookup textures.
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool mask = name.Contains("metallic") || name.Contains("roughness") || name.Contains("occlusion");
            bool materialMap = mask || name.Contains("base_color") || name.Contains("basecolor") || name.Contains("normal") || name.Contains("emission");
            if (!materialMap || !path.StartsWith("Assets/")) continue;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture || Math.Max(texture.width, texture.height) < 512) continue;
            string backup = Evidence + "/Backup/" + path + ".meta";
            if (!File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(path + ".meta", backup);
            }
            var settings = importer.GetPlatformTextureSettings("Android");
            int oldMax = importer.maxTextureSize;
            settings.overridden = true;
            bool heroColor = !mask && !name.Contains("normal") && !name.Contains("emission") &&
                (name.Contains("modelbuilder") || name.Contains("referencelefthand") || name.Contains("workbench"));
            settings.maxTextureSize = Math.Min(oldMax, heroColor ? 2048 : 1024);
            settings.format = mask ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            lines.Add($"{path}\t{texture.width}\t{texture.height}\t{texture.format}\t{settings.maxTextureSize}\t{settings.format}");
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
        }
        File.WriteAllLines(Evidence + "/texture-optimization.tsv", lines);
        var iconImporter = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        iconImporter.textureType = TextureImporterType.Default;
        iconImporter.mipmapEnabled = false;
        iconImporter.maxTextureSize = 2048;
        iconImporter.textureCompression = TextureImporterCompression.Uncompressed;
        iconImporter.SaveAndReimport();
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.show = false;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var slot in slots)
                slot.SetTextures(Enumerable.Repeat(icon, slot.minLayerCount).ToArray());
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            Debug.Log("[Optimization] ICON " + kind + " slots=" + slots.Length);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Optimization] APPLY PASSED textures=" + (lines.Count - 1));
    }

    public static void RunFullFlow()
    {
        Environment.SetEnvironmentVariable("PPVR_CURRENT_RELEASE_FLOW", "1");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s => s.enabled).path);
        EditorApplication.isPlaying = true;
    }

    public static void CaptureAndBuild()
    {
        LatestProjectTools.CaptureFinal();
        LatestProjectTools.BuildPico();
    }

    public static void ValidateAndRunFlow()
    {
        LatestProjectTools.Validate();
        RunFullFlow();
    }
}
