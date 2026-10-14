using System;
using System.Linq;
using UnityEngine;

/// <summary>Locks the hero, glass dome, pedestal and lower labels to one axis.</summary>
[DefaultExecutionOrder(230)]
[DisallowMultipleComponent]
public sealed class RuntimeCompletionShowcaseAlignmentV73 : MonoBehaviour
{
    public CompletionInteractionController completion;
    public Transform dome;
    public Transform pedestal;
    public Transform plaque;
    public Transform returnButton;
    public Transform upperUiRoot;
    public Camera sceneCamera;
    public TextMesh[] lowerLabels;

    void Start() { AlignNow(); }
    void LateUpdate() { FaceLabels(); }

    public bool AlignNow()
    {
        if (!completion) completion = FindFirstObjectByType<CompletionInteractionController>();
        if (!sceneCamera)
            sceneCamera = PersistentPuzzleBootstrap.Instance && PersistentPuzzleBootstrap.Instance.xrCamera
                ? PersistentPuzzleBootstrap.Instance.xrCamera : Camera.main;
        if (!dome) dome = FindTransform("dome");
        if (!pedestal)
            pedestal = FindTransform("pedestal") ?? FindTransform("plinth") ??
                FindTransform("platform") ?? FindTransform("stand");
        if (!plaque) plaque = FindTransform("plinth-mounted completion plaque");
        if (!returnButton) returnButton = FindTransform("return-selection physical button");
        if (!completion || !completion.heroModel || !dome || !pedestal)
            return false;
        // Some original completion shells keep their glass renderers disabled
        // until their stage animation starts. Root positions remain the stable
        // alignment datum in that state; use renderer centres when available.
        Vector3 axis = pedestal.position;
        if (BoundsOf(pedestal, out Bounds baseBounds)) axis = baseBounds.center;
        Vector3 domeCenter = BoundsOf(dome, out Bounds domeBounds)
            ? domeBounds.center : dome.position;
        Vector3 heroCenter = BoundsOf(completion.heroModel, out Bounds heroBounds)
            ? heroBounds.center : completion.heroModel.position;
        axis.y = 0f;
        MoveHorizontal(dome, domeCenter, axis);
        MoveHorizontal(completion.heroModel, heroCenter, axis);
        // Every physical completion element is referenced to the wooden plinth,
        // never to the last selected exhibit.  This fixes model-specific (Ship)
        // drift where the dome was centred but its lower furniture was not.
        // The plaque and Return control are already mounted in the plinth
        // furniture.  Do not force their depth to the hero centre: that puts
        // them in front of deep models.  Their authored physical mounts are
        // the alignment authority.
        if (upperUiRoot) upperUiRoot.gameObject.SetActive(false);
        if (lowerLabels == null || lowerLabels.Length == 0)
            lowerLabels = FindObjectsByType<TextMesh>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Where(label => label &&
                (label.name.IndexOf("completion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 label.name.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 label.name.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        FaceLabels();
        return true;
    }

    void FaceLabels()
    {
        if (!sceneCamera || lowerLabels == null) return;
        foreach (TextMesh label in lowerLabels)
        {
            if (!label) continue;
            Vector3 forward = sceneCamera.transform.position - label.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(-forward.normalized, sceneCamera.transform.up);
        }
    }

    static void MoveHorizontal(Transform item, Vector3 center, Vector3 axis)
    {
        item.position += new Vector3(axis.x - center.x, 0f, axis.z - center.z);
    }

    Transform FindTransform(string token) => FindObjectsByType<Transform>(
        FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value =>
        value && value.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

    public static bool BoundsOf(Transform root, out Bounds bounds)
    {
        bounds = default; bool initialized = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer || !renderer.enabled || renderer is ParticleSystemRenderer ||
                !renderer.gameObject.activeInHierarchy) continue;
            if (!initialized) { bounds = renderer.bounds; initialized = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return initialized;
    }
}
