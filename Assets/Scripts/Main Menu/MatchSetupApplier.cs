using System.Collections.Generic;
using UnityEngine;

// Lives in the game scene and runs before everything else: puts the main menu's choices into the house.
//  - characters: every seat wears the character Game setup and Characters gave it (models, avatars and HUD
//    pictures swap between players), so you always play as Player 1 in blue, wearing the character you chose
//  - empty seats are always the last ones (Game setup moves players up): those players are switched off and
//    taken out of every list that would look for them
//  - names: every player's HUD card, scoreboard row and toast uses the name typed in Game setup
[DefaultExecutionOrder(-1000)]
public class MatchSetupApplier : MonoBehaviour
{
    [Tooltip("The four players in seat order (Player1 is you).")]
    public Hider[] seats = new Hider[4];
    [Tooltip("Each player's own Death and Idle clips, in seat order (filled from their animator controllers). When a character moves to another player, its death and idle go with it.")]
    public AnimationClip[] deathClips = new AnimationClip[4];
    public AnimationClip[] idleClips = new AnimationClip[4];

    void Awake()
    {
        if (seats == null || seats.Length < MatchSetup.Seats) return;
        for (int i = 0; i < MatchSetup.Seats; i++) if (seats[i] == null) return;

        // 1. characters: each seat wears the character chosen for it (yours in seat 1); swap models until they match
        int[] want = MatchSetup.SeatCharacters();
        int[] has = (int[])MatchSetup.SceneCharacter.Clone();
        for (int i = 0; i < MatchSetup.Seats; i++)
        {
            if (has[i] == want[i]) continue;
            int j = System.Array.IndexOf(has, want[i]);
            if (j <= i) continue;
            SwapCharacters(seats[i], seats[j]);
            int t = has[i]; has[i] = has[j]; has[j] = t;
        }
        // the character's own death (and idle) plays, whichever player wears it
        for (int i = 0; i < MatchSetup.Seats; i++)
        {
            int from = System.Array.IndexOf(MatchSetup.SceneCharacter, want[i]); // the player who wears this character in the scene
            if (from == i || from < 0) continue;
            CharacterClips(seats[i], i, from);
        }

        // 2. names
        string[] names = MatchSetup.FinalNames();
        for (int i = 0; i < MatchSetup.Seats; i++)
        {
            seats[i].displayName = names[i];
            PlayerHUD hud = seats[i].GetComponent<PlayerHUD>();
            if (hud != null) hud.playerName = names[i];
        }

        // 3. empty seats
        var playing = new List<Hider>();
        for (int i = 0; i < MatchSetup.Seats; i++)
        {
            bool empty = MatchSetup.GetSeat(i) == MatchSetup.Seat.Empty;
            if (empty) seats[i].gameObject.SetActive(false);
            else playing.Add(seats[i]);
        }
        Hider[] list = playing.ToArray();
        RoundManager rm = FindAnyObjectByType<RoundManager>();
        if (rm != null) rm.hiders = list;
        foreach (EnemyVision ev in FindObjectsByType<EnemyVision>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ev.hiders = list;
    }

    // the visible model of a player: the child holding its skinned meshes
    static Transform ModelOf(Hider h)
    {
        foreach (Transform t in h.transform)
            if (t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null && t.GetComponent<Canvas>() == null) return t;
        return null;
    }

    static void SwapCharacters(Hider a, Hider b)
    {
        Transform ma = ModelOf(a), mb = ModelOf(b);
        if (ma == null || mb == null) return;

        // each model keeps its own offset and size, but takes the facing the other player's rig expects
        Quaternion rotA = ma.localRotation, rotB = mb.localRotation;
        Vector3 posA = ma.localPosition, posB = mb.localPosition;
        Vector3 scaleA = ma.localScale, scaleB = mb.localScale;
        int idxA = ma.GetSiblingIndex(), idxB = mb.GetSiblingIndex();
        ma.SetParent(b.transform, false); mb.SetParent(a.transform, false);
        ma.localPosition = posA; ma.localScale = scaleA; ma.localRotation = rotB; ma.SetSiblingIndex(idxB);
        mb.localPosition = posB; mb.localScale = scaleB; mb.localRotation = rotA; mb.SetSiblingIndex(idxA);

        // the root animators drive the swapped skeletons, so their avatars swap too
        Animator aa = a.GetComponent<Animator>(), ab = b.GetComponent<Animator>();
        if (aa != null && ab != null)
        {
            Avatar t = aa.avatar; aa.avatar = ab.avatar; ab.avatar = t;
            aa.Rebind(); ab.Rebind();
        }

        // HUD pictures follow the character
        PlayerHUD ha = a.GetComponent<PlayerHUD>(), hb = b.GetComponent<PlayerHUD>();
        if (ha != null && hb != null)
        {
            Sprite f = ha.characterFaceSprite; ha.characterFaceSprite = hb.characterFaceSprite; hb.characterFaceSprite = f;
            Sprite p = ha.portraitSprite; ha.portraitSprite = hb.portraitSprite; hb.portraitSprite = p;
        }

        // components that were pointed at the old model
        Remap(a, mb, ma); Remap(b, ma, mb);
    }

    // keeps the seat's own controller (its parameters and layers), but plays the character's Death and Idle clips
    void CharacterClips(Hider seat, int seatIndex, int characterHome)
    {
        Animator a = seat.GetComponent<Animator>();
        if (a == null || a.runtimeAnimatorController == null) return;
        if (deathClips == null || idleClips == null || deathClips.Length < MatchSetup.Seats || idleClips.Length < MatchSetup.Seats) return;
        var o = new AnimatorOverrideController(a.runtimeAnimatorController);
        o.name = a.runtimeAnimatorController.name + " (" + MatchSetup.CharacterNames[MatchSetup.SceneCharacter[characterHome]] + ")";
        if (deathClips[seatIndex] != null && deathClips[characterHome] != null) o[deathClips[seatIndex]] = deathClips[characterHome];
        if (idleClips[seatIndex] != null && idleClips[characterHome] != null) o[idleClips[seatIndex]] = idleClips[characterHome];
        a.runtimeAnimatorController = o;
    }

    static void Remap(Hider owner, Transform newModel, Transform oldModel)
    {
        PlayerMarker pm = owner.GetComponent<PlayerMarker>();
        if (pm != null && (pm.model == null || pm.model == oldModel)) pm.model = newModel;
        FirstPersonArms fa = owner.GetComponent<FirstPersonArms>();
        if (fa != null && fa.modelToCopy == oldModel) fa.modelToCopy = newModel;
    }
}
