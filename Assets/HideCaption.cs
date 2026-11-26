using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A caption at the bottom of the screen while the player hides in a disguise spot
// (the floor lamp: "No one can see me here..."). Same dark paper scrap as the hide prompt.
public class HideCaption : MonoBehaviour
{
    private static HideCaption instance;

    // Called by the hiding spot the player just hid in
    public static void Show(Object owner, string text, string speaker = "")
    {
        if (instance == null)
        {
            GameObject go = new GameObject("HideCaption");
            instance = go.AddComponent<HideCaption>();
            instance.Build();
        }
        instance.owner = owner;
        instance.label.text = text;
        instance.nameTag.SetActive(!string.IsNullOrEmpty(speaker));
        instance.nameText.text = speaker;
        instance.label.maxVisibleCharacters = 0; // typed out one letter at a time (the panel is already sized to the full line)
        instance.wanted = true;
        instance.hideAt = 0f;
        instance.shownAt = Time.unscaledTime;
        LayoutRebuilder.MarkLayoutForRebuild(instance.panel);
    }

    // Shows the caption for a few seconds, then it goes by itself (e.g. an item's story when it is first picked up)
    public static void ShowFor(Object owner, string text, string speaker, float seconds)
    {
        Show(owner, text, speaker);
        instance.hideAt = Time.unscaledTime + seconds;
    }

    // Called by that spot when the player comes out (only the spot showing it can hide it)
    public static void Hide(Object owner)
    {
        if (instance != null && instance.owner == owner) instance.wanted = false;
    }

    // ---------------------------------------------------------------
    private Object owner;
    private bool wanted;
    private float hideAt;   // > 0: hides itself at this time
    private float shownAt;
    private CanvasGroup group;
    private RectTransform panel;
    private TextMeshProUGUI label, nameText;
    private GameObject nameTag;
    const float LettersPerSecond = 28f;
    const float BottomY = 55f;
    // torn edges all round, with the top-right corner cut off where it is folded over (fractions, measured from the top-left)
    static readonly Vector2[] CaptionTear = {
        new Vector2(0f, 0.06f), new Vector2(0.12f, 0f), new Vector2(0.27f, 0.05f), new Vector2(0.44f, 0f), new Vector2(0.6f, 0.04f),
        new Vector2(0.76f, 0f), new Vector2(0.93f, 0.02f), new Vector2(1f, 0.38f), new Vector2(0.985f, 0.55f), new Vector2(1f, 0.72f),
        new Vector2(0.975f, 0.88f), new Vector2(0.99f, 1f), new Vector2(0.82f, 0.96f), new Vector2(0.64f, 1f), new Vector2(0.46f, 0.95f),
        new Vector2(0.28f, 1f), new Vector2(0.1f, 0.96f), new Vector2(0f, 1f), new Vector2(0.02f, 0.8f), new Vector2(0f, 0.62f),
        new Vector2(0.025f, 0.44f), new Vector2(0f, 0.26f) };   // how high the caption sits above the bottom of the screen

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 89;   // just under the hide prompt
        PaperUI.Scaler(gameObject.AddComponent<CanvasScaler>());
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // a torn scrap of cream paper, low in the middle of the screen, sized to the text,
        // with a dog-eared top-right corner
        PaperShape scrap = PaperUI.Cut(PaperUI.Torn(transform, "Caption", PaperUI.Hex("fbf4e4"), CaptionTear));
        scrap.edgeColor = PaperUI.Hex("e2d3b4");
        scrap.Refresh();
        panel = scrap.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, BottomY);

        // the folded-over corner: a darker triangle filling the cut corner
        PaperShape fold = PaperUI.Torn(panel, "Fold", PaperUI.Hex("e9dcc0"), new[] { new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
        fold.shadow = true; fold.shadowOffset = new Vector2(-2f, -3f); fold.shadowColor = new Color(0.2f, 0.12f, 0.08f, 0.35f);
        fold.Refresh();
        RectTransform fr = fold.rectTransform;
        fr.anchorMin = new Vector2(0.93f, 0.62f); fr.anchorMax = new Vector2(1f, 1f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        fold.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        HorizontalLayoutGroup row = scrap.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        row.padding = new RectOffset(28, 28, 24, 14); // room under the name tag
        ContentSizeFitter fit = scrap.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        label = PaperUI.Text(panel, "Text", "", 24f, PaperUI.Hex("2b2427"), true, TextAlignmentOptions.Center);   // charcoal ink

        // the speaker's name on a dark scrap stuck to the top-left edge (like the dialogue box)
        PaperShape tag = PaperUI.Cut(PaperUI.Torn(panel, "Name Tag", PaperUI.CardDark, PaperUI.TagTear));
        RectTransform tr = tag.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 0.5f);
        tr.sizeDelta = new Vector2(170f, 36f); tr.anchoredPosition = new Vector2(18f, 4f);
        tr.localEulerAngles = new Vector3(0f, 0f, 2f);
        tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        nameText = PaperUI.Text(tr, "Name", "", 19f, PaperUI.Light, true, TextAlignmentOptions.Center);
        PaperUI.Stretch(nameText.rectTransform);
        nameTag = tag.gameObject;
        nameTag.SetActive(false);
    }

    void Update()
    {
        if (wanted && hideAt > 0f && Time.unscaledTime > hideAt) { wanted = false; hideAt = 0f; }
        group.alpha = Mathf.MoveTowards(group.alpha, wanted ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        if (PauseMenu.IsOpen) group.alpha = 0f; // never shows through the pause menu
        // rises in and settles, with a slight tilt like a stuck-on paper label
        float t = (Time.unscaledTime - shownAt) / 0.35f;
        float e = t < 1f ? PaperUI.EaseOutBack(t) : 1f;
        panel.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(BottomY - 30f, BottomY, e));
        // typewriter: reveal the line letter by letter
        label.maxVisibleCharacters = Mathf.Min(label.text.Length, Mathf.FloorToInt((Time.unscaledTime - shownAt) * LettersPerSecond));
        panel.localEulerAngles = new Vector3(0f, 0f, -1.2f);
    }
}
