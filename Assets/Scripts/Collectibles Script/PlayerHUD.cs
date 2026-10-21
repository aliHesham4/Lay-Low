using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("UI Element References")]
    public Image avatarIcon;
    public TextMeshProUGUI nameText;
    public Image bagIcon;          // The bag frame background
    public Image itemIcon;         // First item slot
    public Image[] extraSlots;     // Further item slots (2 more)

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

    Image[] AllSlots()
    {
        int n = 1 + (extraSlots != null ? extraSlots.Length : 0);
        Image[] r = new Image[n];
        r[0] = itemIcon;
        for (int i = 1; i < n; i++) r[i] = extraSlots[i - 1];
        return r;
    }

    void ClearAllSlots()
    {
        foreach (Image s in AllSlots()) if (s != null) s.gameObject.SetActive(false);
    }

    // Fills the next free slot (up to 3 items)
    public void ShowStoneInBag()
    {
        if (stoneSprite == null) return;
        foreach (Image s in AllSlots())
        {
            if (s != null && !s.gameObject.activeSelf)
            {
                s.sprite = stoneSprite;
                s.gameObject.SetActive(true);
                return;
            }
        }
    }

    // Removes the most recently added item
    public void EmptyBag()
    {
        Image[] all = AllSlots();
        for (int i = all.Length - 1; i >= 0; i--)
        {
            if (all[i] != null && all[i].gameObject.activeSelf) { all[i].gameObject.SetActive(false); return; }
        }
    }

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
}
