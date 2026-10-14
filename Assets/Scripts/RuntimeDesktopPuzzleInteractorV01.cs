using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Complete mouse fallback for personally playing the VR assembly in the
/// Unity Game view. The player still chooses, grabs, moves and releases every
/// complete module. Near-socket orientation assistance mirrors the authored
/// VR magnet rather than force-completing a Part.
/// </summary>
public sealed class RuntimeDesktopPuzzleInteractorV01 : MonoBehaviour
{
    const int PrimaryHolder = 303;
    const int SupportingHolder = 304;

    public Camera sceneCamera;
    public float maximumRayDistance = 30f;
    public float nearSocketScreenRadius = 150f;
    public float nearSocketMagnetStrength = 0.82f;
    public bool showGameViewInstructions = true;

    RuntimePuzzlePiece held;
    float heldDepth;
    Vector3 worldGrabOffset;
    bool supportingHeavyPart;

    public bool IsHolding => held;
    public RuntimePuzzlePiece HeldPiece => held;

    void Awake()
    {
        if (!sceneCamera)
            sceneCamera = Camera.main;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!PersonallyPuzzledExperienceModeState.IsPlayerManual)
        {
            ReleaseHeld();
            return;
        }
        if (!sceneCamera)
            sceneCamera = Camera.main;
        Mouse mouse = Mouse.current;
        if (!sceneCamera || mouse == null)
            return;

        if (mouse.leftButton.wasPressedThisFrame)
            TryBegin(mouse.position.ReadValue());
        if (held && mouse.leftButton.isPressed)
            MoveHeld(mouse.position.ReadValue(), mouse.scroll.ReadValue().y);
        if (held && mouse.leftButton.wasReleasedThisFrame)
            ReleaseHeld();
        if (held && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            ReleaseHeld();
#endif
    }

    void TryBegin(Vector2 screenPosition)
    {
        Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maximumRayDistance,
            ~0,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        RuntimePuzzlePiece directPiece = null;
        foreach (RaycastHit hit in hits)
        {
            RuntimePuzzlePiece piece = hit.collider
                ? hit.collider.GetComponentInParent<RuntimePuzzlePiece>()
                : null;
            if (!piece || !piece.CanExternalGrab(PrimaryHolder))
                continue;
            directPiece = piece;
            break;
        }

        // Complete miniature modules often have deep, irregular silhouettes.
        // Their conservative BoxColliders can overlap in screen space even when
        // the visible meshes do not.  Resolve the player's intended miniature
        // from its projected visible bounds first; retain the physical ray as
        // the fallback.  This makes clicking a small visible turret/window/roof
        // deterministic without enlarging the geometry or adding visible pads.
        RuntimePuzzlePiece intended = FindVisibleScreenIntent(screenPosition);
        BeginPiece(intended ? intended : directPiece, ray);
    }

    RuntimePuzzlePiece FindVisibleScreenIntent(Vector2 screenPosition)
    {
        RuntimePuzzlePiece best = null;
        float bestScore = float.PositiveInfinity;
        foreach (RuntimePuzzlePiece piece in FindObjectsByType<RuntimePuzzlePiece>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (!piece || !piece.CanExternalGrab(PrimaryHolder))
                continue;
            Renderer[] renderers = piece.solidRenderers != null
                ? piece.solidRenderers.Where(value => value && value.enabled).ToArray()
                : piece.GetComponentsInChildren<Renderer>(false)
                    .Where(value => value && value.enabled).ToArray();
            if (renderers.Length == 0)
                continue;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            if (!TryProjectBounds(bounds, out Rect rect, out float depth))
                continue;
            float padding = Mathf.Clamp(
                Mathf.Min(rect.width, rect.height) * 0.12f,
                7f,
                24f);
            rect.xMin -= padding;
            rect.xMax += padding;
            rect.yMin -= padding;
            rect.yMax += padding;
            if (!rect.Contains(screenPosition))
                continue;
            Vector2 radii = new Vector2(
                Mathf.Max(12f, rect.width * 0.5f),
                Mathf.Max(12f, rect.height * 0.5f));
            Vector2 delta = screenPosition - rect.center;
            float normalizedCenterDistance =
                delta.x * delta.x / (radii.x * radii.x) +
                delta.y * delta.y / (radii.y * radii.y);
            // Prefer the complete module whose visible centre was clicked;
            // depth only breaks near-identical projected candidates.
            float score = normalizedCenterDistance + depth * 0.0001f;
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = piece;
        }
        return best;
    }

