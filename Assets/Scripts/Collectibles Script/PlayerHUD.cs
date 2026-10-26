using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("UI Element References")]
    public Image avatarIcon;
    public TextMeshProUGUI nameText;
    public Image bagIcon;          // The bag frame background
    public Image itemIcon;         // TEMPLATE for one item slot (stays hidden, copied for every item)
    [Tooltip("Row (with a Horizontal Layout Group) that sits right next to the backpack and holds the carried items.")]
    public RectTransform itemsRow;
    public float itemSlotSize = 36f;
    public int maxItems = 3;

    [Header("Player Setup Data")]
    public string playerName = "Player 1";
    public Sprite characterFaceSprite;
    public Sprite stoneSprite;

    [Header("Score (1 point for surviving a round)")]
    public int score = 0;
    public TextMeshProUGUI scoreText;

    [Header("Caught State")]
    public Sprite deadSprite;
    public GameObject caughtOverlay;   // red translucent rectangle + white CAUGHT (starts inactive)
    Hider hiderRef;
    bool caughtShown;

    void Start()
    {
        if (nameText != null) nameText.text = playerName;
        if (avatarIcon != null) avatarIcon.sprite = characterFaceSprite;
        ClearAllSlots();
        if (caughtOverlay != null) caughtOverlay.SetActive(false);
        hiderRef = GetComponentInParent<Hider>();
        if (hiderRef == null) hiderRef = GetComponent<Hider>();
        SetScore(score);
    }

    void Update()
    {
        if (!caughtShown && hiderRef != null && hiderRef.IsCaught) ShowCaught();
    }

    // ---------- Carried items (shown as a row next to the backpack) ----------
    readonly System.Collections.Generic.List<Image> carried = new System.Collections.Generic.List<Image>();
    readonly System.Collections.Generic.List<string> carriedIds = new System.Collections.Generic.List<string>();

    public int ItemCount { get { return carried.Count; } }

    // Add any item (rock, feather, door lock...) with its own icon. Returns false when the bag is full.
    public bool AddItem(string id, Sprite icon)
    {
        if (icon == null || itemIcon == null || carried.Count >= maxItems) return false;
        Transform parent = itemsRow != null ? (Transform)itemsRow : itemIcon.transform.parent;
        Image slot = Instantiate(itemIcon, parent);
        slot.name = "Item_" + id;
        slot.sprite = icon;
        slot.preserveAspect = true;
        slot.raycastTarget = false;
        float aspect = icon.rect.width / Mathf.Max(1f, icon.rect.height);
        slot.rectTransform.sizeDelta = new Vector2(itemSlotSize * Mathf.Clamp(aspect, 1f, 1.4f), itemSlotSize);
        slot.gameObject.SetActive(true);
        carried.Add(slot);
        carriedIds.Add(id);
        return true;
    }

    // Remove one item by id (the most recently added of that kind). Returns false if she has none.
    public bool RemoveItem(string id)
    {
        for (int i = carried.Count - 1; i >= 0; i--)
        {
            if (carriedIds[i] != id) continue;
            if (carried[i] != null) Destroy(carried[i].gameObject);
            carried.RemoveAt(i); carriedIds.RemoveAt(i);
            return true;
        }
        return false;
    }

    void ClearAllSlots()
    {
        foreach (Image s in carried) if (s != null) Destroy(s.gameObject);
        carried.Clear(); carriedIds.Clear();
        if (itemIcon != null) itemIcon.gameObject.SetActive(false); // the template stays hidden
    }

    // Rock helpers used by the throwers (unchanged API)
    public void ShowStoneInBag() { AddItem("rock", stoneSprite); }
    public void EmptyBag() { if (!RemoveItem("rock") && carried.Count > 0) RemoveItem(carriedIds[carried.Count - 1]); }

    // Call AddScore(1) for every player who survives a round.
    public void AddScore(int amount) { SetScore(score + amount); }
    public void SetScore(int value)
    {
        score = value;
        if (scoreText != null) scoreText.text = "<size=34%><color=#CCCCCC>SCORE</color></size>\n" + score;
    }

    void ShowCaught()
    {
        caughtShown = true;
        if (avatarIcon != null)
        {
            GameObject d = new GameObject("DeadOverlay", typeof(RectTransform), typeof(Image));
            d.transform.SetParent(avatarIcon.rectTransform, false);
            RectTransform dr = (RectTransform)d.transform;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
            Image im = d.GetComponent<Image>();
            im.sprite = deadSprite; im.preserveAspect = true; im.raycastTarget = false;
        }
        if (caughtOverlay != null) caughtOverlay.SetActive(true);
        ClearAllSlots();
        if (bagIcon != null) bagIcon.gameObject.SetActive(false);
    }

    // ---- feather: pick this player by clicking his card ----
    RectTransform cardRect;
    GameObject tickleHighlight;

    public RectTransform CardRect
    {
        get
        {
            if (cardRect == null && bagIcon != null && bagIcon.transform.parent != null && bagIcon.transform.parent.parent != null)
            {
                Transform c = bagIcon.transform.parent.parent.Find("HUDCard");
                if (c != null) cardRect = c as RectTransform;
            }
            return cardRect;
        }
    }

    public void SetTickleHighlight(bool on)
    {
        RectTransform card = CardRect;
        if (card == null) return;
        if (tickleHighlight == null)
        {
            if (!on) return;
            tickleHighlight = new GameObject("TickleHighlight", typeof(RectTransform), typeof(Image), typeof(Outline));
            tickleHighlight.transform.SetParent(card, false);
            RectTransform r = (RectTransform)tickleHighlight.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            Image img = tickleHighlight.GetComponent<Image>();
            img.color = new Color(1f, 0.9f, 0.1f, 0.25f);
            img.raycastTarget = false;
            Outline o = tickleHighlight.GetComponent<Outline>();
            o.effectColor = new Color(1f, 0.9f, 0.1f, 1f);
            o.effectDistance = new Vector2(4f, -4f);
        }
        tickleHighlight.SetActive(on);
    }
}
