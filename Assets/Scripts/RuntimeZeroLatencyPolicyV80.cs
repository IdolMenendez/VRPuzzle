using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_PICO_OPENXR_SDK
using ByteDance.PICO.OpenXR;
#endif

[DefaultExecutionOrder(-10010)]
[DisallowMultipleComponent]
public sealed class RuntimeZeroLatencyPolicyV80 : MonoBehaviour
{
    public RenderPipelineAsset optimizedPipelineV80;
    [Min(60)] public int targetFrameRate = 72;
    public bool useLowFixedFoveation = true;
    [Min(0.5f)] public float sampleWindowSeconds = 1f;
    public bool logFrameWindows;

    int frames;
    float elapsed;
    float worst;

    public float LastFpsV80 { get; private set; }
    public float LastAverageFrameMsV80 { get; private set; }
    public float LastWorstFrameMsV80 { get; private set; }
    public int CompletedWindowsV80 { get; private set; }

    void Awake()
    {
        ApplyCorePolicy();
    }

    IEnumerator Start()
    {
        yield return null;
        ApplyFoveation();
        yield return new WaitForSecondsRealtime(1f);
        ApplyFoveation();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
            return;
        ApplyCorePolicy();
        ApplyFoveation();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        frames++;
        elapsed += dt;
        worst = Mathf.Max(worst, dt);
        if (elapsed < Mathf.Max(0.5f, sampleWindowSeconds))
            return;
        LastFpsV80 = frames / Mathf.Max(0.0001f, elapsed);
        LastAverageFrameMsV80 =
            elapsed / Mathf.Max(1, frames) * 1000f;
        LastWorstFrameMsV80 = worst * 1000f;
        CompletedWindowsV80++;
        if (logFrameWindows)
        {
            string stage = PersistentPuzzleBootstrap.Instance
                ? PersistentPuzzleBootstrap.Instance.CurrentStage.ToString()
                : "none";
            Debug.Log(
                "[PPVR V80 Frame] stage=" + stage +
                " fps=" + LastFpsV80.ToString("F1") +
                " avgMs=" + LastAverageFrameMsV80.ToString("F2") +
                " worstMs=" + LastWorstFrameMsV80.ToString("F2"));
        }
        frames = 0;
        elapsed = 0f;
        worst = 0f;
    }

    void ApplyCorePolicy()
    {
        if (optimizedPipelineV80)
        {
            GraphicsSettings.defaultRenderPipeline =
                optimizedPipelineV80;
            QualitySettings.renderPipeline =
                optimizedPipelineV80;
        }
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate =
            Mathf.Max(60, targetFrameRate);
    }

    void ApplyFoveation()
    {
#if ENABLE_PICO_OPENXR_SDK && UNITY_ANDROID && !UNITY_EDITOR
        if (useLowFixedFoveation &&
            FoveationFeature.isExtensionEnable)
        {
            FoveationFeature.useDynamicFoveatedRendering = false;
            FoveationFeature.foveatedRenderingLevel =
                FoveationFeature.FoveatedRenderingLevel.Low;
        }
#endif
    }
}
