using UnityEngine;

/// <summary>
/// V62 selection feedback. A stable target becomes the persistent selected
/// exhibit until another exhibit is deliberately gazed. The feedback uses
/// opaque URP outline geometry and a base ring; it has no particles, no scene
/// light, and no transparent fallback shader that can render magenta on PICO.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class SelectionGazeOutlineV62 : MonoBehaviour
{
    [System.Serializable]
    public sealed class ExhibitFeedback
    {
        public int exhibitIndex;
        public Renderer[] outlineRenderers;
        public Renderer baseRing;
    }

    public RuntimeIntentRayV46 ray;
    public SelectionCarouselController selection;
    public ExhibitFeedback[] feedback;
    [Min(0.2f)] public float pulseHz = 1.35f;
    [Range(0f, 1f)] public float pulseAmount = 0.22f;
    public Color outlineColor = new Color(0.20f, 0.90f, 1f, 1f);

    int selectedIndex = -1;
    MaterialPropertyBlock block;

    public int SelectedIndex => selectedIndex;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        ApplySelection(-1, false);
    }

    void Update()
    {
        if (!ray)
            return;

        SelectionExhibitTargetV09 target = ray.StableSelectionTargetV46;
        if (target && target.exhibitIndex != selectedIndex)
        {
            ApplySelection(target.exhibitIndex, true);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Hint, target.transform.position);
        }

        float pulse = 1f + pulseAmount *
            (0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * pulseHz * Mathf.PI * 2f));
        ApplyPulse(pulse);
    }

    public void SetPreviewIndexV62(int exhibitIndex)
    {
        if (block == null)
            block = new MaterialPropertyBlock();
        ApplySelection(exhibitIndex, false);
        ApplyPulse(1.18f);
    }

    void ApplySelection(int exhibitIndex, bool keepWhenRayLeaves)
    {
        selectedIndex = exhibitIndex;
        if (feedback == null)
            return;

        foreach (ExhibitFeedback item in feedback)
        {
            if (item == null)
                continue;
            bool active = item.exhibitIndex == selectedIndex;
            if (item.outlineRenderers != null)
                foreach (Renderer renderer in item.outlineRenderers)
                    if (renderer)
                        renderer.enabled = active;
            if (item.baseRing)
                item.baseRing.enabled = active;
        }
    }

    void ApplyPulse(float intensity)
    {
        if (feedback == null || selectedIndex < 0)
            return;
        foreach (ExhibitFeedback item in feedback)
        {
            if (item == null || item.exhibitIndex != selectedIndex)
                continue;
            if (item.outlineRenderers != null)
                foreach (Renderer renderer in item.outlineRenderers)
                    SetRendererIntensity(renderer, intensity);
            SetRendererIntensity(item.baseRing, intensity * 1.12f);
        }
    }

    void SetRendererIntensity(Renderer renderer, float intensity)
    {
        if (!renderer)
            return;
        if (block == null)
            block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Color color = outlineColor * intensity;
        color.a = 1f;
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color);
        renderer.SetPropertyBlock(block);
    }
}
