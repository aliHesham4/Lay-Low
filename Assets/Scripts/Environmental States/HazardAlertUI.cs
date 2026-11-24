using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The paper alerts everyone sees.
//  Warning (amber scrap, bottom-left): hazard badge, title, message, 5 paper pips + an upright "3s" counter.
//  Sealed  (same spot, red paper):     cream lock badge and a "SEALED" stamp that thumps on, then it slides away.
//  Danger  (same spot, red paper):     like sealed without the stamp (power cut, "he's hunting").
//  Announcement (black paper with a gold foil line): game-changing news, no countdown. Same bottom-left corner;
//               if a hazard alert is up at the same time, it sits just above it.
public class HazardAlertUI : MonoBehaviour
{
    static HazardAlertUI instance;

    public static void ShowWarning(string titleText, string messageText, float seconds)
    {
        HazardAlertUI ui = Get(); if (ui == null) return;
        ui.Warn(titleText, messageText, seconds);
    }

    // a plain announcement (no 'sealed' tag), used by the golden-knife round
    public static void ShowAnnouncement(string titleText, string messageText, float seconds)
    {
        HazardAlertUI ui = Get(); if (ui == null) return;
        ui.Announce(titleText, messageText, seconds);
    }

    public static void ShowSealed(string titleText, string messageText)
    {
        HazardAlertUI ui = Get(); if (ui == null) return;
        ui.Red(titleText, messageText, true, 4f);
    }

    // red paper without the SEALED stamp (power outage, the hunt)
    public static void ShowDanger(string titleText, string messageText, float seconds = 4f)
    {
        HazardAlertUI ui = Get(); if (ui == null) return;
        ui.Red(titleText, messageText, false, seconds);
    }

    // true while the announcement is on screen (toasts pin in under it)
    public static bool AnnouncementShowing { get { return instance != null && instance.news.shown; } }

    static HazardAlertUI Get()
    {
        if (instance != null) return instance;
        Canvas canvas = PaperUI.HudCanvas;
        if (canvas == null) return null;
        RectTransform r = PaperUI.Rect("Alerts", canvas.transform);
        PaperUI.Stretch(r);
        instance = r.gameObject.AddComponent<HazardAlertUI>();
        instance.Build();
        return instance;
    }

    // which badge goes with which alert
    static string IconFor(string title)
    {
        string t = title.ToUpperInvariant();
        if (t.Contains("FIREPLACE")) return "fireplace";
        if (t.Contains("FIRE") || t.Contains("BLAZE")) return "fire2";
        if (t.Contains("SEWAGE") || t.Contains("LEAK") || t.Contains("FLOOD")) return "drop";
        if (t.Contains("THUNDER") || t.Contains("STORM") || t.Contains("LIGHTNING")) return "bolt";
        if (t.Contains("ELECTRIC") || t.Contains("POWER") || t.Contains("OVERLOAD")) return "bulb";
        if (t.Contains("HUNT")) return "skull";
        return "fire2";
    }

    // =====================================================================
    class Slip
    {
        public RectTransform root;
        public CanvasGroup group;
        public PaperShape paper, badge;
        public Image icon;
        public TextMeshProUGUI title, message;
        public bool shown;
        public float slide;       // 0 hidden .. 1 on screen
        public float shownAt;
        public float hideAt = -1f;
        public float titleTop, titleLine, messageGap, messageUp; // CSS layout of the words
    }

    Slip warn, red, news;
    float newsY = 36f;
    PaperShape[] pips = new PaperShape[5];
    TextMeshProUGUI counter;
    RectTransform redStamp;
    float countdownEnd, countdownLength;
    bool counting;
    int lastPips = 5;

    static readonly Color PipEmpty = PaperUI.Hex("1d1322", 0.35f);

    void Warn(string t, string m, float seconds)
    {
        Fill(warn, t, m);
        warn.icon.sprite = PaperUI.Icon(IconFor(t));
        countdownLength = Mathf.Max(0.1f, seconds);
        countdownEnd = Time.time + seconds;
        counting = true;
        lastPips = 5;
        Show(warn, -1f);
        Hide(red);
    }

