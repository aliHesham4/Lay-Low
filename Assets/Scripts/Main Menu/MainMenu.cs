using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu scene, built from the "Lay Low – Main Menu" design (1280 x 720 reference, CSS-like placement):
//  - Title: the house at night (blurred) with the logo, tickets peeking in from the left edge
//    (Quit, Settings, How to play, Characters), the PLAY marquee, and the clown peeking up from the bottom,
//    who ducks out of sight when the mouse goes over him.
//  - Characters: two windows. The left one holds every character, one per pane, and scrolls up and down;
//    the right one shows the picked character's item-shop picture behind a roller shutter. "Use" puts the
//    IN USE stamp on them and that is who you play as.
//  - Game setup: the four players as framed portraits with editable names, AI / Empty per seat, and the
//    number of hiders; "Start the night" begins the match.
// Settings and How to play are the pause menu's own screens (the PauseMenu on "Menu Sheets", in title mode),
// so every setting changed here is the same saved setting the game uses.
public class MainMenu : MonoBehaviour
{
    [Header("Pictures")]
    public Texture2D titleBackground;   // Assets/Canvas/Main Menu/Title Background.jpg
    public Texture2D menuBackdrop;      // Assets/Canvas/Main Menu/Menu Backdrop.jpg
    public Texture2D clownPeek;         // Assets/Canvas/Main Menu/Clown Peek.png
    public Sprite backArrow;            // Assets/Canvas/Back Arrow.png
    [Tooltip("Close-ups, in roster order: Pyjama Man, Chubby Man, Marly The Bride, Grandpa Will.")]
    public Texture2D[] closeups = new Texture2D[4];
    [Tooltip("Item-shop pictures, same order (leave one empty and its close-up is shown instead).")]
    public Texture2D[] itemShop = new Texture2D[4];

    [Header("Links")]
    public PauseMenu sheets;            // Settings and How to play
    public string gameScene = "SampleScene";
    [Tooltip("Empty panes after the real characters, so the roster window shows it scrolls.")]
    public int comingSoonPanes = 11;

    // palette (pause menu)
    static readonly Color Ink = PaperUI.Hex("1d1322"), Night = PaperUI.Hex("0e070c"), Sheet = PaperUI.Hex("241820");
    static readonly Color Card = PaperUI.Hex("2b1c26"), Stub = PaperUI.Hex("22161e"), Border = PaperUI.Hex("4a3442");
    static readonly Color Dust = PaperUI.Hex("d9b6a0"), Faint = PaperUI.Hex("8d7180"), DustCard = PaperUI.Hex("3a2832");
    static readonly Color DustStub = PaperUI.Hex("2e2028"), RedDeep = PaperUI.Hex("a51018"), GoldDeep = PaperUI.Hex("dba52c");
    static readonly Color Red = PaperUI.Red, Gold = PaperUI.Gold, Cream = PaperUI.Light, Shadow = new Color(0, 0, 0, 0.55f);
    static readonly Color[] SeatColours = { PaperUI.Hex("3b7ee8"), PaperUI.Hex("e0353d"), PaperUI.Hex("3fb35a"), PaperUI.Hex("e04fb3") };

    RectTransform root;
    GameObject titleScr, charsScr, setupScr;
    readonly List<Action> charRefresh = new List<Action>();
    readonly List<Action> setupRefresh = new List<Action>();
    Sprite dashH, dashV, fadeDown;

