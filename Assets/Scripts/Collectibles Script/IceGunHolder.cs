using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Carried by a player (human or AI) who has picked up the ice gun.
// Human: press the key, the mouse turns into the ice gun, click a player's card in the top HUD to freeze them.
// The frozen player gets ice cubes on both legs and moves slowly for a few seconds (see IceSlow).
public class IceGunHolder : MonoBehaviour
{
    [Header("Use")]
    public Key useKey = Key.G;
    [Tooltip("Size of the ice gun that follows the mouse (canvas units).")]
    public float cursorSize = 90f;
    [Tooltip("Where the muzzle is inside its image (0..1), so the muzzle is on the mouse position.")]
    public Vector2 cursorTip = new Vector2(0.95f, 0.72f);

    [HideInInspector] public Sprite icon;
    [HideInInspector] public AudioClip gunSound;
    [HideInInspector] public AudioClip crackSound;
    [HideInInspector] public Material iceMaterial;
    [HideInInspector] public GameObject iceVfx;
    [HideInInspector] public float freezeDuration = 3f;
    [HideInInspector] public float slowFactor = 0.35f;

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
    GameObject hint; // the paper hint card (centre-bottom) while aiming

    public bool HasIceGun { get { return has; } }
    public bool IsAiming { get { return aiming; } }

    void Awake()
    {
        hider = GetComponent<Hider>();
        hud = GetComponent<PlayerHUD>();
        thrower = GetComponent<PlayerStoneThrower>();
    }

    // Called by the ice gun pickup. Returns false if the bag is full or there is already one.
    public bool Collect(IceGunCollectible c)
    {
        if (has || c == null) return false;
        if (hud != null && !hud.AddItem("icegun", c.icon)) return false;
        icon = c.icon; gunSound = c.gunSound; crackSound = c.crackSound; iceMaterial = c.iceMaterial; iceVfx = c.iceVfx;
        freezeDuration = c.freezeDuration; slowFactor = c.slowFactor;
        has = true;
        return true;
    }

    void Update()
    {
        if (PauseMenu.IsOpen) return; // the pause menu owns the mouse and keys
        if (!has || hider == null || !hider.isHumanControlled) return;

        if (hider.IsCaught) { if (aiming) EndAim(); return; }
        if (Keyboard.current == null || Mouse.current == null) return;

        bool keyPressed = Keyboard.current[useKey].wasPressedThisFrame;

        if (!aiming)
        {
            if (keyPressed && Time.timeScale > 0f) BeginAim();
            return;
        }

        if (keyPressed || Mouse.current.rightButton.wasPressedThisFrame)
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
            Shoot(target);
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

    // Fires the gun at that player: they freeze, and the gun is used up. Also used by the AI.
    public bool Shoot(Hider target)
    {
        if (!has || target == null || target == hider || target.IsCaught) return false;
        if (IceSlow.IsFrozen(target.gameObject)) return false; // already frozen: keep the gun
        if (!IceSlow.Apply(target.gameObject, freezeDuration, slowFactor, crackSound, iceMaterial, iceVfx)) return false;

        if (gunSound != null)
        {
            // played at the frozen player, but loud enough to be heard from anywhere in the house
            GameObject s = new GameObject("IceGunShot");
            s.transform.position = target.transform.position + Vector3.up;
            AudioSource src = s.AddComponent<AudioSource>();
            src.clip = gunSound;
            src.spatialBlend = 0.5f;
            src.minDistance = 4f;
            src.Play();
            Destroy(s, gunSound.length + 0.2f);
        }

        has = false;
        if (hud != null) hud.RemoveItem("icegun");
        return true;
    }

    // ---------------- mouse becomes the ice gun ----------------

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
        hint.SetActive(true);
        MoveCursor(Mouse.current.position.ReadValue());
    }

    void EndAim()
    {
        if (!aiming) return;
        aiming = false;
        Cursor.visible = cursorWasVisible;
        if (hovered != null) { hovered.SetTickleHighlight(false); hovered = null; }
        if (cursorImage != null) cursorImage.gameObject.SetActive(false);
        if (hint != null) hint.SetActive(false);
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

        GameObject g = new GameObject("IceGunCursor", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(canvas.transform, false);
        cursorImage = g.GetComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
        RectTransform r = cursorImage.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = cursorTip;
        r.sizeDelta = new Vector2(cursorSize, cursorSize);
        g.SetActive(false);

        hint = PaperUI.Hint(canvas.transform, "Click a player's card to freeze them", "G");
    }
}
