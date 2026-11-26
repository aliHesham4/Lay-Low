using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A player's card in the HUD row (top-left, up to 4 players side by side).
// Paper-craft look from the design board: torn dark paper with a cream cut edge, a strip of tape in the player's
// colour, the face in a ring of that colour, the name, the backpack with 3 item slots, and a cream score tag.
// States: caught (red paper, skull, "CAUGHT" stamp) and golden-knife holder (gold paper, cleaver after the name).
// The card is built in code at Start; the old hand-made card in the scene is switched off.
public class PlayerHUD : MonoBehaviour
{
    [Header("UI Element References (filled in by the card at Start)")]
    public Image avatarIcon;
    public TextMeshProUGUI nameText;
    public Image bagIcon;          // the backpack
    [Tooltip("Old item template (no longer used: the card has 3 fixed item slots).")]
    public Image itemIcon;
    [Tooltip("The row that holds the backpack and the item slots.")]
    public RectTransform itemsRow;
    public float itemSlotSize = 36f;
    public int maxItems = 3;

    [Header("Player Setup Data")]
    public string playerName = "Player 1";
    public Sprite characterFaceSprite;
    [Tooltip("Close-up picture for the winner's polaroid on the match-over scoreboard (falls back to the face).")]
    public Sprite portraitSprite;
    public Sprite stoneSprite;

    [Header("Score (1 point for surviving a round)")]
    public int score = 0;
    [HideInInspector] public bool goldScore; // true while this player holds the golden knife
    public TextMeshProUGUI scoreText;

    [Header("Caught State")]
    public Sprite deadSprite;
    public GameObject caughtOverlay;   // the "CAUGHT" stamp (starts hidden)
    Hider hiderRef;
    bool caughtShown;

    void Awake()
    {
        // remember where the old card sat so the new cards keep the same left-to-right order
        Transform old = OldCard();
        oldOrder = old != null ? old.GetComponent<RectTransform>().anchoredPosition.x : transform.GetSiblingIndex() * 1000f;
    }

    void Start()
    {
        EnsureCard();
        nameText.text = playerName;
        hiderRef = GetComponentInParent<Hider>();
        if (hiderRef == null) hiderRef = GetComponent<Hider>();
        SetScore(score);
    }

    void Update()
    {
        if (!caughtShown && hiderRef != null && hiderRef.IsCaught) ShowCaught();
        Animate();
    }

    // ---------- Carried items (up to 3, in the slots next to the backpack) ----------
    readonly List<Sprite> carried = new List<Sprite>();
    readonly List<string> carriedIds = new List<string>();

    public int ItemCount { get { return carried.Count; } }

    // Add any item (rock, feather, door lock...) with its own icon. Returns false when the bag is full.
    public bool AddItem(string id, Sprite icon)
    {
        EnsureCard();
        if (icon == null || carried.Count >= Mathf.Min(maxItems, SlotCount)) return false;
        carried.Add(icon);
        carriedIds.Add(id);
        RefreshItems();
        popAt[carried.Count - 1] = Time.unscaledTime;
        return true;
    }

    // Remove one item by id (the most recently added of that kind). Returns false if she has none.
    public bool RemoveItem(string id)
    {
        for (int i = carried.Count - 1; i >= 0; i--)
        {
            if (carriedIds[i] != id) continue;
            carried.RemoveAt(i); carriedIds.RemoveAt(i);
            RefreshItems();
            return true;
        }
        return false;
    }

    void ClearAllSlots()
    {
        carried.Clear(); carriedIds.Clear();
        RefreshItems();
    }

    // Rock helpers used by the throwers (unchanged API)
    public void ShowStoneInBag() { AddItem("rock", stoneSprite); }
    public void EmptyBag() { if (!RemoveItem("rock") && carried.Count > 0) RemoveItem(carriedIds[carried.Count - 1]); }

    // Call AddScore(1) for every player who survives a round.
    public void AddScore(int amount) { SetScore(score + amount); }
    public void SetScore(int value)
    {
        score = value;
        EnsureCard();
        scoreText.text = score.ToString();
    }

    // Round 4: the golden knife holder's card turns gold (called by KnifeHolder)
    public void SetGold(bool on)
    {
        EnsureCard();
        if (goldScore == on) return;
        goldScore = on;
        ApplyLook();
        if (on) goldAt = Time.unscaledTime;
    }

    void ShowCaught()
    {
        EnsureCard();
        caughtShown = true;
        ClearAllSlots();
        ApplyLook();
        caughtAt = Time.unscaledTime;
    }

    // ---- feather / ice gun: pick a player by clicking his card ----
    public RectTransform CardRect { get { EnsureCard(); return card; } }

