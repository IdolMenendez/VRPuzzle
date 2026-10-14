using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Keeps one authored model identity through Selection, assembly, completion
/// free-play and the archive flight.  The accepted Eiffel presentation remains
/// untouched for the Eiffel level; Cottage and Ship reuse their approved full
/// model assets and are fitted into the same physical presentation volume.
/// </summary>
[DefaultExecutionOrder(80)]
public sealed class RuntimeLevelHeroContinuityV04 : MonoBehaviour
{
    public Transform heroRoot;
    public GameObject[] levelModelPrefabs;
    public TextMesh[] modelIdentityLabels;
    public Vector3[] levelEulerOffsets;
    [Tooltip("Optional per-model world-space fitting correction after the replacement hero is normalized to the authored display volume.")]
    public Vector3[] levelPositionOffsets;
    public Material presentationMaterial;
    public GameObject[] duplicateArchiveDisplays;
    [Range(0.72f, 1.08f)] public float fitMultiplier = 0.92f;
    public bool preserveAuthoredEiffel = true;
    [Min(0f)] public float explicitTargetLongestDimension;
    public bool forceUprightRoot;

    public int AppliedLevelIndex { get; private set; } = -1;
    public string AppliedModelName { get; private set; }

    static readonly string[] ModelNames =
    {
        "CLOCKMAKER COTTAGE",
        "MASTER SHIP",
        "EIFFEL TOWER",
        "GOLDEN PAGODA",
        "DUTCH WINDMILL",
    };

    void Start()
    {
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        int level = bootstrap
            ? Mathf.Clamp(bootstrap.CurrentLevelIndex, 0, ModelNames.Length - 1)
            : 2;
        if (duplicateArchiveDisplays != null &&
            level < duplicateArchiveDisplays.Length &&
            duplicateArchiveDisplays[level])
            duplicateArchiveDisplays[level].SetActive(false);
        ApplyLevel(level);
    }

