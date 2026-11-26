using UnityEngine;
using UnityEngine.Audio;

// Every option of the pause menu's Settings, saved between matches (PlayerPrefs).
// Audio categories run through Assets/Sounds/GameAudio.mixer: each sound in the scene is sent to its group
// (Music, Clown, House, Heartbeat) by AudioRouter; Master is the listener's own volume, so it also covers
// one-shot sounds in the moment before they are routed.
public static class GameSettings
{
    const string P = "LayLow.";

    // ---------------- audio (0..100) ----------------
    public static int Master    { get { return PlayerPrefs.GetInt(P + "Master", 80); }    set { PlayerPrefs.SetInt(P + "Master", value); ApplyAudio(); } }
    public static int Music     { get { return PlayerPrefs.GetInt(P + "Music", 70); }     set { PlayerPrefs.SetInt(P + "Music", value); ApplyAudio(); } }
    public static int Clown     { get { return PlayerPrefs.GetInt(P + "Clown", 90); }     set { PlayerPrefs.SetInt(P + "Clown", value); ApplyAudio(); } }
    public static int House     { get { return PlayerPrefs.GetInt(P + "House", 80); }     set { PlayerPrefs.SetInt(P + "House", value); ApplyAudio(); } }
    public static int Heartbeat { get { return PlayerPrefs.GetInt(P + "Heartbeat", 80); } set { PlayerPrefs.SetInt(P + "Heartbeat", value); ApplyAudio(); } }

    // ---------------- machine ----------------
    public static int ResolutionIndex { get { return PlayerPrefs.GetInt(P + "Res", -1); } set { PlayerPrefs.SetInt(P + "Res", value); ApplyDisplay(); } }
    public static int WindowMode      { get { return PlayerPrefs.GetInt(P + "Window", 0); } set { PlayerPrefs.SetInt(P + "Window", value); ApplyDisplay(); } } // 0 full, 1 borderless, 2 window
    public static int Quality         { get { return PlayerPrefs.GetInt(P + "Quality", 2); } set { PlayerPrefs.SetInt(P + "Quality", value); ApplyDisplay(); } }
    public static int FrameLimit      { get { return PlayerPrefs.GetInt(P + "Fps", 0); } set { PlayerPrefs.SetInt(P + "Fps", value); ApplyDisplay(); } } // index into FrameLimits
    public static bool VSync          { get { return PlayerPrefs.GetInt(P + "VSync", 1) == 1; } set { PlayerPrefs.SetInt(P + "VSync", value ? 1 : 0); ApplyDisplay(); } }
    public static readonly int[] FrameLimits = { 60, 120, 144, -1 };

    // ---------------- gameplay ----------------
    public static int TurnSpeed          { get { return PlayerPrefs.GetInt(P + "Turn", 50); } set { PlayerPrefs.SetInt(P + "Turn", value); } }
    public static bool StartFirstPerson  { get { return PlayerPrefs.GetInt(P + "StartFpv", 0) == 1; } set { PlayerPrefs.SetInt(P + "StartFpv", value ? 1 : 0); } }
    public static bool AimHints          { get { return PlayerPrefs.GetInt(P + "Hints", 1) == 1; } set { PlayerPrefs.SetInt(P + "Hints", value ? 1 : 0); } }
    public static bool PlayerOutlines    { get { return PlayerPrefs.GetInt(P + "Outlines", 1) == 1; } set { PlayerPrefs.SetInt(P + "Outlines", value ? 1 : 0); } }
    public static bool CalmFlicker       { get { return PlayerPrefs.GetInt(P + "CalmFlicker", 0) == 1; } set { PlayerPrefs.SetInt(P + "CalmFlicker", value ? 1 : 0); } }

    // 50 = normal; 0 = half speed, 100 = one and a half
    public static float TurnFactor { get { return 0.5f + TurnSpeed / 100f; } }

    // ---------------- applying ----------------
    public static AudioMixer Mixer;

    public static void ApplyAll() { ApplyAudio(); ApplyDisplay(); }