    // this player's colour on the HUD (tape and ring), used by the scoreboard
    public Color CardColour { get { EnsureCard(); return mine; } }

    public void SetTickleHighlight(bool on)
    {
        EnsureCard();
        highlight.gameObject.SetActive(on);
        if (on) highlight.SetAsLastSibling();
    }

    // =====================================================================
    //  The paper card
    // =====================================================================
    const int SlotCount = 3;
    float oldOrder;
    RectTransform card, highlight, bagRow, stamp, cleaver;
    PaperShape paper, tapeEdge, tape, ring, faceBack, scoreTag;
    CanvasGroup scoreGroup;
    TextMeshProUGUI scoreLabel;
    Image[] slotDash = new Image[SlotCount];
    Image[] slotItem = new Image[SlotCount];
    float[] popAt = { -9f, -9f, -9f };
    float caughtAt = -9f, goldAt = -9f;
    Color mine;

    static readonly Color GoldRing = PaperUI.Hex("b07a12");
    static readonly Color DashLight = PaperUI.Hex("f1e4c6", 0.28f);
    static readonly Color DashDark = PaperUI.Hex("1d1322", 0.3f);

    Transform OldCard()
    {
        // the hand-made card: Panel > InventoryRow > BagImage
        Image b = bagIcon;
        if (b == null || b.GetComponentInParent<PaperShape>() != null) return null;
        return b.transform.parent != null ? b.transform.parent.parent : null;
    }

