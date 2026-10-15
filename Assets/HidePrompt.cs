using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One shared on-screen prompt for every hiding spot:  Press [H] to hide
// It follows a point on the hiding spot, always faces the player, and draws on top of walls and furniture,
// so it reads the same in first person and third person.
public class HidePrompt : MonoBehaviour
{
    public enum Mode { Hide, ComeOut, Occupied }

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
    private Image panelImage;
    private TextMeshProUGUI preText, postText, keyText, messageText;
    private GameObject keyCap;
    private RectTransform keyFace;

    private static readonly Color PanelNormal = new Color(0.06f, 0.07f, 0.10f, 0.86f);
    private static readonly Color PanelTaken = new Color(0.30f, 0.07f, 0.07f, 0.90f);
    private static readonly Color WarnText = new Color(1f, 0.82f, 0.4f);

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // under the start menu (100)
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        canvasRect = gameObject.GetComponent<RectTransform>();

        // --- rounded dark panel that sizes itself to its content ---
        GameObject p = new GameObject("Panel", typeof(RectTransform));
        panel = p.GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0f);   // sits just above the point it follows
        panelImage = p.AddComponent<Image>();
        panelImage.sprite = Rounded();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = PanelNormal;
        panelImage.raycastTarget = false;

        HorizontalLayoutGroup row = p.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 14f;
        row.padding = new RectOffset(26, 26, 14, 14);
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        ContentSizeFitter fit = p.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        preText = MakeText("Pre", panel, "Press", 34, Color.white, FontStyles.Bold);
        keyCap = MakeKeyCap(panel);
        postText = MakeText("Post", panel, "to hide", 34, Color.white, FontStyles.Bold);
        messageText = MakeText("Message", panel, "A player is hiding here", 34, WarnText, FontStyles.Bold);

        ApplyMode(Mode.Hide);
    }

    void ApplyMode(Mode newMode)
    {
        mode = newMode;
        modeApplied = true;

        bool taken = newMode == Mode.Occupied;
        preText.gameObject.SetActive(!taken);
        keyCap.SetActive(!taken);
        postText.gameObject.SetActive(!taken);
        messageText.gameObject.SetActive(taken);

        panelImage.color = taken ? PanelTaken : PanelNormal;
        postText.text = newMode == Mode.ComeOut ? "to come out" : "to hide";
    }

    void Update()
    {
        float target = wanted ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 8f);

        // the key cap "presses" down and up so it reads as a button
        if (keyFace != null && keyCap.activeSelf)
        {
            float press = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            keyFace.anchoredPosition = new Vector2(0f, -4f * press);
        }
    }

    void LateUpdate()
    {
        if (group.alpha <= 0.001f) return;

        Camera cam = Camera.main;
        Vector2 local = new Vector2(0f, -330f);   // fallback: low and centred on screen

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
                    float halfW = panel.rect.width * 0.5f + 20f;
                    lp.x = Mathf.Clamp(lp.x, -half.x + halfW, half.x - halfW);
                    lp.y = Mathf.Clamp(lp.y, -half.y + 40f, half.y - panel.rect.height - 40f);
                    local = lp;
                }
            }
        }

        panel.anchoredPosition = local;
    }

    // ---------------------------------------------------------------
    //  UI helpers
    // ---------------------------------------------------------------
    TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    GameObject MakeKeyCap(Transform parent)
    {
        GameObject root = new GameObject("KeyCap", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        LayoutElement le = root.AddComponent<LayoutElement>();
        le.preferredWidth = 68f;
        le.preferredHeight = 74f;

        // darker base = the side of the key
        GameObject baseGo = new GameObject("Base", typeof(RectTransform));
        baseGo.transform.SetParent(root.transform, false);
        RectTransform b = baseGo.GetComponent<RectTransform>();
        b.anchorMin = new Vector2(0f, 0f); b.anchorMax = new Vector2(1f, 0f);
        b.pivot = new Vector2(0.5f, 0f);
        b.sizeDelta = new Vector2(0f, 62f);
        b.anchoredPosition = Vector2.zero;
        Image bi = baseGo.AddComponent<Image>();
        bi.sprite = Rounded(); bi.type = Image.Type.Sliced;
        bi.color = new Color(0.50f, 0.53f, 0.62f);
        bi.raycastTarget = false;

        // light top = the face of the key, with the letter on it
        GameObject faceGo = new GameObject("Face", typeof(RectTransform));
        faceGo.transform.SetParent(root.transform, false);
        keyFace = faceGo.GetComponent<RectTransform>();
        keyFace.anchorMin = new Vector2(0f, 1f); keyFace.anchorMax = new Vector2(1f, 1f);
        keyFace.pivot = new Vector2(0.5f, 1f);
        keyFace.sizeDelta = new Vector2(0f, 62f);
        keyFace.anchoredPosition = Vector2.zero;
        Image fi = faceGo.AddComponent<Image>();
        fi.sprite = Rounded(); fi.type = Image.Type.Sliced;
        fi.color = new Color(0.96f, 0.97f, 0.99f);
        fi.raycastTarget = false;

        keyText = MakeText("Letter", keyFace, "H", 44, new Color(0.12f, 0.13f, 0.18f), FontStyles.Bold);
        RectTransform kt = keyText.rectTransform;
        kt.anchorMin = Vector2.zero; kt.anchorMax = Vector2.one;
        kt.offsetMin = Vector2.zero; kt.offsetMax = Vector2.zero;

        return root;
    }

    // A small rounded-rectangle sprite made in code, so no image assets are needed
    private static Sprite rounded;
    static Sprite Rounded()
    {
        if (rounded != null) return rounded;

        const int size = 64;
        const float radius = 18f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return rounded;
    }
}
