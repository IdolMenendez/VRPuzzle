using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RuntimeAudioCue
{
    Touch,
    CarouselSwipe,
    Grab,
    SocketBreath,
    Snap,
    Miss,
    Hint,
    GhostReveal,
    HeavyLift,
    Complete,
    StarLight,
    Error,
    Archive,
    Transition
}

/// <summary>Original procedural score, ambience and interaction cues.</summary>
public sealed class RuntimeAudioConductor : MonoBehaviour
{
    public AudioSource musicA;
    public AudioSource musicB;
    public AudioSource effects;
    public float crossfadeSeconds = 1.45f;

    [Header("Original authored BGM")]
    public AudioClip splashMusic;
    public AudioClip selectionMusic;
    public AudioClip cottageMusic;
    public AudioClip shipMusic;
    public AudioClip eiffelMusic;
    public AudioClip completionMusic;
    public AudioClip collectionMusic;

    [Header("V14 randomized puzzle light-music suite")]
    [Tooltip("Original loopable light-music pieces used by all playable " +
             "puzzle stages. A new non-repeating selection is made whenever " +
             "the player enters a puzzle.")]
    public AudioClip[] puzzleMusicPool;
    public bool randomizePuzzleMusic = true;

    [Header("Original authored interaction cues")]
    public AudioClip touchCue;
    public AudioClip carouselSwipeCue;
    public AudioClip grabCue;
    public AudioClip socketBreathCue;
    public AudioClip snapCue;
    public AudioClip missCue;
    public AudioClip hintCue;
    public AudioClip ghostRevealCue;
    public AudioClip heavyLiftCue;
    public AudioClip completeCue;
    public AudioClip starLightCue;
    public AudioClip errorCue;
    public AudioClip archiveCue;
    public AudioClip transitionCue;

    readonly Dictionary<RuntimePuzzleStage, AudioClip> music = new Dictionary<RuntimePuzzleStage, AudioClip>();
    readonly Dictionary<RuntimeAudioCue, AudioClip> cues = new Dictionary<RuntimeAudioCue, AudioClip>();
    AudioSource active;
    Coroutine fade;
    float master = 0.85f;
    float musicVolume = 0.72f;
    float effectsVolume = 0.88f;
    int lastPuzzleMusicIndex = -1;

    public AudioClip CurrentMusicClip => active ? active.clip : null;
    public bool HasPuzzleMusicPool =>
        puzzleMusicPool != null &&
        puzzleMusicPool.Count(value => value) >= 2;

    void Awake()
    {
        EnsureSources();
        BuildCues();
    }

    void EnsureSources()
    {
        if (!musicA) musicA = gameObject.AddComponent<AudioSource>();
        if (!musicB) musicB = gameObject.AddComponent<AudioSource>();
        if (!effects) effects = gameObject.AddComponent<AudioSource>();
        musicA.loop = musicB.loop = true;
        musicA.spatialBlend = musicB.spatialBlend = 0f;
        effects.spatialBlend = 0.28f;
        if (!active) active = musicA;
    }

    public void ApplyVolumes(float masterValue, float musicValue, float effectsValue)
    {
        EnsureSources();
        master = Mathf.Clamp01(masterValue);
        musicVolume = Mathf.Clamp01(musicValue);
        effectsVolume = Mathf.Clamp01(effectsValue);
        effects.volume = master * effectsVolume;
        if (fade == null && active) active.volume = master * musicVolume * 0.34f;
    }