    bool TryProjectBounds(Bounds bounds, out Rect rect, out float depth)
    {
        Vector2 min = Vector2.one * float.PositiveInfinity;
        Vector2 max = Vector2.one * float.NegativeInfinity;
        depth = float.PositiveInfinity;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 screen = sceneCamera.WorldToScreenPoint(new Vector3(
                x == 0 ? bounds.min.x : bounds.max.x,
                y == 0 ? bounds.min.y : bounds.max.y,
                z == 0 ? bounds.min.z : bounds.max.z));
            if (screen.z <= 0f)
            {
                rect = default;
                return false;
            }
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
            depth = Mathf.Min(depth, screen.z);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 1f && rect.height > 1f;
    }

    void BeginPiece(RuntimePuzzlePiece piece, Ray ray)
    {
        if (!piece || !piece.CanExternalGrab(PrimaryHolder))
            return;
        held = piece;
        heldDepth = Mathf.Max(
            sceneCamera.nearClipPlane + 0.15f,
            Vector3.Dot(
                held.transform.position - ray.origin,
                ray.direction));
        Vector3 initial = ray.GetPoint(heldDepth);
        worldGrabOffset = held.transform.position - initial;
        Pose pose = new Pose(
            held.transform.position,
            held.transform.rotation);
        held.BeginExternalGrab(PrimaryHolder, pose);
        supportingHeavyPart = held.requiresTwoHands;
        if (supportingHeavyPart && held.CanExternalGrab(SupportingHolder))
            held.BeginExternalGrab(SupportingHolder, pose);
    }

    void MoveHeld(Vector2 screenPosition, float scroll)
    {
        if (!held)
            return;
        heldDepth = Mathf.Clamp(
            heldDepth + scroll * 0.00045f,
            0.25f,
            maximumRayDistance);
        Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
        Vector3 position = ray.GetPoint(heldDepth) + worldGrabOffset;
        Quaternion rotation = held.transform.rotation;

        if (held.targetSocket)
        {
            Vector3 targetScreen3 = sceneCamera.WorldToScreenPoint(
                held.targetSocket.position);
            if (targetScreen3.z > 0f)
            {
                float screenDistance = Vector2.Distance(
                    screenPosition,
                    new Vector2(targetScreen3.x, targetScreen3.y));
                float proximity = 1f - Mathf.Clamp01(
                    screenDistance /
                    Mathf.Max(24f, nearSocketScreenRadius));
                proximity = Mathf.SmoothStep(0f, 1f, proximity);
                float targetDepth = Vector3.Dot(
                    held.targetSocket.position - ray.origin,
                    ray.direction);
                float depth = Mathf.Lerp(
                    heldDepth,
                    targetDepth,
                    proximity * nearSocketMagnetStrength);
                position = Vector3.Lerp(
                    position,
                    held.targetSocket.position,
                    proximity * nearSocketMagnetStrength);
                position = Vector3.Lerp(
                    position,
                    ray.GetPoint(Mathf.Max(0.1f, depth)),
                    (1f - proximity) * 0.35f);
                rotation = Quaternion.Slerp(
                    rotation,
                    held.targetSocket.rotation,
                    proximity * 0.92f);
            }
        }

        if (Keyboard.current != null)
        {
            float yaw = 0f;
            if (Keyboard.current.zKey.isPressed) yaw -= 1f;
            if (Keyboard.current.xKey.isPressed) yaw += 1f;
            if (Mathf.Abs(yaw) > 0.01f)
                rotation = Quaternion.AngleAxis(
                    yaw * 72f * Time.unscaledDeltaTime,
                    Vector3.up) * rotation;
        }

        Pose pose = new Pose(position, rotation);
        held.MoveExternalGrab(PrimaryHolder, pose);
        if (supportingHeavyPart)
            held.MoveExternalGrab(SupportingHolder, pose);
    }

    void ReleaseHeld()
    {
        if (!held)
            return;
        RuntimePuzzlePiece releasing = held;
        held = null;
        releasing.EndExternalGrab(PrimaryHolder);
        if (supportingHeavyPart)
            releasing.EndExternalGrab(SupportingHolder);
        supportingHeavyPart = false;
    }

    void OnDisable()
    {
        ReleaseHeld();
    }

#if UNITY_EDITOR
    public bool TryBeginForAutomation(Vector2 screenPosition)
    {
        TryBegin(screenPosition);
        return held;
    }

    public void ReleaseForAutomation()
    {
        ReleaseHeld();
    }
#endif

    void OnGUI()
    {
#if UNITY_EDITOR
        if (!showGameViewInstructions ||
            !PersonallyPuzzledExperienceModeState.IsPlayerManual)
            return;
        const float width = 390f;
        GUI.Box(
            new Rect(18f, 18f, width, 82f),
            "玩家手动模式 · MANUAL PLAYER\n" +
            "鼠标左键拖放零件 · 滚轮调深度 · Z/X 微调旋转\n" +
            "←/→ 或 A/D 切换展品 · Enter/Space 确认 · H 提示 · R 重置");
#endif
    }
}