    // left-to-right place of this player's card (0..3)
    int Slot()
    {
        // Player1..Player4 always sit in their own place, even when a seat is left empty in Game setup
        string n = gameObject.name;
        if (n.Length > 6 && n.StartsWith("Player") && char.IsDigit(n[n.Length - 1])) return Mathf.Clamp(n[n.Length - 1] - '1', 0, 3);
        int slot = 0;
        foreach (PlayerHUD h in FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (h != this && (h.oldOrder < oldOrder || (h.oldOrder == oldOrder && h.GetInstanceID() < GetInstanceID()))) slot++;
        return slot;
    }

    void EnsureCard()
    {
        if (card != null) return;
        Transform old = OldCard();
        Sprite bagSprite = bagIcon != null ? bagIcon.sprite : null;
        if (old != null) old.gameObject.SetActive(false);

        Canvas canvas = PaperUI.HudCanvas;
        Transform rowT = canvas.transform.Find("Cards");
        RectTransform row = rowT != null ? (RectTransform)rowT : PaperUI.Rect("Cards", canvas.transform);
        if (rowT == null) { PaperUI.Stretch(row); row.SetAsFirstSibling(); }

        int slot = Slot();
        mine = PaperUI.PlayerColours[slot % PaperUI.PlayerColours.Length];
        card = PaperUI.Place(PaperUI.Rect("Card " + playerName, row), 24f + slot * 312f, 14f, 296f, 90f);

        // tape: its cut edge goes under the paper, the tape itself on top
        tapeEdge = PaperUI.Shape(card, "Tape Edge", mine);
        PaperUI.Place(tapeEdge.rectTransform, -10f, -8f, 54f, 18f); PaperUI.Rotate(tapeEdge.rectTransform, -24f);
        tapeEdge.edge = 3f; tapeEdge.edgeColor = PaperUI.CutEdge; tapeEdge.drawFill = false;

        paper = PaperUI.Cut(PaperUI.Torn(card, "Paper", PaperUI.CardDark, PaperUI.CardTear));
        PaperUI.Stretch(paper.rectTransform);

        tape = PaperUI.Shape(card, "Tape", new Color(mine.r, mine.g, mine.b, 0.9f));
        PaperUI.Place(tape.rectTransform, -10f, -8f, 54f, 18f); PaperUI.Rotate(tape.rectTransform, -24f);

        // face in a ring of the player's colour, with a thin cream ring outside
        ring = PaperUI.Shape(card, "Ring", mine, 32f);
        PaperUI.Place(ring.rectTransform, 12f, 13f, 64f, 64f);
        ring.edge = 3f; ring.edgeColor = PaperUI.CutEdge;
        faceBack = PaperUI.Shape(ring.transform, "Back", PaperUI.CardDark, 27f);
        PaperUI.Stretch(faceBack.rectTransform, 5f);
        avatarIcon = PaperUI.Img(ring.transform, "Face", characterFaceSprite != null ? characterFaceSprite : PaperUI.Icon("p4"));
        PaperUI.Stretch(avatarIcon.rectTransform, 5f);

        nameText = PaperUI.Text(card, "Name", playerName, 18f, PaperUI.Light);
        PaperUI.Place(nameText.rectTransform, 86f, 8f, 140f, 28f);
        Image cleaverImg = PaperUI.IconWithShadow(card, "Cleaver", PaperUI.Icon("cleaver_icon"), new Vector2(2f, -2f), 0.35f);
        cleaver = (RectTransform)cleaverImg.transform.parent;
        cleaver.gameObject.SetActive(false);

        // backpack + 3 slots
        bagRow = PaperUI.Place(PaperUI.Rect("Bag", card), 86f, 46f, 30f + SlotCount * 35f, 30f);
        itemsRow = bagRow;
        bagIcon = PaperUI.Img(bagRow, "Backpack", bagSprite);
        PaperUI.Place(bagIcon.rectTransform, 0f, 0f, 30f, 30f);
        for (int i = 0; i < SlotCount; i++)
        {
            RectTransform s = PaperUI.Place(PaperUI.Rect("Slot " + (i + 1), bagRow), 35f + i * 35f, 0f, 30f, 30f);
            slotDash[i] = PaperUI.Img(s, "Empty", PaperUI.Icon("slot"), DashLight);
            PaperUI.Stretch(slotDash[i].rectTransform);
            slotItem[i] = PaperUI.IconWithShadow(s, "Item", null, new Vector2(2f, -3f), 0.45f);
            RectTransform holder = (RectTransform)slotItem[i].transform.parent;
            PaperUI.Place(holder, -3f, -3f, 36f, 36f);
            holder.pivot = new Vector2(0.5f, 0.5f); holder.anchoredPosition = new Vector2(15f, -15f);
            holder.gameObject.SetActive(false);
        }

        // "CAUGHT" stamp where the bag was
        stamp = PaperUI.Rect("Caught Stamp", card);
        PaperShape border = PaperUI.Shape(stamp, "Border", PaperUI.Light, 7f);
        border.ring = 3f;
        PaperUI.Stretch(border.rectTransform);
        TextMeshProUGUI st = PaperUI.Text(stamp, "Text", "CAUGHT", 17f, PaperUI.Light, true, TextAlignmentOptions.Center);
        PaperUI.LetterSpacing(st, 3f);
        // border 3 + padding 3 10 0 round one 17 px line (CSS letter-spacing also pads after the last letter)
        float sw = st.preferredWidth + 20f + 6f + 3f, sh = 17f * PaperUI.NormalLine + 3f + 6f;
        st.alignment = TextAlignmentOptions.TopLeft;
        PaperUI.Place(st.rectTransform, 13f, 6f, sw, 30f);
        PaperUI.Place(stamp, 86f, 44f, sw, sh);
        PaperUI.Rotate(stamp, -5f);
        stamp.gameObject.SetActive(false);
        caughtOverlay = stamp.gameObject;

        // score tag
        scoreTag = PaperUI.Shape(card, "Score", PaperUI.Cream, 10f);
        PaperUI.Place(scoreTag.rectTransform, 296f - 10f - 52f, 13f, 52f, 64f);
        scoreTag.shadow = true; scoreTag.shadowOffset = new Vector2(3f, -4f);
        scoreTag.shadowColor = new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.4f);
        scoreGroup = scoreTag.gameObject.AddComponent<CanvasGroup>();
        scoreGroup.blocksRaycasts = false;
        scoreLabel = PaperUI.Text(scoreTag.transform, "Label", "SCORE", 9f, PaperUI.Hex("8a7a6a"), true, TextAlignmentOptions.Center);
        PaperUI.LetterSpacing(scoreLabel, 1.4f);
        // a centred column: 'SCORE' (9 px, normal line) over the number (28 px, line-height 1)
        float colTop = (64f - (9f * PaperUI.NormalLine + 28f)) * 0.5f;
        scoreLabel.alignment = TextAlignmentOptions.Top;
        PaperUI.Place(scoreLabel.rectTransform, 0f, colTop, 52f, 9f * PaperUI.NormalLine);
        scoreText = PaperUI.Text(scoreTag.transform, "Number", score.ToString(), 28f, PaperUI.Ink, true, TextAlignmentOptions.Top);
        float up = PaperUI.LineHeight(scoreText, 1f);
        PaperUI.Place(scoreText.rectTransform, 0f, colTop + 9f * PaperUI.NormalLine - up, 52f, 40f);

        // feather / ice gun aiming: a glowing outline round the card you point at
        PaperShape glowFill = PaperUI.Torn(card, "Pick Highlight", new Color(1f, 0.9f, 0.1f, 0.1f), PaperUI.CardTear);
        highlight = glowFill.rectTransform;
        PaperUI.Stretch(highlight, -7f);
        PaperShape glowRing = PaperUI.Torn(highlight, "Ring", new Color(1f, 0.9f, 0.1f, 1f), PaperUI.CardTear);
        glowRing.ring = 4f;
        PaperUI.Stretch(glowRing.rectTransform);
        highlight.gameObject.SetActive(false);

        ApplyLook();
        RefreshItems();
    }

