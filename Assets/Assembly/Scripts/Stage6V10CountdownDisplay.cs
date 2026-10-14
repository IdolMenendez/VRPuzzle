using UnityEngine;

/// <summary>
/// Diegetic two-second readout placed at the exact hinted socket. It follows
/// the Stage 6 physical-lamp controller and never acts as a virtual button.
/// </summary>
public sealed class Stage6V10CountdownDisplay : MonoBehaviour
{
    public Stage6LampGhostHintV06Controller controller;
    public Renderer focusGhost;
    public LineRenderer outerRing;
    public LineRenderer innerRing;
    public TextMesh secondsLabel;
    public TextMesh socketLabel;
    public float editorOverrideSeconds = -1f;

    public void RefreshNow()
    {
        bool visible = editorOverrideSeconds >= 0f ||
            controller && controller.GhostVisible;
        if (outerRing) outerRing.enabled = visible;
        if (innerRing) innerRing.enabled = visible;
        if (secondsLabel) secondsLabel.gameObject.SetActive(visible);
        if (socketLabel) socketLabel.gameObject.SetActive(visible);
        if (!visible || !focusGhost) return;

        Camera camera = Camera.main;
        if (camera)
        {
            transform.position = focusGhost.bounds.center +
                camera.transform.up * 0.040f -
                camera.transform.forward * 0.012f;
            transform.rotation = camera.transform.rotation;
        }
        float seconds = editorOverrideSeconds >= 0f
            ? editorOverrideSeconds
            : Mathf.Clamp(controller.HintRemaining, 0f, controller.hintDuration);
        if (secondsLabel) secondsLabel.text = seconds.ToString("0.0") + "s";
        if (socketLabel) socketLabel.text = "NEXT SOCKET";
    }

    void Update() => RefreshNow();
}
