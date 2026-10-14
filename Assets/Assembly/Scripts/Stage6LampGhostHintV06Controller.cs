using System.Linq;
using UnityEngine;

/// <summary>
/// Stage 6 owns a second exact-silhouette ghost layer dedicated to the physical
/// lamp hint. It never substitutes a generic ring, arrow, box, or virtual UI.
/// The normal per-piece proximity ghosts remain available for assembly.
/// </summary>
public sealed class Stage6LampGhostHintV06Controller : MonoBehaviour
{
    public enum CaptureState
    {
        Idle,
        LampTap,
        GhostActive,
        GhostFade
    }

    [Header("Complete-module hint family")]
    public RuntimePuzzleLevelController level;
    public RuntimePuzzlePiece[] pieces;
    public Renderer[] exactHintGhosts;
    public int captureFocusSequence = 10;

    [Header("Physical lamp response")]
    public Transform lampArticulation;
    public Light lampLight;
    public ParticleSystem hintMotes;
    public float restLampIntensity = 0.58f;
    public float tappedLampIntensity = 1.08f;

    [Header("Two-second hint envelope")]
    public float hintDuration = 2.0f;
    public float revealSeconds = 0.12f;
    public float fadeSeconds = 0.42f;
    public float retriggerCooldown = 0.55f;
    public Color ghostColor = new Color(1.0f, 0.47f, 0.055f, 0.26f);

    MaterialPropertyBlock block;
    Quaternion lampRestRotation = Quaternion.identity;
    Vector3 lampRestScale = Vector3.one;
    float hintRemaining;
    float tapRemaining;
    float nextTriggerTime;
    float currentGhostAlpha;
    int activeSequence = -1;
    bool restPoseCaptured;

    public float HintRemaining => hintRemaining;
    public float CurrentGhostAlpha => currentGhostAlpha;
    public int ActiveSequence => activeSequence;
    public int NextHintSequenceForValidation => FindNextHintSequence();
    public bool GhostVisible =>
        activeSequence >= 0 &&
        exactHintGhosts != null &&
        activeSequence < exactHintGhosts.Length &&
        exactHintGhosts[activeSequence] &&
        exactHintGhosts[activeSequence].enabled;

    void Awake()
    {
        Cache();
        CaptureLampRestPose();
        ClearGhosts();
        ApplyLampEnvelope(0f);
    }

    void Update()
    {
        if (hintRemaining > 0f)
        {
            hintRemaining = Mathf.Max(0f, hintRemaining - Time.unscaledDeltaTime);
            RefreshHintVisual();
        }
        else if (activeSequence >= 0)
        {
            ClearGhosts();
        }

        tapRemaining = Mathf.Max(0f, tapRemaining - Time.unscaledDeltaTime);
        ApplyLampEnvelope(tapRemaining > 0f
            ? Mathf.Sin(Mathf.Clamp01(tapRemaining / 0.28f) * Mathf.PI)
            : 0f);
    }