    public static void ApplyAudio()
    {
        AudioListener.volume = Master / 100f;
        if (Mixer == null) return;
        Mixer.SetFloat("MusicVol", Db(Music));
        Mixer.SetFloat("ClownVol", Db(Clown));
        Mixer.SetFloat("HouseVol", Db(House));
        Mixer.SetFloat("HeartbeatVol", Db(Heartbeat));
    }

    static float Db(int v) { return v <= 0 ? -80f : Mathf.Log10(v / 100f) * 20f; }

    // Low / Medium / High: picture sharpness (render scale) and how far shadows reach, on the project's render pipeline.
    // The pipeline asset's own values are High; in the editor they are put back when play stops.
    static float origScale = -1f, origShadow;
    static void ApplyQuality()
    {
        var u = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (u == null) return;
        if (origScale < 0f)
        {
            origScale = u.renderScale; origShadow = u.shadowDistance;
            Application.quitting += RestoreQuality;
        }
        int q = Mathf.Clamp(Quality, 0, 2);
        u.renderScale = origScale * (q == 0 ? 0.7f : q == 1 ? 0.85f : 1f);
        u.shadowDistance = origShadow * (q == 0 ? 0.5f : q == 1 ? 0.75f : 1f);
    }

    static void RestoreQuality()
    {
        var u = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (u != null && origScale > 0f) { u.renderScale = origScale; u.shadowDistance = origShadow; }
        Application.quitting -= RestoreQuality;
        origScale = -1f;
    }

    public static Resolution[] Resolutions()
    {
        var list = new System.Collections.Generic.List<Resolution>();
        foreach (Resolution r in Screen.resolutions)
            if (!list.Exists(x => x.width == r.width && x.height == r.height)) list.Add(r);
        list.Sort((a, b) => b.width != a.width ? b.width.CompareTo(a.width) : b.height.CompareTo(a.height));
        return list.ToArray();
    }

    public static void ApplyDisplay()
    {
        ApplyQuality();
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FrameLimits[Mathf.Clamp(FrameLimit, 0, FrameLimits.Length - 1)];
#if !UNITY_EDITOR
        FullScreenMode mode = WindowMode == 0 ? FullScreenMode.ExclusiveFullScreen : WindowMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Resolution[] rs = Resolutions();
        if (ResolutionIndex >= 0 && ResolutionIndex < rs.Length) Screen.SetResolution(rs[ResolutionIndex].width, rs[ResolutionIndex].height, mode);
        else Screen.fullScreenMode = mode;
#endif
    }
}

// Sends every sound in the scene to its mixer group, so the Settings sliders reach it.
public class AudioRouter : MonoBehaviour
{
    AudioMixerGroup music, clown, house, heartbeat;
    float next;

    public void Init(AudioMixer mixer)
    {
        music = Group(mixer, "Music"); clown = Group(mixer, "Clown"); house = Group(mixer, "House"); heartbeat = Group(mixer, "Heartbeat");
        Route();
    }

    static AudioMixerGroup Group(AudioMixer m, string n) { AudioMixerGroup[] g = m.FindMatchingGroups(n); return g.Length > 0 ? g[0] : null; }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.1f; // new sounds (one-shots, hazard loops...) are picked up within a tenth of a second
        Route();
    }

    void Route()
    {
        if (house == null) return;
        foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.outputAudioMixerGroup != null) continue;
            AudioMixerGroup g = Pick(s);
            if (g != null) s.outputAudioMixerGroup = g;
        }
    }

    AudioMixerGroup Pick(AudioSource s)
    {
        string clip = s.clip != null ? s.clip.name.ToLowerInvariant() : "";
        if (clip.Contains("heart")) return heartbeat;
        if (s.GetComponent<HeartbeatMonitor>() != null && s.clip == null) return null; // the heartbeat's own source: wait for its clip
        Transform root = s.transform.root;
        if (root.GetComponent<EnemyVision>() != null || clip.Contains("clown") || clip.Contains("laugh") || clip.Contains("enemy") || s.name.ToLowerInvariant().Contains("roar")) return clown;
        if (s.GetComponentInParent<RoundIntro>() != null || s.GetComponentInParent<RoundManager>() != null || s.GetComponentInParent<PauseMenu>() != null) return music;
        return house;
    }
}
