using UnityEngine;

/// <summary>
/// Persistent, particle-free V66 selection feedback. A stable gaze locks a
/// physical brass selector around that exhibit until another stable target is
/// acquired. Only the narrow amber groove pulses; the mechanical brass stays
/// opaque and materially stable on PICO.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class SelectionMechanicalSelectorV66 : MonoBehaviour
{
    [System.Serializable]
    public sealed class ExhibitFeedback
    {
        public int exhibitIndex;
        public Renderer[] mechanicalRenderers;
        public Renderer amberGroove;
    }

    public RuntimeIntentRayV46 ray;
    public SelectionCarouselController selection;
    public ExhibitFeedback[] feedback;
    [Min(0.2f)] public float pulseHz = 0.72f;
    [Range(0f, 0.3f)] public float pulseAmount = 0.10f;
    public Color grooveColor = new Color(0.78f, 0.32f, 0.055f, 1f);
    [Tooltip("The brighter, quicker mechanical lock state used after the first\n+             deliberate selection pinch.  A second separate pinch commits.")]
    public Color confirmGrooveColor = new Color(1f, 0.70f, 0.13f, 1f);

    int selectedIndex = -1;
    bool awaitingSecondConfirm;
    MaterialPropertyBlock block;

    public int SelectedIndex => selectedIndex;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        ApplySelection(-1);
    }

    void Update()
    {
        if (!ray) return;
        SelectionExhibitTargetV09 target = ray.StableSelectionTargetV46;
        if (target && target.exhibitIndex != selectedIndex)
        {
            ApplySelection(target.exhibitIndex);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Hint, target.transform.position);
        }
        float speed = pulseHz * (awaitingSecondConfirm ? 2.0f : 1f);
        float pulse = 1f + pulseAmount * (awaitingSecondConfirm ? 1.65f : 1f) *
            (0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * speed * Mathf.PI * 2f));
        ApplyGroovePulse(pulse);
    }

    public void SetPreviewIndexV66(int exhibitIndex)
    {
        if (block == null) block = new MaterialPropertyBlock();
        awaitingSecondConfirm = false;
        ApplySelection(exhibitIndex);
        ApplyGroovePulse(1.08f);
    }

    /// <summary>
    /// Keeps the existing brass selector as the confirmation affordance.  It
    /// deliberately avoids a floating UI: after the first pinch its amber
    /// groove becomes a fast, bright gold lock until the second pinch arrives.
    /// </summary>
    public void SetSecondConfirmArmedV75(int exhibitIndex, bool armed)
    {
        if (selectedIndex != exhibitIndex) return;
        awaitingSecondConfirm = armed;
        ApplyGroovePulse(armed ? 1.24f : 1f);
    }

    void ApplySelection(int exhibitIndex)
    {
        selectedIndex = exhibitIndex;
        if (feedback == null) return;
        foreach (ExhibitFeedback item in feedback)
        {
            if (item == null) continue;
            bool active = item.exhibitIndex == selectedIndex;
            if (item.mechanicalRenderers != null)
                foreach (Renderer renderer in item.mechanicalRenderers)
                    if (renderer) renderer.enabled = active;
            if (item.amberGroove) item.amberGroove.enabled = active;
        }
    }

    void ApplyGroovePulse(float intensity)
    {
        if (feedback == null || selectedIndex < 0) return;
        foreach (ExhibitFeedback item in feedback)
        {
            if (item == null || item.exhibitIndex != selectedIndex ||
                !item.amberGroove) continue;
            item.amberGroove.GetPropertyBlock(block);
            Color color = (awaitingSecondConfirm ? confirmGrooveColor : grooveColor) * intensity;
            color.a = 1f;
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color);
            item.amberGroove.SetPropertyBlock(block);
        }
    }
}