    // Characters state
    int picked, shown;
    RectTransform shutter;
    RawImage shopPic, shopFace;
    TextMeshProUGUI[] nameLayers;
    Coroutine rolling;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        picked = shown = MatchSetup.Character;
        EnsureEventSystem();
        Build();
        Show(titleScr);
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null || !k.escapeKey.wasPressedThisFrame) return;
        if (sheets != null && sheets.SheetOpen) return; // the sheet closes itself
        if (!titleScr.activeSelf) Show(titleScr);
    }

    void Show(GameObject s)
    {
        foreach (GameObject g in new[] { titleScr, charsScr, setupScr }) g.SetActive(g == s);
        if (s == charsScr) { foreach (Action a in charRefresh) a(); OpenShutter(); }
        if (s == setupScr) foreach (Action a in setupRefresh) a();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ---------------- building ----------------
    void Build()
    {
        GameObject cg = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cg.transform.SetParent(transform, false);
        Canvas c = cg.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        CanvasScaler sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280f, 720f);
        sc.matchWidthOrHeight = 0.5f;
        root = PaperUI.Rect("Menu", cg.transform);
        PaperUI.Stretch(root);
        dashH = DashSprite(true); dashV = DashSprite(false); fadeDown = FadeSprite();

        titleScr = BuildTitle();
        charsScr = BuildCharacters();
        setupScr = BuildSetup();
    }

    RectTransform Screen(string name, Texture2D bg)
    {
        RectTransform r = PaperUI.Rect(name, root);
        PaperUI.Stretch(r);
        RawImage b = new GameObject("Background", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        b.transform.SetParent(r, false); b.texture = bg; b.raycastTarget = true; b.color = bg != null ? Color.white : Night;
        PaperUI.Stretch(b.rectTransform);
        AspectRatioFitter f = b.gameObject.AddComponent<AspectRatioFitter>();
        f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; f.aspectRatio = 16f / 9f;
        return r;
    }

    // ---------------- title ----------------
    GameObject BuildTitle()
    {
        RectTransform s = Screen("Title", titleBackground);

        // the clown, peeking up from the bottom edge; he slides out of sight while the mouse is over him
        RectTransform zone = PaperUI.Place(PaperUI.Rect("Clown Hide Zone", s), 870, 490, 350, 230);
        Image hit = zone.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        RawImage clown = new GameObject("Clown", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        clown.transform.SetParent(zone, false); clown.texture = clownPeek; clown.raycastTarget = false;
        PaperUI.Place(clown.rectTransform, -50, 9, 380, 260);
        HoverSlide hs = zone.gameObject.AddComponent<HoverSlide>(); hs.target = clown.rectTransform; hs.offset = new Vector2(0f, -270f); hs.speed = 2.4f;

        // tickets peeking in from the left edge (each a little longer than the one above)
        PeekTicket(s, 112, 330, 64, "05", "Quit", true, QuitGame);
        PeekTicket(s, 196, 420, 74, "04", "Settings", false, () => OpenSheet(true));
        PeekTicket(s, 290, 450, 74, "03", "How to play", false, () => OpenSheet(false));
        PeekTicket(s, 384, 480, 74, "02", "Characters", false, () => Show(charsScr));
        Marquee(s);
        return s.gameObject;
    }

    void OpenSheet(bool settings)
    {
        if (sheets == null) return;
        sheets.OpenSheet(settings, null);
    }

    void PeekTicket(RectTransform parent, float top, float w, float h, string number, string label, bool dust, Action onClick)
    {
        RectTransform t = PaperUI.Place(PaperUI.Rect("Ticket " + label, parent), -36, top, w, h);
        TiltFromLeft(t, -6f);
        Color body = dust ? DustCard : Card, stub = dust ? DustStub : Stub, text = dust ? Dust : Cream;
        PaperShape bg = PaperUI.Torn(t, "Body", body, TornRight(0.93f));
        PaperUI.Stretch(bg.rectTransform); bg.raycastTarget = true;
        bg.shadow = true; bg.shadowOffset = new Vector2(8f, -10f); bg.shadowColor = Shadow; bg.Refresh();
        PaperShape st = PaperUI.Shape(t, "Stub", stub);
        PaperUI.Place(st.rectTransform, 0, 0, 126, h);
        Image perf = PaperUI.Img(t, "Perforation", dashV, new Color(1f, 241f / 255f, 214f / 255f, dust ? 0.4f : 0.45f));
        perf.type = Image.Type.Tiled; perf.preserveAspect = false;
        PaperUI.Place(perf.rectTransform, 124.5f, 0, 3, h);
        TextMeshProUGUI n = T(t, "<size=13><cspace=2>NO.</cspace></size>\n" + number, 28f, text, 36, h * 0.5f - 27, 90, 54, 0, TextAlignmentOptions.Center);
        n.lineSpacing = -40f;
        TextMeshProUGUI l = T(t, label, dust ? 28f : 32f, text, 126 + 26, h * 0.5f - 24, w - 160, 48, 0, TextAlignmentOptions.Left);
        Button b = t.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        MenuHover mh = t.gameObject.AddComponent<MenuHover>(); mh.slide = 18f;
        mh.onHover = on =>
        {
            bg.color = on ? (dust ? Dust : Red) : body;
            st.color = on ? (dust ? PaperUI.Hex("c4a08a") : RedDeep) : stub;
            n.color = l.color = on && dust ? Ink : text;
        };
    }

    void Marquee(RectTransform parent)
    {
        RectTransform m = PaperUI.Place(PaperUI.Rect("Play", parent), -40, 486, 540, 156);
        TiltFromLeft(m, -6f);
        PaperShape plate = PaperUI.Torn(m, "Plate", Ink, new[] {
            new Vector2(0, 0), new Vector2(0.94f, 0), new Vector2(0.97f, 0.16f), new Vector2(0.935f, 0.33f), new Vector2(1f, 0.5f),
            new Vector2(0.945f, 0.67f), new Vector2(0.98f, 0.84f), new Vector2(0.94f, 1f), new Vector2(0, 1f) });
        PaperUI.Stretch(plate.rectTransform); plate.raycastTarget = true;
        plate.shadow = true; plate.shadowOffset = new Vector2(9f, -12f); plate.shadowColor = new Color(0, 0, 0, 0.6f); plate.Refresh();
        var bulbs = new List<PaperShape>();
        for (float x = 52; x <= 482; x += 26)
            foreach (float y in new[] { 13f, 143f })
            {
                Image glow = PaperUI.Img(m, "Glow", GlowSprite(), new Color(Gold.r, Gold.g, Gold.b, 0.85f)); glow.preserveAspect = false;
                PaperUI.Place(glow.rectTransform, x - 14, y - 14, 28, 28);
                PaperShape b = PaperUI.Shape(m, "Bulb", Gold, 5.5f);
                PaperUI.Place(b.rectTransform, x - 5.5f, y - 5.5f, 11, 11);
                bulbs.Add(b);
            }
        Image wordGlow = PaperUI.Img(m, "Word Glow", GlowSprite(), new Color(Gold.r, Gold.g, Gold.b, 0.32f)); wordGlow.preserveAspect = false;
        PaperUI.Place(wordGlow.rectTransform, 56, 12, 330, 150);
        // the word sits a little lower, clear of the top row of bulbs
        TextMeshProUGUI word = TitleText(m, "PLAY", 116f, 84, 36, 420, 120, TextAlignmentOptions.Left, 5f, 8f);
        Button btn = m.gameObject.AddComponent<Button>(); btn.targetGraphic = plate; btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => Show(setupScr));
        MenuHover mh = m.gameObject.AddComponent<MenuHover>(); mh.slide = 18f;
        mh.onHover = on => { word.color = on ? Cream : Gold; foreach (PaperShape b in bulbs) b.color = on ? Cream : Gold; };
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------- characters ----------------
    GameObject BuildCharacters()
    {
        RectTransform s = Screen("Characters", menuBackdrop);
        RectTransform title = TitleText(s, "Characters", 56f, 66, 34, 600, 64, TextAlignmentOptions.Left, 4f, 6f).transform.parent as RectTransform;
        PaperUI.Rotate(title, -2f);
        BackArrow(s, () => Show(titleScr));

        // window 1: the roster, one character per pane, scrolling up and down
        RectTransform w1 = Window(s, "Roster Window", 65, 116, 481, 440, -0.6f);
        RectTransform inner = PaperUI.Place(PaperUI.Rect("Glass", w1), 20, 20, 447, 400);
        PaperShape glass = PaperUI.Shape(inner, "Bg", Card, 6f); PaperUI.Stretch(glass.rectTransform); glass.raycastTarget = true;
        RectTransform view = PaperUI.Place(PaperUI.Rect("Viewport", inner), 0, 0, 435, 400);
        Image vhit = view.gameObject.AddComponent<Image>(); vhit.color = Color.white;
        view.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform content = PaperUI.Rect("Panes", view);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0f, 0f);
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(133, 126); grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(6, 6, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int real = MatchSetup.CharacterNames.Length;
        for (int i = 0; i < real + comingSoonPanes; i++) Pane(content, i, i < real);

        ScrollRect sr = inner.gameObject.AddComponent<ScrollRect>();
        sr.viewport = view; sr.content = content; sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f; sr.inertia = true; sr.decelerationRate = 0.12f;
        // the thin gold scrollbar
        RectTransform bar = PaperUI.Place(PaperUI.Rect("Scrollbar", inner), 441, 4, 6, 392);
        Image track = bar.gameObject.AddComponent<Image>(); track.color = new Color(1, 1, 1, 0.06f);
        RectTransform slide = PaperUI.Rect("Sliding Area", bar); PaperUI.Stretch(slide);
        PaperShape handle = PaperUI.Shape(slide, "Handle", Gold, 3f); handle.raycastTarget = true;
        PaperUI.Stretch(handle.rectTransform);
        Scrollbar sb = bar.gameObject.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop; sb.handleRect = handle.rectTransform; sb.targetGraphic = handle; sb.transition = Selectable.Transition.None;
        sr.verticalScrollbar = sb; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        RowSnap snap = inner.gameObject.AddComponent<RowSnap>(); snap.scroll = sr; snap.row = 138f;
        // the bottom row fades out to show there is more below
        Image fade = PaperUI.Img(inner, "Fade", fadeDown, Card); fade.preserveAspect = false;
        PaperUI.Place(fade.rectTransform, 0, 344, 435, 56);
        // sill
        Sill(s, 52, 560, 507);
        TextMeshProUGUI more = T(s, "SCROLL FOR MORE", 13f, Faint, 52, 588, 507, 20, 0, TextAlignmentOptions.Center);
        PaperUI.LetterSpacing(more, 2f);

        // window 2: the picked character, behind a roller shutter
        RectTransform w2 = Window(s, "Show Window", 578, 116, 634, 396, 0.5f);
        RectTransform glass2 = PaperUI.Place(PaperUI.Rect("Glass", w2), 20, 20, 594, 356);
        Image back2 = glass2.gameObject.AddComponent<Image>(); back2.color = PaperUI.Hex("1b2338");
        glass2.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        shopPic = new GameObject("Item Shop", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        shopPic.transform.SetParent(glass2, false); PaperUI.Stretch(shopPic.rectTransform); shopPic.raycastTarget = false;
        shopFace = new GameObject("Close-up", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        shopFace.transform.SetParent(glass2, false); PaperUI.Place(shopFace.rectTransform, 187, 68, 220, 220); shopFace.raycastTarget = false;
        shutter = PaperUI.Place(PaperUI.Rect("Roller Shutter", glass2), 0, 0, 594, 356);
        RectTransform slats = PaperUI.Place(PaperUI.Rect("Slats", shutter), 0, 0, 594, 334);
        Image slatBg = slats.gameObject.AddComponent<Image>(); slatBg.color = PaperUI.Hex("e9dcc3");
        slats.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        float[] tilt = { 0.6f, -0.7f, 0.7f, -0.6f, 0.4f, -0.7f, 0.6f, -0.4f, 0.5f };
        for (int i = 0; i < 9; i++)
        {
            RectTransform sl = PaperUI.Place(PaperUI.Rect("Slat", slats), -10, -8 + i * 40, 614, 46);
            PaperShape a = PaperUI.Shape(sl, "Face", PaperUI.Hex("f4eada"), 6f); PaperUI.Stretch(a.rectTransform);
            a.shadow = true; a.shadowOffset = new Vector2(0, -5); a.shadowColor = new Color(90 / 255f, 66 / 255f, 36 / 255f, 0.3f); a.Refresh();
            PaperShape low = PaperUI.Shape(sl, "Lower", PaperUI.Hex("eadcc0"), 0f); PaperUI.Place(low.rectTransform, 0, 26, 614, 13);
            PaperShape lip = PaperUI.Shape(sl, "Lip", PaperUI.Hex("d6c4a0"), 4f); PaperUI.Place(lip.rectTransform, 0, 39, 614, 7);
            PaperShape top = PaperUI.Shape(sl, "Shine", PaperUI.Hex("fffaf0"), 0f); PaperUI.Place(top.rectTransform, 0, 0, 614, 3);
            sl.localEulerAngles = new Vector3(0, 0, -tilt[i]);
        }
        PaperShape bottom = PaperUI.Shape(shutter, "Bottom Bar", PaperUI.Hex("e6d6b6"), 8f); PaperUI.Place(bottom.rectTransform, -6, 332, 606, 24);
        PaperShape bottomLip = PaperUI.Shape(shutter, "Bottom Lip", PaperUI.Hex("cdb891"), 0f); PaperUI.Place(bottomLip.rectTransform, -6, 350, 606, 6);
        PaperShape housing = PaperUI.Shape(glass2, "Housing", PaperUI.Hex("f1e6cf"), 0f); PaperUI.Place(housing.rectTransform, 0, 0, 594, 16);
        housing.shadow = true; housing.shadowOffset = new Vector2(0, -4); housing.shadowColor = new Color(8 / 255f, 2 / 255f, 12 / 255f, 0.35f); housing.Refresh();
        Sill(s, 564, 516, 662);

        // the name, and Use
        TextMeshProUGUI nm = TitleText(s, "", 44f, 584, 582, 400, 52, TextAlignmentOptions.Left, 4f, 6f);
        nameLayers = nm.transform.parent.GetComponentsInChildren<TextMeshProUGUI>();
        TextMeshProUGUI useNo, useLabel; PaperShape useBody, useStub;
        RectTransform use = NotchTicket(s, 1280 - 68 - 230, 572, 230, 66, "01", "Use", Red, RedDeep, out useNo, out useLabel, out useBody, out useStub, UseShown);
        MenuHover uh = use.GetComponent<MenuHover>();
        uh.onHover = on =>
        {
            bool inUse = shown == MatchSetup.Character;
            if (inUse) return;
            useBody.color = on ? Gold : Red; useStub.color = on ? GoldDeep : RedDeep;
            useNo.color = useLabel.color = on ? Ink : Cream;
        };
        charRefresh.Add(() =>
        {
            bool inUse = shown == MatchSetup.Character;
            useLabel.text = inUse ? "In use" : "Use";
            useNo.text = "<size=12><cspace=2>NO.</cspace></size>\n" + (shown + 1).ToString("00");
            useBody.color = inUse ? DustCard : Red; useStub.color = inUse ? DustStub : RedDeep;
            useNo.color = useLabel.color = inUse ? Dust : Cream;
            uh.enabled = !inUse;
            foreach (TextMeshProUGUI t in nameLayers) t.text = MatchSetup.CharacterNames[shown];
            Texture2D shop = itemShop != null && shown < itemShop.Length ? itemShop[shown] : null;
            shopPic.gameObject.SetActive(shop != null);
            shopFace.gameObject.SetActive(shop == null);
            if (shop != null) { shopPic.texture = shop; Cover(shopPic, shop, 594f / 356f, 0.5f); }
            else { Texture2D f = Closeup(shown); shopFace.texture = f; Cover(shopFace, f, 1f, 0.3f); }
        });
        return s.gameObject;
    }

    void Pane(RectTransform content, int i, bool real)
    {
        RectTransform p = PaperUI.Rect(real ? MatchSetup.CharacterNames[i] : "Coming soon", content);
        PaperShape bg = PaperUI.Shape(p, "Bg", PaperUI.Hex("170d14"), 6f); PaperUI.Stretch(bg.rectTransform); bg.raycastTarget = true;
        if (!real)
        {
            T(p, "?", 54f, PaperUI.Hex("3a2834"), 0, 38, 133, 70, 2, TextAlignmentOptions.Center);
            return;
        }
        RawImage pic = new GameObject("Close-up", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        pic.transform.SetParent(p, false); PaperUI.Stretch(pic.rectTransform); pic.raycastTarget = false;
        Texture2D tex = Closeup(i); pic.texture = tex; Cover(pic, tex, 133f / 126f, 0.28f);
        PaperShape ring = PaperUI.Shape(p, "Ring", new Color(241 / 255f, 228 / 255f, 198 / 255f, 0.7f), 6f); PaperUI.Stretch(ring.rectTransform); ring.ring = 3f; ring.Refresh();
        RectTransform stamp = PaperUI.Place(PaperUI.Rect("In Use", p), 133 - 8 - 70, 10, 70, 26);
        PaperShape sbg = PaperUI.Shape(stamp, "Bg", new Color(14 / 255f, 7 / 255f, 12 / 255f, 0.82f), 6f); PaperUI.Stretch(sbg.rectTransform);
        PaperShape sring = PaperUI.Shape(stamp, "Ring", Gold, 6f); PaperUI.Stretch(sring.rectTransform); sring.ring = 3f; sring.Refresh();
        T(stamp, "IN USE", 15f, Gold, 0, 4, 70, 22, 2, TextAlignmentOptions.Center);
        PaperUI.Rotate(stamp, 7f);
        bool hover = false;
        Action draw = () =>
        {
            bool on = i == picked, inUse = i == MatchSetup.Character;
            pic.color = on || hover ? Color.white : new Color(0.62f, 0.62f, 0.62f);
            ring.gameObject.SetActive(on && !inUse);
            stamp.gameObject.SetActive(inUse);
        };
        charRefresh.Add(draw);
        Button b = p.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => Pick(i));
        MenuHover mh = p.gameObject.AddComponent<MenuHover>(); mh.slide = 0f; mh.grow = 0.06f; mh.turn = 1.5f;
        mh.onHover = on => { hover = on; draw(); };
    }

    void Pick(int i)
    {
        if (i == picked) return;
        picked = i;
        foreach (Action a in charRefresh) a();
        if (rolling != null) StopCoroutine(rolling);
        rolling = StartCoroutine(Roll(i));
    }

    void UseShown()
    {
        if (shown == MatchSetup.Character) return;
        MatchSetup.Character = shown;
        MatchSetup.Save();
        foreach (Action a in charRefresh) a();
    }

    // the shutter rolls down, the picture changes, and it rolls back up
    IEnumerator Roll(int to)
    {
        yield return MoveShutter(0f, 0.36f);
        shown = to;
        foreach (Action a in charRefresh) a();
        yield return MoveShutter(1f, 0.36f);
        rolling = null;
    }

    void OpenShutter()
    {
        shown = picked;
        foreach (Action a in charRefresh) a();
        SetShutter(0f);
        if (rolling != null) StopCoroutine(rolling);
        rolling = StartCoroutine(OpenAfter());
    }

    IEnumerator OpenAfter()
    {
        yield return new WaitForSecondsRealtime(0.45f);
        yield return MoveShutter(1f, 0.36f);
        rolling = null;
    }

    float shutterOpen;
    IEnumerator MoveShutter(float to, float time)
    {
        float from = shutterOpen, t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / time;
            float e = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            SetShutter(Mathf.Lerp(from, to, Mathf.Clamp01(e)));
            yield return null;
        }
        SetShutter(to);
    }

    void SetShutter(float open)
    {
        shutterOpen = open;
        shutter.anchoredPosition = new Vector2(0f, open * 356f * 1.04f);
    }

    // ---------------- game setup ----------------
    GameObject BuildSetup()
    {
        RectTransform s = Screen("Setup", menuBackdrop);
        RectTransform title = TitleText(s, "Who's in the house?", 56f, 66, 34, 900, 64, TextAlignmentOptions.Left, 4f, 6f).transform.parent as RectTransform;
        PaperUI.Rotate(title, -2f);
        BackArrow(s, () => Show(titleScr));

        float[] tilts = { -2f, 1.5f, -1f, 2f };
        for (int i = 0; i < MatchSetup.Seats; i++) SeatColumn(s, i, 120 + i * 270, 122, tilts[i]);

        RectTransform q = TitleText(s, "How many hiders?", 32f, 120, 600, 330, 40, TextAlignmentOptions.Left, 4f, 6f).transform.parent as RectTransform;
        PaperUI.Rotate(q, -2f);
        for (int n = 2; n <= 4; n++) CountDisc(s, n, 462 + (n - 2) * 78, 590);

        TextMeshProUGUI a, b; PaperShape c, d;
        NotchTicket(s, 1280 - 110 - 330, 588, 330, 70, "01", "Start the night", Red, RedDeep, out a, out b, out c, out d, StartNight);
        return s.gameObject;
    }

    void SeatColumn(RectTransform s, int seat, float x, float y, float tilt)
    {
        Color col = SeatColours[seat];
        RectTransform colRt = PaperUI.Place(PaperUI.Rect("Seat " + (seat + 1), s), x, y, 230, 400);
        // the string and its nail
        Bar(colRt, new Vector2(52, 52), new Vector2(115, 2), 4f, col);
        Bar(colRt, new Vector2(115, 2), new Vector2(178, 52), 4f, col);
        PaperShape nailRing = PaperUI.Shape(colRt, "Nail Ring", Gold, 7.5f); PaperUI.Place(nailRing.rectTransform, 107.5f, -5.5f, 15, 15);
        PaperShape nail = PaperUI.Shape(colRt, "Nail", Ink, 4.5f); PaperUI.Place(nail.rectTransform, 110.5f, -2.5f, 9, 9);

        // the frame
        RectTransform frame = PaperUI.Place(PaperUI.Rect("Frame", colRt), 15, 42, 200, 232);
        PaperUI.Rotate(frame, tilt);
        CanvasGroup fg = frame.gameObject.AddComponent<CanvasGroup>();
        PaperShape paper = PaperUI.Torn(frame, "Paper", Sheet, FrameTear);
        PaperUI.Stretch(paper.rectTransform); PaperUI.Cut(paper);
        paper.shadowOffset = new Vector2(8f, -11f); paper.shadowColor = new Color(0, 0, 0, 0.6f); paper.Refresh();
        RectTransform photo = PaperUI.Place(PaperUI.Rect("Photo", frame), 16, 16, 168, 200);
        Image pbg = photo.gameObject.AddComponent<Image>(); pbg.color = PaperUI.Hex("170d14");
        photo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        RawImage pic = new GameObject("Close-up", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        pic.transform.SetParent(photo, false); PaperUI.Stretch(pic.rectTransform); pic.raycastTarget = false;
        TextMeshProUGUI qm = T(photo, "?", 92f, PaperUI.Hex("3a2834"), 0, 50, 168, 110, 2, TextAlignmentOptions.Center);
        PaperShape ring = PaperUI.Shape(photo, "Ring", col, 4f); PaperUI.Stretch(ring.rectTransform); ring.ring = 3f; ring.Refresh();
        RectTransform tag = PaperUI.Place(PaperUI.Rect("Tag", frame), -12, -10, 64, 26);
        PaperShape tagBg = PaperUI.Shape(tag, "Bg", col); PaperUI.Stretch(tagBg.rectTransform);
        tagBg.shadow = true; tagBg.shadowOffset = new Vector2(3, -4); tagBg.shadowColor = new Color(0, 0, 0, 0.5f); tagBg.Refresh();
        T(tag, "P" + (seat + 1), 16f, Cream, 0, 4, 64, 22, 2, TextAlignmentOptions.Center);
        PaperUI.Rotate(tag, -14f);

        // the name field
        TMP_InputField field = NameField(colRt, seat, 8, 290, 214, 38);
        TextMeshProUGUI charLine = T(colRt, "", 13f, Faint, 0, 332, 230, 20, 0, TextAlignmentOptions.Center);
        PaperUI.LetterSpacing(charLine, 1f);

        // AI / Empty (or You)
        RectTransform chips = PaperUI.Place(PaperUI.Rect("Who", colRt), 0, 360, 230, 40);
        var chipBgs = new List<PaperShape>(); var chipText = new List<TextMeshProUGUI>();
        if (seat == 0)
        {
            Chip(chips, 115, 70, "You", out PaperShape yb, out TextMeshProUGUI yt, null);
            yb.color = Gold; yt.color = Ink;
        }
        else
        {
            Chip(chips, 71, 60, "AI", out PaperShape ab, out TextMeshProUGUI at, () => { if (MatchSetup.GetSeat(seat) == MatchSetup.Seat.Empty) MatchSetup.AddSeat(); MatchSetup.Save(); RefreshSetup(); });
            Chip(chips, 153, 76, "Empty", out PaperShape eb, out TextMeshProUGUI et, () => { MatchSetup.RemoveSeat(seat); MatchSetup.Save(); RefreshSetup(); });
            chipBgs.Add(ab); chipBgs.Add(eb); chipText.Add(at); chipText.Add(et);
        }

        setupRefresh.Add(() =>
        {
            bool empty = MatchSetup.GetSeat(seat) == MatchSetup.Seat.Empty;
            int ch = MatchSetup.CharacterInSeat(seat);
            Texture2D tex = Closeup(ch);
            pic.texture = tex; Cover(pic, tex, 168f / 200f, 0.3f);
            pic.gameObject.SetActive(!empty); qm.gameObject.SetActive(empty);
            fg.alpha = empty ? 0.55f : 1f;
            field.interactable = !empty;
            field.SetTextWithoutNotify(empty ? "" : MatchSetup.GetName(seat));
            ((TextMeshProUGUI)field.placeholder).text = empty ? "Empty seat" : MatchSetup.DefaultName(seat);
            charLine.text = empty ? "NOBODY HIDING HERE" : MatchSetup.CharacterNames[ch].ToUpperInvariant();
            if (seat > 0)
            {
                bool ai = !empty;
                chipBgs[0].color = ai ? Red : Card; chipText[0].color = ai ? Cream : Dust;
                chipBgs[1].color = !ai ? Red : Card; chipText[1].color = !ai ? Cream : Dust;
            }
        });
    }

    void RefreshSetup() { foreach (Action a in setupRefresh) a(); }

    void Chip(RectTransform parent, float centreX, float w, string label, out PaperShape bg, out TextMeshProUGUI text, Action onClick)
    {
        RectTransform c = PaperUI.Place(PaperUI.Rect("Chip " + label, parent), centreX - w * 0.5f, 0, w, 38);
        bg = PaperUI.Shape(c, "Bg", Card, 8f); PaperUI.Stretch(bg.rectTransform); bg.raycastTarget = true;
        bg.shadow = true; bg.shadowOffset = new Vector2(5, -6); bg.shadowColor = new Color(0, 0, 0, 0.5f); bg.Refresh();
        text = T(c, label, 17f, Dust, 0, 7, w, 26, 0, TextAlignmentOptions.Center);
        if (onClick == null) return;
        Button b = c.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        MenuHover mh = c.gameObject.AddComponent<MenuHover>(); mh.slide = 0f; mh.lift = 2f; mh.turn = 1.5f;
    }

    void CountDisc(RectTransform parent, int n, float x, float y)
    {
        RectTransform d = PaperUI.Place(PaperUI.Rect("Hiders " + n, parent), x, y, 62, 62);
        PaperShape bg = PaperUI.Shape(d, "Bg", Card, 31f); PaperUI.Stretch(bg.rectTransform); bg.raycastTarget = true;
        bg.shadow = true; bg.shadowOffset = new Vector2(5, -7); bg.shadowColor = Shadow; bg.Refresh();
        PaperShape ring = PaperUI.Shape(d, "Ring", Border, 31f); PaperUI.Stretch(ring.rectTransform); ring.ring = 3f; ring.Refresh();
        TextMeshProUGUI t = T(d, n.ToString(), 32f, Dust, 0, 12, 62, 40, 2, TextAlignmentOptions.Center);
        Button b = d.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() =>
        {
            MatchSetup.Hiders = n;
            MatchSetup.Save(); RefreshSetup();
        });
        MenuHover mh = d.gameObject.AddComponent<MenuHover>(); mh.slide = 0f; mh.lift = 3f; mh.turn = 3f;
        setupRefresh.Add(() =>
        {
            bool on = MatchSetup.Hiders == n;
            bg.color = on ? Red : Card; ring.color = on ? Gold : Border; t.color = on ? Cream : Dust;
        });
    }

    TMP_InputField NameField(RectTransform parent, int seat, float x, float y, float w, float h)
    {
        RectTransform r = PaperUI.Place(PaperUI.Rect("Name", parent), x, y, w, h);
        Image hitbox = r.gameObject.AddComponent<Image>(); hitbox.color = new Color(1, 1, 1, 0f);
        Image line = PaperUI.Img(r, "Underline", dashH, new Color(241 / 255f, 228 / 255f, 198 / 255f, 0.35f));
        line.type = Image.Type.Tiled; line.preserveAspect = false;
        PaperUI.Place(line.rectTransform, 0, h - 3, w, 3);
        RectTransform area = PaperUI.Place(PaperUI.Rect("Text Area", r), 6, 0, w - 12, h - 4);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI ph = PaperUI.Text(area, "Placeholder", MatchSetup.DefaultName(seat), 24f, Faint, true, TextAlignmentOptions.Center);
        ph.font = PaperUI.Title; PaperUI.Stretch(ph.rectTransform); PaperUI.LetterSpacing(ph, 1f);
        TextMeshProUGUI txt = PaperUI.Text(area, "Text", "", 24f, Cream, true, TextAlignmentOptions.Center);
        txt.font = PaperUI.Title; PaperUI.Stretch(txt.rectTransform); PaperUI.LetterSpacing(txt, 1f);
        TMP_InputField f = r.gameObject.AddComponent<TMP_InputField>();
        f.textViewport = area; f.textComponent = txt; f.placeholder = ph;
        f.characterLimit = 14; f.lineType = TMP_InputField.LineType.SingleLine;
        f.caretColor = Gold; f.customCaretColor = true; f.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
        f.transition = Selectable.Transition.None; f.targetGraphic = hitbox;
        f.onValueChanged.AddListener(v => { MatchSetup.SetName(seat, v); });
        f.onSelect.AddListener(_ => { line.sprite = null; line.type = Image.Type.Simple; line.color = Gold; hitbox.color = new Color(1, 241 / 255f, 214 / 255f, 0.08f); });
        f.onDeselect.AddListener(_ => { line.sprite = dashH; line.type = Image.Type.Tiled; line.color = new Color(241 / 255f, 228 / 255f, 198 / 255f, 0.35f); hitbox.color = new Color(1, 1, 1, 0f); MatchSetup.Save(); });
        return f;
    }

    void StartNight()
    {
        MatchSetup.Save();
        PlayerPrefs.Save();
        StaticStateReset.ForNewMatch();
        RoundManager.ResetMatch();
        SceneManager.LoadScene(gameScene);
    }

    // ---------------- pieces ----------------
    Texture2D Closeup(int character)
    {
        return closeups != null && character >= 0 && character < closeups.Length ? closeups[character] : null;
    }

    // fills the box with the picture (CSS object-fit: cover); vBias picks which part of a tall picture shows (0 top, 1 bottom)
    static void Cover(RawImage img, Texture tex, float boxAspect, float vBias)
    {
        if (tex == null) { img.uvRect = new Rect(0, 0, 1, 1); return; }
        float texAspect = (float)tex.width / tex.height;
        if (texAspect > boxAspect) { float w = boxAspect / texAspect; img.uvRect = new Rect((1 - w) * 0.5f, 0, w, 1); }
        else { float h = texAspect / boxAspect; img.uvRect = new Rect(0, (1 - h) * (1 - vBias), 1, h); }
    }

    RectTransform Window(RectTransform parent, string name, float x, float y, float w, float h, float tilt)
    {
        RectTransform r = PaperUI.Place(PaperUI.Rect(name, parent), x, y, w, h);
        PaperUI.Rotate(r, tilt);
        PaperShape paper = PaperUI.Torn(r, "Frame", Sheet, WindowTear);
        PaperUI.Stretch(paper.rectTransform); PaperUI.Cut(paper);
        paper.shadowOffset = new Vector2(8f, -11f); paper.shadowColor = new Color(0, 0, 0, 0.6f); paper.Refresh();
        return r;
    }

    void Sill(RectTransform parent, float x, float y, float w)
    {
        PaperShape sill = PaperUI.Shape(parent, "Sill", Card, 4f);
        PaperUI.Place(sill.rectTransform, x, y, w, 20);
        sill.shadow = true; sill.shadowOffset = new Vector2(6, -8); sill.shadowColor = Shadow; sill.Refresh();
        PaperShape edge = PaperUI.Shape(sill.rectTransform, "Top", Border, 0f); PaperUI.Place(edge.rectTransform, 2, 0, w - 4, 3);
    }

    void BackArrow(RectTransform parent, Action onClick)
    {
        RectTransform back = PaperUI.Place(PaperUI.Rect("Back", parent), 1280 - 56 - 72, 30, 72, 63);
        Image arrow = PaperUI.Img(back, "Arrow", backArrow, Color.white);
        PaperUI.Stretch(arrow.rectTransform); arrow.preserveAspect = true; arrow.raycastTarget = true;
        Button b = back.gameObject.AddComponent<Button>(); b.targetGraphic = arrow; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        HoverNudge bn = back.gameObject.AddComponent<HoverNudge>(); bn.amount = new Vector2(-6f, 2f); bn.turn = -4f;
    }

    // the pause menu's carnival ticket: notched both ends, numbered stub behind a perforation; returns the ticket
    RectTransform NotchTicket(RectTransform parent, float x, float y, float w, float h, string number, string label, Color fill, Color stubFill,
                              out TextMeshProUGUI numText, out TextMeshProUGUI labelText, out PaperShape body, out PaperShape stub, Action onClick)
    {
        RectTransform t = PaperUI.Place(PaperUI.Rect("Ticket " + label, parent), x, y, w, h);
        body = PaperUI.Torn(t, "Body", fill, TicketPoints(w, h, 11f, true, true));
        PaperUI.Stretch(body.rectTransform); body.raycastTarget = true;
        const float sw = 80f;
        stub = PaperUI.Torn(t, "Stub", stubFill, TicketPoints(sw, h, 11f, true, false));
        PaperUI.Place(stub.rectTransform, 0, 0, sw, h);
        Image perf = PaperUI.Img(t, "Perforation", dashV, new Color(1f, 241f / 255f, 214f / 255f, 0.45f));
        perf.type = Image.Type.Tiled; perf.preserveAspect = false;
        PaperUI.Place(perf.rectTransform, sw - 1.5f, 6, 3, h - 12);
        numText = T(t, "<size=12><cspace=2>NO.</cspace></size>\n" + number, 24f, Cream, 0, h * 0.5f - 24, sw, 48, 0, TextAlignmentOptions.Center);
        numText.lineSpacing = -38f;
        labelText = T(t, label, 28f, Cream, sw + 22, h * 0.5f - 21, w - sw - 30, 42, 0, TextAlignmentOptions.Left);
        Button b = t.gameObject.AddComponent<Button>(); b.targetGraphic = body; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        MenuHover mh = t.gameObject.AddComponent<MenuHover>(); mh.slide = 10f; mh.turn = 0.6f;
        PaperShape bodyRef = body, stubRef = stub; TextMeshProUGUI nRef = numText, lRef = labelText;
        mh.onHover = on =>
        {
            bodyRef.color = on ? Gold : fill; stubRef.color = on ? GoldDeep : stubFill;
            nRef.color = lRef.color = on ? Ink : Cream;
        };
        return t;
    }

    static Vector2[] TicketPoints(float w, float h, float r, bool left, bool right)
    {
        var p = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0) };
        if (right) for (int i = 0; i <= 8; i++) { float a = Mathf.Lerp(-90f, 90f, i / 8f) * Mathf.Deg2Rad; p.Add(new Vector2((w - r * Mathf.Cos(a)) / w, (h * 0.5f + r * Mathf.Sin(a)) / h)); }
        p.Add(new Vector2(1, 1)); p.Add(new Vector2(0, 1));
        if (left) for (int i = 0; i <= 8; i++) { float a = Mathf.Lerp(90f, -90f, i / 8f) * Mathf.Deg2Rad; p.Add(new Vector2(r * Mathf.Cos(a) / w, (h * 0.5f + r * Mathf.Sin(a)) / h)); }
        return p.ToArray();
    }

    static Vector2[] TornRight(float edge)
    {
        return new[] { new Vector2(0, 0), new Vector2(edge, 0), new Vector2(edge + 0.03f, 0.14f), new Vector2(edge - 0.005f, 0.30f), new Vector2(0.99f, 0.46f),
                       new Vector2(edge + 0.01f, 0.62f), new Vector2(edge + 0.05f, 0.78f), new Vector2(edge, 1f), new Vector2(0, 1f) };
    }

    static readonly Vector2[] WindowTear = {
        new Vector2(0, 0.016f), new Vector2(0.09f, 0), new Vector2(0.22f, 0.013f), new Vector2(0.37f, 0.002f), new Vector2(0.52f, 0.015f),
        new Vector2(0.66f, 0.003f), new Vector2(0.81f, 0.014f), new Vector2(0.93f, 0.002f), new Vector2(1f, 0.012f), new Vector2(0.994f, 0.26f),
        new Vector2(1f, 0.52f), new Vector2(0.993f, 0.77f), new Vector2(1f, 0.986f), new Vector2(0.87f, 1f), new Vector2(0.71f, 0.988f),
        new Vector2(0.55f, 1f), new Vector2(0.40f, 0.987f), new Vector2(0.24f, 0.998f), new Vector2(0.09f, 0.986f), new Vector2(0, 0.996f),
        new Vector2(0.006f, 0.76f), new Vector2(0, 0.50f), new Vector2(0.007f, 0.24f) };

    static readonly Vector2[] FrameTear = {
        new Vector2(0.01f, 0.02f), new Vector2(0.30f, 0), new Vector2(0.62f, 0.015f), new Vector2(0.99f, 0), new Vector2(1f, 0.34f),
        new Vector2(0.99f, 0.70f), new Vector2(1f, 0.99f), new Vector2(0.64f, 1f), new Vector2(0.30f, 0.986f), new Vector2(0, 1f),
        new Vector2(0.01f, 0.66f), new Vector2(0, 0.30f) };

    // a straight line between two points (CSS coordinates inside parent)
    static void Bar(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color c)
    {
        RectTransform r = PaperUI.Rect("String", parent);
        Image i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        Vector2 ua = new Vector2(a.x, -a.y), ub = new Vector2(b.x, -b.y);
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(Vector2.Distance(ua, ub), thickness);
        r.anchoredPosition = (ua + ub) * 0.5f;
        Vector2 d = ub - ua; r.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // CSS 'transform-origin: left center; rotate(deg)'
    static void TiltFromLeft(RectTransform r, float cssDegrees)
    {
        Vector2 size = r.sizeDelta, topLeft = r.anchoredPosition;
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = topLeft + new Vector2(0f, -size.y * 0.5f);
        r.localEulerAngles = new Vector3(0f, 0f, -cssDegrees);
    }

    // Luckiest Guy in gold with the sticker shadows (red, then ink); returns the gold text
    TextMeshProUGUI TitleText(RectTransform parent, string text, float size, float x, float y, float w, float h, TextAlignmentOptions align, float redOff, float inkOff)
    {
        RectTransform g = PaperUI.Place(PaperUI.Rect("Title " + text, parent), x, y, w, h);
        TextMeshProUGUI ink = T(g, text, size, Ink, inkOff, inkOff, w, h, 2, align);
        TextMeshProUGUI red = T(g, text, size, Red, redOff, redOff, w, h, 2, align);
        TextMeshProUGUI top = T(g, text, size, Gold, 0, 0, w, h, 2, align);
        foreach (TextMeshProUGUI t in new[] { ink, red, top }) PaperUI.LetterSpacing(t, 1f);
        return top;
    }

    // font: 0 = Baloo 2 ExtraBold, 1 = Baloo 2 SemiBold, 2 = Luckiest Guy
    static TextMeshProUGUI T(Transform parent, string text, float size, Color colour, float x, float y, float w, float h, int font,
                             TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI t = PaperUI.Text(parent, "Text", text, size, colour, font != 1, align);
        if (font == 2) t.font = PaperUI.Title;
        t.richText = true;
        PaperUI.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    static Sprite DashSprite(bool horizontal)
    {
        int len = 12, on = 7;
        Texture2D tex = horizontal ? new Texture2D(len, 2, TextureFormat.RGBA32, false) : new Texture2D(2, len, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Point;
        for (int i = 0; i < len; i++)
            for (int j = 0; j < 2; j++)
            {
                Color c = i < on ? Color.white : Color.clear;
                if (horizontal) tex.SetPixel(i, j, c); else tex.SetPixel(j, i, c);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    // a soft round glow (white in the middle, fading to nothing at the edge)
    static Sprite glowSprite;
    static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d); a = a * a;
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return glowSprite;
    }

    // transparent at the top, solid at the bottom (tinted by the Image colour)
    static Sprite FadeSprite()
    {
        Texture2D tex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 64; i++) tex.SetPixel(0, i, new Color(1, 1, 1, 1f - i / 63f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 100f);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }
}

// Hover for menu pieces: slides along its own tilt, lifts, grows or turns a little, on real time;
// onHover lets the owner change colours as the mouse comes and goes.
public class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float slide = 10f, lift = 0f, grow = 0f, turn = 0f;
    public Action<bool> onHover;
    RectTransform rt;
    Vector2 home; float baseAngle, k, target; bool ready;

    void Start() { rt = (RectTransform)transform; ready = true; }
    // the resting place is taken when the mouse arrives, so layout groups can place the piece first
    void OnDisable() { if (ready && k > 0f) { k = 0f; Apply(); } k = target = 0f; if (onHover != null) onHover(false); }
    public void OnPointerEnter(PointerEventData e)
    {
        if (!enabled || !ready) return;
        if (k <= 0f) { home = rt.anchoredPosition; baseAngle = rt.localEulerAngles.z; }
        target = 1f; if (onHover != null) onHover(true);
    }
    public void OnPointerExit(PointerEventData e) { target = 0f; if (onHover != null) onHover(false); }

    void Update()
    {
        if (!ready || Mathf.Approximately(k, target)) return;
        k = Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * 7f);
        Apply();
    }

    void Apply()
    {
        float e = PaperUI.EaseOut(k);
        Vector2 along = Quaternion.Euler(0, 0, baseAngle) * Vector2.right;
        rt.anchoredPosition = home + along * slide * e + Vector2.up * lift * e;
        rt.localEulerAngles = new Vector3(0f, 0f, baseAngle + turn * e);
        rt.localScale = Vector3.one * (1f + grow * e);
    }
}

// Mouse over the zone: the target slides away (the clown ducks below the screen) and comes back when the mouse leaves.
public class HoverSlide : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform target;
    public Vector2 offset = new Vector2(0f, -270f);
    public float speed = 2.4f;
    Vector2 home; float k, goal; bool ready;

    void Start() { if (target != null) { home = target.anchoredPosition; ready = true; } }
    public void OnPointerEnter(PointerEventData e) { goal = 1f; }
    public void OnPointerExit(PointerEventData e) { goal = 0f; }
    void OnDisable() { k = goal = 0f; if (ready) target.anchoredPosition = home; }

    void Update()
    {
        if (!ready || Mathf.Approximately(k, goal)) return;
        k = Mathf.MoveTowards(k, goal, Time.unscaledDeltaTime * speed);
        float e = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
        target.anchoredPosition = home + offset * e;
    }
}

// Lets the roster settle on whole rows once a scroll or drag has slowed down.
public class RowSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public ScrollRect scroll;
    public float row = 142f;
    bool dragging; float idle;

    public void OnBeginDrag(PointerEventData e) { dragging = true; }
    public void OnEndDrag(PointerEventData e) { dragging = false; }

    void LateUpdate()
    {
        if (scroll == null || dragging) { idle = 0f; return; }
        RectTransform c = scroll.content;
        float max = Mathf.Max(0f, c.rect.height - scroll.viewport.rect.height);
        if (Mathf.Abs(scroll.velocity.y) > 40f) { idle = 0f; return; }
        idle += Time.unscaledDeltaTime;
        if (idle < 0.12f) return;
        float y = c.anchoredPosition.y;
        float goal = Mathf.Clamp(Mathf.Round(y / row) * row, 0f, max);
        if (Mathf.Abs(goal - y) < 0.5f) return;
        scroll.velocity = Vector2.zero;
        c.anchoredPosition = new Vector2(c.anchoredPosition.x, Mathf.Lerp(y, goal, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime)));
    }
}
