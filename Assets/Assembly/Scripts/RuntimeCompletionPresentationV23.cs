using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Treats the model, dome, pedestal, plaque and celebration accents as one
/// spatial showcase.  The whole group is fitted and lifted together, so side
/// movement cannot reveal the front-view separation that existed in V22.
/// </summary>
[DefaultExecutionOrder(220)]
[DisallowMultipleComponent]
public sealed class RuntimeCompletionPresentationV23 : MonoBehaviour
{
    public Camera sceneCamera;
    public Transform showcaseRoot;
    public Transform displayShellRoot;
    public Transform heroRoot;
    public RuntimeLevelHeroContinuityV04 continuity;
    public Transform plaqueRoot;
    public TextMesh celebrationLabel;
    public Transform[] orbitRings;
    public Transform[] achievementStars;
    public Light celebrationLight;
    public ParticleSystem mainBurst;
    public ParticleSystem crownBurst;
    [Range(0.45f, 0.78f)] public float targetViewportHeight = 0.64f;
    [Range(0.38f, 0.76f)] public float targetViewportWidth = 0.58f;
    public Vector2 targetViewportCenter = new Vector2(0.5f, 0.52f);
    public Vector3 authoredRootPosition;
    public Vector3 authoredRootScale = Vector3.one;

    Vector3 settledPosition;
    Vector3 settledScale;
    float startedAt;
    bool fitted;
    bool firstStar;
    bool secondStar;
    bool thirdStar;

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        ResetShowcasePose();
        FitNow();
        startedAt = Time.unscaledTime;
        if (mainBurst)
            mainBurst.Play(true);
        RuntimeAudioRouter.PlayCue(
            RuntimeAudioCue.Complete,
            heroRoot ? heroRoot.position : transform.position);
        RuntimeHaptics.PulseAll(0.58f, 0.13f);
        if (achievementStars != null)
            foreach (Transform star in achievementStars)
                if (star)
                    star.localScale = Vector3.zero;
    }

    void Update()
    {
        if (!fitted || !showcaseRoot)
            return;
        float elapsed = Time.unscaledTime - startedAt;
        float lift = EaseOutBack(Mathf.Clamp01(elapsed / 0.90f));
        showcaseRoot.position = Vector3.LerpUnclamped(
            settledPosition + Vector3.down * 0.055f,
            settledPosition,
            lift);
        showcaseRoot.localScale = Vector3.LerpUnclamped(
            settledScale * 0.90f,
            settledScale,
            lift);

        if (orbitRings != null)
        {
            for (int i = 0; i < orbitRings.Length; i++)
            {
                Transform ring = orbitRings[i];
                if (!ring)
                    continue;
                float direction = i % 2 == 0 ? 1f : -1f;
                ring.Rotate(
                    i == 0 ? Vector3.up : Vector3.right,
                    direction * (10f + i * 3f) * Time.unscaledDeltaTime,
                    Space.Self);
                float pulse = 0.96f + 0.045f *
                    Mathf.Sin(elapsed * 3.1f + i * 1.7f);
                ring.localScale = Vector3.one * pulse;
            }
        }

        TriggerStar(0, elapsed >= 0.38f, ref firstStar);
        TriggerStar(1, elapsed >= 0.68f, ref secondStar);
        TriggerStar(2, elapsed >= 0.98f, ref thirdStar);
        if (achievementStars != null)
        {
            for (int i = 0; i < achievementStars.Length; i++)
            {
                Transform star = achievementStars[i];
                if (!star)
                    continue;
                float localElapsed = elapsed - (0.38f + i * 0.30f);
                if (localElapsed < 0f)
                    continue;
                float reveal = Mathf.Clamp01(localElapsed / 0.30f);
                float bounce = EaseOutBack(reveal);
                star.localScale = Vector3.one * bounce;
            }
        }

        if (celebrationLight)
        {
            float bloom = Mathf.Exp(
                -Mathf.Max(0f, elapsed - 0.16f) * 1.05f);
            celebrationLight.intensity =
                0.30f + 1.05f * bloom;
        }
        if (celebrationLabel)
        {
            celebrationLabel.text = elapsed < 1.35f
                ? "杰作完成\nMASTERPIECE COMPLETE"
                : elapsed < 2.70f
                    ? "可自由旋转欣赏模型\nROTATE TO INSPECT"
                    : "按确认 / 扳机进入下一关\nCONFIRM TO CONTINUE";
        }
    }

    void TriggerStar(int index, bool ready, ref bool triggered)
    {
        if (!ready || triggered)
            return;
        triggered = true;
        RuntimeAudioRouter.PlayCue(
            RuntimeAudioCue.StarLight,
            heroRoot ? heroRoot.position : transform.position);
        RuntimeHaptics.PulseAll(0.28f + index * 0.05f, 0.055f);
        if (index == 1 && crownBurst)
            crownBurst.Play(true);
    }

    public bool FitNow()
    {
        if (!sceneCamera)
            sceneCamera = Camera.main;
        if (!sceneCamera || !showcaseRoot || !displayShellRoot || !heroRoot)
            return false;
        Bounds shell = BoundsOf(displayShellRoot);
        Bounds hero = BoundsOf(heroRoot);
        if (shell.size.sqrMagnitude <= 0.000001f ||
            hero.size.sqrMagnitude <= 0.000001f)
            return false;
        float factor = Mathf.Min(
            shell.size.x * 0.66f / Mathf.Max(0.001f, hero.size.x),
            Mathf.Min(
                shell.size.y * 0.75f / Mathf.Max(0.001f, hero.size.y),
                shell.size.z * 0.72f / Mathf.Max(0.001f, hero.size.z)));
        heroRoot.localScale *= Mathf.Clamp(factor, 0.42f, 1.55f);
        hero = BoundsOf(heroRoot);
        Vector3 targetCenter = new Vector3(
            shell.center.x,
            shell.min.y + shell.size.y * 0.14f + hero.extents.y,
            shell.center.z);
        heroRoot.position += targetCenter - hero.center;
        settledPosition = showcaseRoot.position;
        settledScale = showcaseRoot.localScale;
        fitted = CompletionFitsViewport(out _);
        return fitted;
    }

    public bool ApplyLevelAndFitForEditor(int levelIndex)
    {
        ResetShowcasePose();
        if (!continuity || !continuity.ApplyLevel(levelIndex))
            return false;
        return FitNow();
    }

    public void ResetShowcasePose()
    {
        if (!showcaseRoot)
            return;
        showcaseRoot.position = authoredRootPosition;
        showcaseRoot.rotation = Quaternion.identity;
        showcaseRoot.localScale =
            authoredRootScale.sqrMagnitude > 0.001f
                ? authoredRootScale
                : Vector3.one;
    }

    public bool CompletionFitsViewport(out Rect viewport)
    {
        viewport = ProjectedRect(ShowcaseBounds());
        return viewport.xMin >= 0.08f &&
               viewport.xMax <= 0.92f &&
               viewport.yMin >= 0.08f &&
               viewport.yMax <= 0.92f;
    }

    public Bounds ShowcaseBounds()
    {
        Renderer[] renderers = showcaseRoot
            .GetComponentsInChildren<Renderer>(true)
            .Where(value => value && value.enabled &&
                !(value is ParticleSystemRenderer) &&
                !value.GetComponent<TextMesh>())
            .ToArray();
        if (renderers.Length == 0)
            return default;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static Bounds BoundsOf(Transform root)
    {
        Renderer[] renderers = root
            .GetComponentsInChildren<Renderer>(true)
            .Where(value => value && value.enabled &&
                !(value is ParticleSystemRenderer) &&
                !value.GetComponent<TextMesh>())
            .ToArray();
        if (renderers.Length == 0)
            return default;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    Vector2 ProjectedSize(Bounds bounds)
    {
        Rect rect = ProjectedRect(bounds);
        return new Vector2(rect.width, rect.height);
    }

    Rect ProjectedRect(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        float xMin = float.PositiveInfinity;
        float yMin = float.PositiveInfinity;
        float xMax = float.NegativeInfinity;
        float yMax = float.NegativeInfinity;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 point = sceneCamera.WorldToViewportPoint(new Vector3(
                x == 0 ? min.x : max.x,
                y == 0 ? min.y : max.y,
                z == 0 ? min.z : max.z));
            xMin = Mathf.Min(xMin, point.x);
            yMin = Mathf.Min(yMin, point.y);
            xMax = Mathf.Max(xMax, point.x);
            yMax = Mathf.Max(yMax, point.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    static float EaseOutBack(float value)
    {
        const float overshoot = 1.15f;
        float t = value - 1f;
        return 1f + t * t * ((overshoot + 1f) * t + overshoot);
    }
}
