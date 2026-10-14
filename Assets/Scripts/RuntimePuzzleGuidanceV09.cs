using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps the next semantic Part recommendation internal and turns an explicit
/// hint request into a short cyan Part-to-socket path plus the exact full-Part
/// socket ghost. Nothing is left visible while the player has not asked for
/// help; normal held-Part socket feedback remains owned by RuntimePuzzlePiece.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimePuzzleGuidanceV09 : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    [Min(0.001f)] public float pathWidth = 0.0018f;
    [Min(0.02f)] public float pathArcHeight = 0.055f;
    [Min(8)] public int pathSegments = 24;
    public bool explicitPickupHighlightV25;
    [Tooltip(
        "V80: recommendation changes are pushed by reset/snap events. The " +
        "Update loop then animates only an explicitly visible hint path.")]
    public bool eventDrivenRecommendationV80;

    RuntimePuzzlePiece recommended;
    RuntimePuzzlePiece pathPiece;
    LineRenderer path;
    Material pathMaterial;
    float pathRemaining;
    float pathDuration;

    public bool PathVisible => path && path.enabled;
    public RuntimePuzzlePiece RecommendedPiece => recommended;

    void Awake()
    {
        if (!level)
            level = GetComponent<RuntimePuzzleLevelController>();
        EnsurePath();
    }

    void OnEnable()
    {
        RefreshRecommendation();
    }

    void Update()
    {
        if (!eventDrivenRecommendationV80)
            RefreshRecommendation();
        UpdatePath();
    }

    public void ShowHintPath(RuntimePuzzlePiece piece, float seconds)
    {
        if (!piece || !piece.targetSocket)
            return;
        EnsurePath();
        pathPiece = piece;
        pathDuration = Mathf.Max(0.1f, seconds);
        pathRemaining = pathDuration;
        path.enabled = true;
        if (explicitPickupHighlightV25)
            piece.PulsePickupHighlight(seconds);
        UpdatePathGeometry();
    }

    public void RefreshRecommendation()
    {
        RuntimePuzzlePiece next = level && level.pieces != null
            ? level.pieces
                .Where(value => value && !value.IsSnapped && value.IsAvailable)
                .OrderBy(value => value.sequence)
                .FirstOrDefault()
            : null;
        if (next == recommended)
            return;
        if (recommended)
            recommended.SetRecommended(false);
        recommended = next;
        // A recommendation is data, not a permanent visual hint. The previous
        // V09 behaviour kept a faint ghost alive for the entire puzzle and was
        // ambiguous in VR. Explicit H/lamp assistance owns the two-second cue.
        if (recommended)
            recommended.SetRecommended(false);
    }

    void EnsurePath()
    {
        if (path)
            return;
        GameObject pathObject = new GameObject(
            "V09 dynamic two-second Part-to-socket hint path");
        pathObject.transform.SetParent(transform, false);
        path = pathObject.AddComponent<LineRenderer>();
        path.useWorldSpace = true;
        path.loop = false;
        path.positionCount = Mathf.Max(8, pathSegments);
        path.widthMultiplier = pathWidth;
        path.numCapVertices = 4;
        path.numCornerVertices = 4;
        path.textureMode = LineTextureMode.Stretch;
        path.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        path.receiveShadows = false;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color");
        if (shader)
        {
            pathMaterial = new Material(shader)
            {
                name = "V10 runtime cyan semantic Part-to-socket hint path",
            };
            pathMaterial.SetColor(
                "_BaseColor",
                new Color(0.08f, 0.72f, 1f, 0.92f));
            pathMaterial.SetColor(
                "_Color",
                new Color(0.08f, 0.72f, 1f, 0.92f));
            if (pathMaterial.HasProperty("_Surface"))
            {
                pathMaterial.SetFloat("_Surface", 1f);
                pathMaterial.SetFloat("_Blend", 0f);
                pathMaterial.SetFloat("_ZWrite", 0f);
                pathMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                pathMaterial.renderQueue =
                    (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            path.material = pathMaterial;
        }
        path.enabled = false;
    }

    void UpdatePath()
    {
        if (!path || !path.enabled)
            return;
        pathRemaining = Mathf.Max(
            0f,
            pathRemaining - Time.unscaledDeltaTime);
        if (!pathPiece || !pathPiece.targetSocket || pathRemaining <= 0f)
        {
            path.enabled = false;
            pathPiece = null;
            return;
        }
        UpdatePathGeometry();
        float normalized = pathRemaining / Mathf.Max(0.1f, pathDuration);
        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(normalized * 2f, 1f));
        Color color = new Color(0.06f, 0.68f, 1f, 0.92f * alpha);
        path.startColor = color;
        path.endColor = new Color(0.34f, 0.92f, 1f, color.a * 0.86f);
        path.widthMultiplier =
            pathWidth * (0.90f + 0.10f * Mathf.Sin(Time.unscaledTime * 4f));
    }

    void UpdatePathGeometry()
    {
        int count = Mathf.Max(8, pathSegments);
        if (path.positionCount != count)
            path.positionCount = count;
        // The hint must answer both questions at once: which complete Part and
        // where it belongs. A short lifted curve joins the visible Part centre
        // to the exact full-model socket ghost; it is not a screen-space ring.
        Renderer[] ghosts = pathPiece.ghostRenderers != null &&
                            pathPiece.ghostRenderers.Length > 0
            ? pathPiece.ghostRenderers
            : pathPiece.ghostRenderer
                ? new[] { pathPiece.ghostRenderer }
                : System.Array.Empty<Renderer>();
        bool semanticClip = HasSemanticClip(pathPiece.solidRenderers) ||
                            HasSemanticClip(ghosts);
        // Renderer.bounds still describes the full unclipped mother mesh, so
        // using it for a V10 semantic module made the curve float overhead.
        // The authored Part/socket transforms are placed at the visible
        // clipped-module centres and therefore remain exact from every angle.
        Vector3 start = semanticClip
            ? pathPiece.transform.position
            : VisualCenter(pathPiece.solidRenderers, pathPiece.transform.position);
        Vector3 end = semanticClip
            ? pathPiece.targetSocket.position
            : VisualCenter(ghosts, pathPiece.targetSocket.position);
        Vector3 midpoint = Vector3.Lerp(start, end, 0.5f) +
                           Vector3.up * Mathf.Max(
                               pathArcHeight,
                               Vector3.Distance(start, end) * 0.12f);
        for (int index = 0; index < count; index++)
        {
            float t = index / (float)(count - 1);
            float inverse = 1f - t;
            path.SetPosition(index,
                inverse * inverse * start +
                2f * inverse * t * midpoint +
                t * t * end);
        }
    }

    static Vector3 VisualCenter(Renderer[] renderers, Vector3 fallback)
    {
        if (renderers == null)
            return fallback;
        bool found = false;
        Bounds combined = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer || !renderer.gameObject.activeInHierarchy)
                continue;
            if (!found)
            {
                combined = renderer.bounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }
        return found ? combined.center : fallback;
    }

    static bool HasSemanticClip(Renderer[] renderers)
    {
        if (renderers == null)
            return false;
        return renderers.Any(renderer => renderer &&
            renderer.sharedMaterials.Any(material => material &&
                material.shader &&
                material.shader.name.StartsWith(
                    "PersonallyPuzzled/V10SemanticModuleClip",
                    System.StringComparison.Ordinal)));
    }

    static Bounds VisualBounds(Renderer[] renderers, Vector3 fallback)
    {
        if (renderers == null)
            return new Bounds(fallback, Vector3.one * 0.08f);
        bool found = false;
        Bounds combined = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer || !renderer.gameObject.activeInHierarchy)
                continue;
            if (!found)
            {
                combined = renderer.bounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }
        return found
            ? combined
            : new Bounds(fallback, Vector3.one * 0.08f);
    }

    void OnDestroy()
    {
        if (pathMaterial)
            Destroy(pathMaterial);
    }
}
