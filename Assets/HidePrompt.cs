using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One shared on-screen prompt for every hiding spot:  [H] to hide
// A dark paper scrap with a chunky paper keycap (the paper-craft alert style).
// It follows a point on the hiding spot and draws on top of walls and furniture,
// so it reads the same in first person and third person.
public class HidePrompt : MonoBehaviour
{
    public enum Mode { Hide, ComeOut, Occupied, Stab }

    private static HidePrompt instance;
    public static HidePrompt Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HidePrompt");
                instance = go.AddComponent<HidePrompt>();
                instance.Build();
            }
            return instance;
        }
    }

    // Called by a hiding spot while the player is inside its trigger
    public void Show(Object owner, Vector3 worldPosition, Mode newMode)
    {
        this.owner = owner;
        worldPos = worldPosition;
        wanted = true;
        if (newMode != mode || !modeApplied) ApplyMode(newMode);
    }

    // Called by a hiding spot when the player leaves (only the spot that is showing can hide it)
    public static void Hide(Object owner)
    {
        if (instance != null && instance.owner == owner) instance.wanted = false;
    }

    // ---------------------------------------------------------------
    private Object owner;
    private Vector3 worldPos;
    private bool wanted = false;
    private Mode mode = Mode.Hide;
    private bool modeApplied = false;

    private CanvasGroup group;
    private RectTransform canvasRect;
    private RectTransform panel;
    private HorizontalLayoutGroup row;
    private TextMeshProUGUI actionText, messageText;
    private GameObject keyCap, lockIcon;
    private RectTransform keyFace;
    private float shownAt;

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // under the start menu (100)
        PaperUI.Scaler(gameObject.AddComponent<CanvasScaler>());
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        canvasRect = gameObject.GetComponent<RectTransform>();

        // dark paper scrap that sizes itself to its content
        PaperShape scrap = PaperUI.Cut(PaperUI.Shape(transform, "Panel", PaperUI.CardDark, 14f));
        panel = scrap.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0f);   // sits just above the point it follows

        row = scrap.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        ContentSizeFitter fit = scrap.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        keyCap = PaperUI.KeyCap(panel, "H", false).gameObject;
        keyFace = (RectTransform)keyCap.transform.Find("Face");
        actionText = PaperUI.Text(panel, "Action", "to hide", 21f, PaperUI.Light, true, TextAlignmentOptions.Left);

        Image lk = PaperUI.Img(panel, "Lock", PaperUI.Icon("lock_grey"));
        LayoutElement le = lk.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 22f; le.preferredHeight = 24f;
        lockIcon = lk.gameObject;
        messageText = PaperUI.Text(panel, "Message", "A player is hiding here", 19f, PaperUI.Hex("cbbdc8"), true, TextAlignmentOptions.Left);

        ApplyMode(Mode.Hide);
    }

    void ApplyMode(Mode newMode)
    {
        mode = newMode;
        modeApplied = true;

        bool taken = newMode == Mode.Occupied;
        keyCap.SetActive(!taken);
        actionText.gameObject.SetActive(!taken);
        lockIcon.SetActive(taken);
        messageText.gameObject.SetActive(taken);

        // the board's padding: key prompt 10 22 10 12 / gap 14, the 'taken' one 12 22 10 16 / gap 12
        row.padding = taken ? new RectOffset(16, 22, 12, 10) : new RectOffset(12, 22, 10, 10);
        row.spacing = taken ? 12f : 14f;
        actionText.text = newMode == Mode.ComeOut ? "to come out" : (newMode == Mode.Stab ? "to open and stab" : "to hide");
        LayoutRebuilder.MarkLayoutForRebuild(panel);
    }

    void Update()
    {
        float target = wanted ? 1f : 0f;
        if (wanted && group.alpha <= 0.001f) shownAt = Time.unscaledTime;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 8f);
        // pops up a little when it appears
        float t = (Time.unscaledTime - shownAt) / 0.25f;
        panel.localScale = Vector3.one * (t < 1f ? Mathf.LerpUnclamped(0.85f, 1f, PaperUI.EaseOutBack(t)) : 1f);

        // the key cap presses down now and then so it reads as a button
        if (keyFace != null && keyCap.activeSelf)
        {
            float p = Mathf.Repeat(Time.unscaledTime, 1.2f);
            float press = p < 0.18f ? Mathf.Sin(p / 0.18f * Mathf.PI) : 0f;
            keyFace.anchoredPosition = new Vector2(0f, -4f * press);
        }
    }

    void LateUpdate()
    {
        if (group.alpha <= 0.001f) return;

        Camera cam = Camera.main;
        Vector2 local = new Vector2(0f, -220f);   // fallback: low and centred on screen

        if (cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z > 0f)
            {
                Vector2 lp;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(sp.x, sp.y), null, out lp))
                {
                    // keep the whole panel on screen
                    Vector2 half = canvasRect.rect.size * 0.5f;
                    float halfW = panel.rect.width * 0.5f + 14f;
                    lp.x = Mathf.Clamp(lp.x, -half.x + halfW, half.x - halfW);
                    lp.y = Mathf.Clamp(lp.y, -half.y + 28f, half.y - panel.rect.height - 28f);
                    local = lp;
                }
            }
        }

        panel.anchoredPosition = local;
    }
}