    void Red(string t, string m, bool sealedStamp, float seconds)
    {
        redStamp.gameObject.SetActive(sealedStamp);
        Fill(red, t, m);
        string icon = IconFor(t);
        // sealed rooms get the red lock on a cream badge; other danger keeps its own badge on ink
        red.badge.color = sealedStamp ? PaperUI.Cream : PaperUI.Ink;
        red.icon.sprite = PaperUI.Icon(sealedStamp ? "lock_red" : icon);
        counting = false;
        bool swap = warn.shown;
        Hide(warn);
        if (swap) warn.slide = 0f; // the red slip takes the amber one's place straight away
        Show(red, Time.time + seconds);
        if (swap) red.slide = 1f;
    }

    void Announce(string t, string m, float seconds)
    {
        Fill(news, t, m);
        Show(news, Time.time + seconds);
    }

    void Fill(Slip s, string t, string m)
    {
        s.root.gameObject.SetActive(true); // text is only measured on an active object
        s.title.text = t.ToUpperInvariant();
        s.message.text = m.Replace("\r", "").Replace("\n", " "); // the slip wraps the words itself
        // the message starts under however many lines the title took
        s.title.ForceMeshUpdate();
        int lines = Mathf.Max(1, s.title.textInfo.lineCount);
        float msgTop = s.titleTop + lines * s.title.fontSize * s.titleLine + s.messageGap;
        // ...and must end above the countdown row (warning) or the SEALED stamp; a long message shrinks to fit
        float limit = s == warn ? 150f - 18f - 30f - 4f
                    : (s == red && redStamp.gameObject.activeSelf) ? 150f - 18f - StampHeight - 2f
                    : 150f - 16f;
        RectTransform r = s.message.rectTransform;
        r.anchoredPosition = new Vector2(r.anchoredPosition.x, -(msgTop - s.messageUp));
        r.sizeDelta = new Vector2(r.sizeDelta.x, Mathf.Max(18f, limit - msgTop + s.messageUp));
        s.message.enableAutoSizing = true;
        s.message.fontSizeMin = 11f;
        s.message.fontSizeMax = 16f;
        s.message.overflowMode = TextOverflowModes.Truncate;
    }

    static readonly float StampHeight = 26f * PaperUI.NormalLine + 4f + 8f;

    void Show(Slip s, float hideAt)
    {
        s.shown = true;
        s.shownAt = Time.unscaledTime;
        s.hideAt = hideAt;
        transform.SetAsLastSibling();
    }

    void Hide(Slip s) { s.shown = false; s.hideAt = -1f; }

    void Update()
    {
        if (counting)
        {
            float left = Mathf.Max(0f, countdownEnd - Time.time);
            counter.text = Mathf.CeilToInt(left) + "s";
            int full = Mathf.Clamp(Mathf.CeilToInt(left / countdownLength * pips.Length - 0.001f), 0, pips.Length);
            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < full;
                pips[i].ring = on ? 0f : 2f;
                pips[i].color = on ? PaperUI.Ink : PipEmpty;
                pips[i].Refresh();
            }
            if (full < lastPips) { for (int i = full; i < lastPips; i++) DropPip(i); lastPips = full; }
            // the counter gives a little kick every second
            float frac = left - Mathf.Floor(left);
            counter.transform.parent.localScale = Vector3.one * (1f + 0.12f * Mathf.Clamp01((frac - 0.85f) / 0.15f));
        }

        Animate(warn, 36f);
        Animate(red, 36f);
        bool alertUp = warn.slide > 0f || red.slide > 0f;
        newsY = Mathf.Lerp(newsY, alertUp ? 36f + 150f + 22f : 36f, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        Animate(news, newsY);

        // the SEALED stamp thumps on a moment after the red slip arrives
        if (redStamp.gameObject.activeSelf)
        {
            float k = (Time.unscaledTime - red.shownAt - 0.2f) / 0.22f;
            float s = k <= 0f ? 0f : (k < 1f ? Mathf.Lerp(2.4f, 1f, PaperUI.EaseOut(k)) : 1f);
            redStamp.localScale = Vector3.one * s;
        }
    }

