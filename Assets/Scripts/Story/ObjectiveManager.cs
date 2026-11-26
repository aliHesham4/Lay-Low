using System;
using System.Collections.Generic;
using UnityEngine;

// One objective: shown once 'showWhen' is set (empty = from the start of the night),
// done once any of 'doneWhen' is set.
[Serializable]
public class Objective
{
    [TextArea(1, 2)] public string text;
    public string showWhen;
    public string[] doneWhen;

    public Objective(string text, string showWhen, params string[] doneWhen) { this.text = text; this.showWhen = showWhen; this.doneWhen = doneWhen; }

    public bool Shown(StoryStateManager s) { return string.IsNullOrEmpty(showWhen) || s.HasFlag(showWhen); }
    public bool Done(StoryStateManager s)
    {
        if (doneWhen != null) foreach (string f in doneWhen) if (s.HasFlag(f)) return true;
        return false;
    }
}

// Story-driven objectives: each night has a list, and which ones show and which are ticked off
// follows the story flags (it listens to StoryStateManager.OnFlagSet), so they always match the story.
// The pause menu shows them on the right (PauseMenu reads Visible()).
// A night scene can replace its list in the inspector (NightScene.objectives); otherwise the built-in one is used.
public class ObjectiveManager : MonoBehaviour
{
    static ObjectiveManager instance;
    public static ObjectiveManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Objectives");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ObjectiveManager>();
            }
            return instance;
        }
    }

    // fired whenever an objective appears or is ticked off
    public event Action OnObjectivesChanged;

    void OnEnable() { StoryStateManager.Instance.OnFlagSet += FlagSet; }
    void OnDisable() { if (StoryStateManager.HasInstance) StoryStateManager.Instance.OnFlagSet -= FlagSet; }

    void FlagSet(string flag)
    {
        // only tell listeners when the flag actually changes something on the list
        foreach (Objective o in ListFor(RoundManager.CurrentRound))
        {
            bool touches = o.showWhen == flag;
            if (o.doneWhen != null) foreach (string d in o.doneWhen) touches |= d == flag;
            if (touches) { if (OnObjectivesChanged != null) OnObjectivesChanged(); return; }
        }
    }

    // The objectives to show right now, in order, each with whether it is done.
    public List<KeyValuePair<Objective, bool>> Visible()
    {
        StoryStateManager s = StoryStateManager.Instance;
        List<KeyValuePair<Objective, bool>> list = new List<KeyValuePair<Objective, bool>>();
        foreach (Objective o in ListFor(RoundManager.CurrentRound))
            if (o.Shown(s)) list.Add(new KeyValuePair<Objective, bool>(o, o.Done(s)));
        return list;
    }

    public Objective[] ListFor(int night)
    {
        NightScene n = NightScene.Current;
        if (n != null && n.night == night && n.objectives != null && n.objectives.Length > 0) return n.objectives;
        return Defaults(night);
    }

    // The built-in objectives for each night.
    public static Objective[] Defaults(int night)
    {
        switch (night)
        {
            case 1: return new[] {
                new Objective("Settle into your new home", "", "metClown"),
                new Objective("Hide before the clown finds you", "metClown", "night1Complete"),
                new Objective("Survive the first night", "metClown", "night1Complete") };
            case 2: return new[] {
                new Objective("Watch out: the house itself is turning on you", "", "night2Complete"),
                new Objective("Survive the hazards until morning", "", "night2Complete") };
            case 3: return new[] {
                new Objective("Stay away from the flickering lights", "", "night3Complete"),
                new Objective("Survive the blackout", "", "night3Complete") };
            default: return new[] {
                new Objective("Survive until the golden knife drops", "", "knifeDropped"),
                new Objective("Grab the golden knife... or don't", "knifeDropped", "knifeTaken"),
                new Objective("Use the knife, or throw it away (T)", "grabbedKnife", "refusedKnife", "night4Complete"),
                new Objective("Stay away from whoever holds the knife", "knifeTakenByOther", "grabbedKnife", "night4Complete"),
                new Objective("Survive the second clown", "refusedKnife", "night4Complete"),
                new Objective("Be the only one who leaves this house alive", "knifeDropped", "night4Complete") };
        }
    }
}
