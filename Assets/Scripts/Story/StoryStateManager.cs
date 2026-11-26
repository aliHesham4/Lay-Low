using System;
using System.Collections.Generic;
using UnityEngine;

// The story's memory: a set of named flags (e.g. "movedIn", "night1Complete", "tookKnife") that survive every
// scene change. Triggers, dialogue and items SET flags; doors, dialogue, objectives and level changes READ them.
//
//   StoryStateManager.Instance.SetFlag("foundNote");
//   if (StoryStateManager.Instance.HasFlag("tookKnife")) ...
//   StoryStateManager.Instance.OnFlagSet += flag => { ... };   // e.g. the ObjectiveManager updates the HUD
//
// It creates itself the first time it is used, so nothing has to be placed in a scene.
//
// Flags the game sets on its own:
//   movedIn                 'Start the night' in the main menu (the family has moved in)
//   night{N}Started         night N's scene has loaded (N = 1..4)
//   night{N}Complete        night N is over (the clock ran out or one player was left)
//   night{N}Survived        the human player was still alive at the end of night N
//   night{N}Caught          the human player was caught (or stabbed) during night N
//   gameComplete            the last night is over
public class StoryStateManager : MonoBehaviour
{
    public const int Nights = 4;

    static StoryStateManager instance;
    // true if it already exists (checking this never creates it, e.g. while the game is shutting down)
    public static bool HasInstance { get { return instance != null; } }

    public static StoryStateManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Story State");
                instance = go.AddComponent<StoryStateManager>();   // Awake makes it persistent
            }
            return instance;
        }
    }

    [Tooltip("Print every flag in the Console as it is set (handy while testing and recording the video).")]
    public bool logFlags = true;

    readonly HashSet<string> flags = new HashSet<string>();
    readonly List<string> order = new List<string>();   // the order flags were set in, for the debug list

    // fired once for each new flag
    public event Action<string> OnFlagSet;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ---------------- flags ----------------

    public bool HasFlag(string flag) { return !string.IsNullOrEmpty(flag) && flags.Contains(flag); }

    // Sets a flag. Setting one that is already set does nothing, so a beat never fires twice.
    public bool SetFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return false;
        order.Add(flag);
        if (logFlags) Debug.Log("[Story] " + flag);
        if (OnFlagSet != null) OnFlagSet(flag);
        return true;
    }

    // True only if every flag listed is set (an empty list counts as true).
    public bool HasAll(params string[] list)
    {
        if (list == null) return true;
        foreach (string f in list) if (!string.IsNullOrEmpty(f) && !flags.Contains(f)) return false;
        return true;
    }

    public IList<string> Flags { get { return order.AsReadOnly(); } }

    // A new match: forget the whole story.
    public void ResetStory()
    {
        flags.Clear();
        order.Clear();
        if (logFlags) Debug.Log("[Story] reset");
    }

    // ---------------- the nights ----------------

    public static string Started(int night) { return "night" + night + "Started"; }
    public static string Complete(int night) { return "night" + night + "Complete"; }
    public static string Survived(int night) { return "night" + night + "Survived"; }
    public static string Caught(int night) { return "night" + night + "Caught"; }

    // A night can be played once the family has moved in and the night before it is over.
    public bool CanStartNight(int night)
    {
        if (night <= 1) return true;
        return HasFlag(Complete(night - 1));
    }

    // The latest night the story has reached (where a level change may go).
    public int FurthestNight()
    {
        int n = 1;
        while (n < Nights && HasFlag(Complete(n))) n++;
        return n;
    }

    // Called by RoundManager when a night ends.
    public void CompleteNight(int night, bool playerSurvived)
    {
        SetFlag(playerSurvived ? Survived(night) : Caught(night));
        SetFlag(Complete(night));
        if (night >= Nights) SetFlag("gameComplete");
    }

    // The ending, from how the last night went. Sets one ending flag too (endingKnife / endingRefused / endingSurvived / endingLost).
    public string EndingFor(bool playerWon)
    {
        if (!playerWon)
        {
            SetFlag("endingLost");
            return "Someone else walked out that morning. You're part of the house now.";
        }
        if (HasFlag("refusedKnife"))
        {
            SetFlag("endingRefused");
            return "You never played his game. Somehow, you're still here. The house goes quiet.";
        }
        if (HasFlag("tookKnife"))
        {
            SetFlag("endingKnife");
            return "The sun came up. You walked out the front door alive... still holding the knife. You played his game, and you won.";
        }
        SetFlag("endingSurvived");
        return "The sun came up. You walked out the front door alive. The house let you go... this time.";
    }

    // How many nights the human player has survived so far (for the ending and recaps).
    public int NightsSurvived()
    {
        int c = 0;
        for (int n = 1; n <= Nights; n++) if (HasFlag(Survived(n))) c++;
        return c;
    }
}
