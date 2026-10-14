using UnityEngine;

/// <summary>
/// V61 #3/#8/#9: a fixed retro control console on the left-front of the assembly
/// bench with three physical buttons — 重置 RESET / Home / 提示 HINT — operated
/// by either index fingertip (never a pinch). Each
/// press eases the button cap down, plays RuntimeAudioCue.Touch and a haptic
/// pulse, and runs its action:
///   RESET → level.ResetLevel() (all pieces home)
///   HOME  → bootstrap.EnterSelection() (back to level select)
///   HINT  → enable guidance and immediately pulse the next exact hint
/// The bell is removed in V61; this console owns the hint action. The hand
/// interactor feeds either tracked index tip so a temporarily missing left hand
/// does not make the controls inert.
/// </summary>
[DefaultExecutionOrder(-70)]
[DisallowMultipleComponent]
public sealed class RuntimeAssemblyConsoleV61 : MonoBehaviour
{
    public enum ButtonId { Reset, Home, Hint }

    [System.Serializable]
    public sealed class ConsoleButton
    {
        public ButtonId id;
        public Transform cap;          // the pressable cap (eased down on press)
        public Collider touchCollider; // fingertip hit target
        public Vector3 restLocalPos;
        [HideInInspector] public float pressAnim;
        [HideInInspector] public float nextAllowed;
        [HideInInspector] public bool fingertipLatched;
    }

    public RuntimePuzzleLevelController level;
    public ConsoleButton[] buttons;
    [Min(0.02f)] public float fingertipRadius = 0.055f;
    [Min(0.2f)] public float debounceSeconds = 0.6f;
    [Min(0.001f)] public float pressDepth = 0.012f;

    void Reset()
    {
        // Capture rest positions if wired in-editor.
        if (buttons == null) return;
        foreach (ConsoleButton b in buttons)
            if (b != null && b.cap)
                b.restLocalPos = b.cap.localPosition;
    }

    void Update()
    {
        if (buttons == null) return;
        foreach (ConsoleButton b in buttons)
        {
            if (b == null || !b.cap) continue;
            b.pressAnim = Mathf.MoveTowards(
                b.pressAnim, 0f, Time.unscaledDeltaTime * 4f);
            b.cap.localPosition =
                b.restLocalPos + new Vector3(0f, -pressDepth * b.pressAnim, 0f);
        }
    }

    // Called by either hand interactor with its index fingertip world
    // position. Returns true if a button fired this frame.
    public bool TryFingertipPressV61(Vector3 indexTip)
    {
        if (buttons == null) return false;
        ConsoleButton nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (ConsoleButton b in buttons)
        {
            if (b == null || !b.touchCollider) continue;
            float dist = Vector3.Distance(
                b.touchCollider.ClosestPoint(indexTip), indexTip);
            if (b.fingertipLatched)
            {
                if (dist > fingertipRadius * 1.6f)
                    b.fingertipLatched = false;
                else
                    continue;
            }
            if (Time.unscaledTime < b.nextAllowed) continue;
            if (dist > fingertipRadius) continue;
            if (dist < nearestDistance)
            {
                nearest = b;
                nearestDistance = dist;
            }
        }
        if (nearest == null) return false;
        nearest.nextAllowed =
            Time.unscaledTime + Mathf.Max(0.2f, debounceSeconds);
        nearest.fingertipLatched = true;
        nearest.pressAnim = 1f;
        Fire(nearest.id);
        RuntimeAudioRouter.PlayCue(
            RuntimeAudioCue.Touch, nearest.touchCollider.bounds.center);
        RuntimeHaptics.PulseAll(0.24f, 0.05f);
        Debug.Log(
            "[PPVR V67 Console] exclusive press=" +
            nearest.id.ToString().ToLower() +
            " cue=touch distance=" + nearestDistance.ToString("F3"));
        return true;
    }

    void Fire(ButtonId id)
    {
        switch (id)
        {
            case ButtonId.Reset:
                if (level) level.ResetLevel();
                break;
            case ButtonId.Home:
                if (PersistentPuzzleBootstrap.Instance)
                    PersistentPuzzleBootstrap.Instance.EnterSelection();
                break;
            case ButtonId.Hint:
                RuntimePuzzlePiece.HintsEnabledV59 = true;
                if (level)
                    level.PulseNextHint();
                break;
        }
    }
}
