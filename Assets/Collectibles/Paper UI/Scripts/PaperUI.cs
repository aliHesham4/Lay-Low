using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Shared pieces of the paper-craft UI (HUD cards, alerts, toasts, prompts, hints, the clock):
// the palette, the torn outlines from the design boards, the Baloo 2 fonts, the icons and small builders.
// Positions in the builders are CSS-like: x, y from the parent's top-left corner, in the 1280 x 720 design space.
public static class PaperUI
{
    // ---------------- palette ----------------
    public static readonly Color Ink = Hex("1d1322");
    public static readonly Color CardDark = Hex("2a1d24");
    public static readonly Color Cream = Hex("f6ead0");
    public static readonly Color CutEdge = Hex("f1e4c6");
    public static readonly Color Light = Hex("fff1d6");
    public static readonly Color Red = Hex("c8171f");
    public static readonly Color Gold = Hex("f6c443");
    public static readonly Color Amber = Hex("ffc21a");
    public static readonly Color KeyBase = Hex("d8c7a4");
    public static readonly Color Shadow = new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.5f);
    public static readonly Color[] PlayerColours = { Hex("338cff"), Hex("ff4040"), Hex("4de659"), Hex("ff59cc") };

    public static Color Hex(string hex, float alpha = 1f)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        c.a = alpha;
        return c;
    }

    // ---------------- torn outlines (the boards' clip-path polygons, in %) ----------------
    public static readonly Vector2[] CardTear = P(0, 4, 20, 0, 46, 3, 72, 0, 100, 3, 99, 50, 100, 97, 74, 100, 50, 96, 24, 100, 0, 97, 1, 50);
    public static readonly Vector2[] WarningTear = P(0, 6, 12, 0, 38, 4, 64, 0, 88, 5, 100, 1, 99, 50, 100, 96, 72, 100, 48, 96, 22, 100, 1, 95, 2, 52);
    public static readonly Vector2[] SealedTear = P(0, 4, 18, 0, 44, 5, 70, 0, 100, 3, 98, 48, 100, 97, 66, 100, 38, 95, 12, 100, 0, 96, 2, 50);
    public static readonly Vector2[] NewsTear = P(0, 5, 22, 0, 50, 4, 78, 0, 100, 5, 98, 50, 100, 95, 74, 100, 50, 96, 24, 100, 0, 95, 2, 50);
    public static readonly Vector2[] ToastTear = P(2, 0, 98, 4, 100, 22, 97, 42, 100, 62, 96, 80, 99, 100, 3, 96, 0, 78, 3, 58, 0, 38, 2, 18);
    public static readonly Vector2[] TagTear = P(0, 6, 18, 0, 44, 5, 70, 0, 100, 5, 98, 50, 100, 95, 72, 100, 46, 95, 20, 100, 0, 95, 2, 50);

    static Vector2[] P(params float[] v)
    {
        Vector2[] p = new Vector2[v.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Vector2(v[i * 2] / 100f, v[i * 2 + 1] / 100f);
        return p;
    }

    // ---------------- fonts (Baloo 2, made into TextMeshPro fonts the first time they're needed) ----------------
    static TMP_FontAsset bold, semi, title;
    // Luckiest Guy, for the big paper-cut headlines (extra padding so the thick outline fits)
    public static TMP_FontAsset Title { get { if (title == null) title = MakeFont("LuckiestGuy", 16); return title; } }
    public static TMP_FontAsset Bold { get { if (bold == null) bold = MakeFont("Baloo2-800"); return bold; } }
    public static TMP_FontAsset Semi { get { if (semi == null) semi = MakeFont("Baloo2-600"); return semi; } }

    static TMP_FontAsset MakeFont(string file, int padding = 9)
    {
        Font f = Resources.Load<Font>("PaperUI/" + file);
        if (f == null) return TMP_Settings.defaultFontAsset;
        TMP_FontAsset a = TMP_FontAsset.CreateFontAsset(f, 90, padding, GlyphRenderMode.SDFAA, 1024, 1024);
        a.name = file;
        return a;
    }

    // ---------------- icons (Assets/Collectibles/Paper UI/Resources/PaperUI) ----------------
    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string name)
    {
        Sprite s;
        if (icons.TryGetValue(name, out s) && s != null) return s;
        s = Resources.Load<Sprite>("PaperUI/" + name);
        icons[name] = s;
        return s;
    }

    // ---------------- the HUD canvas (cards, alerts, toasts, clock) ----------------
    static Canvas hud;
    public static Canvas HudCanvas
    {
        get
        {
            if (hud != null) return hud;
            GameObject g = GameObject.Find("PaperHUD");
            if (g == null)
            {
                g = new GameObject("PaperHUD", typeof(Canvas), typeof(CanvasScaler));
                Canvas c = g.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 45; // over the world, under the round results (50) and the hiding prompt (90)
                Scaler(g.GetComponent<CanvasScaler>());
            }
            hud = g.GetComponent<Canvas>();
            return hud;
        }
    }

    // the boards are drawn for a 1280-wide screen; scaling by width keeps 4 cards fitting on any screen
    public static void Scaler(CanvasScaler s)
    {
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1280f, 720f);
        s.matchWidthOrHeight = 0f;
    }

    // ---------------- builders ----------------
    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.layer = 5; // UI
        g.transform.SetParent(parent, false);
        return (RectTransform)g.transform;
    }

    // CSS-like placement inside the parent: left/top corner at (x, y), size w x h
    public static RectTransform Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    // places it at the same spot but turns it around its own centre (CSS 'transform: rotate')
    public static void Rotate(RectTransform r, float cssDegrees)
    {
        Vector2 size = r.sizeDelta;
        Vector2 topLeft = r.anchoredPosition;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = topLeft + new Vector2(size.x * 0.5f, -size.y * 0.5f);
        r.localEulerAngles = new Vector3(0f, 0f, -cssDegrees);
    }

    public static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    public static PaperShape Shape(Transform parent, string name, Color colour, float radius = 0f)
    {
        RectTransform r = Rect(name, parent);
        PaperShape s = r.gameObject.AddComponent<PaperShape>();
        s.color = colour;
        s.radius = radius;
        s.raycastTarget = false;
        return s;
    }

    public static PaperShape Torn(Transform parent, string name, Color colour, Vector2[] tear)
    {
        PaperShape s = Shape(parent, name, colour);
        s.kind = PaperShape.Kind.Polygon;
        s.points = tear;
        return s;
    }

    // the cream cut edge + hard drop shadow every paper scrap gets (the boards' '.cut' filter)
    public static PaperShape Cut(PaperShape s, bool withShadow = true)
    {
        s.edge = 3f;
        s.edgeColor = CutEdge;
        s.shadow = withShadow;
        s.shadowOffset = new Vector2(5f, -8f);
        s.shadowColor = Shadow;
        s.Refresh();
        return s;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color? tint = null)
    {
        RectTransform r = Rect(name, parent);
        Image i = r.gameObject.AddComponent<Image>();
        i.sprite = sprite;
        i.preserveAspect = true;
        i.raycastTarget = false;
        i.color = tint ?? Color.white;
        return i;
    }

    // an icon with the boards' hard 'drop-shadow(2px 3px 0 ...)' under it; returns the icon (the shadow is its first child)
    // (place it with the returned image's parent; change the picture with SetIcon so the shadow follows)
    public static Image IconWithShadow(Transform parent, string name, Sprite sprite, Vector2 shadowOffset, float shadowAlpha)
    {
        RectTransform r = Rect(name, parent);
        Image sh = Img(r, "Shadow", sprite, new Color(6f / 255f, 2f / 255f, 10f / 255f, shadowAlpha));
        Stretch(sh.rectTransform);
        sh.rectTransform.anchoredPosition = shadowOffset;
        Image top = Img(r, "Icon", sprite);
        Stretch(top.rectTransform);
        return top;
    }

    public static void SetIcon(Image top, Sprite sprite)
    {
        top.sprite = sprite;
        Image sh = top.transform.parent.GetChild(0).GetComponent<Image>();
        if (sh != null && sh != top) sh.sprite = sprite;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color colour, bool bold = true,
                                       TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        RectTransform r = Rect(name, parent);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = bold ? Bold : Semi;
        t.text = text;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero;
        return t;
    }

    // Baloo 2's normal line height is 1.602 em (the same in CSS and TextMeshPro).
    // Sets a CSS line-height (in em) and returns how far to move the text box up so the first line
    // sits where the browser puts it (CSS splits the missing leading above and below the line).
    public const float NormalLine = 1.602f;
    public static float LineHeight(TextMeshProUGUI t, float cssLineHeight)
    {
        t.lineSpacing = (cssLineHeight - NormalLine) * 100f;
        return (NormalLine - cssLineHeight) * 0.5f * t.fontSize;
    }

    // CSS letter-spacing in px -> TextMeshPro character spacing
    // (kerning is switched off on spaced text: with kerning on, TextMeshPro drops the spacing between kerned pairs)
    public static void LetterSpacing(TextMeshProUGUI t, float px) { t.characterSpacing = px / t.fontSize * 100f; t.fontFeatures.Clear(); }

    // ---------------- keycaps ----------------
    // A chunky paper key: ink outline, cream face, darker lip at the bottom. 'small' is the hint-card size.
    public static RectTransform KeyCap(Transform parent, string key, bool small)
    {
        float h = small ? 30f : 44f, rad = small ? 7f : 10f, lip = small ? 4f : 6f, line = small ? 2f : 3f;
        float font = small ? 15f : 22f, pad = small ? 8f : 10f;

        RectTransform root = Rect("Key " + key, parent);
        TextMeshProUGUI measure = Text(root, "Letter", key, font, Ink, true, TextAlignmentOptions.Center);
        float w = Mathf.Max(h, measure.preferredWidth + pad * 2f);
        root.sizeDelta = new Vector2(w, h);
        LayoutElement le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = w;
        le.preferredHeight = le.minHeight = h;

        PaperShape shadow = Shape(root, "Shadow", new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.45f), rad + line);
        Stretch(shadow.rectTransform, -line);
        shadow.rectTransform.anchoredPosition = small ? new Vector2(3f, -3f) : new Vector2(4f, -5f);
        PaperShape outline = Shape(root, "Outline", Ink, rad + line);
        Stretch(outline.rectTransform, -line);
        PaperShape lipShape = Shape(root, "Lip", KeyBase, rad);
        Stretch(lipShape.rectTransform);
        PaperShape face = Shape(root, "Face", Cream, rad);
        face.rectTransform.anchorMin = new Vector2(0f, 1f); face.rectTransform.anchorMax = new Vector2(1f, 1f);
        face.rectTransform.pivot = new Vector2(0.5f, 1f);
        face.rectTransform.sizeDelta = new Vector2(0f, h - lip);
        face.rectTransform.anchoredPosition = Vector2.zero;

        measure.transform.SetParent(face.transform, false);
        measure.transform.SetAsLastSibling();
        Stretch(measure.rectTransform);
        measure.rectTransform.offsetMin = new Vector2(0f, -lip * 0.5f); // centre on the whole key, like the board
        measure.rectTransform.offsetMax = new Vector2(0f, -(small ? 1f : 1.5f));
        return root;
    }

    // ---------------- item-aiming hint (cream card, centre-bottom) ----------------
    // "Click a doorway to slam it shut" + [E] [Esc] or right-click to cancel. Shown/hidden with SetActive.
    public static GameObject Hint(Transform canvas, string title, params string[] cancelKeys)
    {
        RectTransform root = Rect("PaperHint", canvas);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        // the canvas may be an old 1920-wide one: keep the card the board's size on screen
        CanvasScaler cs = canvas.GetComponentInParent<CanvasScaler>();
        float k = 1f;
        if (cs != null && cs.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) k = cs.referenceResolution.x / 1280f;
        root.localScale = Vector3.one * k;
        root.anchoredPosition = new Vector2(0f, 40f * k);

        PaperShape card = Cut(Shape(root, "Card", Cream, 10f));
        Stretch(card.rectTransform);
        card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        VerticalLayoutGroup v = root.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(22, 22, 14, 12);
        v.spacing = 8f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        ContentSizeFitter fit = root.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text(root, "Title", title, 21f, Ink);

        RectTransform row = Rect("Cancel", root);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        h.padding = new RectOffset(2, 0, 2, 2);
        foreach (string key in cancelKeys) KeyCap(row, key, true);
        Text(row, "Or", "or right-click to cancel", 15f, Hex("6e5f52"), false, TextAlignmentOptions.Left);

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    // ---------------- easing ----------------
    public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
