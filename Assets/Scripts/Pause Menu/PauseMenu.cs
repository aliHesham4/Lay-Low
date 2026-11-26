using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The pause menu (Esc), built from the "Lay Low – Pause Menu" design: the frozen game behind a dark overlay,
// a marquee PAUSED sign, carnival tickets (Resume, Restart round, Settings, Controls, Leave the house),
// Settings and Controls on a torn sheet of dark paper, and paper confirm cards for restarting and leaving.
// Esc is the only key that opens it, closes it or steps back out of a sub-screen.
[DefaultExecutionOrder(-50)]
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Assets/Sounds/GameAudio.mixer (its groups are what the audio sliders turn up and down).")]
    public AudioMixer mixer;
    [Header("Camera view pictures (Settings)")]
    public Texture2D camClassic;
    public Texture2D camHigher;
    public Texture2D camShoulder;
    [Tooltip("Assets/Canvas/Back Arrow.png: the back button on the Settings and Controls sheets.")]
    public Sprite backArrow;
    [Tooltip("Scene loaded by 'Leave the house'. If it isn't in the build, the game closes instead.")]
    public string mainMenuScene = "MainMenu";
    [Tooltip("On in the main menu: only Settings and How to play are built, and they open over the title screen.")]
    public bool titleMode;

    public static bool IsOpen { get; private set; }

    // palette (the design's)
    static readonly Color Ink = PaperUI.Hex("1d1322"), Night = PaperUI.Hex("0e070c"), Sheet = PaperUI.Hex("241820");
    static readonly Color Card = PaperUI.Hex("2b1c26"), CardDeep = PaperUI.Hex("170d14"), Stub = PaperUI.Hex("22161e");
    static readonly Color Line = PaperUI.Hex("33232e"), Border = PaperUI.Hex("4a3442"), Mute = PaperUI.Hex("a88b98");
    static readonly Color Dust = PaperUI.Hex("d9b6a0"), Faint = PaperUI.Hex("8d7180");
    static readonly Color Red = PaperUI.Red, Gold = PaperUI.Gold, Cream = PaperUI.Light;

    RectTransform root;
    GameObject mainScr, settingsScr, controlsScr, restartScr, leaveScr, howScr, current;
    // the objectives note on the right of the main screen
    TextMeshProUGUI objectivesTitle, objectivesText;
    // the "Your bag" note under it (the inventory, with each item's story)
    readonly Image[] bagIcon = new Image[4];
    readonly TextMeshProUGUI[] bagName = new TextMeshProUGUI[4], bagText = new TextMeshProUGUI[4];
    Action sheetClosed;
    TextMeshProUGUI subtitle, restartText;
    readonly List<PaperShape> bulbs = new List<PaperShape>();
    readonly List<Action> refreshers = new List<Action>();
    float prevScale = 1f;
    CursorLockMode prevLock;
    bool prevVisible;
    Sprite dashH, dashV;

    // ---------------- life ----------------
    void Awake()
    {
        IsOpen = false;
        GameSettings.Mixer = mixer;
        GameSettings.ApplyAll();
        if (!titleMode)
        foreach (CameraSwitcher cs in FindObjectsByType<CameraSwitcher>(FindObjectsSortMode.None))
            if (cs.GetComponent<Hider>() != null) cs.startInFirstPerson = GameSettings.StartFirstPerson;
        if (mixer != null) gameObject.AddComponent<AudioRouter>().Init(mixer);
        Build();
        root.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (IsOpen) { Time.timeScale = prevScale; AudioListener.pause = false; }
        IsOpen = false;
    }

    void Update()
    {
        if (titleMode)
        {
            Keyboard kk = Keyboard.current;
            if (kk != null && kk.escapeKey.wasPressedThisFrame && root.gameObject.activeSelf) CloseSheet();
            return;
        }
        Keyboard k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame)
        {
            if (!IsOpen) { if (CanPause()) Open(); }
            else if (current != mainScr) Show(mainScr);
            else Close();
        }
        if (!IsOpen) return;

        // the bulbs around PAUSED chase round the sign
        float t = Time.unscaledTime * 6f;
        for (int i = 0; i < bulbs.Count; i++)
        {
            bool on = ((int)t + i) % 3 != 0;
            Color c = Gold; c.a = on ? 1f : 0.35f;
            if (bulbs[i].color != c) bulbs[i].color = c;
        }
    }

    bool CanPause()
    {
        RoundManager rm = RoundManager.Instance;
        return rm != null && rm.IntroDone && !rm.RoundOver && Time.timeScale > 0f;
    }

    void Open()
    {
        IsOpen = true;
        prevScale = Time.timeScale;
        Time.timeScale = 0f;          // the house freezes
        AudioListener.pause = true;   // and holds its breath
        prevLock = Cursor.lockState; prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        RoundManager rm = RoundManager.Instance;
        int r = RoundManager.CurrentRound, s = Mathf.CeilToInt(rm.TimeLeft);
        subtitle.text = "Round " + r + " of " + rm.totalRounds + "  ·  " + (s / 60) + ":" + (s % 60).ToString("00") + " on the clock  ·  the house is frozen";
        restartText.text = "Round " + r + " starts over for everyone. Points from this round are gone" + (r > 1 ? "; earlier rounds keep their score." : ".");
        root.gameObject.SetActive(true);
        RefreshObjectives();
        RefreshBag();
        Show(mainScr);
    }

    // Fills the "Your bag" note: each carried item (and the golden knife, if held) with its story; empty slots say so.
    void RefreshBag()
    {
        if (bagName[0] == null) return;
        Hider human = null;
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None)) if (h.isHumanControlled) human = h;
        PlayerHUD hud = human != null ? human.GetComponent<PlayerHUD>() : null;
        var ids = new System.Collections.Generic.List<string>();
        var icons = new System.Collections.Generic.List<Sprite>();
        if (human != null && KnifeHolder.IsHolder(human)) { ids.Add("goldenknife"); icons.Add(null); }
        if (hud != null) for (int i = 0; i < hud.CarriedIds.Count; i++) { ids.Add(hud.CarriedIds[i]); icons.Add(hud.CarriedIcons[i]); }

        for (int i = 0; i < 4; i++)
        {
            bool has = i < ids.Count;
            bool show = has || i < 3; // three bag slots always show; the 4th row only for the knife
            bagIcon[i].gameObject.SetActive(has && icons[i] != null);
            if (has) bagIcon[i].sprite = icons[i];
            ItemLoreEntry e = has ? ItemLore.Find(ids[i]) : null;
            bagName[i].gameObject.SetActive(show);
            bagText[i].gameObject.SetActive(show);
            bagName[i].text = has ? (e != null ? e.displayName : ids[i]) : "<color=#a99c90>Empty</color>";
            bagText[i].text = has && e != null ? e.description : "";
        }
    }

    // Writes the night's objectives on the note: open ones in charcoal, done ones crossed out.
    void RefreshObjectives()
    {
        if (objectivesText == null) return;
        objectivesTitle.text = "NIGHT " + RoundManager.CurrentRound + "  OBJECTIVES";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var o in ObjectiveManager.Instance.Visible())
        {
            if (sb.Length > 0) sb.Append('\n');
            if (o.Value) sb.Append("<color=#a99c90><s>").Append(o.Key.text).Append("</s></color>");
            else sb.Append("<color=#c8171f>›</color>  ").Append(o.Key.text);
        }
        objectivesText.text = sb.ToString();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        root.gameObject.SetActive(false);
        Time.timeScale = prevScale;
        AudioListener.pause = false;
        Cursor.lockState = prevLock; Cursor.visible = prevVisible;
    }

    void Show(GameObject screen)
    {
        foreach (GameObject g in new[] { mainScr, settingsScr, controlsScr, restartScr, leaveScr, howScr }) if (g != null) g.SetActive(g == screen);
        current = screen;
        if (screen == settingsScr) foreach (Action a in refreshers) a();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    void RestartRound()
    {
        IsOpen = false;
        root.gameObject.SetActive(false);
        AudioListener.pause = false;
        Cursor.lockState = prevLock; Cursor.visible = prevVisible;
        // the house stays frozen while the blind comes down and the round loads again
        if (RoundManager.Instance != null) RoundManager.Instance.RestartRound();
    }

    void LeaveHouse()
    {
        IsOpen = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(mainMenuScene)) { SceneManager.LoadScene(mainMenuScene); return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------- building ----------------
    void Build()
    {
        GameObject cg = new GameObject("PauseMenu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cg.transform.SetParent(transform, false);
        Canvas c = cg.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 600; // over the HUD, alerts and the round intro
        CanvasScaler sc = cg.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280f, 720f);
        sc.matchWidthOrHeight = 0.5f;
        root = PaperUI.Rect("Pause", cg.transform);
        PaperUI.Stretch(root);

        dashH = DashSprite(true); dashV = DashSprite(false);

        // the frozen house behind a dark overlay (it blocks clicks reaching the game)
        Image dim = Fill(root, "Dim", new Color(8f / 255f, 3f / 255f, 7f / 255f, 0.72f));
        dim.raycastTarget = true;

        if (titleMode) { settingsScr = BuildSettings(); howScr = BuildHowToPlay(); return; }
        mainScr = BuildMain();
        settingsScr = BuildSettings();
        controlsScr = BuildControls();
        restartScr = BuildConfirm("Back to the elevator?", "", "Stay hidden", "Restart round", RestartRound, out restartText);
        TextMeshProUGUI unused;
        leaveScr = BuildConfirm("Leave the house?", "You walk out to the main menu. This match ends here and every score so far is lost.", "Stay inside", "Leave the house", LeaveHouse, out unused);
    }

    GameObject BuildMain()
    {
        RectTransform s = Screen("Main");
        // darker on the left, where the tickets are
        Image side = PaperUI.Img(s, "LeftShade", GradientSprite(), new Color(8f / 255f, 3f / 255f, 7f / 255f, 0.55f));
        side.preserveAspect = false;
        PaperUI.Place(side.rectTransform, 0, 0, 760, 720);

        // the marquee sign
        RectTransform sign = PaperUI.Place(PaperUI.Rect("Sign", s), 96, 116, 320, 100);
        PaperShape plate = PaperUI.Shape(sign, "Plate", Ink, 14f);
        PaperUI.Stretch(plate.rectTransform);
        plate.shadow = true; plate.shadowOffset = new Vector2(8f, -10f); plate.shadowColor = new Color(0, 0, 0, 0.6f); plate.Refresh();
        Bulbs(sign, 320, 100);
        TitleText(sign, "PAUSED", 66f, 0, 16, 320, 76, TextAlignmentOptions.Center);
        PaperUI.Rotate(sign, -2f);

        subtitle = T(s, "", 18f, Dust, 96, 228, 900, 30, 0);

        float y = 270;
        Ticket(s, 96, y, 470, 66, "01", "Resume", Red, PaperUI.Hex("a51018"), Close, false); y += 76;
        Ticket(s, 96, y, 470, 66, "02", "Restart round", Card, Stub, () => Show(restartScr), false); y += 76;
        Ticket(s, 96, y, 470, 66, "03", "Settings", Card, Stub, () => Show(settingsScr), false); y += 76;
        Ticket(s, 96, y, 470, 66, "04", "Controls", Card, Stub, () => Show(controlsScr), false); y += 76;
        Ticket(s, 96, y, 470, 58, "05", "Leave the house", Dust, Dust, () => Show(leaveScr), true); y += 72;
        TextMeshProUGUI esc = T(s, "PRESS ESC TO RESUME", 15f, Faint, 96, y, 400, 24, 0);
        PaperUI.LetterSpacing(esc, 1f);

        // the night's objectives, on a cream note pinned on the right
        RectTransform note = PaperUI.Place(PaperUI.Rect("Objectives", s), 712, 214, 410, 226);
        PaperShape paper = PaperUI.Cut(PaperUI.Torn(note, "Paper", PaperUI.Hex("fbf4e4"), PaperUI.NewsTear));
        paper.edgeColor = PaperUI.Hex("e2d3b4"); paper.Refresh();
        PaperUI.Stretch(paper.rectTransform);
        PaperShape pin = PaperUI.Shape(note, "Pin", Red, 9f);
        PaperUI.Place(pin.rectTransform, 201, 8, 18, 18);
        pin.shadow = true; pin.shadowOffset = new Vector2(2f, -3f); pin.shadowColor = new Color(0, 0, 0, 0.45f); pin.Refresh();
        objectivesTitle = PaperUI.Text(note, "Title", "OBJECTIVES", 30f, Red, true, TextAlignmentOptions.Center);
        objectivesTitle.font = PaperUI.Title;
        PaperUI.Place(objectivesTitle.rectTransform, 20, 30, 380, 40);
        objectivesText = PaperUI.Text(note, "List", "", 20f, PaperUI.Hex("2b2427"), true, TextAlignmentOptions.TopLeft);
        PaperUI.Place(objectivesText.rectTransform, 34, 80, 352, 140);
        objectivesText.textWrappingMode = TextWrappingModes.Normal;
        objectivesText.paragraphSpacing = 10f;
        objectivesText.enableAutoSizing = true; objectivesText.fontSizeMin = 13f; objectivesText.fontSizeMax = 20f;
        PaperUI.Rotate(note, 1.5f);

        // "Your bag": what the player carries, each item with its story
        RectTransform bag = PaperUI.Place(PaperUI.Rect("Your Bag", s), 722, 454, 410, 252);
        PaperShape bagPaper = PaperUI.Cut(PaperUI.Torn(bag, "Paper", PaperUI.Hex("fbf4e4"), PaperUI.CardTear));
        bagPaper.edgeColor = PaperUI.Hex("e2d3b4"); bagPaper.Refresh();
        PaperUI.Stretch(bagPaper.rectTransform);
        TextMeshProUGUI bagTitle = PaperUI.Text(bag, "Title", "YOUR BAG", 24f, Red, true, TextAlignmentOptions.Left);
        bagTitle.font = PaperUI.Title;
        PaperUI.Place(bagTitle.rectTransform, 26, 14, 300, 32);
        for (int i = 0; i < 4; i++)
        {
            float ry = 52 + i * 62;
            bagIcon[i] = PaperUI.Img(bag, "Icon " + i, null);
            bagIcon[i].preserveAspect = true;
            PaperUI.Place(bagIcon[i].rectTransform, 24, ry, 38, 38);
            bagName[i] = PaperUI.Text(bag, "Name " + i, "", 17f, PaperUI.Hex("2b2427"), true, TextAlignmentOptions.TopLeft);
            PaperUI.Place(bagName[i].rectTransform, 72, ry - 2, 310, 20);
            bagText[i] = PaperUI.Text(bag, "Story " + i, "", 15f, PaperUI.Hex("5e524a"), false, TextAlignmentOptions.TopLeft);
            PaperUI.Place(bagText[i].rectTransform, 72, ry + 19, 316, 36);
            bagText[i].textWrappingMode = TextWrappingModes.Normal;
            bagText[i].enableAutoSizing = true; bagText[i].fontSizeMin = 13f; bagText[i].fontSizeMax = 15f;
        }
        PaperUI.Rotate(bag, -1f);
        return s.gameObject;
    }

    // ---------------- settings ----------------
    GameObject BuildSettings()
    {
        RectTransform s = Screen("Settings");
        RectTransform sheet = SheetOf(s, "Settings");

        T(sheet, "How you watch the house", 19f, Gold, 36, 80, 600, 28, 2);
        Texture2D[] pics = { camClassic, camHigher, camShoulder };
        string[] names = { "Classic", "Higher", "Over the shoulder" };
        string[] tags = { "", "DEFAULT", "" };
        string[] descs = {
            "Behind and above you. The view Lay Low started with.",
            "Up over the rooms, so the player cards never hide the floor.",
            "Low and close. Scarier, but walls hide more." };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float x = 36 + i * 378;
            RectTransform card = PaperUI.Place(PaperUI.Rect("View " + names[i], sheet), x, 110, 360, 218);
            PaperShape bg = PaperUI.Shape(card, "Bg", CardDeep, 12f); PaperUI.Stretch(bg.rectTransform); bg.raycastTarget = true;
            PaperShape ring = PaperUI.Shape(card, "Ring", Line, 12f); PaperUI.Stretch(ring.rectTransform); ring.ring = 2f; ring.Refresh();
            RawImage pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            pic.transform.SetParent(card, false); pic.raycastTarget = false;
            PaperUI.Place(pic.rectTransform, 10, 10, 340, 128);
            pic.texture = pics[i];
            float vis = 128f / (340f * 9f / 16f);             // show the middle band of the 16:9 shot
            pic.uvRect = new Rect(0f, (1f - vis) * 0.5f, 1f, vis);
            RectTransform stamp = PaperUI.Place(PaperUI.Rect("InUse", card), 254, 18, 88, 28);
            PaperShape sb = PaperUI.Shape(stamp, "Bg", new Color(14f / 255f, 7f / 255f, 12f / 255f, 0.78f), 6f); PaperUI.Stretch(sb.rectTransform);
            PaperShape sr = PaperUI.Shape(stamp, "Ring", Gold, 6f); PaperUI.Stretch(sr.rectTransform); sr.ring = 3f; sr.Refresh();
            TextMeshProUGUI st = T(stamp, "IN USE", 15f, Gold, 0, 3, 88, 24, 2, TextAlignmentOptions.Center);
            PaperUI.Rotate(stamp, 6f);
            T(card, names[i], 19f, Cream, 10, 142, 260, 30, 0);
            if (tags[i] != "") { TextMeshProUGUI tg = T(card, tags[i], 12f, Gold, 230, 150, 120, 20, 0, TextAlignmentOptions.Right); PaperUI.LetterSpacing(tg, 1.5f); }
            TextMeshProUGUI d = T(card, descs[i], 13f, Mute, 10, 172, 340, 40, 1); Wrap(d, 1.3f);
            Button b = card.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { SetCameraView(idx); foreach (Action a in refreshers) a(); });
            refreshers.Add(() =>
            {
                bool on = CurrentCameraView() == idx;
                bg.color = on ? Card : CardDeep;
                ring.color = on ? Red : Line; ring.ring = on ? 3f : 2f; ring.Refresh();
                stamp.gameObject.SetActive(on);
            });
        }

        const float C0 = 36, CW = 345.33f, GAP = 40, Y0 = 342;
        float[] cx = { C0, C0 + CW + GAP, C0 + 2 * (CW + GAP) };

        T(sheet, "What you hear", 19f, Gold, cx[0], Y0, CW, 28, 2);
        SliderRow(sheet, cx[0], Y0 + 30, CW, "Master", "Everything at once", () => GameSettings.Master, v => GameSettings.Master = v);
        SliderRow(sheet, cx[0], Y0 + 85, CW, "Music", "Round music and the intro cards", () => GameSettings.Music, v => GameSettings.Music = v);
        SliderRow(sheet, cx[0], Y0 + 140, CW, "The clown", "His laughs, footsteps and roars", () => GameSettings.Clown, v => GameSettings.Clown = v);
        SliderRow(sheet, cx[0], Y0 + 195, CW, "House", "Doors, TV, vacuum, rocks, hazards", () => GameSettings.House, v => GameSettings.House = v);
        SliderRow(sheet, cx[0], Y0 + 250, CW, "Heartbeat", "Your pulse as he gets closer", () => GameSettings.Heartbeat, v => GameSettings.Heartbeat = v);

        T(sheet, "Your machine", 19f, Gold, cx[1], Y0, CW, 28, 2);
        Resolution[] res = GameSettings.Resolutions();
        CyclerRow(sheet, cx[1], Y0 + 30, CW, "Resolution", "Size of the picture on screen", res.Length,
            () => { int i = GameSettings.ResolutionIndex; if (i < 0 || i >= res.Length) { i = Array.FindIndex(res, r => r.width == UnityEngine.Screen.currentResolution.width && r.height == UnityEngine.Screen.currentResolution.height); if (i < 0) i = 0; } return i; },
            i => GameSettings.ResolutionIndex = i,
            i => res.Length == 0 ? "This screen" : res[i].width + " × " + res[i].height);
        SegmentRow(sheet, cx[1], Y0 + 85, CW, "Window", "Fullscreen, borderless or a window", new[] { "Full", "Borderless", "Window" },
            () => GameSettings.WindowMode, v => GameSettings.WindowMode = v);
        string[] q = QualityLabels();
        SegmentRow(sheet, cx[1], Y0 + 140, CW, "Quality", "Shadows, lights and effects detail", q,
            () => Mathf.Clamp(GameSettings.Quality, 0, q.Length - 1), v => GameSettings.Quality = v);
        CyclerRow(sheet, cx[1], Y0 + 195, CW, "Frame limit", "Lower it if your laptop runs hot", GameSettings.FrameLimits.Length,
            () => GameSettings.FrameLimit, i => GameSettings.FrameLimit = i,
            i => GameSettings.FrameLimits[i] < 0 ? "Off" : GameSettings.FrameLimits[i] + " fps");
        ToggleRow(sheet, cx[1], Y0 + 250, CW, "V-Sync", "Stops the picture from tearing", () => GameSettings.VSync, v => GameSettings.VSync = v);

        T(sheet, "How you play", 19f, Gold, cx[2], Y0, CW, 28, 2);
        SliderRow(sheet, cx[2], Y0 + 30, CW, "Turn speed", "How fast A and D turn you", () => GameSettings.TurnSpeed, v => GameSettings.TurnSpeed = v);
        ToggleRow(sheet, cx[2], Y0 + 85, CW, "Start in first person", "Begin each round behind your own eyes", () => GameSettings.StartFirstPerson, v => GameSettings.StartFirstPerson = v);
        ToggleRow(sheet, cx[2], Y0 + 140, CW, "Aiming hints", "The card that explains an item while aiming", () => GameSettings.AimHints, v => GameSettings.AimHints = v);
        ToggleRow(sheet, cx[2], Y0 + 195, CW, "Player outlines", "See the others through walls in their colour", () => GameSettings.PlayerOutlines, v => GameSettings.PlayerOutlines = v);
        ToggleRow(sheet, cx[2], Y0 + 250, CW, "Calm the flicker", "Softer light flashes in the blackout round", () => GameSettings.CalmFlicker, v => GameSettings.CalmFlicker = v);
        return s.gameObject;
    }

    // the camera view lives on the game's camera; from the main menu it is only saved, and the game reads it
    const string CameraViewKey = "LayLow.CameraView";
    static void SetCameraView(int idx)
    {
        if (CameraViewSettings.Instance != null) { CameraViewSettings.Instance.SetView(idx); return; }
        PlayerPrefs.SetInt(CameraViewKey, idx); PlayerPrefs.Save();
    }
    static int CurrentCameraView()
    {
        return CameraViewSettings.Instance != null ? CameraViewSettings.Instance.Current : PlayerPrefs.GetInt(CameraViewKey, 1);
    }

    // ---------------- main menu (title mode) ----------------
    public bool SheetOpen { get { return root != null && root.gameObject.activeSelf; } }

    // opens Settings (settings = true) or How to play over the title screen; onClosed runs when it is closed again
    public void OpenSheet(bool settings, Action onClosed)
    {
        sheetClosed = onClosed;
        root.gameObject.SetActive(true);
        Show(settings ? settingsScr : howScr);
    }

    public void CloseSheet()
    {
        root.gameObject.SetActive(false);
        Action a = sheetClosed; sheetClosed = null;
        if (a != null) a();
    }

    // How to play: the rules of the night on the left, every key on the right
    GameObject BuildHowToPlay()
    {
        RectTransform s = Screen("HowToPlay");
        RectTransform sheet = SheetOf(s, "How to play");
        float L = 36, R = 36 + 526 + 64;
        T(sheet, "The night", 21f, Gold, L, 80, 500, 30, 2);
        string[] rules = {
            "Four rounds, one clown. Every round you live through scores a point.",
            "Each round starts in the elevator. When the doors open, find somewhere to hide.",
            "Press H at a hiding spot to get in. He checks hiding spots too, so don't get comfortable.",
            "Your heartbeat speeds up the closer he gets.",
            "Rocks, feathers, door locks, ice guns and whistles: use them to throw him off, or to slow the others down.",
            "Round 4: a golden knife drops. Whoever grabs it hunts the rest. Only one of you leaves this house alive." };
        float y = 116;
        for (int i = 0; i < rules.Length; i++)
        {
            RectTransform dot = PaperUI.Place(PaperUI.Rect("Rule " + (i + 1), sheet), L, y + 2, 32, 32);
            PaperShape disc = PaperUI.Shape(dot, "Disc", Red, 16f); PaperUI.Stretch(disc.rectTransform);
            T(dot, (i + 1).ToString(), 18f, Cream, 0, 5, 32, 26, 2, TextAlignmentOptions.Center);
            TextMeshProUGUI t = T(sheet, rules[i], 17f, Cream, L + 46, y, 480, 60, 1); Wrap(t, 1.3f);
            Dash(sheet, L, y + 64, 526);
            y += 76;
        }

        T(sheet, "Keys", 21f, Gold, R, 80, 500, 30, 2);
        float k = 112, step = 44;
        KeyRow(sheet, R, k, new[] { "W", "S" }, "Walk forward and back"); k += step;
        KeyRow(sheet, R, k, new[] { "A", "D" }, "Turn left and right"); k += step;
        KeyRow(sheet, R, k, new[] { "Space" }, "Jump"); k += step;
        KeyRow(sheet, R, k, new[] { "V" }, "First or third person"); k += step;
        KeyRow(sheet, R, k, new[] { "H" }, "Hide, or come out"); k += step;
        KeyRow(sheet, R, k, new[] { "R" }, "Rock: aim, click the floor"); k += step;
        KeyRow(sheet, R, k, new[] { "F" }, "Feather: tickle a player"); k += step;
        KeyRow(sheet, R, k, new[] { "E" }, "Door lock: lock a doorway"); k += step;
        KeyRow(sheet, R, k, new[] { "G" }, "Ice gun: freeze a player"); k += step;
        KeyRow(sheet, R, k, new[] { "Click" }, "Round 4: stab with the knife"); k += step;
        KeyRow(sheet, R, k, new[] { "Esc" }, "Pause and resume");
        return s.gameObject;
    }

    static string[] QualityLabels()
    {
        return new[] { "Low", "Medium", "High" };
    }

    // ---------------- controls ----------------
    GameObject BuildControls()
    {
        RectTransform s = Screen("Controls");
        RectTransform sheet = SheetOf(s, "Controls");
        float L = 36, R = 36 + 526 + 64;
        T(sheet, "Sneaking around", 21f, Gold, L, 80, 500, 30, 2);
        KeyRow(sheet, L, 112, new[] { "W", "S" }, "Walk forward and back");
        KeyRow(sheet, L, 158, new[] { "A", "D" }, "Turn left and right");
        KeyRow(sheet, L, 204, new[] { "Space" }, "Jump");
        KeyRow(sheet, L, 250, new[] { "V" }, "Switch first and third person");
        KeyRow(sheet, L, 296, new[] { "H" }, "Hide in a spot, or come out");
        T(sheet, "The menu", 21f, Gold, L, 362, 500, 30, 2);
        KeyRow(sheet, L, 394, new[] { "Esc" }, "Pause and resume, nothing else");

        T(sheet, "Using what you find", 21f, Gold, R, 80, 500, 30, 2);
        KeyRow(sheet, R, 112, new[] { "R" }, "Rock: aim, then click the floor");
        KeyRow(sheet, R, 158, new[] { "F" }, "Feather: click a card to tickle");
        KeyRow(sheet, R, 204, new[] { "E" }, "Door lock: click a doorway");
        KeyRow(sheet, R, 250, new[] { "G" }, "Ice gun: click a card to freeze");
        KeyRow(sheet, R, 296, new[] { "R-Click" }, "Put the item away");
        T(sheet, "Round 4 · the golden knife", 21f, Gold, R, 362, 500, 30, 2);
        KeyRow(sheet, R, 394, new[] { "Click" }, "Stab whoever is closest");
        KeyRow(sheet, R, 440, new[] { "H" }, "Rip open a hiding spot");

        T(sheet, "Arrow keys work in place of W A S D. Pressing an item's key again also puts it away.", 15f, Faint, L, 510, 1100, 24, 1);
        return s.gameObject;
    }

    void KeyRow(RectTransform parent, float x, float y, string[] keys, string what)
    {
        float kx = x;
        foreach (string k in keys)
        {
            float w = Mathf.Max(36f, k.Length * 10f + 20f);
            RectTransform cap = PaperUI.Place(PaperUI.Rect("Key " + k, parent), kx, y + 4, w, 34);
            PaperShape lip = PaperUI.Shape(cap, "Lip", Faint, 8f); PaperUI.Stretch(lip.rectTransform); lip.rectTransform.anchoredPosition = new Vector2(0, -4);
            PaperShape face = PaperUI.Shape(cap, "Face", Cream, 8f); PaperUI.Stretch(face.rectTransform);
            T(cap, k, 16f, Ink, 0, 4, w, 26, 0, TextAlignmentOptions.Center);
            kx += w + 6;
        }
        T(parent, what, 19f, Cream, x + 166, y + 6, 360, 30, 0);
        Dash(parent, x, y + 45, 526);
    }

    // ---------------- confirm cards ----------------
    GameObject BuildConfirm(string title, string body, string stay, string go, Action onGo, out TextMeshProUGUI bodyText)
    {
        RectTransform s = Screen("Confirm " + title);
        RectTransform card = PaperUI.Place(PaperUI.Rect("Card", s), 360, 172, 560, 376);
        PaperShape paper = PaperUI.Torn(card, "Paper", Sheet, PaperUI.WarningTear);
        PaperUI.Stretch(paper.rectTransform); PaperUI.Cut(paper);
        paper.shadowOffset = new Vector2(8f, -11f); paper.shadowColor = new Color(0, 0, 0, 0.6f); paper.Refresh();
        TextMeshProUGUI tt = TitleText(card, title, 44f, 30, 34, 500, 112, TextAlignmentOptions.Center);
        foreach (TextMeshProUGUI t in tt.transform.parent.GetComponentsInChildren<TextMeshProUGUI>()) { Wrap(t, 1f); t.lineSpacing = 8f; } // Luckiest Guy: a little air between the two lines
        bodyText = T(card, body, 19f, Dust, 50, 154, 460, 90, 1, TextAlignmentOptions.Top);
        Wrap(bodyText, 1.4f);
        Ticket(card, 62, 270, 210, 60, null, stay, Card, Card, () => Show(mainScr), false);
        Ticket(card, 288, 270, 210, 60, null, go, Red, Red, onGo, false);
        PaperUI.Rotate(card, -1.5f);
        return s.gameObject;
    }

    // ---------------- pieces ----------------
    RectTransform Screen(string name)
    {
        RectTransform r = PaperUI.Rect(name, root);
        PaperUI.Stretch(r);
        return r;
    }

    // the torn sheet of dark paper that Settings and Controls sit on, with its title and Back button
    RectTransform SheetOf(RectTransform screen, string title)
    {
        RectTransform sheet = PaperUI.Place(PaperUI.Rect("Sheet", screen), 46, 18, 1188, 684);
        PaperShape paper = PaperUI.Torn(sheet, "Paper", Sheet, PaperUI.CardTear);
        PaperUI.Stretch(paper.rectTransform); PaperUI.Cut(paper);
        paper.shadowOffset = new Vector2(8f, -11f); paper.shadowColor = new Color(0, 0, 0, 0.6f); paper.Refresh();
        TitleText(sheet, title, 44f, 36, 22, 600, 56, TextAlignmentOptions.Left);
        // back: the curled gold arrow, drawn like the titles (design option B), a little below the sheet's top edge
        const float BW = 64f, BH = 56f;
        RectTransform back = PaperUI.Place(PaperUI.Rect("Back", sheet), 1188 - 44 - BW, 40, BW, BH);
        Image arrow = PaperUI.Img(back, "Arrow", backArrow, Color.white);
        PaperUI.Stretch(arrow.rectTransform); arrow.preserveAspect = true; arrow.raycastTarget = true;
        Button b = back.gameObject.AddComponent<Button>(); b.targetGraphic = arrow; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (titleMode) CloseSheet(); else Show(mainScr); });
        HoverNudge bn = back.gameObject.AddComponent<HoverNudge>(); bn.amount = new Vector2(-6f, 2f); bn.turn = -4f; // swings back on hover
        PaperUI.Rotate(sheet, -0.4f);
        return sheet;
    }

    // a carnival ticket: notched both ends, a numbered stub behind a perforation
    void Ticket(RectTransform parent, float x, float y, float w, float h, string number, string label, Color fill, Color stubFill, Action onClick, bool outline)
    {
        RectTransform t = PaperUI.Place(PaperUI.Rect("Ticket " + label, parent), x, y, w, h);
        PaperShape body = PaperUI.Torn(t, "Body", fill, TicketPoints(w, h, 11f, true, true));
        PaperUI.Stretch(body.rectTransform);
        body.raycastTarget = true;
        if (outline) { Color c = fill; c.a = 0.55f; body.color = c; body.ring = 3f; body.Refresh(); }
        Color text = outline ? Dust : Cream;
        float lx = 0;
        if (number != null)
        {
            const float sw = 84f;
            if (!outline)
            {
                PaperShape stub = PaperUI.Torn(t, "Stub", stubFill, TicketPoints(sw, h, 11f, true, false));
                PaperUI.Place(stub.rectTransform, 0, 0, sw, h);
            }
            Image perf = PaperUI.Img(t, "Perforation", dashV, new Color(1f, 241f / 255f, 214f / 255f, outline ? 0.4f : 0.45f));
            perf.type = Image.Type.Tiled; perf.preserveAspect = false;
            PaperUI.Place(perf.rectTransform, sw - 1.5f, 6, 3, h - 12);
            TextMeshProUGUI n = T(t, "<size=12><cspace=2>NO.</cspace></size>\n" + number, 24f, text, 0, h * 0.5f - 24, sw, 48, 0, TextAlignmentOptions.Center);
            n.lineSpacing = -38f;
            lx = sw + 24;
        }
        if (number != null) T(t, label, outline ? 23f : 27f, text, lx, h * 0.5f - 21, w - lx, 42, 0, TextAlignmentOptions.Left);
        else T(t, label, 22f, text, 0, h * 0.5f - 18, w, 36, 0, TextAlignmentOptions.Center);
        Button b = t.gameObject.AddComponent<Button>(); b.targetGraphic = body; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => onClick());
        HoverNudge hn = t.gameObject.AddComponent<HoverNudge>();
        if (number == null) { hn.amount = new Vector2(0, 4); hn.turn = 0f; }
    }

    static Vector2[] TicketPoints(float w, float h, float r, bool left, bool right)
    {
        var p = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0) };
        if (right) for (int i = 0; i <= 8; i++) { float a = Mathf.Lerp(-90f, 90f, i / 8f) * Mathf.Deg2Rad; p.Add(new Vector2((w - r * Mathf.Cos(a)) / w, (h * 0.5f + r * Mathf.Sin(a)) / h)); }
        p.Add(new Vector2(1, 1)); p.Add(new Vector2(0, 1));
        if (left) for (int i = 0; i <= 8; i++) { float a = Mathf.Lerp(90f, -90f, i / 8f) * Mathf.Deg2Rad; p.Add(new Vector2(r * Mathf.Cos(a) / w, (h * 0.5f + r * Mathf.Sin(a)) / h)); }
        return p.ToArray();
    }

    void Bulbs(RectTransform sign, float w, float h)
    {
        const float step = 15f, inset = 7f;
        var pts = new List<Vector2>();
        for (float x = inset; x <= w - inset; x += step) { pts.Add(new Vector2(x, inset)); pts.Add(new Vector2(x, h - inset)); }
        for (float y = inset + step; y < h - inset; y += step) { pts.Add(new Vector2(inset, y)); pts.Add(new Vector2(w - inset, y)); }
        foreach (Vector2 p in pts)
        {
            PaperShape b = PaperUI.Shape(sign, "Bulb", Gold, 4f);
            PaperUI.Place(b.rectTransform, p.x - 4f, p.y - 4f, 8f, 8f);
            bulbs.Add(b);
        }
    }

    // Luckiest Guy in gold with the sticker shadows (red, then ink); returns the gold text
    TextMeshProUGUI TitleText(RectTransform parent, string text, float size, float x, float y, float w, float h, TextAlignmentOptions align)
    {
        RectTransform g = PaperUI.Place(PaperUI.Rect("Title " + text, parent), x, y, w, h);
        TextMeshProUGUI ink = T(g, text, size, Ink, 6, 6, w, h, 2, align);
        TextMeshProUGUI red = T(g, text, size, Red, 4, 4, w, h, 2, align);
        TextMeshProUGUI top = T(g, text, size, Gold, 0, 0, w, h, 2, align);
        foreach (TextMeshProUGUI t in new[] { ink, red, top }) PaperUI.LetterSpacing(t, 1f);
        return top;
    }

    // ---------------- settings rows ----------------
    void RowBase(RectTransform p, float x, float y, float w, string name, string desc)
    {
        T(p, name, 17f, Cream, x, y + 4, w * 0.55f, 26, 0);
        TextMeshProUGUI d = T(p, desc, 13f, Mute, x, y + 29, w, 20, 1);
        Dash(p, x, y + 52, w);
    }

    void SliderRow(RectTransform p, float x, float y, float w, string name, string desc, Func<int> get, Action<int> set)
    {
        RowBase(p, x, y, w, name, desc);
        TextMeshProUGUI val = T(p, "", 14f, Gold, x + w - 34, y + 6, 34, 22, 0, TextAlignmentOptions.Right);
        RectTransform r = PaperUI.Place(PaperUI.Rect("Slider " + name, p), x + w - 34 - 8 - 150, y + 6, 150, 20);
        PaperShape track = PaperUI.Shape(r, "Track", Border, 4f); track.raycastTarget = true;
        Center(track.rectTransform, 8f);
        RectTransform fillArea = PaperUI.Rect("Fill Area", r); Center(fillArea, 8f);
        PaperShape fill = PaperUI.Shape(fillArea, "Fill", Red, 4f); PaperUI.Stretch(fill.rectTransform);
        RectTransform handleArea = PaperUI.Rect("Handle Area", r); PaperUI.Stretch(handleArea); handleArea.offsetMin = new Vector2(9, 0); handleArea.offsetMax = new Vector2(-9, 0);
        PaperShape handle = PaperUI.Shape(handleArea, "Handle", Cream, 9f); handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(18f, 0f);
        Slider s = r.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0; s.maxValue = 100; s.wholeNumbers = true;
        s.transition = Selectable.Transition.None;
        Navigation nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
        bool loading = false;
        s.onValueChanged.AddListener(v => { val.text = ((int)v).ToString(); if (!loading) set((int)v); });
        refreshers.Add(() => { loading = true; s.value = get(); val.text = get().ToString(); loading = false; });
    }

    void ToggleRow(RectTransform p, float x, float y, float w, string name, string desc, Func<bool> get, Action<bool> set)
    {
        RowBase(p, x, y, w, name, desc);
        RectTransform r = PaperUI.Place(PaperUI.Rect("Toggle " + name, p), x + w - 42, y + 5, 42, 24);
        PaperShape bg = PaperUI.Shape(r, "Bg", Border, 12f); PaperUI.Stretch(bg.rectTransform); bg.raycastTarget = true;
        PaperShape knob = PaperUI.Shape(r, "Knob", Mute, 9f);
        Action draw = () =>
        {
            bool on = get();
            bg.color = on ? Red : Border; knob.color = on ? Cream : Mute;
            PaperUI.Place(knob.rectTransform, on ? 21 : 3, 3, 18, 18);
        };
        Button b = r.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { set(!get()); draw(); });
        refreshers.Add(draw);
    }

    void SegmentRow(RectTransform p, float x, float y, float w, string name, string desc, string[] options, Func<int> get, Action<int> set)
    {
        RowBase(p, x, y, w, name, desc);
        float[] ws = new float[options.Length]; float total = 0;
        for (int i = 0; i < options.Length; i++) { ws[i] = options[i].Length * 7.6f + 18f; total += ws[i]; }
        RectTransform r = PaperUI.Place(PaperUI.Rect("Segments " + name, p), x + w - total - 4, y + 3, total + 4, 26);
        PaperShape frame = PaperUI.Shape(r, "Frame", Border, 8f); PaperUI.Stretch(frame.rectTransform); frame.ring = 2f; frame.Refresh();
        var bgs = new PaperShape[options.Length]; var labels = new TextMeshProUGUI[options.Length];
        float sx = 2;
        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            RectTransform seg = PaperUI.Place(PaperUI.Rect(options[i], r), sx, 2, ws[i], 22);
            bgs[i] = PaperUI.Shape(seg, "Bg", Color.clear, 6f); PaperUI.Stretch(bgs[i].rectTransform); bgs[i].raycastTarget = true;
            labels[i] = T(seg, options[i], 13f, Mute, 0, 1, ws[i], 20, 0, TextAlignmentOptions.Center);
            Button b = seg.gameObject.AddComponent<Button>(); b.targetGraphic = bgs[i]; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { set(idx); foreach (Action a in refreshers) a(); });
            sx += ws[i];
        }
        refreshers.Add(() =>
        {
            int cur = get();
            for (int i = 0; i < options.Length; i++) { bgs[i].color = i == cur ? Red : Color.clear; labels[i].color = i == cur ? Cream : Mute; }
        });
    }

    void CyclerRow(RectTransform p, float x, float y, float w, string name, string desc, int count, Func<int> get, Action<int> set, Func<int, string> label)
    {
        RowBase(p, x, y, w, name, desc);
        RectTransform r = PaperUI.Place(PaperUI.Rect("Cycler " + name, p), x + w - 150, y + 3, 150, 26);
        PaperShape bg = PaperUI.Shape(r, "Bg", Card, 8f); PaperUI.Stretch(bg.rectTransform);
        PaperShape frame = PaperUI.Shape(r, "Frame", Border, 8f); PaperUI.Stretch(frame.rectTransform); frame.ring = 2f; frame.Refresh();
        TextMeshProUGUI v = T(r, "", 14f, Cream, 24, 2, 102, 22, 0, TextAlignmentOptions.Center);
        Action draw = () => v.text = count > 0 ? label(Mathf.Clamp(get(), 0, count - 1)) : label(0);
        for (int side = -1; side <= 1; side += 2)
        {
            int dir = side;
            RectTransform a = PaperUI.Place(PaperUI.Rect(side < 0 ? "Prev" : "Next", r), side < 0 ? 0 : 126, 0, 24, 26);
            PaperShape ab = PaperUI.Shape(a, "Hit", Color.clear, 6f); PaperUI.Stretch(ab.rectTransform); ab.raycastTarget = true;
            T(a, side < 0 ? "<" : ">", 16f, Gold, 0, 1, 24, 24, 0, TextAlignmentOptions.Center);
            Button b = a.gameObject.AddComponent<Button>(); b.targetGraphic = ab; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { if (count <= 0) return; set((get() + dir + count) % count); draw(); });
        }
        refreshers.Add(draw);
    }

    // ---------------- small helpers ----------------
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

    static void Wrap(TextMeshProUGUI t, float lineHeight)
    {
        t.textWrappingMode = TextWrappingModes.Normal;
        PaperUI.LineHeight(t, lineHeight);
    }

    static void Center(RectTransform r, float height)
    {
        r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(0f, height); r.anchoredPosition = Vector2.zero;
    }

    static Image Fill(Transform parent, string name, Color c)
    {
        RectTransform r = PaperUI.Rect(name, parent);
        Image i = r.gameObject.AddComponent<Image>();
        i.color = c; i.raycastTarget = false;
        PaperUI.Stretch(r);
        return i;
    }

    void Dash(RectTransform parent, float x, float y, float w)
    {
        Image d = PaperUI.Img(parent, "Dash", dashH, Line);
        d.type = Image.Type.Tiled; d.preserveAspect = false;
        PaperUI.Place(d.rectTransform, x, y, w, 2);
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

    static Sprite GradientSprite()
    {
        Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 256; i++) tex.SetPixel(i, 0, new Color(1, 1, 1, 1f - i / 255f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f), 100f);
    }
}

// The ticket hover: slides right a little and tips, on real time (the game is frozen while paused)
public class HoverNudge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Vector2 amount = new Vector2(10f, 0f);
    public float turn = -0.6f;
    RectTransform rt;
    Vector2 home;
    float baseAngle, k, target;
    bool ready;

    void Start() { rt = (RectTransform)transform; home = rt.anchoredPosition; baseAngle = rt.localEulerAngles.z; ready = true; }
    void OnDisable() { k = target = 0f; if (ready) Apply(); }
    public void OnPointerEnter(PointerEventData e) { target = 1f; }
    public void OnPointerExit(PointerEventData e) { target = 0f; }

    void Update()
    {
        if (!ready || Mathf.Approximately(k, target)) return;
        k = Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    void Apply()
    {
        float e = PaperUI.EaseOut(k);
        rt.anchoredPosition = home + amount * e;
        rt.localEulerAngles = new Vector3(0f, 0f, baseAngle - turn * e);
    }
}
