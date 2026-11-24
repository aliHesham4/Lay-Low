using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Carried by a player (human or AI) who has picked up the door lock.
// Human: press the key, every free doorway glows in your colour (even through walls), the mouse becomes
// the padlock; hover a doorway and click: a metal shutter slams down there for a few seconds.
public class DoorLockHolder : MonoBehaviour
{
    [Header("Use")]
    public Key useKey = Key.E;
    public float cursorSize = 70f;

    [HideInInspector] public Sprite icon;
    [HideInInspector] public AudioClip slamSound;
    [HideInInspector] public float lockSeconds = 5f;

    Hider hider;
    PlayerHUD hud;
    PlayerStoneThrower thrower;
    bool has, aiming, throwerWasEnabled, cursorWasVisible;
    LockableDoorway hovered;
    Canvas canvas;
    RectTransform canvasRect;
    Image cursorImage;
    GameObject hint; // the paper hint card (centre-bottom) while aiming

    public bool HasLock { get { return has; } }
    public bool IsAiming { get { return aiming; } }
    public Color MyColor { get { return PlayerColors.For(hider); } }

    void Awake()
    {
        hider = GetComponent<Hider>();
        hud = GetComponent<PlayerHUD>();
        thrower = GetComponent<PlayerStoneThrower>();
    }

    public bool Collect(Sprite iconSprite, AudioClip clip, float seconds)
    {
        if (has) return false;
        if (hud != null && !hud.AddItem("doorlock", iconSprite)) return false;
        icon = iconSprite; slamSound = clip; lockSeconds = seconds;
        has = true;
        return true;
    }

    // Used by the click and by the AI. Returns false if that doorway cannot be locked now.
    public bool LockDoorway(LockableDoorway d)
    {
        if (!has || d == null || d.IsLocked) return false;
        if (slamSound != null && d.slamSound == null) d.slamSound = slamSound;
        if (!d.Lock(hider, MyColor, lockSeconds)) return false;
        has = false;
        if (hud != null) hud.RemoveItem("doorlock");
        return true;
    }

    void Update()
    {
        if (!has || hider == null || !hider.isHumanControlled) return;
        if (hider.IsCaught) { if (aiming) EndAim(); return; }
        if (Keyboard.current == null || Mouse.current == null) return;

        bool key = Keyboard.current[useKey].wasPressedThisFrame;
        if (!aiming)
        {
            if (key && Time.timeScale > 0f) BeginAim();
            return;
        }

        if (key || Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            EndAim();
            return;
        }

        Vector2 mouse = Mouse.current.position.ReadValue();
        MoveCursor(mouse);

        // the nearest doorway under the mouse
        Camera cam = Camera.main;
        LockableDoorway over = null;
        float best = float.MaxValue;
        if (cam != null)
            foreach (LockableDoorway d in LockableDoorway.All)
            {
                if (d == null || d.IsLocked) continue;
                float depth;
                if (d.ScreenContains(cam, mouse, out depth) && depth < best) { best = depth; over = d; }
            }

        if (over != hovered)
        {
            if (hovered != null && !hovered.IsLocked) hovered.SetHighlight(1, MyColor);
            hovered = over;
            if (hovered != null) hovered.SetHighlight(2, MyColor);
        }
        // doorways that got locked by someone else meanwhile drop out
        foreach (LockableDoorway d in LockableDoorway.All) if (d != null && d.IsLocked) d.SetHighlight(0, MyColor);

        if (hovered != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockableDoorway target = hovered;
            EndAim();
            LockDoorway(target);
        }
    }

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
        foreach (LockableDoorway d in LockableDoorway.All) if (d != null) d.SetHighlight(d.IsLocked ? 0 : 1, MyColor);
        hovered = null;
    }

    void EndAim()
    {
        if (!aiming) return;
        aiming = false;
        Cursor.visible = cursorWasVisible;
        foreach (LockableDoorway d in LockableDoorway.All) if (d != null) d.SetHighlight(0, MyColor);
        hovered = null;
        if (cursorImage != null) cursorImage.gameObject.SetActive(false);
        if (hint != null) hint.SetActive(false);
        if (thrower != null) StartCoroutine(RestoreThrower()); // so the click that locked is not also a throw
    }

    IEnumerator RestoreThrower()
    {
        yield return null;
        if (thrower != null) thrower.enabled = throwerWasEnabled;
    }

    void OnDisable() { if (aiming) EndAim(); }

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

        GameObject g = new GameObject("DoorLockCursor", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(canvas.transform, false);
        cursorImage = g.GetComponent<Image>();
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
        RectTransform r = cursorImage.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(cursorSize, cursorSize);
        g.SetActive(false);

        hint = PaperUI.Hint(canvas.transform, "Click a doorway to slam it shut", "E", "Esc");
    }
}
