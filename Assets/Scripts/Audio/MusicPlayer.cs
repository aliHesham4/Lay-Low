using UnityEngine;
using UnityEngine.Audio;

// Background soundtrack for a scene (main menu or gameplay). Loops and plays through the mixer's Music group,
// so the Music slider in Settings controls it. It carries over to the next scene: when a round restarts
// the same track keeps playing instead of starting over, and a scene with a different track fades the old one out.
public class MusicPlayer : MonoBehaviour
{
    public AudioClip track;
    [Range(0f, 1f)] public float volume = 0.5f;
    [Tooltip("Assets/Sounds/GameAudio.mixer, so the track plays through its Music group.")]
    public AudioMixer mixer;
    public float fadeIn = 2f;

    static MusicPlayer current;
    AudioSource source;
    float fadeOutSpeed;   // > 0 while this one is fading away

    void Awake()
    {
        // the same track is already playing (next round): keep it going
        if (current != null && current.track == track && current.fadeOutSpeed <= 0f) { Destroy(gameObject); return; }
        if (current != null) current.FadeOut(1.5f);

        current = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.clip = track;
        source.loop = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;
        if (mixer != null)
        {
            AudioMixerGroup[] g = mixer.FindMatchingGroups("Music");
            if (g.Length > 0) source.outputAudioMixerGroup = g[0];
        }
        if (track != null) source.Play();
    }

    void FadeOut(float seconds) { fadeOutSpeed = volume / Mathf.Max(0.05f, seconds); }

    // Fades out whatever track is playing (e.g. on the match-over scoreboard).
    public static void FadeOutCurrent(float seconds)
    {
        if (current != null && current.fadeOutSpeed <= 0f) current.FadeOut(seconds);
    }

    void Update()
    {
        if (source == null) return;
        if (fadeOutSpeed > 0f)
        {
            source.volume -= fadeOutSpeed * Time.unscaledDeltaTime;
            if (source.volume <= 0f) Destroy(gameObject);
            return;
        }
        source.volume = Mathf.MoveTowards(source.volume, volume, volume / Mathf.Max(0.05f, fadeIn) * Time.unscaledDeltaTime);
    }

    void OnDestroy() { if (current == this) current = null; }
}
