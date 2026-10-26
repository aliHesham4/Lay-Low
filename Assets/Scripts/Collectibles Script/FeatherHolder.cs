using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Carried by a player (human or AI) who has picked up the feather.
// Human: press the key, the mouse turns into the feather, click a player's card in the top HUD to tickle them.
// The tickled player laughs (animation + sound) and the killer runs to where the laugh came from.
public class FeatherHolder : MonoBehaviour
{
    [Header("Use")]
    public Key useKey = Key.F;
    [Tooltip("Size of the feather that follows the mouse (canvas units).")]
    public float cursorSize = 80f;
    [Tooltip("Where the tip of the feather is inside its image (0..1), so the tip is on the mouse position.")]
    public Vector2 cursorTip = new Vector2(0.1f, 0.1f);

    [HideInInspector] public Sprite icon;
    [HideInInspector] public AudioClip laughSound;
    [HideInInspector] public float hearingRadius = 40f;
    [HideInInspector] public float laughVolume = 1f;

    Hider hider;
    PlayerHUD hud;
    PlayerStoneThrower thrower;
    bool has;
    bool aiming;
    bool throwerWasEnabled;
    bool cursorWasVisible;
    PlayerHUD hovered;

    Canvas canvas;
    RectTransform canvasRect;
    Image cursorImage;
    TMP_Text hint;

    public bool HasFeather { get { return has; } }
    public bool IsAiming { get { return aiming; } }

    void Awake()
    {
        hider = GetComponent<Hider>();
        hud = GetComponent<PlayerHUD>();
        thrower = GetComponent<PlayerStoneThrower>();
    }

    // Called by the feather pickup. Returns false if the bag is full or there is already one.
    public bool Collect(Sprite iconSprite, AudioClip clip, float radius, float volume)
    {
        if (has) return false;
        if (hud != null && !hud.AddItem("feather", iconSprite)) return false;
        icon = iconSprite; laughSound = clip; hearingRadius = radius; laughVolume = volume;
        has = true;
        return true;
    }

    void Update()
    {
        if (!has || hider == null || !hider.isHumanControlled) return;

        if (hider.IsCaught) { if (aiming) EndAim(); return; }
        if (Keyboard.current == null || Mouse.current == null) return;

        bool keyPressed = Keyboard.current[useKey].wasPressedThisFrame;

        if (!aiming)
        {
            if (keyPressed && Time.timeScale > 0f) BeginAim();
            return;
        }

        // ---- aiming ----
        if (keyPressed || Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            EndAim();
            return;
        }

        Vector2 mouse = Mouse.current.position.ReadValue();
        MoveCursor(mouse);

        PlayerHUD over = HudUnder(mouse);
        if (over != hovered)
        {
            if (hovered != null) hovered.SetTickleHighlight(false);
            hovered = over;
            if (hovered != null) hovered.SetTickleHighlight(true);
        }

        if (hovered != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Hider target = hovered.GetComponent<Hider>();
            EndAim();
            Tickle(target);
        }
    }

    // The card of another, still free player that the mouse is over
    PlayerHUD HudUnder(Vector2 mouse)
    {
        foreach (PlayerHUD h in FindObjectsByType<PlayerHUD>(FindObjectsSortMode.None))
        {
            if (h == null || h == hud) continue;
            Hider th = h.GetComponent<Hider>();
            if (th == null || th.IsCaught) continue;
            RectTransform card = h.CardRect;
            if (card != null && RectTransformUtility.RectangleContainsScreenPoint(card, mouse, null)) return h;
        }
        return null;
    }

    // The feather is used up and the chosen player laughs. Also used by the AI.
    public bool Tickle(Hider target)
    {
        if (!has || target == null || target == hider || target.IsCaught) return false;

        LaughEffect laugh = target.GetComponent<LaughEffect>();
        if (laugh == null) laugh = target.gameObject.AddComponent<LaughEffect>();
        if (!laugh.Laugh(laughSound, hearingRadius, laughVolume)) return false; // already laughing: keep the feather

        has = false;
        if (hud != null) hud.RemoveItem("feather");
        return true;
    }

    // ---------------- mouse becomes the feather ----------------

    void BeginAim()
    {
        BuildCursorUi();
        if (canvas == null) return;

        aiming = true;
        cursorWasVisible = Cursor.visible;
        Cursor.visible = false;
        if (thrower != null) { throwerWasEnabled = thrower.enabled; thrower.enabled = false; }

        cursorImage.sprite = icon;
        cursorImage.gameObject.SetActive(true);
        hint.gameObject.SetActive(true);
        MoveCursor(Mouse.current.position.ReadValue());
    }

    void EndAim()
    {
        if (!aiming) return;
        aiming = false;
        Cursor.visible = cursorWasVisible;
        if (hovered != null) { hovered.SetTickleHighlight(false); hovered = null; }
        if (cursorImage != null) cursorImage.gameObject.SetActive(false);
        if (hint != null) hint.gameObject.SetActive(false);
        // give the thrower back one frame later so the click that picked the player is not also a throw
        if (thrower != null) StartCoroutine(RestoreThrower());
    }

    IEnumerator RestoreThrower()
    {
        yield return null;
        if (thrower != null) thrower.enabled = throwerWasEnabled;
    }

    void OnDisable()
    {
        if (aiming) EndAim();
    }

    void MoveCursor(Vector2 screen)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        cursorImage.rectTransform.anchoredPosition = local;
    }

    void BuildCursorUi()
    {
        if (cursorImage != null) return;
        if (hud == null || hud.bagIcon == null) return;
        canvas = hud.bagIcon.canvas.rootCanvas;
        canvasRect = canvas.GetComponent<RectTransform>();

        GameObject g = new GameObject("FeatherCursor", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(canvas.transform, false);
        cursorImage = g.GetComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
        RectTransform r = cursorImage.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = cursorTip;
        r.sizeDelta = new Vector2(cursorSize, cursorSize);
        g.SetActive(false);

        GameObject t = new GameObject("FeatherHint", typeof(RectTransform));
        t.transform.SetParent(canvas.transform, false);
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        if (hud.nameText != null) tmp.font = hud.nameText.font;
        tmp.fontSize = 30;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.text = "Click a player's card to tickle them\n<size=70%><color=#CCCCCC>F / Esc / right-click to cancel</color></size>";
        RectTransform tr = tmp.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
        tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = new Vector2(0f, 60f);
        tr.sizeDelta = new Vector2(900f, 90f);
        hint = tmp;
        t.SetActive(false);
    }
}
