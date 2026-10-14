using UnityEngine;

/// <summary>
/// V54 lightweight frame-rate probe. Accumulates unscaled delta time and, once
/// per reporting window, logs the average FPS, the average and worst frame time
/// in the window, and the current stage. It performs no per-frame logging, so it
/// is cheap on device; it exists to locate where and how badly the experience
/// drops frames (the Selection carousel was reported very laggy but the cause is
/// not visible in ordinary logcat). Attach to the persistent bootstrap rig.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class RuntimeFrameRateProbeV54 : MonoBehaviour
{
    [Min(0.25f)] public float reportIntervalSeconds = 1f;
    public bool logEnabled = true;

    int frames;
    float elapsed;
    float worstFrame;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        frames++;
        elapsed += dt;
        if (dt > worstFrame)
            worstFrame = dt;
        if (elapsed < Mathf.Max(0.25f, reportIntervalSeconds))
            return;

        if (logEnabled && frames > 0)
        {
            float avgFrameMs = elapsed / frames * 1000f;
            float fps = frames / elapsed;
            string stage =
                PersistentPuzzleBootstrap.Instance
                    ? PersistentPuzzleBootstrap.Instance.CurrentStage.ToString()
                    : "none";
            Debug.Log(
                "[PPVR V54 FPS] stage=" + stage +
                " fps=" + fps.ToString("F1") +
                " avgFrameMs=" + avgFrameMs.ToString("F1") +
                " worstFrameMs=" + (worstFrame * 1000f).ToString("F1") +
                " frames=" + frames);
        }

        frames = 0;
        elapsed = 0f;
        worstFrame = 0f;
    }
}