    public bool ApplyLevel(int levelIndex)
    {
        int level = Mathf.Clamp(levelIndex, 0, ModelNames.Length - 1);
        AppliedLevelIndex = level;
        AppliedModelName = ModelNames[level];
        UpdateLabels(level);

        if (!heroRoot)
        {
            Debug.LogError(
                "[Personally Puzzled Continuity] Missing completion/archive hero root.",
                this);
            return false;
        }
        if (forceUprightRoot)
        {
            float yaw = heroRoot.eulerAngles.y;
            heroRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        if (level == 2 && preserveAuthoredEiffel)
        {
            ConstrainExistingHero();
            Debug.Log(
                "[Personally Puzzled Continuity] preserved authored Eiffel hero",
                this);
            return true;
        }
        if (levelModelPrefabs == null ||
            level >= levelModelPrefabs.Length ||
            !levelModelPrefabs[level])
        {
            Debug.LogError(
                "[Personally Puzzled Continuity] Missing approved full-model prefab for level " +
                level,
                this);
            return false;
        }

        foreach (Transform existing in heroRoot
                     .Cast<Transform>()
                     .Where(value => value.name.StartsWith(
                         "Current completed model continuity",
                         System.StringComparison.Ordinal))
                     .ToArray())
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        Renderer[] authoredRenderers = heroRoot
            .GetComponentsInChildren<Renderer>(true)
            .Where(IsModelRenderer)
            .ToArray();
        Bounds referenceBounds = Encapsulate(authoredRenderers);
        float referenceExtent = LargestDimension(referenceBounds.size);

        GameObject visual = Instantiate(
            levelModelPrefabs[level],
            heroRoot,
            false);
        visual.name =
            "Current completed model continuity · " + ModelNames[level];
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(
            levelEulerOffsets != null && level < levelEulerOffsets.Length
                ? levelEulerOffsets[level]
                : Vector3.zero);
        visual.transform.localScale = Vector3.one;
        RemoveNestedPhysics(visual);

        Renderer[] newRenderers = visual
            .GetComponentsInChildren<Renderer>(true)
            .Where(IsModelRenderer)
            .ToArray();
        if (newRenderers.Length == 0)
        {
            Destroy(visual);
            Debug.LogError(
                "[Personally Puzzled Continuity] Approved prefab has no renderable model geometry.",
                this);
            return false;
        }
        if (presentationMaterial)
        {
            foreach (Renderer renderer in newRenderers)
            {
                int materialCount = Mathf.Max(
                    1,
                    renderer.sharedMaterials != null
                        ? renderer.sharedMaterials.Length
                        : 0);
                renderer.sharedMaterials = Enumerable
                    .Repeat(presentationMaterial, materialCount)
                    .ToArray();
            }
        }

        Bounds newBounds = Encapsulate(newRenderers);
        float newExtent = LargestDimension(newBounds.size);
        float targetExtent = explicitTargetLongestDimension > 0.001f
            ? explicitTargetLongestDimension
            : referenceExtent > 0.001f
                ? referenceExtent * fitMultiplier
                : 0.64f;
        if (newExtent > 0.0001f)
            visual.transform.localScale *= targetExtent / newExtent;
        newBounds = Encapsulate(newRenderers);
        Vector3 targetCenter = referenceExtent > 0.001f
            ? referenceBounds.center
            : heroRoot.position;
        visual.transform.position += targetCenter - newBounds.center;
        if (levelPositionOffsets != null &&
            level < levelPositionOffsets.Length)
            visual.transform.position += levelPositionOffsets[level];

        foreach (Renderer renderer in authoredRenderers)
            if (renderer)
                renderer.enabled = false;

        Debug.Log(
            "[Personally Puzzled Continuity] " +
            $"level={level} model={AppliedModelName} " +
            $"targetExtent={targetExtent:F3}m source={levelModelPrefabs[level].name}",
            this);
        return true;
    }

    void ConstrainExistingHero()
    {
        if (!heroRoot || explicitTargetLongestDimension <= 0.001f)
            return;
        Renderer[] renderers = heroRoot
            .GetComponentsInChildren<Renderer>(true)
            .Where(IsModelRenderer)
            .ToArray();
        if (renderers.Length == 0)
            return;
        Bounds before = Encapsulate(renderers);
        float current = LargestDimension(before.size);
        if (current <= 0.0001f)
            return;
        Vector3 desiredCenter = before.center;
        heroRoot.localScale *= explicitTargetLongestDimension / current;
        Bounds after = Encapsulate(renderers);
        heroRoot.position += desiredCenter - after.center;
    }

    void UpdateLabels(int level)
    {
        if (modelIdentityLabels == null)
            return;
        foreach (TextMesh label in modelIdentityLabels)
        {
            if (!label)
                continue;
            string prefix = label.text != null &&
                label.text.Contains("·")
                    ? level == 0
                        ? "LONDON · "
                        : level == 1
                            ? "PORTSMOUTH · "
                            : level == 2
                                ? "PARIS · "
                                : level == 3
                                    ? "SUZHOU · "
                                    : "ZAANSE SCHANS · "
                    : string.Empty;
            label.text = prefix + ModelNames[level];
        }
    }

    static bool IsModelRenderer(Renderer renderer)
    {
        return renderer &&
            (renderer is MeshRenderer || renderer is SkinnedMeshRenderer);
    }

    static Bounds Encapsulate(IEnumerable<Renderer> renderers)
    {
        Bounds bounds = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer)
                continue;
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }

    static float LargestDimension(Vector3 size)
    {
        return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
    }

    static void RemoveNestedPhysics(GameObject visual)
    {
        foreach (Collider value in visual.GetComponentsInChildren<Collider>(true))
            Destroy(value);
        foreach (Rigidbody value in visual.GetComponentsInChildren<Rigidbody>(true))
            Destroy(value);
        foreach (XRGrabInteractable value in
                 visual.GetComponentsInChildren<XRGrabInteractable>(true))
            Destroy(value);
    }
}
