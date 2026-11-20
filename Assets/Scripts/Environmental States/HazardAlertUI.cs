using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The notification bar in the bottom-left corner for environmental states. Everyone sees it.
// Warning: amber bar with a countdown. Sealed: red bar for a few seconds, then it slides away.
public class HazardAlertUI : MonoBehaviour
{
    static HazardAlertUI instance;

    RectTransform panel;
    Image background, accent, barFill, badge;
    TextMeshProUGUI title, message, countdown, badgeText;
    CanvasGroup group;

    float countdownEnd, countdownLength;
    bool counting;
    float hideAt = -1f;
    float slide;          // 0 = hidden (off to the left), 1 = shown
    bool shown;

    static readonly Color Amber = new Color(1f, 0.72f, 0.12f);
    static readonly Color Danger = new Color(0.95f, 0.22f, 0.18f);

    public static void ShowWarning(string titleText, string messageText, float seconds)
    {
        HazardAlertUI ui = Get(); if (ui == null) return;
        ui.Set(titleText, messageText, Amber, new Color(0.10f, 0.08f, 0.04f, 0.92f));
        ui.countdownLength = Mathf.Max(0.1f, seconds);
        ui.countdownEnd = Time.time + seconds;
        ui.counting = true;
        ui.hideAt = -1f;
        ui.barFill.transform.parent.gameObject.SetActive(true);
        ui.countdown.gameObject.SetActive(true);
    }

    public static void ShowSealed(string titleText, string messageText)
    {
        HazardAlertUI ui = Get(); if (ui == null) return;
        ui.Set(titleText + " - SEALED", messageText, Danger, new Color(0.14f, 0.04f, 0.04f, 0.92f));
        ui.counting = false;
        ui.barFill.transform.parent.gameObject.SetActive(false);
        ui.countdown.gameObject.SetActive(false);
        ui.hideAt = Time.time + 4f;
    }

    static HazardAlertUI Get()
    {
        if (instance != null) return instance;
        PlayerHUD hud = FindFirstObjectByType<PlayerHUD>();
        Canvas canvas = (hud != null && hud.bagIcon != null) ? hud.bagIcon.canvas.rootCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null) return null;
        GameObject g = new GameObject("HazardAlert", typeof(RectTransform));
        g.transform.SetParent(canvas.transform, false);
        instance = g.AddComponent<HazardAlertUI>();
        instance.Build(hud != null && hud.nameText != null ? hud.nameText.font : null);
        return instance;
    }

    void Set(string t, string m, Color accentColor, Color bg)
    {
        title.text = t.ToUpperInvariant();
        message.text = m;
        title.color = accentColor;
        accent.color = accentColor;
        badge.color = accentColor;
        barFill.color = accentColor;
        background.color = bg;
        shown = true;
        transform.SetAsLastSibling(); // on top of the HUD
    }

    void Update()
    {
        if (counting)
        {
            float left = Mathf.Max(0f, countdownEnd - Time.time);
            countdown.text = left > 0f ? "Sealing in " + left.ToString("0.0") + " s" : "Sealing...";
            barFill.rectTransform.anchorMax = new Vector2(left / countdownLength, 1f);
            // urgent pulse
            float p = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * (left < 2f ? 9f : 5f)));
            Color c = accent.color; c.a = p; accent.color = c; badge.color = c;
        }
        if (hideAt > 0f && Time.time >= hideAt) { shown = false; hideAt = -1f; }

        slide = Mathf.MoveTowards(slide, shown ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        float e = 1f - (1f - slide) * (1f - slide);
        panel.anchoredPosition = new Vector2(Mathf.Lerp(-560f, 24f, e), 24f);
        group.alpha = e;
    }

    // ---------- building the bar ----------
    void Build(TMP_FontAsset font)
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;

        panel = NewRect("Panel", root);
        panel.anchorMin = panel.anchorMax = Vector2.zero;
        panel.pivot = Vector2.zero;
        panel.sizeDelta = new Vector2(520f, 118f);
        background = panel.gameObject.AddComponent<Image>();
        background.raycastTarget = false;

        RectTransform acc = NewRect("Accent", panel);
        acc.anchorMin = new Vector2(0f, 0f); acc.anchorMax = new Vector2(0f, 1f);
        acc.pivot = new Vector2(0f, 0.5f); acc.sizeDelta = new Vector2(8f, 0f);
        accent = acc.gameObject.AddComponent<Image>(); accent.raycastTarget = false;

        RectTransform bd = NewRect("Badge", panel);
        bd.anchorMin = bd.anchorMax = new Vector2(0f, 1f); bd.pivot = new Vector2(0f, 1f);
        bd.anchoredPosition = new Vector2(22f, -14f); bd.sizeDelta = new Vector2(30f, 30f);
        badge = bd.gameObject.AddComponent<Image>(); badge.raycastTarget = false;
        badgeText = Text("!", bd, font, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        badgeText.color = new Color(0.1f, 0.06f, 0.02f);
        Stretch(badgeText.rectTransform);

        title = Text("", panel, font, 26f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(62f, -14f); tr.sizeDelta = new Vector2(-80f, 32f);

        message = Text("", panel, font, 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        message.color = new Color(0.95f, 0.95f, 0.95f);
        message.enableWordWrapping = true;
        RectTransform mr = message.rectTransform;
        mr.anchorMin = new Vector2(0f, 1f); mr.anchorMax = new Vector2(1f, 1f); mr.pivot = new Vector2(0f, 1f);
        mr.anchoredPosition = new Vector2(24f, -50f); mr.sizeDelta = new Vector2(-40f, 46f);

        countdown = Text("", panel, font, 17f, FontStyles.Bold, TextAlignmentOptions.BottomRight);
        countdown.color = Color.white;
        RectTransform cr = countdown.rectTransform;
        cr.anchorMin = new Vector2(0f, 0f); cr.anchorMax = new Vector2(1f, 0f); cr.pivot = new Vector2(1f, 0f);
        cr.anchoredPosition = new Vector2(-16f, 14f); cr.sizeDelta = new Vector2(-40f, 22f);

        RectTransform track = NewRect("Bar", panel);
        track.anchorMin = new Vector2(0f, 0f); track.anchorMax = new Vector2(1f, 0f); track.pivot = new Vector2(0f, 0f);
        track.anchoredPosition = new Vector2(24f, 8f); track.sizeDelta = new Vector2(-170f, 5f);
        Image tbg = track.gameObject.AddComponent<Image>(); tbg.color = new Color(1f, 1f, 1f, 0.15f); tbg.raycastTarget = false;
        RectTransform fill = NewRect("Fill", track);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
        barFill = fill.gameObject.AddComponent<Image>(); barFill.raycastTarget = false;
    }

    static RectTransform NewRect(string n, Transform parent)
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return (RectTransform)g.transform;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static TextMeshProUGUI Text(string s, Transform parent, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align)
    {
        RectTransform r = NewRect("Text", parent);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = s; t.fontSize = size; t.fontStyle = style; t.alignment = align;
        t.raycastTarget = false; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    void OnDestroy() { if (instance == this) instance = null; }
}