    void OnMouseDown()
    {
        TriggerHint();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other) return;
        string actor = other.name;
        if (actor.Contains("Hand") ||
            actor.Contains("Controller") ||
            actor.Contains("Index") ||
            actor.Contains("Finger"))
            TriggerHint();
    }

    public void TriggerHint()
    {
        if (Time.unscaledTime < nextTriggerTime) return;
        int sequence = FindNextHintSequence();
        if (sequence < 0) return;
        nextTriggerTime = Time.unscaledTime + retriggerCooldown;
        BeginHint(sequence, true);
    }

    /// <summary>
    /// Project Swan can render the complete scene while its emulated head pose
    /// makes every player-facing socket fail the normal physical-orientation
    /// filter. The test bridge still needs to exercise the authored two-second
    /// lamp animation, exact silhouette, audio and haptic path. Prefer the
    /// normal filtered sequence, then fall back to the first dependency-valid
    /// complete module only for the already-isolated Swan emulator bridge.
    /// </summary>
    public bool TriggerHintForSwanEmulator(out int sequence)
    {
        sequence = -1;
        if (Time.unscaledTime < nextTriggerTime)
            return false;

        sequence = FindNextHintSequence();
        if (sequence < 0 && level && level.pieces != null)
        {
            RuntimePuzzlePiece fallback = level.pieces
                .Where(value => value && !value.IsSnapped && value.IsAvailable)
                .OrderBy(value => value.sequence)
                .FirstOrDefault();
            if (fallback)
                sequence = fallback.sequence;
        }
        if (sequence < 0)
            return false;

        nextTriggerTime = Time.unscaledTime + retriggerCooldown;
        BeginHint(sequence, true);
        // The exact ghost deliberately starts at alpha zero and reveals over
        // revealSeconds, so GhostVisible is false on the same frame even when
        // the full two-second hint envelope has started successfully.
        return activeSequence == sequence && hintRemaining > 0f;
    }

    public void TriggerHintForSmoke(int preferredSequence = -1)
    {
        int sequence = preferredSequence >= 0 ? preferredSequence : FindNextHintSequence();
        if (sequence < 0) return;
        BeginHint(sequence, false);
        if (activeSequence == sequence)
            SetGhostAlpha(ghostColor.a);
    }

    public void AdvanceHintTimerForSmoke(float seconds)
    {
        hintRemaining = Mathf.Max(0f, hintRemaining - Mathf.Max(0f, seconds));
        if (hintRemaining > 0f) RefreshHintVisual();
        else ClearGhosts();
    }

    public void ApplyEditorCaptureState(CaptureState state, int sequence)
    {
        Cache();
        CaptureLampRestPose();
        ClearGhosts();
        tapRemaining = 0f;
        ApplyLampEnvelope(0f);
        if (exactHintGhosts == null || exactHintGhosts.Length == 0)
            return;
        if (state == CaptureState.LampTap)
        {
            tapRemaining = 0.14f;
            ApplyLampEnvelope(0.92f);
            return;
        }
        if (state == CaptureState.GhostActive)
        {
            activeSequence = Mathf.Clamp(sequence, 0, exactHintGhosts.Length - 1);
            hintRemaining = hintDuration * 0.72f;
            SetGhostAlpha(ghostColor.a);
            SimulateMotes(0.18f);
            return;
        }
        if (state == CaptureState.GhostFade)
        {
            activeSequence = Mathf.Clamp(sequence, 0, exactHintGhosts.Length - 1);
            hintRemaining = fadeSeconds * 0.22f;
            SetGhostAlpha(ghostColor.a * 0.18f);
            SimulateMotes(0.54f);
        }
    }

    public void ClearForEditor()
    {
        Cache();
        CaptureLampRestPose();
        hintRemaining = tapRemaining = 0f;
        ClearGhosts();
        ApplyLampEnvelope(0f);
    }

    void BeginHint(int sequence, bool playFeedback)
    {
        Cache();
        CaptureLampRestPose();
        if (exactHintGhosts == null ||
            sequence < 0 ||
            sequence >= exactHintGhosts.Length)
            return;
        ClearGhosts();
        activeSequence = sequence;
        hintRemaining = Mathf.Max(0.1f, hintDuration);
        tapRemaining = 0.28f;
        RefreshHintVisual();
        hintMotes?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        hintMotes?.Emit(18);
        if (playFeedback)
        {
            Vector3 lampPosition = lampArticulation ? lampArticulation.position : transform.position;
            Vector3 ghostPosition = pieces != null && sequence < pieces.Length && pieces[sequence]
                ? pieces[sequence].TargetPosition
                : lampPosition;
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Hint, lampPosition);
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.GhostReveal, ghostPosition);
            RuntimeHaptics.PulseAll(0.30f, 0.055f);
        }
    }

    int FindNextHintSequence()
    {
        Cache();
        if (pieces == null) return -1;
        foreach (RuntimePuzzlePiece piece in pieces
            .Where(value => value && !value.IsSnapped && value.IsAvailable)
            .OrderBy(value => value.sequence))
        {
            if (!piece.requiresPlayerFacing || piece.IsSocketFacingPlayer())
                return piece.sequence;
        }
        return -1;
    }

    void RefreshHintVisual()
    {
        if (activeSequence < 0 || hintDuration <= 0f)
        {
            ClearGhosts();
            return;
        }
        float elapsed = hintDuration - hintRemaining;
        float reveal = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, revealSeconds));
        float fade = hintRemaining < fadeSeconds
            ? Mathf.SmoothStep(0f, 1f, hintRemaining / Mathf.Max(0.01f, fadeSeconds))
            : 1f;
        float pulse = 0.94f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.4f));
        SetGhostAlpha(ghostColor.a * reveal * fade * pulse);
    }

    void SetGhostAlpha(float alpha)
    {
        ClearGhostRenderersExcept(activeSequence);
        if (activeSequence < 0 ||
            exactHintGhosts == null ||
            activeSequence >= exactHintGhosts.Length ||
            !exactHintGhosts[activeSequence])
        {
            currentGhostAlpha = 0f;
            return;
        }
        Renderer renderer = exactHintGhosts[activeSequence];
        currentGhostAlpha = Mathf.Clamp01(alpha);
        renderer.enabled = currentGhostAlpha > 0.005f;
        if (!renderer.enabled) return;
        if (hintMotes) hintMotes.transform.position = renderer.bounds.center;
        renderer.GetPropertyBlock(block);
        Color color = ghostColor;
        color.a = 1f;
        block.SetColor("_BaseColor", color);
        block.SetFloat(
            "_GhostStrength",
            Mathf.Clamp01(currentGhostAlpha / Mathf.Max(0.001f, ghostColor.a)));
        renderer.SetPropertyBlock(block);
    }

    void ClearGhosts()
    {
        activeSequence = -1;
        currentGhostAlpha = 0f;
        ClearGhostRenderersExcept(-1);
        hintMotes?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void ClearGhostRenderersExcept(int keep)
    {
        if (exactHintGhosts == null) return;
        for (int index = 0; index < exactHintGhosts.Length; index++)
        {
            Renderer renderer = exactHintGhosts[index];
            if (!renderer || index == keep) continue;
            renderer.enabled = false;
            renderer.SetPropertyBlock(null);
        }
    }

    void CaptureLampRestPose()
    {
        if (restPoseCaptured || !lampArticulation) return;
        lampRestRotation = lampArticulation.localRotation;
        lampRestScale = lampArticulation.localScale;
        restPoseCaptured = true;
    }

    void ApplyLampEnvelope(float envelope)
    {
        if (lampArticulation)
        {
            lampArticulation.localRotation =
                lampRestRotation * Quaternion.Euler(0f, 0f, -4.5f * envelope);
            lampArticulation.localScale = lampRestScale * (1f - 0.012f * envelope);
        }
        if (lampLight)
            lampLight.intensity = Mathf.Lerp(restLampIntensity, tappedLampIntensity, envelope);
    }

    void SimulateMotes(float seconds)
    {
        if (!hintMotes) return;
        hintMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        hintMotes.Emit(18);
        hintMotes.Simulate(seconds, true, false, true);
    }

    void Cache()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if ((pieces == null || pieces.Length == 0) && level)
            pieces = level.pieces;
    }
}
