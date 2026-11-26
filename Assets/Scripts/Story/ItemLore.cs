using System;
using UnityEngine;

[Serializable]
public class ItemLoreEntry
{
    [Tooltip("The id the bag uses: rock, feather, doorlock, icegun, whistle, goldenknife.")]
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
}

// The story behind every item, in one asset (Assets/Resources/Item Lore.asset) so the text can be edited
// without touching scripts. Shown in two places:
//  - the pause menu's "Your bag" note (the inventory screen)
//  - a caption the first time the player picks each kind of item up (sets the flag "found_<id>")
[CreateAssetMenu(fileName = "Item Lore", menuName = "Dialogue/Item Lore")]
public class ItemLore : ScriptableObject
{
    public ItemLoreEntry[] items;

    static ItemLore instance;
    public static ItemLore Instance
    {
        get { if (instance == null) instance = Resources.Load<ItemLore>("Item Lore"); return instance; }
    }

    public static ItemLoreEntry Find(string id)
    {
        ItemLore l = Instance;
        if (l == null || l.items == null) return null;
        foreach (ItemLoreEntry e in l.items) if (e != null && e.id == id) return e;
        return null;
    }

    // The player picked an item up: the first time for each kind, its story shows under the HUD for a few seconds.
    public static void FirstPickup(string id)
    {
        ItemLoreEntry e = Find(id);
        if (e == null) return;
        if (StoryStateManager.Instance.SetFlag("found_" + id))
            HideCaption.ShowFor(Instance, e.description, e.displayName, 5.5f);
    }
}
