using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A match is 4 rounds. Round 1 lasts 1 minute, the others 2 minutes.
// A round ends when the time runs out or when every player has been caught.
// Everyone still free at the end of a round gets +1. Scores carry over between rounds.
// Each round reloads the scene, so the house, killer, items and distractions start fresh.
public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Tooltip("Every player in the round (Player1, Player2, ...).")]
    public Hider[] hiders;

    [Header("Match")]
    public int totalRounds = 4;
    public float firstRoundSeconds = 60f;
    public float otherRoundSeconds = 120f;
    public int pointsForSurviving = 1;
    [Tooltip("Seconds the round result stays on screen before the next round loads.")]
    public float resultsSeconds = 5f;
    [Tooltip("Seconds the 'Round X' banner shows at the start.")]
    public float introSeconds = 2.5f;
    [Tooltip("Seconds the house keeps moving after the round ends (so a death animation can finish) before it freezes.")]
    public float freezeAfterEnd = 2.5f;

    [Header("Sounds")]
    [Tooltip("Plays when the match-over scoreboard appears.")]
    public AudioClip winnerSound;
    [Range(0f, 1f)] public float winnerVolume = 0.8f;

    [Header("Testing")]
    [Tooltip("Press Play and the match starts at this round (1 = normal game). Set it back to 1 when you're done testing.")]
    [Range(1, 4)] public int startAtRound = 1;

    // ---- survives the scene reload ----
    static int currentRound = 1;
    static readonly Dictionary<string, int> scores = new Dictionary<string, int>();
    // what happened to each player in each round so far: s = survived, c = caught, k = stabbed (for the final scoreboard)
    static readonly Dictionary<string, string> history = new Dictionary<string, string>();
    static bool matchStarted; // false until the first round of a match has loaded
    bool introDone;   // false while the round intro (blind + round card) is playing
    bool loadingNext; // the blind is closing / the next round is loading

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetMatch() { currentRound = 1; scores.Clear(); history.Clear(); matchStarted = false; }

    public static int CurrentRound { get { return currentRound; } }

    // Called by a night scene (Night 1-4) before the house loads under it: this is the round being played.
    public static void BeginNight(int night)
    {
        currentRound = Mathf.Clamp(night, 1, 4);
        matchStarted = true;
    }
    public float TimeLeft { get { return timeLeft; } }
    public bool RoundOver { get { return roundOver; } }
    public bool IntroDone { get { return introDone; } }
    // set by a cutscene (the clown's greeting): the round clock waits until it is over
    public bool ClockHeld { get; set; }

    // pause menu: play this round again from the elevator (nothing has been scored for it yet)
    public void RestartRound()
    {
        if (roundOver || loadingNext) return;
        LoadNextBehindBlind();
    }

    float timeLeft;
    bool roundOver;
    bool matchOver;
    TextMeshProUGUI timerText, bannerText, toastText;
    PaperClock clock;
    float RoundLength { get { return currentRound == 1 ? firstRoundSeconds : otherRoundSeconds; } }
    Image bannerBack;
    float toastUntil;
    float freezeAt = float.MaxValue, nextAt = float.MaxValue;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        // testing: jump straight to a later round when a new match begins
        if (!matchStarted)
        {
            matchStarted = true;
            if (startAtRound > 1) currentRound = Mathf.Clamp(startAtRound, 1, totalRounds);
        }
    }

    void Start()
    {
        if (hiders == null || hiders.Length == 0) hiders = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        timeLeft = currentRound == 1 ? firstRoundSeconds : otherRoundSeconds;
        BuildUI();
        if (clock != null) clock.Set(timeLeft, RoundLength, false); // full clock while the round intro plays

        // put the scores from earlier rounds back on the HUD cards
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            PlayerHUD hud = h.GetComponent<PlayerHUD>();
            int s; scores.TryGetValue(h.displayName, out s);
            if (hud != null) hud.SetScore(s);
        }

        if (RoundIntro.Instance != null)
        {
            // the house waits, frozen, behind the shut blind until this round is loaded and ready,
            // then the blind opens, the round card plays, and only then does the round begin
            Time.timeScale = 0f;
            RoundIntro.Instance.ShowRound(currentRound, () => { introDone = true; Time.timeScale = 1f; });
        }
        else
        {
            introDone = true;
            StartCoroutine(Intro());
        }
    }

    IEnumerator Intro()
    {
        ShowBanner("Round " + currentRound + " / " + totalRounds + "\n<size=55%>" + FormatTime(timeLeft) + " to survive</size>");
        yield return new WaitForSeconds(introSeconds);
        if (!roundOver) HideBanner();
    }

    void Update()
    {
        if (matchOver)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) NewMatch();
            return;
        }
        if (toastText != null && toastText.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastText.gameObject.SetActive(false);
        if (roundOver) { UpdateResults(); return; }
        if (!introDone || ClockHeld) return; // no clock while the round intro or a cutscene plays

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { timeLeft = 0f; EndRound("Time's up!"); }

        if (clock != null) clock.Set(timeLeft, RoundLength, BlackoutRound.Active); // the paper wall clock, top-right
        if (toastText != null && toastText.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastText.gameObject.SetActive(false);
    }

    // Called by Hider.Freeze when the killer grabs someone
    public void ReportCaught(Hider caught)
    {
        if (roundOver) return;
        Toast(caught.KilledBy != null ? caught.displayName + " was stabbed by " + caught.KilledBy.displayName + "!" : caught.displayName + " was caught!");

        // count who is still free: nobody -> everyone was caught; only one left -> they are the last one standing
        int players = 0, free = 0;
        Hider survivor = null;
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            players++;
            if (!h.IsCaught) { free++; survivor = h; }
        }
        if (free == 0) EndRound("Everyone was caught!");
        else if (free == 1 && players > 1) EndRound("Last one standing!", survivor);
    }

    void EndRound(string reason, Hider lastStanding = null)
    {
        if (roundOver) return;
        roundOver = true;
        StartCoroutine(SilenceAppliances());

        // tell the story how this night went for the human player (this is what unlocks the next night)
        bool humanAlive = false;
        foreach (Hider h in hiders) if (h != null && h.isHumanControlled && !h.IsCaught) humanAlive = true;
        StoryStateManager.Instance.CompleteNight(currentRound, humanAlive);

        // scores, each player's result this round, and the scoreboard rows
        List<ScoreLine> lines = new List<ScoreLine>();
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            int s; scores.TryGetValue(h.displayName, out s);
            int gained = h.IsCaught ? 0 : pointsForSurviving;
            s += gained;
            scores[h.displayName] = s;
            string past; history.TryGetValue(h.displayName, out past);
            history[h.displayName] = (past ?? "") + (!h.IsCaught ? "s" : h.KilledBy != null ? "k" : "c");
            PlayerHUD hud = h.GetComponent<PlayerHUD>();
            if (hud != null) hud.SetScore(s);
            lines.Add(Line(h, gained, s));
        }

        if (timerText != null) timerText.text = "Round " + currentRound + "/" + totalRounds + "   0:00";
        if (clock != null) clock.Set(0f, RoundLength, BlackoutRound.Active);
        if (lastStanding != null) PaperScoreboard.ShowRound(currentRound, totalRounds, reason, true, lastStanding.displayName, lines);
        else PaperScoreboard.ShowRound(currentRound, totalRounds, reason, false, null, lines);
        freezeAt = Time.unscaledTime + freezeAfterEnd;
        nextAt = Time.unscaledTime + resultsSeconds;
    }

    // Round over: anything still running in the house (TV, vacuum, piano...) fades out and switches off,
    // on real time because the house freezes a moment later.
    System.Collections.IEnumerator SilenceAppliances(float seconds = 1.5f)
    {
        var running = new List<MonoBehaviour>();
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            System.Type tp = mb.GetType();
            var isOn = tp.GetProperty("IsOn");
            var off = tp.GetMethod("TurnOff", System.Type.EmptyTypes);
            if (isOn == null || off == null || isOn.PropertyType != typeof(bool)) continue;
            if ((bool)isOn.GetValue(mb, null)) running.Add(mb);
        }
        if (running.Count == 0) yield break;
        var sources = new List<AudioSource>(); var vols = new List<float>();
        foreach (MonoBehaviour mb in running)
            foreach (AudioSource s in mb.GetComponentsInChildren<AudioSource>()) { sources.Add(s); vols.Add(s.volume); }
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < sources.Count; i++) if (sources[i] != null) sources[i].volume = vols[i] * (1f - t / seconds);
            yield return null;
        }
        foreach (MonoBehaviour mb in running) if (mb != null) mb.GetType().GetMethod("TurnOff", System.Type.EmptyTypes).Invoke(mb, null);
        for (int i = 0; i < sources.Count; i++) if (sources[i] != null) { sources[i].Stop(); sources[i].volume = vols[i]; }
    }

    // one player's row on the scoreboard
    ScoreLine Line(Hider h, int gained, int total)
    {
        PlayerHUD hud = h.GetComponent<PlayerHUD>();
        ScoreLine l = new ScoreLine();
        l.name = h.displayName;
        l.face = hud != null ? hud.characterFaceSprite : null;
        l.portrait = hud != null ? hud.portraitSprite : null;
        l.colour = hud != null ? hud.CardColour : Color.white;
        if (!h.IsCaught) l.result = h.GetComponent<KnifeHolder>() != null ? ScoreLine.Result.SurvivedWithKnife : ScoreLine.Result.Survived;
        else if (h.KilledBy != null) { l.result = ScoreLine.Result.Stabbed; l.stabbedBy = h.KilledBy.displayName; }
        else l.result = ScoreLine.Result.Caught;
        l.gained = gained;
        l.total = total;
        string past; history.TryGetValue(h.displayName, out past);
        l.history = past ?? "";
        return l;
    }

    // after the round ends (real time, so it works while the game is frozen)
    void UpdateResults()
    {
        if (Time.timeScale > 0f && Time.unscaledTime >= freezeAt) Time.timeScale = 0f; // freeze the house once deaths have played
        if (Time.unscaledTime < nextAt) return;

        if (currentRound >= totalRounds) { Time.timeScale = 0f; ShowFinal(); return; }

        currentRound++;
        LoadNextBehindBlind();
        nextAt = float.MaxValue;
    }

    void ShowFinal()
    {
        matchOver = true;
        MusicPlayer.FadeOutCurrent(3f); // the soundtrack dies away under the final scoreboard
        if (toastText != null) toastText.gameObject.SetActive(false);

        // the ending first, written on the shutter, then the final scoreboard behind it
        int best = int.MinValue, mine = int.MinValue;
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            int s; scores.TryGetValue(h.displayName, out s);
            best = Mathf.Max(best, s);
            if (h.isHumanControlled) mine = s;
        }
        string ending = StoryStateManager.Instance.EndingFor(mine == best);
        if (RoundIntro.Instance != null) RoundIntro.Instance.ShowEnding(ending, 7f, FinalBoard);
        else FinalBoard();
    }

    void FinalBoard()
    {
        List<ScoreLine> standings = new List<ScoreLine>();
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            int s; scores.TryGetValue(h.displayName, out s);
            standings.Add(Line(h, 0, s));
        }
        // highest first (players with the same score keep their card order)
        List<ScoreLine> sorted = new List<ScoreLine>();
        foreach (ScoreLine l in standings)
        {
            int at = sorted.Count;
            for (int i = 0; i < sorted.Count; i++) if (l.total > sorted[i].total) { at = i; break; }
            sorted.Insert(at, l);
        }
        List<ScoreLine> winners = new List<ScoreLine>();
        foreach (ScoreLine l in sorted) if (l.total == sorted[0].total) winners.Add(l);
        PaperScoreboard.ShowFinal(sorted, winners, totalRounds);
        if (winnerSound != null)
        {
            // the house is frozen (timeScale 0) but audio still plays; 2D so it sounds the same wherever the camera is
            AudioSource a = gameObject.AddComponent<AudioSource>();
            a.clip = winnerSound; a.volume = winnerVolume; a.spatialBlend = 0f; a.ignoreListenerPause = true;
            a.Play();
        }
    }

    void NewMatch()
    {
        ResetMatch();
        StoryStateManager.Instance.ResetStory();
        StoryStateManager.Instance.SetFlag("movedIn");
        LoadNextBehindBlind();
    }

