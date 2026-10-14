using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// V78 completion presentation. It preserves each approved assembled exhibit's
/// native PBR materials, fits it inside the glass and seats it on the plinth.
/// </summary>
[DefaultExecutionOrder(320)]
[DisallowMultipleComponent]
public sealed class RuntimeCompletionDisplayParityV78 : MonoBehaviour
{
    [Serializable]
    public sealed class RendererPaletteBinding
    {
        public string rendererName;
        public string meshName;
        public Material[] materials;
    }

    [Serializable]
    public sealed class LevelPalette
    {
        public RendererPaletteBinding[] bindings;
        public string fallbackSourceMaterialName;
        public Material fallbackMaterial;
    }

    public RuntimeLevelHeroContinuityV04 continuity;
    public CompletionInteractionController completion;
    public RuntimeCompletionShowcaseAlignmentV73 alignment;
    public Transform dome;
    public Transform pedestal;
    [Range(0.55f, 0.90f)] public float widthFill = 0.76f;
    [Range(0.55f, 0.90f)] public float heightFill = 0.82f;
    [Range(0.45f, 0.85f)] public float depthFill = 0.68f;
    [Min(0f)] public float contactClearance = 0.0015f;
    public LevelPalette[] selectionPalettes;

    public int AppliedLevelIndex { get; private set; } = -1;
    public float LastSupportGap { get; private set; }
    public string LastModelName { get; private set; }
    public int LastAppliedPaletteBindings { get; private set; }

    void Start()
    {
        int level = PersistentPuzzleBootstrap.Instance
            ? PersistentPuzzleBootstrap.Instance.CurrentLevelIndex
            : 2;
        FitCurrent(level);
    }

    public bool ApplyLevelAndFit(int levelIndex)
    {
        ResolveReferences();
        if (continuity && continuity.heroRoot)
        {
            foreach (Transform existing in continuity.heroRoot.Cast<Transform>()
                         .Where(value => value && value.name.StartsWith(
                             "Current completed model continuity",
                             StringComparison.Ordinal)))
                existing.gameObject.SetActive(false);
        }
        if (!continuity || !continuity.ApplyLevel(levelIndex))
            return false;
        return FitCurrent(levelIndex);
    }

    public bool FitCurrent(int levelIndex)
    {
        ResolveReferences();
        if (!continuity || !completion || !completion.heroModel ||
            !dome || !pedestal)
            return false;
        LastAppliedPaletteBindings = ApplySelectionPalette(levelIndex);
        if (!BoundsOf(dome, out Bounds domeBounds) ||
            !BoundsOf(pedestal, out Bounds pedestalBounds) ||
            !BoundsOf(completion.heroModel, out Bounds heroBounds))
            return false;

        float availableHeight =
            Mathf.Max(0.01f, domeBounds.max.y - pedestalBounds.max.y);
        float scale = Mathf.Min(
            domeBounds.size.x * widthFill / Mathf.Max(0.001f, heroBounds.size.x),
            Mathf.Min(
                availableHeight * heightFill /
                    Mathf.Max(0.001f, heroBounds.size.y),
                domeBounds.size.z * depthFill /
                    Mathf.Max(0.001f, heroBounds.size.z)));
        if (!float.IsFinite(scale) || scale <= 0.0001f)
            return false;
        completion.heroModel.localScale *= scale;

        if (!BoundsOf(completion.heroModel, out heroBounds))
            return false;
        Vector3 target = new Vector3(
            pedestalBounds.center.x,
            pedestalBounds.max.y + contactClearance + heroBounds.extents.y,
            pedestalBounds.center.z);
        completion.heroModel.position += target - heroBounds.center;

        if (alignment && !alignment.AlignNow())
            return false;
        if (!BoundsOf(completion.heroModel, out heroBounds) ||
            !BoundsOf(pedestal, out pedestalBounds))
            return false;

        // AlignNow only changes X/Z. Reassert exact support after any model-
        // specific horizontal correction and cache the free-play upright Y.
        completion.heroModel.position += Vector3.up *
            (pedestalBounds.max.y + contactClearance - heroBounds.min.y);
        completion.RecacheUprightYV78();
        if (completion.enableFreeLiftAndTwoHandScaleV79)
            completion.RecacheInspectionBaselineV79();

        if (!BoundsOf(completion.heroModel, out heroBounds))
            return false;
        LastSupportGap = heroBounds.min.y - pedestalBounds.max.y;
        AppliedLevelIndex = Mathf.Clamp(levelIndex, 0, 4);
        LastModelName = continuity.AppliedModelName;
        Debug.Log("[PPVR V78 Completion] model=" + LastModelName +
            " supportGap=" + LastSupportGap.ToString("F6") +
            " paletteBindings=" + LastAppliedPaletteBindings +
            " heroBounds=" + heroBounds.size.ToString("F3"));
        return Mathf.Abs(LastSupportGap - contactClearance) <= 0.001f;
    }

    void ResolveReferences()
    {
        if (!continuity)
            continuity = FindFirstObjectByType<RuntimeLevelHeroContinuityV04>();
        if (!completion)
            completion = FindFirstObjectByType<CompletionInteractionController>();
        if (!alignment)
            alignment = FindFirstObjectByType<RuntimeCompletionShowcaseAlignmentV73>();
        if (!dome && alignment)
            dome = alignment.dome;
        if (!pedestal && alignment)
            pedestal = alignment.pedestal;
    }

    int ApplySelectionPalette(int levelIndex)
    {
        if (selectionPalettes == null ||
            levelIndex < 0 ||
            levelIndex >= selectionPalettes.Length ||
            selectionPalettes[levelIndex] == null)
            return 0;
        LevelPalette palette = selectionPalettes[levelIndex];
        Transform visual = CurrentVisual(continuity);
        if (!visual)
            return 0;
        int applied = 0;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)
                     .Where(value => value && value.enabled))
        {
            string meshName = MeshName(renderer);
            RendererPaletteBinding binding =
                palette.bindings != null
                    ? palette.bindings.FirstOrDefault(value => value != null &&
                        value.materials != null &&
                        value.materials.Length > 0 &&
                        (value.rendererName == renderer.name ||
                         !string.IsNullOrEmpty(value.meshName) &&
                         value.meshName == meshName))
                    : null;
            if (binding != null)
            {
                renderer.sharedMaterials = binding.materials;
                applied++;
                continue;
            }
            if (!palette.fallbackMaterial ||
                string.IsNullOrEmpty(palette.fallbackSourceMaterialName))
                continue;
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                if (!materials[index] ||
                    materials[index].name.IndexOf(
                        palette.fallbackSourceMaterialName,
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                materials[index] = palette.fallbackMaterial;
                changed = true;
            }
            if (!changed)
                continue;
            renderer.sharedMaterials = materials;
            applied++;
        }
        return applied;
    }

    static string MeshName(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
            return skinned.sharedMesh.name;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter && filter.sharedMesh ? filter.sharedMesh.name : string.Empty;
    }

    public static bool BoundsOf(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool has = false;
        if (!root)
            return false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)
                     .Where(value => value && value.enabled &&
                         value.gameObject.activeInHierarchy &&
                         !(value is ParticleSystemRenderer) &&
                         !value.GetComponent<TextMesh>()))
        {
            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return has;
    }

    public static Transform CurrentVisual(RuntimeLevelHeroContinuityV04 value)
    {
        return value && value.heroRoot
            ? value.heroRoot.Cast<Transform>().LastOrDefault(child =>
                child && child.gameObject.activeInHierarchy &&
                child.name.StartsWith(
                    "Current completed model continuity",
                    StringComparison.Ordinal))
            : null;
    }
}