    void Animate(Slip s, float y)
    {
        if (s.hideAt > 0f && Time.time >= s.hideAt) Hide(s);
        s.slide = Mathf.MoveTowards(s.slide, s.shown ? 1f : 0f, Time.unscaledDeltaTime * (s.shown ? 3f : 4f));
        float e = s.shown ? PaperUI.EaseOutBack(s.slide) : PaperUI.EaseOut(s.slide);
        s.group.alpha = Mathf.Clamp01(s.slide * 3f);
        s.root.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-460f, 32f, e), y);
        s.root.gameObject.SetActive(s.slide > 0f);
    }

    // a pip that runs out falls off the card
    void DropPip(int i)
    {
        PaperShape src = pips[i];
        PaperShape f = PaperUI.Shape(src.transform.parent, "Falling Pip", PaperUI.Ink, 5f);
        RectTransform r = f.rectTransform;
        r.anchorMin = src.rectTransform.anchorMin; r.anchorMax = src.rectTransform.anchorMax;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = src.rectTransform.rect.size;
        r.position = src.rectTransform.TransformPoint(src.rectTransform.rect.center);
        f.gameObject.AddComponent<FallingScrap>();
    }

    // =====================================================================
    //  Building the three slips
    // =====================================================================
    void Build()
    {
        CanvasGroup g = gameObject.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false; g.interactable = false;

        // ---------- warning: amber, 401 x 150, bottom-left ----------
        warn = NewSlip("Warning", 401f, 150f, false);
        PaperShape wEdge = PaperUI.Shape(warn.root, "Tape Edge", PaperUI.Cream);
        PaperUI.Place(wEdge.rectTransform, -14f, -10f, 74f, 24f); PaperUI.Rotate(wEdge.rectTransform, -18f);
        wEdge.edge = 3f; wEdge.drawFill = false;
        warn.paper = PaperUI.Cut(PaperUI.Torn(warn.root, "Paper", PaperUI.Amber, PaperUI.WarningTear));
        PaperUI.Stretch(warn.paper.rectTransform);
        PaperShape wTape = PaperUI.Shape(warn.root, "Tape", PaperUI.Hex("f6ead0", 0.85f));
        PaperUI.Place(wTape.rectTransform, -14f, -10f, 74f, 24f); PaperUI.Rotate(wTape.rectTransform, -18f);
        Badge(warn, 24f, 26f, PaperUI.Ink);
        Words(warn, 106f, 20f, 401f - 106f - 22f, 26f, PaperUI.Ink, 16f, PaperUI.Hex("3a2a1a"));

        // pips + counter along the bottom: 273 wide = 5 pips + 5 gaps of 8 + 6 + the 52-wide counter
        float rowX = 106f, rowW = 401f - 106f - 22f, cy = 150f - 18f - 15f;
        float pipW = (rowW - 52f - 6f - 8f * 5f) / 5f;
        for (int i = 0; i < 5; i++)
        {
            pips[i] = PaperUI.Shape(warn.root, "Pip " + (i + 1), PaperUI.Ink, 5f);
            PaperUI.Place(pips[i].rectTransform, rowX + i * (pipW + 8f), cy - 5f, pipW, 10f);
        }
        PaperShape pill = PaperUI.Shape(warn.root, "Counter", PaperUI.Ink, 15f);
        PaperUI.Place(pill.rectTransform, rowX + rowW - 52f, cy - 15f, 52f, 30f);
        pill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        pill.rectTransform.anchoredPosition += new Vector2(26f, -15f);
        counter = PaperUI.Text(pill.transform, "Seconds", "5s", 17f, PaperUI.Amber, true, TextAlignmentOptions.Center);
        PaperUI.Stretch(counter.rectTransform);
        counter.rectTransform.offsetMax = new Vector2(0f, -1f);

        // ---------- sealed / danger: red, same spot ----------
        red = NewSlip("Sealed", 401f, 150f, false);
        red.paper = PaperUI.Cut(PaperUI.Torn(red.root, "Paper", PaperUI.Red, PaperUI.SealedTear));
        PaperUI.Stretch(red.paper.rectTransform);
        Badge(red, 24f, 26f, PaperUI.Cream);
        Words(red, 106f, 20f, 401f - 106f - 22f, 26f, PaperUI.Light, 16f, PaperUI.Hex("ffd9d2"));
        redStamp = PaperUI.Rect("SEALED Stamp", red.root);
        PaperShape sb = PaperUI.Shape(redStamp, "Border", PaperUI.Light, 8f);
        sb.ring = 4f;
        PaperUI.Stretch(sb.rectTransform);
        TextMeshProUGUI st = PaperUI.Text(redStamp, "Text", "SEALED", 26f, PaperUI.Light, true, TextAlignmentOptions.Center);
        PaperUI.LetterSpacing(st, 4f);
        float sw = st.preferredWidth + 28f + 8f + 4f, sh = StampHeight;
        st.alignment = TextAlignmentOptions.TopLeft;
        PaperUI.Place(st.rectTransform, 18f, 8f, sw, 44f);
        PaperUI.Place(redStamp, 401f - 26f - sw, 150f - 18f - sh, sw, sh);
        PaperUI.Rotate(redStamp, -8f);

        // ---------- announcement: black with a gold foil line, same size and corner as the others ----------
        news = NewSlip("Announcement", 401f, 150f, false);
        news.paper = PaperUI.Cut(PaperUI.Torn(news.root, "Paper", PaperUI.Ink, PaperUI.NewsTear));
        PaperUI.Stretch(news.paper.rectTransform);
        PaperShape foil = PaperUI.Shape(news.root, "Gold Foil", PaperUI.Gold, 2f);
        PaperUI.Place(foil.rectTransform, 14f, 12f, 401f - 28f, 4f);
        Badge(news, 24f, 26f, PaperUI.Gold);
        news.icon.sprite = PaperUI.Icon("cleaver");
        news.icon.rectTransform.sizeDelta = new Vector2(52f, 36f);
        Words(news, 106f, 24f, 401f - 106f - 22f, 21f, PaperUI.Gold, 16f, PaperUI.Hex("e8dcc4"));

        warn.root.gameObject.SetActive(false);
        red.root.gameObject.SetActive(false);
        news.root.gameObject.SetActive(false);
    }

    Slip NewSlip(string name, float w, float h, bool centreTop)
    {
        Slip s = new Slip();
        s.root = PaperUI.Rect(name, transform);
        if (centreTop)
        {
            s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 1f);
            s.root.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            s.root.anchorMin = s.root.anchorMax = Vector2.zero;
            s.root.pivot = Vector2.zero;
        }
        s.root.sizeDelta = new Vector2(w, h);
        s.group = s.root.gameObject.AddComponent<CanvasGroup>();
        s.group.alpha = 0f;
        return s; // (switched off at the end of Build, once its text has been measured)
    }

    void Badge(Slip s, float x, float y, Color colour)
    {
        s.badge = PaperUI.Shape(s.root, "Badge", colour, 32f);
        PaperUI.Place(s.badge.rectTransform, x, y, 64f, 64f);
        s.icon = PaperUI.Img(s.badge.transform, "Icon", PaperUI.Icon("fire2"));
        s.icon.rectTransform.anchorMin = s.icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        s.icon.rectTransform.sizeDelta = new Vector2(36f, 38f);
    }

    // title (CSS line-height 1.1, or 1.15 for the long announcement) and the message under it (16 px, line-height 1.35)
    void Words(Slip s, float x, float y, float w, float titleSize, Color titleColour, float msgSize, Color msgColour)
    {
        bool longTitle = s == news;
        s.title = PaperUI.Text(s.root, "Title", "", titleSize, titleColour);
        PaperUI.LetterSpacing(s.title, 0.5f);
        s.title.textWrappingMode = TextWrappingModes.Normal;
        s.titleTop = y;
        s.titleLine = longTitle ? 1.15f : 1.1f;
        float up = PaperUI.LineHeight(s.title, s.titleLine);
        PaperUI.Place(s.title.rectTransform, x, y - up, w, titleSize * 2.5f);
        s.message = PaperUI.Text(s.root, "Message", "", msgSize, msgColour, false);
        s.message.textWrappingMode = TextWrappingModes.Normal;
        s.messageUp = PaperUI.LineHeight(s.message, 1.35f);
        s.messageGap = longTitle ? 6f : 4f;
        PaperUI.Place(s.message.rectTransform, x, y + titleSize * s.titleLine + s.messageGap - s.messageUp, w, 70f);
    }

    void OnDestroy() { if (instance == this) instance = null; }
}

// a scrap of paper that drops off and fades (a used-up countdown pip)
public class FallingScrap : MonoBehaviour
{
    float t;
    float spin;
    void Start() { spin = Random.Range(-260f, 260f); }
    void Update()
    {
        t += Time.unscaledDeltaTime;
        RectTransform r = (RectTransform)transform;
        r.anchoredPosition += new Vector2(8f, -60f - 500f * t) * Time.unscaledDeltaTime;
        r.localEulerAngles += new Vector3(0f, 0f, spin * Time.unscaledDeltaTime);
        Graphic g = GetComponent<Graphic>();
        if (g != null) { Color c = g.color; c.a = Mathf.Clamp01(1f - t / 0.6f); g.color = c; }
        if (t > 0.6f) Destroy(gameObject);
    }
}