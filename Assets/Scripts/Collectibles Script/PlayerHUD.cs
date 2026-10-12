using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("UI Element References")]
    public Image avatarIcon;
    public TextMeshProUGUI nameText;
    public Image bagIcon;          // The bag frame background
    public Image itemIcon;         // The stone graphic inside the bag

    [Header("Player Setup Data")]
    public string playerName = "Player 1";
    public Sprite characterFaceSprite;
    public Sprite stoneSprite;

    void Start()
    {
        if (nameText != null) nameText.text = playerName;
        if (avatarIcon != null) avatarIcon.sprite = characterFaceSprite;

        // Hide just the item graphic inside the bag at start
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(false);
        }
    }

    public void ShowStoneInBag()
    {
        if (itemIcon != null && stoneSprite != null)
        {
            itemIcon.sprite = stoneSprite;
            itemIcon.gameObject.SetActive(true);
        }
    }

    public void EmptyBag()
    {
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(false);
        }
    }
}