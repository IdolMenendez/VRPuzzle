using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ResourceOptimizationV82
{
    public const string Evidence = "Artifacts/Checkpoints/174_ResourceOptimization_20260910";
    static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
    const string AssetRoot = "Assets/Optimization/V82";
    const string SceneRoot = "Assets/Scenes/RuntimeV82_ResourceOptimization";
    const string OldStem = "20260807_V80_ZeroLatencyAssembly";
    const string NewStem = "20260910_V82_ResourceOptimization";

    static long LocalId(UnityEngine.Object obj)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long id);
        return id;
    }

    public static void Prepare()
    {
        if (Directory.Exists(SceneRoot)) throw new InvalidOperationException("V82 scenes already exist; do not overwrite a checkpoint.");
        string[] sourceScenes = Scenes;
        if (sourceScenes.Any(p => !p.Contains(OldStem))) throw new InvalidOperationException("Expected V80 baseline scenes.");
        var modelPaths = File.ReadAllLines(Evidence + "/models.tsv").Skip(1).Select(l => l.Split('\t')[0]).ToArray();
        var guidMap = new Dictionary<string, string>();
        string[] textures = Directory.GetFiles("Assets/Selection/V04/Generated3D/BiplaneTextures", "*.png");
        foreach (string src in modelPaths.Concat(textures)) Directory.CreateDirectory(Path.GetDirectoryName(AssetRoot + "/" + src));
        Directory.CreateDirectory(SceneRoot);
        AssetDatabase.Refresh();
        var textureMap = new Dictionary<Texture, Texture>();
        foreach (string src in textures)
        {
            string dst = AssetRoot + "/" + src;
            if (!AssetDatabase.CopyAsset(src, dst)) throw new IOException("Cannot copy " + src);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            var settings = ti.GetPlatformTextureSettings("Android");
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.ASTC_6x6;
            settings.compressionQuality = 100;
            ti.SetPlatformTextureSettings(settings);
            ti.SaveAndReimport();
            textureMap.Add(AssetDatabase.LoadAssetAtPath<Texture>(src), AssetDatabase.LoadAssetAtPath<Texture>(dst));
        }
        var materialRefs = new Dictionary<string, string>();
        var textureAudit = new List<string>();
        var meshAudit = new List<string> { "asset\tmesh\tvertices\tmax_position_error\trelative_error\tindices_equal" };
        foreach (string src in modelPaths)
        {
            string dst = AssetRoot + "/" + src;
            if (!AssetDatabase.CopyAsset(src, dst)) throw new IOException("Cannot copy " + src);
            var importer = (ModelImporter)AssetImporter.GetAtPath(dst);
            importer.meshCompression = src.Contains("Workbench")
                ? ((ModelImporter)AssetImporter.GetAtPath(src)).meshCompression
                : ModelImporterMeshCompression.Medium;
            if (src.Contains("SelectionBiplaneMiniature"))
            {
                foreach (Material original in AssetDatabase.LoadAllAssetsAtPath(src).OfType<Material>())
                {
                    var copy = new Material(original);
                    foreach (string property in original.GetTexturePropertyNames())
                    {
                        var texture = original.GetTexture(property);
                        if (texture && textureMap.TryGetValue(texture, out Texture replacement))
                        {
                            copy.SetTexture(property, replacement);
                            textureAudit.Add(original.name + "\t" + property + "\t" + AssetDatabase.GetAssetPath(texture) + "\t" + AssetDatabase.GetAssetPath(replacement));
                        }
                    }
                    string matPath = AssetRoot + "/" + original.name.Replace('/', '_') + "_Biplane.mat";
                    AssetDatabase.CreateAsset(copy, matPath);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), copy);
                    materialRefs.Add("fileID: " + LocalId(original) + ", guid: " + AssetDatabase.AssetPathToGUID(src),
                        "fileID: " + LocalId(copy) + ", guid: " + AssetDatabase.AssetPathToGUID(matPath));
                }
            }
            importer.SaveAndReimport();
            var newMeshes = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Mesh>().ToDictionary(LocalId);
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(src).OfType<Mesh>())
            {
                if (!newMeshes.TryGetValue(LocalId(old), out Mesh mesh) || old.name != mesh.name || old.vertexCount != mesh.vertexCount || old.subMeshCount != mesh.subMeshCount)
                    throw new InvalidOperationException("Mesh identity changed: " + src + "/" + old.name);
                for (int i=0; i<old.subMeshCount; i++)
                    if (!old.GetIndices(i).SequenceEqual(mesh.GetIndices(i))) throw new InvalidOperationException("Topology changed: " + src);
                Vector3[] a = old.vertices, b = mesh.vertices;
                float max = 0;
                for (int i=0; i<a.Length; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i]));
                float relative = max / Mathf.Max(.000001f, old.bounds.size.magnitude);
                meshAudit.Add($"{src}\t{old.name}\t{old.vertexCount}\t{max:R}\t{relative:R}\ttrue");
                if (relative > .001f) throw new InvalidOperationException("Mesh error exceeds 0.1% of bounds: " + src + "/" + old.name);
            }
            guidMap.Add(AssetDatabase.AssetPathToGUID(src), AssetDatabase.AssetPathToGUID(dst));
        }
        File.WriteAllLines(Evidence + "/mesh-integrity.tsv", meshAudit);
        File.WriteAllLines(Evidence + "/biplane-texture-bindings.tsv", textureAudit);
        string fontPath = AssetRoot + "/Fonts/NotoSansCJKsc-Subset.otf";
        AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
        guidMap.Add(AssetDatabase.AssetPathToGUID("Assets/Fonts/OpenSource/NotoSansCJKsc-Regular.otf"), AssetDatabase.AssetPathToGUID(fontPath));
        var newScenes = new List<EditorBuildSettingsScene>();
        foreach (string src in sourceScenes)
        {
            string text = File.ReadAllText(src).Replace(OldStem, NewStem);
            foreach (var pair in materialRefs) text = text.Replace(pair.Key + ", type: 3", pair.Value + ", type: 2");
            foreach (var pair in guidMap) text = text.Replace(pair.Key, pair.Value);
            string dst = SceneRoot + "/" + Path.GetFileName(src).Replace(OldStem, NewStem);
            File.WriteAllText(dst, text);
            newScenes.Add(new EditorBuildSettingsScene(dst, true));
        }
        AssetDatabase.Refresh();
        EditorBuildSettings.scenes = newScenes.ToArray();
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Evidence + "/asset-guid-map.tsv", guidMap.Select(p => p.Key + "\t" + p.Value));
        File.WriteAllLines(Evidence + "/final-dependencies.txt", AssetDatabase.GetDependencies(newScenes.Select(s => s.path).ToArray(), true));
        LatestProjectTools.Validate();
        Debug.Log("[V82] PREPARE PASSED models=" + modelPaths.Length + " textures=" + textures.Length + " scenes=" + newScenes.Count);
    }

    public static void Audit()
    {
        Directory.CreateDirectory(Evidence);
        var deps = AssetDatabase.GetDependencies(Scenes, true);
        File.WriteAllLines(Evidence + "/baseline-dependencies.txt", deps);
        var models = new List<string> { "asset\tmeshes\tvertices\ttriangles\tcompression\treadable" };
        foreach (string path in deps.OrderBy(p => p))
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
            var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
            long indices = meshes.Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i)));
            models.Add($"{path}\t{meshes.Length}\t{meshes.Sum(m => (long)m.vertexCount)}\t{indices/3}\t{importer.meshCompression}\t{importer.isReadable}");
        }
        File.WriteAllLines(Evidence + "/models.tsv", models);
        var refs = new List<string> { "scene\tobject\tactive\tcomponent\tfield\tasset\tsubasset" };
        var audio = new List<string> { "scene\tobject\tspatialize\tambisonic" };
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            foreach (var c in t.GetComponents<Component>().Where(c => c))
            {
                if (c is AudioSource source) audio.Add($"{path}\t{Hierarchy(t)}\t{source.spatialize}\t{(source.clip && source.clip.ambisonic)}");
                var so = new SerializedObject(c);
                var prop = so.GetIterator();
                while (prop.Next(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference || !prop.objectReferenceValue) continue;
                    string asset = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                    if (string.IsNullOrEmpty(asset)) continue;
                    refs.Add($"{path}\t{Hierarchy(t)}\t{t.gameObject.activeInHierarchy}\t{c.GetType().Name}\t{prop.propertyPath}\t{asset}\t{prop.objectReferenceValue.name}");
                }
            }
        }
        File.WriteAllLines(Evidence + "/scene-references.tsv", refs);
        File.WriteAllLines(Evidence + "/audio-usage.tsv", audio);
        Debug.Log("[V82] AUDIT PASSED models=" + (models.Count-1) + " references=" + (refs.Count-1));
    }

    public static void RefineAndCapture()
    {
        foreach (string src in File.ReadAllLines(Evidence + "/models.tsv").Skip(1).Select(l => l.Split('\t')[0]).Where(p => p.Contains("Workbench")))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(AssetRoot + "/" + src);
            importer.meshCompression = ((ModelImporter)AssetImporter.GetAtPath(src)).meshCompression;
            importer.SaveAndReimport();
            Debug.Log("[V82] VISUAL EXCEPTION preserved original mesh compression: " + src);
        }
        LatestProjectTools.CaptureFinal();
    }

    public static void ValidateAndRunFlow()
    {
        LatestProjectTools.Validate();
        string[] deps = AssetDatabase.GetDependencies(Scenes, true);
        string font = "Assets/Fonts/OpenSource/NotoSansCJKsc-Regular.otf";
        var models = File.ReadAllLines(Evidence + "/models.tsv").Skip(1).Select(l => l.Split('\t')[0]).ToArray();
        if (deps.Contains(font) || models.Any(deps.Contains)) throw new InvalidOperationException("Baseline model/font leaked into V82 build.");
        var rows = new List<string> { "asset\tmesh\tcompression\tvertices\tmax_position_error\tmax_uv_error\tmax_normal_angle\tindices_equal" };
        foreach (string src in models)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(AssetRoot + "/" + src).OfType<Mesh>().ToDictionary(LocalId);
            foreach (Mesh old in AssetDatabase.LoadAllAssetsAtPath(src).OfType<Mesh>())
            {
                Mesh mesh = meshes[LocalId(old)];
                if (mesh.vertexCount != old.vertexCount || mesh.subMeshCount != old.subMeshCount) throw new InvalidOperationException("Mesh identity changed");
                for (int i=0; i<mesh.subMeshCount; i++)
                    if (!mesh.GetIndices(i).SequenceEqual(old.GetIndices(i))) throw new InvalidOperationException("Topology changed");
                var a = old.vertices; var b = mesh.vertices;
                var ua = old.uv; var ub = mesh.uv;
                var na = old.normals; var nb = mesh.normals;
                float position=0, uv=0, angle=0;
                for (int i=0; i<a.Length; i++) position = Mathf.Max(position, Vector3.Distance(a[i], b[i]));
                for (int i=0; i<ua.Length; i++) uv = Mathf.Max(uv, Vector2.Distance(ua[i], ub[i]));
                for (int i=0; i<na.Length; i++) angle = Mathf.Max(angle, Vector3.Angle(na[i], nb[i]));
                var compression = ((ModelImporter)AssetImporter.GetAtPath(AssetRoot + "/" + src)).meshCompression;
                rows.Add($"{src}\t{mesh.name}\t{compression}\t{mesh.vertexCount}\t{position:R}\t{uv:R}\t{angle:R}\ttrue");
            }
        }
        File.WriteAllLines(Evidence + "/final-mesh-integrity.tsv", rows);
        File.WriteAllLines(Evidence + "/final-dependencies.txt", deps);
        Debug.Log("[V82] RESOURCE VALIDATION PASSED topology unchanged; original models/font absent from build graph");
        AndroidSizeOptimization.RunFullFlow();
    }

    public static void ValidateFontCoverage()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/OpenSource/NotoSansCJKsc-Regular.otf");
        Font subset = AssetDatabase.LoadAssetAtPath<Font>(AssetRoot + "/Fonts/NotoSansCJKsc-Subset.otf");
        if (!source || !subset) throw new InvalidOperationException("Font baseline/subset missing.");
        var chars = new HashSet<char>();
        foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs").Concat(Scenes))
        {
            string text = File.ReadAllText(file);
            foreach (char c in text) chars.Add(c);
            foreach (Match match in Regex.Matches(text, @"\\u([0-9a-fA-F]{4})"))
                chars.Add((char)Convert.ToInt32(match.Groups[1].Value, 16));
        }
        var missing = chars.Where(c => !char.IsControl(c) && source.HasCharacter(c) && !subset.HasCharacter(c)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Regenerate font subset; missing glyphs: " + new string(missing));
        Debug.Log("[V82] FONT COVERAGE PASSED current scene/script corpus");
    }
}
