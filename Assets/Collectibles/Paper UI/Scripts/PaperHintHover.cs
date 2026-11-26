using UnityEngine;
using UnityEngine.InputSystem;

// Put on an item-aiming hint card (rock, feather, door lock, ice gun).
// When the mouse moves over the card (or close to it), the card fades almost away,
// so it never hides the spot the player is aiming at. It comes back when the mouse leaves.
// Clicks always pass through the card.
[RequireComponent(typeof(RectTransform))]
public class PaperHintHover : MonoBehaviour
{
    [Tooltip("How visible the card stays while the mouse is over it (0 = gone).")]
    [Range(0f, 1f)] public float hoverAlpha = 0.1f;
    [Tooltip("Extra screen pixels around the card that also count as 'over it'.")]
    public float margin = 30f;
    [Tooltip("How fast it fades (alpha per second).")]
    public float fadeSpeed = 8f;

    RectTransform rect;
    CanvasGroup group;
    Canvas canvas;

    void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; // never eats a click aimed through it
        group.interactable = false;
    }

    void OnEnable()
    {
        if (group != null) group.alpha = 1f; // shown fresh each time aiming starts
    }

    void Update()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        Mouse m = Mouse.current;
        bool over = false;
        if (m != null && canvas != null)
        {
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] c = new Vector3[4];
            rect.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            Vector2 p = m.position.ReadValue();
            over = p.x >= Mathf.Min(a.x, b.x) - margin && p.x <= Mathf.Max(a.x, b.x) + margin &&
                   p.y >= Mathf.Min(a.y, b.y) - margin && p.y <= Mathf.Max(a.y, b.y) + margin;
        }
        // real time: the hint must keep reacting even if the game is paused
        float want = !GameSettings.AimHints ? 0f : over ? hoverAlpha : 1f; // hints switched off in Settings
        group.alpha = Mathf.MoveTowards(group.alpha, want, fadeSpeed * Time.unscaledDeltaTime);
    }
}
