using System.Collections.Generic;
using TMPro;
using UnityEngine;

// "Player 2 was caught!" — a torn notebook strip pinned on with a thumbtack, centred under the HUD row.
// It pins in with a little bounce, stays 2.5 s, then lifts away. Newer toasts push older ones down.
// The gold strip (dark tack) is for golden-knife news.
public class PaperToast : MonoBehaviour
{
    public static void Show(string message, bool gold = false)
    {
        PaperToast t = Get();
        if (t != null) t.Add(message, gold);
    }

    // take every toast off the screen (the scoreboard is coming up)
    public static void ClearAll()
    {
        if (instance == null) return;
        foreach (Strip s in instance.strips) if (s.root != null) Destroy(s.root.gameObject);
        instance.strips.Clear();
    }

    static PaperToast instance;
    static PaperToast Get()
    {
        if (instance != null) return instance;
        Canvas c = PaperUI.HudCanvas;
        if (c == null) return null;
        RectTransform r = PaperUI.Rect("Toasts", c.transform);
        PaperUI.Stretch(r);
        instance = r.gameObject.AddComponent<PaperToast>();
        return instance;
    }

    class Strip { public RectTransform root; public CanvasGroup group; public float born; public float y; public float h; }
    readonly List<Strip> strips = new List<Strip>();
    public static float Hold = 2.5f; // seconds a toast stays
    const float Gap = 10f;
    static readonly float[] Tilts = { -1.5f, 1f, -1f, 1.5f };
    int count;

    void Add(string message, bool gold)
    {
        Strip s = new Strip();
        s.root = PaperUI.Rect("Toast", transform);
        s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 1f);
        s.root.pivot = new Vector2(0.5f, 0.5f);
        s.group = s.root.gameObject.AddComponent<CanvasGroup>();
        s.group.blocksRaycasts = false;

        TextMeshProUGUI text = PaperUI.Text(s.root, "Text", message, 20f, PaperUI.Ink, true, TextAlignmentOptions.MidlineLeft);
        text.ForceMeshUpdate();
        float w = text.preferredWidth + 46f + 28f, h = 12f + 20f * PaperUI.NormalLine + 10f;
        s.root.sizeDelta = new Vector2(w, h);
        s.h = h;

        PaperShape paper = PaperUI.Cut(PaperUI.Torn(s.root, "Paper", gold ? PaperUI.Gold : PaperUI.Cream, PaperUI.ToastTear));
        PaperUI.Stretch(paper.rectTransform);
        paper.transform.SetAsFirstSibling();
        PaperShape margin = PaperUI.Shape(s.root, "Margin", gold ? PaperUI.Hex("1d1322", 0.25f) : PaperUI.Hex("c8171f", 0.3f));
        margin.rectTransform.anchorMin = new Vector2(0f, 0f); margin.rectTransform.anchorMax = new Vector2(0f, 1f);
        margin.rectTransform.pivot = new Vector2(0f, 0.5f);
        margin.rectTransform.sizeDelta = new Vector2(2f, 0f);
        margin.rectTransform.anchoredPosition = new Vector2(34f, 0f);
        margin.transform.SetSiblingIndex(1);
        UnityEngine.UI.Image tack = PaperUI.Img(s.root, "Thumbtack", PaperUI.Icon(gold ? "tack_ink" : "tack_red"));
        PaperUI.Place(tack.rectTransform, 10f, h * 0.5f - 9f, 24f, 24f);

        text.alignment = TextAlignmentOptions.TopLeft;
        PaperUI.Place(text.rectTransform, 46f, 12f, w - 46f, h);

        s.root.localEulerAngles = new Vector3(0f, 0f, -Tilts[count++ % Tilts.Length]);
        s.born = Time.unscaledTime;
        strips.Insert(0, s);
        while (strips.Count > 3) { Destroy(strips[strips.Count - 1].root.gameObject); strips.RemoveAt(strips.Count - 1); }
        transform.SetAsLastSibling();
    }

    void Update()
    {
        float top = 124f;
        float y = top;
        for (int i = 0; i < strips.Count; i++)
        {
            Strip s = strips[i];
            float age = Time.unscaledTime - s.born;
            if (age > Hold + 0.35f) { Destroy(s.root.gameObject); strips.RemoveAt(i); i--; continue; }

            float target = y + s.h * 0.5f;
            s.y = age < 0.01f ? target : Mathf.Lerp(s.y, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            float pin = PaperUI.EaseOutBack(age / 0.3f);
            float leave = Mathf.Clamp01((age - Hold) / 0.35f);
            s.root.localScale = Vector3.one * Mathf.LerpUnclamped(1.3f, 1f, pin);
            s.group.alpha = Mathf.Clamp01(age / 0.12f) * (1f - leave);
            s.root.anchoredPosition = new Vector2(0f, -s.y + 14f * PaperUI.EaseOut(leave));
            y += s.h + Gap;
        }
    }

    void OnDestroy() { if (instance == this) instance = null; }
}