// After the scoreboard: the hand pulls the blind shut, and only then does the next round load behind it.
    // The new scene's RoundManager opens the blind once it is up and running.
    void LoadNextBehindBlind()
    {
        if (loadingNext) return;
        loadingNext = true;
        nextAt = float.MaxValue;
        // each round is its own scene (Night 1-4), which loads the house under itself;
        // if the night scenes aren't in the build, the house scene is just reloaded as before
        // the next night only opens once the story says the one before it is over
        // (a restart replays the same night, which the story already allows)
        StoryStateManager story = StoryStateManager.Instance;
        if (!story.CanStartNight(currentRound)) currentRound = story.FurthestNight();
        string night = NightScene.SceneName(currentRound);
        bool hasNight = Application.CanStreamedLevelBeLoaded(night);
        int scene = SceneManager.GetActiveScene().buildIndex;
        if (RoundIntro.Instance == null)
        {
            Time.timeScale = 1f;
            if (hasNight) SceneManager.LoadScene(night); else SceneManager.LoadScene(scene);
            return;
        }
        RoundIntro.Instance.CloseShutter(() => { if (hasNight) SceneManager.LoadSceneAsync(night); else SceneManager.LoadSceneAsync(scene); });
    }


    static string FormatTime(float t)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, t));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // ---------------- UI ----------------

    public void Toast(string msg)
    {
        // a paper strip pinned under the HUD row; golden-knife news gets the gold strip
        PaperToast.Show(msg, msg.ToLowerInvariant().Contains("golden knife"));
    }

    void ShowBanner(string msg)
    {
        if (bannerText == null) return;
        bannerText.text = msg;
        bannerText.gameObject.SetActive(true);
        bannerBack.gameObject.SetActive(true);
    }

    void HideBanner()
    {
        if (bannerText == null) return;
        bannerText.gameObject.SetActive(false);
        bannerBack.gameObject.SetActive(false);
    }

    void BuildUI()
    {
        TMP_FontAsset font = null;
        foreach (Hider h in hiders)
        {
            PlayerHUD hud = h != null ? h.GetComponent<PlayerHUD>() : null;
            if (hud != null && hud.nameText != null) { font = hud.nameText.font; break; }
        }

        GameObject c = new GameObject("RoundUI", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = c.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler sc = c.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;

        timerText = MakeText(c.transform, "Timer", font, 40, new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(600, 60)); // below the player cards
        timerText.fontStyle = FontStyles.Bold;
        timerText.gameObject.SetActive(false); // replaced by the paper wall clock
        clock = PaperClock.Create();

        GameObject back = new GameObject("BannerBack", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(c.transform, false);
        bannerBack = back.GetComponent<Image>();
        bannerBack.color = new Color(0f, 0f, 0f, 0.55f);
        bannerBack.raycastTarget = false;
        RectTransform br = bannerBack.rectTransform;
        br.anchorMin = new Vector2(0f, 0.5f); br.anchorMax = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(0f, 440f); br.anchoredPosition = new Vector2(0f, 60f);

        bannerText = MakeText(c.transform, "Banner", font, 64, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600, 440));
        bannerText.fontStyle = FontStyles.Bold;

        toastText = MakeText(c.transform, "Toast", font, 36, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(1000, 50));
        toastText.color = new Color(1f, 0.55f, 0.5f);

        HideBanner();
        toastText.gameObject.SetActive(false);
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, float size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(0, 0, 0, 200);
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = box;
        return t;
    }
}