    void ApplyLook()
    {
        bool caught = caughtShown;
        bool gold = goldScore && !caught;

        paper.color = caught ? PaperUI.Red : gold ? PaperUI.Gold : PaperUI.CardDark;
        ring.color = caught ? PaperUI.Light : gold ? GoldRing : mine;
        faceBack.color = caught ? PaperUI.Ink : PaperUI.CardDark;
        avatarIcon.sprite = caught ? (deadSprite != null ? deadSprite : PaperUI.Icon("skull"))
                                   : (characterFaceSprite != null ? characterFaceSprite : PaperUI.Icon("p4"));
        PaperUI.Stretch(avatarIcon.rectTransform, caught ? 12f : 5f); // the skull sits a little inside the ring

        nameText.color = gold ? PaperUI.Ink : PaperUI.Light;
        cleaver.gameObject.SetActive(gold);
        if (gold)
        {
            nameText.ForceMeshUpdate();
            float w = nameText.preferredWidth;
            PaperUI.Place(cleaver, 86f + w + 6f, 8f + nameText.preferredHeight * 0.5f - 14f, 28f, 28f);
        }

        bagRow.gameObject.SetActive(!caught);
        stamp.gameObject.SetActive(caught);
        foreach (Image d in slotDash) d.color = gold ? DashDark : DashLight;

        scoreTag.color = gold ? PaperUI.Ink : PaperUI.Cream;
        scoreLabel.color = gold ? PaperUI.Hex("c9a24a") : PaperUI.Hex("8a7a6a");
        scoreText.color = gold ? PaperUI.Gold : PaperUI.Ink;
        scoreGroup.alpha = caught ? 0.6f : 1f;
    }

    void RefreshItems()
    {
        if (card == null) return;
        for (int i = 0; i < SlotCount; i++)
        {
            bool has = i < carried.Count;
            slotDash[i].gameObject.SetActive(!has);
            slotItem[i].transform.parent.gameObject.SetActive(has);
            if (has) { PaperUI.SetIcon(slotItem[i], carried[i]); FitIcon((RectTransform)slotItem[i].transform.parent, carried[i]); }
        }
    }

    // Item pictures have different amounts of empty space round them (the rock fills its whole image, the door lock
    // only half of it), so each one is sized by its visible part: that part fits a 31 x 31 box, centred in the slot.
    const float ItemBox = 31f;
    static void FitIcon(RectTransform holder, Sprite s)
    {
        if (s == null) return;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector2 v in s.vertices) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); } // the sprite's tight outline
        Vector2 visible = max - min;
        if (visible.x <= 0f || visible.y <= 0f) return;
        float k = ItemBox / Mathf.Max(visible.x, visible.y);
        holder.sizeDelta = (Vector2)s.bounds.size * k;
        Vector2 offCentre = (min + max) * 0.5f - (Vector2)s.bounds.center;
        holder.anchoredPosition = new Vector2(15f, -15f) - offCentre * k;
    }

    // small paper animations: items pop in, the card shakes and the stamp thumps when caught, gold card bounces
    void Animate()
    {
        if (card == null) return;
        float now = Time.unscaledTime;
        for (int i = 0; i < SlotCount; i++)
        {
            float t = (now - popAt[i]) / 0.3f;
            float s = t < 1f ? Mathf.LerpUnclamped(1.45f, 1f, PaperUI.EaseOutBack(t)) : 1f;
            slotItem[i].transform.parent.localScale = Vector3.one * s;
        }

        float c = (now - caughtAt) / 0.45f;
        float shake = c < 1f ? Mathf.Sin(c * 40f) * (1f - c) * 5f : 0f;
        float g = (now - goldAt) / 0.4f;
        float bounce = g < 1f ? Mathf.Sin(g * Mathf.PI) * 0.06f : 0f;
        card.localEulerAngles = new Vector3(0f, 0f, shake * 0.4f);
        card.localScale = Vector3.one * (1f + bounce);

        if (stamp.gameObject.activeSelf)
        {
            float k = (now - caughtAt - 0.1f) / 0.25f;
            stamp.localScale = Vector3.one * (k < 1f ? Mathf.Lerp(2.2f, 1f, PaperUI.EaseOut(Mathf.Max(0f, k))) : 1f);
        }

        if (highlight.gameObject.activeSelf)
        {
            float p = 0.75f + 0.25f * Mathf.Sin(now * 8f);
            highlight.localScale = Vector3.one * (1f + 0.01f * Mathf.Sin(now * 8f));
            highlight.GetComponent<PaperShape>().color = new Color(1f, 0.9f, 0.1f, 0.1f * p);
        }
    }
}
