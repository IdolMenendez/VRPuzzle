using UnityEngine;

/// <summary>
/// Restrained two-hand load feedback for the design-fidelity ship lift. The
/// runtime activates it only after the real heavy Part has two holders; the
/// editor capture can opt into the same state without changing clean play.
/// </summary>
public sealed class Stage5V09WeightFeedback : MonoBehaviour
{
    public RuntimePuzzlePiece heavyPiece;
    public Transform leftReferenceHand;
    public Transform rightReferenceHand;
    public LineRenderer[] loadFilaments;
    public bool editorPreview;

    public void RefreshNow()
    {
        bool active = editorPreview ||
            heavyPiece && heavyPiece.ExternalHolderCount >= 2;
        if (loadFilaments == null) return;
        foreach (LineRenderer filament in loadFilaments)
            if (filament) filament.enabled = active;
        if (!active || !heavyPiece ||
            !leftReferenceHand || !rightReferenceHand)
            return;

        Bounds bounds = VisualBounds(heavyPiece.solidRenderers);
        Camera camera = Camera.main;
        Vector3 right = camera ? camera.transform.right : Vector3.right;
        Vector3 up = camera ? camera.transform.up : Vector3.up;
        Vector3 forward = camera ? camera.transform.forward : Vector3.forward;
        float halfSpan = Mathf.Clamp(
            Vector3.Dot(bounds.extents,
                new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z))),
            0.12f, 0.28f);
        Vector3 leftGrip = bounds.center - right * halfSpan;
        Vector3 rightGrip = bounds.center + right * halfSpan;
        Vector3 leftWrist = leftReferenceHand.position;
        Vector3 rightWrist = rightReferenceHand.position;
        float phase = Application.isPlaying ? Time.unscaledTime * 5.2f : 1.35f;
        for (int index = 0; index < loadFilaments.Length; index++)
        {
            bool leftSide = index < loadFilaments.Length / 2;
            Vector3 start = leftSide ? leftWrist : rightWrist;
            Vector3 end = leftSide ? leftGrip : rightGrip;
            LineRenderer line = loadFilaments[index];
            line.positionCount = 10;
            for (int point = 0; point < line.positionCount; point++)
            {
                float t = point / (float)(line.positionCount - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float wave = Mathf.Sin(t * Mathf.PI * 4f + phase + index * 1.7f);
                Vector3 offset = up * wave * envelope * 0.006f +
                                 forward * Mathf.Cos(t * Mathf.PI * 3f + index) *
                                 envelope * 0.0025f;
                line.SetPosition(point, Vector3.Lerp(start, end, t) + offset);
            }
        }
    }

    void Update() => RefreshNow();

    static Bounds VisualBounds(Renderer[] renderers)
    {
        bool initialized = false;
        Bounds bounds = default;
        if (renderers != null)
            foreach (Renderer renderer in renderers)
            {
                if (!renderer) continue;
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
    }
}