    public void CrossfadeTo(RuntimePuzzleStage stage)
    {
        EnsureSources();
        AudioClip clip;
        if (IsPuzzleStage(stage) && HasPuzzleMusicPool)
        {
            clip = SelectPuzzleMusic();
        }
        else if (!music.TryGetValue(stage, out clip))
        {
            clip = AuthoredMusic(stage);
            if (!clip) clip = BuildMusic(stage);
            music[stage] = clip;
        }
        AudioSource incoming = active == musicA ? musicB : musicA;
        incoming.clip = clip;
        incoming.time = 0f;
        incoming.volume = 0f;
        incoming.Play();
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(active, incoming));
        active = incoming;
    }

    AudioClip SelectPuzzleMusic()
    {
        AudioClip[] valid = puzzleMusicPool
            .Where(value => value)
            .Distinct()
            .ToArray();
        if (valid.Length == 0)
            return null;
        if (valid.Length == 1 || !randomizePuzzleMusic)
        {
            lastPuzzleMusicIndex = 0;
            return valid[0];
        }

        int index = Random.Range(0, valid.Length - 1);
        if (index >= lastPuzzleMusicIndex)
            index++;
        index %= valid.Length;
        lastPuzzleMusicIndex = index;
        Debug.Log(
            "[Personally Puzzled Audio V14] Random puzzle BGM: " +
            valid[index].name);
        return valid[index];
    }

    public bool IsPuzzleMusic(AudioClip clip) =>
        clip &&
        puzzleMusicPool != null &&
        puzzleMusicPool.Contains(clip);

    static bool IsPuzzleStage(RuntimePuzzleStage stage) =>
        stage == RuntimePuzzleStage.Cottage ||
        stage == RuntimePuzzleStage.Ship ||
        stage == RuntimePuzzleStage.Eiffel ||
        stage == RuntimePuzzleStage.Pagoda ||
        stage == RuntimePuzzleStage.Windmill;

    IEnumerator Fade(AudioSource outgoing, AudioSource incoming)
    {
        float start = Time.unscaledTime;
        float target = master * musicVolume * 0.34f;
        while (Time.unscaledTime - start < crossfadeSeconds)
        {
            float t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - start) / crossfadeSeconds);
            if (outgoing) outgoing.volume = target * (1f - t);
            incoming.volume = target * t;
            yield return null;
        }
        if (outgoing)
        {
            outgoing.Stop();
            outgoing.volume = 0f;
        }
        incoming.volume = target;
        fade = null;
    }

    public void PlayCue(RuntimeAudioCue cue, Vector3 position)
    {
        EnsureSources();
        if (cues.Count == 0) BuildCues();
        if (!cues.TryGetValue(cue, out AudioClip clip) || !clip)
            return;
        effects.transform.position = position;
        float cueGain = cue == RuntimeAudioCue.Snap ? 0.98f : 0.72f;
        effects.PlayOneShot(clip, master * effectsVolume * cueGain);
    }

    AudioClip BuildMusic(RuntimePuzzleStage stage)
    {
        float root = stage switch
        {
            RuntimePuzzleStage.Splash => 164.81f,
            RuntimePuzzleStage.Selection => 146.83f,
            RuntimePuzzleStage.Cottage => 174.61f,
            RuntimePuzzleStage.Ship => 130.81f,
            RuntimePuzzleStage.Eiffel => 196f,
            RuntimePuzzleStage.Pagoda => 185f,
            RuntimePuzzleStage.Windmill => 146.83f,
            RuntimePuzzleStage.Completion => 220f,
            _ => 155.56f
        };
        float[] ratios = { 1f, 1.25f, 1.5f, 2f };
        const int rate = 22050;
        const int seconds = 24;
        float[] data = new float[rate * seconds];
        uint noise = 0x71A3u + (uint)stage * 137u;
        float filtered = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate;
            float beat = Mathf.Floor(t / 4.5f) % ratios.Length;
            float value = 0f;
            for (int note = 0; note < ratios.Length; note++)
            {
                float f = root * ratios[(note + (int)beat) % ratios.Length];
                float breath = 0.45f + 0.55f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t + note * 0.55f, 4.5f) / 4.5f);
                value += Mathf.Sin(Mathf.PI * 2f * f * t + note * 0.71f) * breath * (0.012f / (1f + note * 0.52f));
            }
            noise = noise * 1664525u + 1013904223u;
            float n = ((noise >> 8) / 16777216f) * 2f - 1f;
            filtered = Mathf.Lerp(filtered, n, 0.0014f);
            data[i] = Mathf.Clamp((value + filtered * 0.0022f) * 2.2f, -0.18f, 0.18f);
        }
        AudioClip clip = AudioClip.Create("Original " + stage + " remembered workshop suite", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void BuildCues()
    {
        foreach (RuntimeAudioCue cue in System.Enum.GetValues(typeof(RuntimeAudioCue)))
        {
            AudioClip authored = AuthoredCue(cue);
            cues[cue] = authored ? authored : BuildCue(cue);
        }
    }

    AudioClip AuthoredMusic(RuntimePuzzleStage stage) => stage switch
    {
        RuntimePuzzleStage.Splash => splashMusic,
        RuntimePuzzleStage.Selection => selectionMusic,
        RuntimePuzzleStage.Cottage => cottageMusic,
        RuntimePuzzleStage.Ship => shipMusic,
        RuntimePuzzleStage.Eiffel => eiffelMusic,
        RuntimePuzzleStage.Pagoda => cottageMusic ? cottageMusic : eiffelMusic,
        RuntimePuzzleStage.Windmill => selectionMusic ? selectionMusic : cottageMusic,
        RuntimePuzzleStage.Completion => completionMusic,
        RuntimePuzzleStage.Collection => collectionMusic,
        _ => null
    };

    AudioClip AuthoredCue(RuntimeAudioCue cue) => cue switch
    {
        RuntimeAudioCue.Touch => touchCue,
        RuntimeAudioCue.CarouselSwipe => carouselSwipeCue,
        RuntimeAudioCue.Grab => grabCue,
        RuntimeAudioCue.SocketBreath => socketBreathCue,
        RuntimeAudioCue.Snap => snapCue,
        RuntimeAudioCue.Miss => missCue,
        RuntimeAudioCue.Hint => hintCue,
        RuntimeAudioCue.GhostReveal => ghostRevealCue ? ghostRevealCue : hintCue,
        RuntimeAudioCue.HeavyLift => heavyLiftCue,
        RuntimeAudioCue.Complete => completeCue,
        RuntimeAudioCue.StarLight => starLightCue,
        RuntimeAudioCue.Error => errorCue ? errorCue : missCue,
        RuntimeAudioCue.Archive => archiveCue,
        RuntimeAudioCue.Transition => transitionCue,
        _ => null
    };

    static AudioClip BuildCue(RuntimeAudioCue cue)
    {
        const int rate = 22050;
        float seconds = cue == RuntimeAudioCue.Complete ? 2.4f : cue == RuntimeAudioCue.Archive ? 1.8f : 0.48f;
        int count = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[count];
        if (cue == RuntimeAudioCue.Snap)
            return BuildSnapCue(rate);
        if (cue == RuntimeAudioCue.Transition)
            return BuildNonSirenTransitionCue(rate);

        float baseFrequency = cue switch
        {
            RuntimeAudioCue.Grab => 246.94f,
            RuntimeAudioCue.CarouselSwipe => 293.66f,
            RuntimeAudioCue.SocketBreath => 440f,
            RuntimeAudioCue.Snap => 523.25f,
            RuntimeAudioCue.Miss => 116.54f,
            RuntimeAudioCue.Hint => 659.25f,
            RuntimeAudioCue.GhostReveal => 698.46f,
            RuntimeAudioCue.HeavyLift => 82.41f,
            RuntimeAudioCue.Complete => 392f,
            RuntimeAudioCue.StarLight => 783.99f,
            RuntimeAudioCue.Error => 98f,
            RuntimeAudioCue.Archive => 587.33f,
            RuntimeAudioCue.Transition => 311.13f,
            _ => 329.63f
        };
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Exp(-3.4f * t / seconds);
            float sweep = cue == RuntimeAudioCue.Miss || cue == RuntimeAudioCue.Error
                ? 1f - t / seconds * 0.35f
                : cue == RuntimeAudioCue.Transition
                    ? 0.82f + Mathf.SmoothStep(0f, 0.56f, t / seconds)
                    : 1f + t / seconds * 0.16f;
            data[i] = (Mathf.Sin(Mathf.PI * 2f * baseFrequency * sweep * t) +
                0.30f * Mathf.Sin(Mathf.PI * 4f * baseFrequency * t)) * envelope * 0.12f;
        }
        AudioClip clip = AudioClip.Create("Original " + cue + " physical cue", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildSnapCue(int rate)
    {
        const float seconds = 0.18f;
        int count = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[count];
        uint noise = 0xC0FFEEu;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            noise = noise * 1664525u + 1013904223u;
            float n = ((noise >> 8) / 16777216f) * 2f - 1f;
            float click = Mathf.Exp(-135f * t) * n * 0.46f;
            float latchT = Mathf.Max(0f, t - 0.016f);
            float latch = Mathf.Sin(Mathf.PI * 2f * 960f * latchT) * Mathf.Exp(-52f * latchT) * 0.15f;
            float brassT = Mathf.Max(0f, t - 0.030f);
            float brass = Mathf.Sin(Mathf.PI * 2f * 1450f * brassT) * Mathf.Exp(-58f * brassT) * 0.075f;
            float wood = Mathf.Sin(Mathf.PI * 2f * 176f * t) * Mathf.Exp(-21f * t) * 0.13f;
            data[i] = Mathf.Clamp(click + latch + brass + wood, -0.80f, 0.80f);
        }
        AudioClip clip = AudioClip.Create("Original crisp socket latch cue", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildNonSirenTransitionCue(int rate)
    {
        const float seconds = 0.72f;
        int count = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[count];
        uint noise = 0xA11CEu;
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float u = Mathf.Clamp01(t / seconds);
            noise = noise * 1664525u + 1013904223u;
            float n = ((noise >> 8) / 16777216f) * 2f - 1f;
            low = Mathf.Lerp(low, n, 0.025f);
            float airEnv = Mathf.Sin(Mathf.PI * u);
            float air = low * airEnv * 0.052f;
            float wood = Mathf.Sin(Mathf.PI * 2f * 132f * t) * Mathf.Exp(-16f * t) * 0.035f;
            float bell = 0f;
            float[] starts = { 0.075f, 0.145f, 0.235f };
            float[] freqs = { 523.25f, 659.25f, 783.99f };
            for (int b = 0; b < starts.Length; b++)
            {
                float bt = Mathf.Max(0f, t - starts[b]);
                bell += Mathf.Sin(Mathf.PI * 2f * freqs[b] * bt) *
                    Mathf.Exp(-8.5f * bt) * (bt > 0f ? 1f : 0f) * (0.028f / (b + 1f));
            }
            data[i] = Mathf.Clamp((air + wood + bell) * 1.18f, -0.40f, 0.40f);
        }
        AudioClip clip = AudioClip.Create("Original soft mechanical scene transition cue", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
