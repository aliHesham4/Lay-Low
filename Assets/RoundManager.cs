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

    // ---- survives the scene reload ----
    static int currentRound = 1;
    static readonly Dictionary<string, int> scores = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetMatch() { currentRound = 1; scores.Clear(); }

    public static int CurrentRound { get { return currentRound; } }
    public float TimeLeft { get { return timeLeft; } }
    public bool RoundOver { get { return roundOver; } }

    float timeLeft;
    bool roundOver;
    bool matchOver;
    TextMeshProUGUI timerText, bannerText, toastText;
    Image bannerBack;
    float toastUntil;
    float freezeAt = float.MaxValue, nextAt = float.MaxValue;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (hiders == null || hiders.Length == 0) hiders = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        timeLeft = currentRound == 1 ? firstRoundSeconds : otherRoundSeconds;
        BuildUI();

        // put the scores from earlier rounds back on the HUD cards
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            PlayerHUD hud = h.GetComponent<PlayerHUD>();
            int s; scores.TryGetValue(h.displayName, out s);
            if (hud != null) hud.SetScore(s);
        }

        StartCoroutine(Intro());
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

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { timeLeft = 0f; EndRound("Time's up!"); }

        if (timerText != null)
        {
            timerText.text = "Round " + currentRound + "/" + totalRounds + "   " + FormatTime(timeLeft);
            timerText.color = timeLeft <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }
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
        else if (free == 1 && players > 1) EndRound(survivor.GetComponent<KnifeHolder>() != null ? survivor.displayName + " is coming home alive!" : survivor.displayName + " is the last one standing!");
    }

    void EndRound(string reason)
    {
        if (roundOver) return;
        roundOver = true;

        string lines = "";
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            int s; scores.TryGetValue(h.displayName, out s);
            if (!h.IsCaught)
            {
                s += pointsForSurviving;
                lines += "\n<color=#7CFF7C>" + h.displayName + " survived  +" + pointsForSurviving + "</color>";
            }
            else lines += "\n<color=#FF8080>" + h.displayName + " was caught</color>";
            scores[h.displayName] = s;
            PlayerHUD hud = h.GetComponent<PlayerHUD>();
            if (hud != null) hud.SetScore(s);
        }

        if (timerText != null) timerText.text = "Round " + currentRound + "/" + totalRounds + "   0:00";
        ShowBanner("<size=120%>" + reason + "</size>\n<size=60%>Round " + currentRound + " over" + lines + "</size>");
        freezeAt = Time.unscaledTime + freezeAfterEnd;
        nextAt = Time.unscaledTime + resultsSeconds;
    }

    // after the round ends (real time, so it works while the game is frozen)
    void UpdateResults()
    {
        if (Time.timeScale > 0f && Time.unscaledTime >= freezeAt) Time.timeScale = 0f; // freeze the house once deaths have played
        if (Time.unscaledTime < nextAt) return;

        if (currentRound >= totalRounds) { Time.timeScale = 0f; ShowFinal(); return; }

        currentRound++;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        nextAt = float.MaxValue;
    }

    void ShowFinal()
    {
        matchOver = true;
        if (toastText != null) toastText.gameObject.SetActive(false);
        int best = int.MinValue; List<string> winners = new List<string>();
        string table = "";
        foreach (Hider h in hiders)
        {
            if (h == null) continue;
            int s; scores.TryGetValue(h.displayName, out s);
            table += "\n" + h.displayName + ":  " + s;
            if (s > best) { best = s; winners.Clear(); winners.Add(h.displayName); }
            else if (s == best) winners.Add(h.displayName);
        }
        string head = winners.Count == 1 ? winners[0] + " wins!" : "It's a tie!  " + string.Join(" & ", winners.ToArray());
        ShowBanner("<size=130%>Match over</size>\n" + head + "<size=60%>" + table + "\n\n<color=#CCCCCC>Press R to play again</color></size>");
    }

    void NewMatch()
    {
        ResetMatch();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static string FormatTime(float t)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, t));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // ---------------- UI ----------------

    public void Toast(string msg)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastText.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + 2.5f;
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
