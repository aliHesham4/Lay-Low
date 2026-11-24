using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Added to whoever picks up the golden knife (human or AI).
// - their HUD card turns gold
// - the bloody knife is in their right hand
// - they can stab other players: click (human) / automatic (AI)
// - they can open a hiding spot (H, human) and whoever hides there is pulled out and stabbed
// If the holder is caught, the knife drops where they fell (see FinalHuntRound).
public class KnifeHolder : MonoBehaviour
{
    public static KnifeHolder Current;
    public static Hider CurrentHider { get { return Current != null ? Current.hider : null; } }
    public static bool IsHolder(Hider h) { return Current != null && h != null && Current.hider == h; }
    // true while the (AI) holder is going after this player
    public static bool IsTargeting(Hider h) { return Current != null && Current.hider != null && !Current.hider.IsCaught && h != null && Current.target == h; }

    [HideInInspector] public Hider target;

    [Header("Stabbing")]
    public float stabRange = 1.5f;
    public float stabCooldown = 1.2f;
    public AudioClip stabSound;
    [Range(0f, 1f)] public float stabVolume = 1f;

    [Header("Knife in the hand")]
    public GameObject knifeModelPrefab;
    [Tooltip("Length of the blade in the hand (metres).")]
    public float knifeLength = 0.335f; // short-handled cleaver: blade the same size as before, handle 40% shorter
    [Header("First-person knife")]
    [Tooltip("Length of the knife in your first-person hands (metres). The first-person arms are scaled up, so it needs its own size.")]
    public float fpvKnifeLength = 0.2f;
    [Tooltip("Resting direction of the blade tip in first person, in camera space (x right, y up, z forward). The edge always faces down.")]
    public Vector3 fpvTipDirection = new Vector3(-0.12f, 0.85f, 0.5f);
    [Tooltip("Which way the blade's back (spine) faces at rest in first person, in camera space. (1,0,0) = spine to the right, so the edge faces the centre of the screen and you see the flat of the blade.")]
    public Vector3 fpvSpineDirection = new Vector3(1f, 0f, 0.15f);
    [Tooltip("How far past the wrist the palm is in the first-person arms (metres).")]
    public float fpvPalmOffset = 0.045f;
    [Tooltip("Extra nudge for the grip (metres, in the hand bone's axes). The handle is already snapped into the palm.")]
    public Vector3 handPosition = Vector3.zero;
    [Tooltip("How far past the wrist the palm is (metres): where the handle sits.")]
    public float palmOffset = 0.03f;
    [Tooltip("Grip: the blade points along the stab thrust, edge down (measured from the stab clip at the hit moment).")]
    public Vector3 handEuler = new Vector3(68.8f, 290.1f, 283.0f);
    [Tooltip("How far the right arm swings in a stab (degrees around its own axes).")]
    public Vector3 armSwing = Vector3.zero; // the animator's stab clip does the arm now
    public float stabDuration = 0.4f;
    [Tooltip("Seconds into the stab animation when the blade lands.")] public float hitDelay = 0.5f;
    [Tooltip("During the strike the blade turns to point straight where the holder is facing (works for every character rig).")]
    public bool aimBladeOnStab = true;
    [Tooltip("The handle centre in the knife model's own units: the knife turns around this point so it stays in the hand.")]
    public Vector3 gripPoint = Vector3.zero;
    [Tooltip("Third person, when not stabbing: hold the blade up at this angle above level (degrees) so it never drags on the floor.")]
    [Range(0f, 80f)] public float readyAngle = 40f; // the Butcher Cleaver prefab's origin is the middle of its handle
    float swingAt = -1f;
    Vector3 knifeRestPos; Quaternion knifeRestRot; bool knifeRestSaved;
    Vector3 fpvRestPos; Quaternion fpvRestRot; bool fpvRestSaved; Animator fpvRig;

    [Header("Gold card")]
    public Color goldA = new Color(0.40f, 0.27f, 0.02f);
    public Color goldB = new Color(0.46f, 0.31f, 0.03f);
    [Tooltip("Picture shown to the left of the holder's face.")] public Texture knifeIcon;
    [Tooltip("Holder moves at this fraction of normal speed.")] [Range(0.4f, 1f)] public float holderSpeed = 1f;
    [Range(0f, 1f)] public float stabVolume2D = 0.35f;
    GameObject iconObj; PlayerMovement move; bool slowed;

    Hider hider;
    PlayerHUD hud;
    Animator anim;
    FirstPersonArms fpv;
    PlayerStoneThrower thrower;
    bool throwerWasOn;
    GameObject handKnife, fpvKnife;
    Transform armBone;
    float stabT = -1f, stabW;
    float cooldownUntil;

    Image cardImage;
    Color cardOriginal;
    GameObject cardOverlay;
    Image overlayImage;
    bool released;

    void Awake()
    {
        hider = GetComponent<Hider>();
        hud = GetComponent<PlayerHUD>();
        fpv = GetComponent<FirstPersonArms>();
        thrower = GetComponent<PlayerStoneThrower>();
        foreach (Animator a in GetComponentsInChildren<Animator>())
            if (a.avatar != null && a.avatar.isHuman) { anim = a; break; }
    }

    void Start()
    {
        Current = this;
        if (anim != null)
        {
            Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            armBone = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            handKnife = Attach(hand, -1, knifeLength, palmOffset);
        }
        if (fpv != null) StartCoroutine(AttachFpvKnife());
        if (thrower != null) { throwerWasOn = thrower.enabled; thrower.enabled = false; } // the left click is the stab now
        MakeGoldCard();
        GoldScoreBox(true);
        GoldPlayerColour(true);
        move = GetComponent<PlayerMovement>();
        if (move != null && hider.isHumanControlled) { move.extraSpeedScale = holderSpeed; slowed = true; }
    }

    // the first-person arms are built a moment after the round starts
    IEnumerator AttachFpvKnife()
    {
        for (int i = 0; i < 30 && fpv != null && fpv.RightHandBone == null; i++) yield return null;
        if (fpv == null || fpv.RightHandBone == null || released) yield break;
        fpvKnife = Attach(fpv.RightHandBone, LayerMask.NameToLayer("FPVArms"), fpvKnifeLength, fpvPalmOffset);
        PoseFpvKnife(fpv.RightHandBone);
    }

    GameObject Attach(Transform bone, int layer, float length, float palm)
    {
        if (bone == null || knifeModelPrefab == null) return null;
        GameObject g = Instantiate(knifeModelPrefab, bone);
        g.name = "Golden Knife (hand)";
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = Vector3.one;

        // size the knife by its real length, whatever the model's own units and the bone's scale are
        Bounds b = new Bounds(g.transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (has)
        {
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f) g.transform.localScale *= length / longest;
        }
        g.transform.localRotation = Quaternion.Euler(handEuler);
        // put the model's handle (gripPoint) in the palm: a little past the wrist, along the forearm
        Vector3 palmPos = bone.position;
        if (bone.parent != null) palmPos += (bone.position - bone.parent.position).normalized * palm;
        g.transform.position += palmPos - g.transform.TransformPoint(gripPoint);
        g.transform.position += bone.rotation * handPosition; // optional extra nudge (metres, hand bone axes)
        if (layer >= 0) foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        // in the world it casts and receives shadows like everything else; the first-person overlay draws no shadows
foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = layer < 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = layer < 0;
        }
        return g;
    }

// First person: hold the knife in a fixed, readable pose relative to the view: tip forward and a bit up/in, edge down,
    // handle in the palm. Set once; it then moves with the hand (sway, run swing, stab).
    void PoseFpvKnife(Transform bone)
    {
        if (fpvKnife == null || bone == null) return;
        GameObject camGo = GameObject.Find("FPVArmsCamera");
        if (camGo == null) return;
        Transform cam = camGo.transform;
        Vector3 tip = cam.TransformDirection(fpvTipDirection.normalized);
        Vector3 up = Vector3.ProjectOnPlane(cam.up, tip).normalized;
        Transform k = fpvKnife.transform;
        Vector3 palmPos = bone.position;
        if (bone.parent != null) palmPos += (bone.position - bone.parent.position).normalized * fpvPalmOffset;
        k.rotation = Quaternion.LookRotation(-tip, up); // model +Z runs blade->handle, +Y is the spine
        k.position += palmPos - k.TransformPoint(gripPoint);
        fpvRestSaved = false; // AimBlade re-reads this as the resting grip
    }


    readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<RectTransform, Vector2>> shifted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<RectTransform, Vector2>>();
    void ShiftRight(RectTransform r, float dx)
    {
        if (r == null) return;
        shifted.Add(new System.Collections.Generic.KeyValuePair<RectTransform, Vector2>(r, r.anchoredPosition));
        r.anchoredPosition += new Vector2(dx, 0f);
    }

    Image scoreBoxImg; Color scoreBoxOrig; Outline scoreFrame;
    void GoldScoreBox(bool on)
    {
        if (hud == null || hud.scoreText == null) return;
        if (scoreBoxImg == null) scoreBoxImg = hud.scoreText.GetComponentInParent<Image>();
        if (scoreBoxImg == null) return;
        if (on) { hud.goldScore = true; scoreBoxOrig = scoreBoxImg.color; scoreBoxImg.color = new Color(0.88f, 0.70f, 0.32f, 1f); hud.scoreText.color = new Color(0.25f, 0.14f, 0.01f);
            scoreFrame = scoreBoxImg.gameObject.AddComponent<Outline>(); scoreFrame.effectColor = new Color(0.30f, 0.19f, 0.01f, 1f); scoreFrame.effectDistance = new Vector2(2f, -2f); hud.SetScore(hud.score); }
        else { hud.goldScore = false; scoreBoxImg.color = scoreBoxOrig; if (scoreFrame != null) Destroy(scoreFrame); hud.scoreText.color = Color.white; hud.SetScore(hud.score); }
    }

// the player's own colour turns gold while they hold the knife: face-circle border, outline, target marker
    // (PlayerColors.For returns gold for the holder, so anything that asks for their colour gets gold too)
    Image avatarBorder; Color avatarBorderOrig; bool borderSaved;
    void GoldPlayerColour(bool on)
    {
        if (hud != null && hud.avatarIcon != null)
        {
            if (avatarBorder == null)
            {
                Transform b = hud.avatarIcon.transform.parent != null ? hud.avatarIcon.transform.parent.parent : null;
                avatarBorder = b != null ? b.GetComponent<Image>() : null;
            }
            if (avatarBorder != null)
            {
                if (on) { if (!borderSaved) { avatarBorderOrig = avatarBorder.color; borderSaved = true; } avatarBorder.color = PlayerColors.Gold; }
                else if (borderSaved) avatarBorder.color = avatarBorderOrig;
            }
        }
        PlayerMarker marker = GetComponent<PlayerMarker>();
        if (marker != null) marker.RefreshColor(); // reads gold while Current == this, normal colour after Release
    }


    void MakeGoldCard()
    {
        if (hud == null) return;
        RectTransform card = hud.CardRect;
        if (card == null) return;
        cardImage = card.GetComponent<Image>();
        if (cardImage != null) cardOriginal = cardImage.color;

        cardOverlay = new GameObject("GoldCard", typeof(RectTransform), typeof(Image), typeof(Outline));
        cardOverlay.transform.SetParent(card, false);
        RectTransform r = (RectTransform)cardOverlay.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        overlayImage = cardOverlay.GetComponent<Image>();
        overlayImage.color = new Color(goldA.r, goldA.g, goldA.b, 0.9f);
        cardOverlay.transform.SetAsFirstSibling(); // behind the name, score and face so they stay readable
        overlayImage.raycastTarget = false;
        Outline o = cardOverlay.GetComponent<Outline>();
        o.effectColor = new Color(0.73f, 0.46f, 0.09f);
        o.effectDistance = new Vector2(5f, -5f);

        // (no text label: the gold card and the knife icon say it)

        // the bloody knife picture to the left of the profile picture
        if (knifeIcon != null && hud.avatarIcon != null)
        {
            RectTransform av = hud.avatarIcon.rectTransform;
            RectTransform border = av.parent != null && av.parent.parent != null ? av.parent.parent as RectTransform : av;
            iconObj = new GameObject("KnifeIcon", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            iconObj.GetComponent<LayoutElement>().ignoreLayout = true; // the row's layout group must not stretch it
            iconObj.transform.SetParent(border.parent, false);
            RectTransform ir = (RectTransform)iconObj.transform;
            ir.anchorMin = border.anchorMin; ir.anchorMax = border.anchorMax; ir.pivot = new Vector2(0.5f, 0.5f);
            float ih = 48f, iw = 62f;
            ir.sizeDelta = new Vector2(iw, ih);
            ir.anchoredPosition = border.anchoredPosition + new Vector2(-border.rect.width * 0.5f + 12f, -border.rect.height * 0.5f + 16f);
            RawImage ri = iconObj.GetComponent<RawImage>();
            ri.texture = knifeIcon; ri.raycastTarget = false;
            Outline io = iconObj.AddComponent<Outline>(); io.effectColor = new Color(0.16f, 0.08f, 0f, 1f); io.effectDistance = new Vector2(1.5f, -1.5f);
            Shadow ish = iconObj.AddComponent<Shadow>(); ish.effectColor = new Color(0f, 0f, 0f, 0.6f); ish.effectDistance = new Vector2(2f, -3f);
        }
    }

    void Update()
    {
        if (hider == null) return;

        // the card shimmers while he has the knife
        if (!hider.IsCaught && (overlayImage != null || cardImage != null))
        {
            float k = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.5f;
            Color gold = Color.Lerp(goldA, goldB, k);
            if (overlayImage != null) overlayImage.color = new Color(gold.r, gold.g, gold.b, 1f);
            if (cardImage != null) cardImage.color = new Color(goldA.r * 0.8f, goldA.g * 0.8f, goldA.b * 0.8f, Mathf.Max(0.9f, cardOriginal.a));
        }

        // stab animation clock
        if (stabT >= 0f)
        {
            stabT += Time.deltaTime;
            float d = Mathf.Max(0.1f, stabDuration), k = stabT / d;
            stabW = k >= 1f ? 0f : (k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f); // quick thrust, slower return
            if (k >= 1f) stabT = -1f;
            if (fpv != null) fpv.rightHandKick = new Vector3(0f, 0.05f, 0.34f) * stabW;
        }

        if (!hider.isHumanControlled || hider.IsCaught || hider.IsHidden || Time.timeScale <= 0f) return;
        FeatherHolder feather = GetComponent<FeatherHolder>();
        if (feather != null && feather.IsAiming) return; // the click belongs to the feather
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) TryStab();
    }

void LateUpdate()
    {
        // the animator has written this frame's pose: swing the right arm on top of it
        if (stabW > 0f && armBone != null) armBone.localRotation = armBone.localRotation * Quaternion.Euler(armSwing * stabW);
        AimBlade();
    }

    // While the blade lands, turn it to point the way the holder faces (edge down), around the handle so it stays in the hand
void AimBlade()
    {
        // third person: follow the stab clip's timing around the hit moment
        float w = 0f;
        if (swingAt >= 0f)
        {
            float t = Time.time - swingAt;
            float inA = hitDelay - 0.3f, full = hitDelay - 0.12f, outA = hitDelay + 0.2f, end = hitDelay + 0.38f;
            w = t < inA ? 0f : t < full ? (t - inA) / (full - inA) : t < outA ? 1f : t < end ? 1f - (t - outA) / (end - outA) : 0f;
            if (t >= end) swingAt = -1f;
        }
        // at rest the cleaver is held up at readyAngle; during the strike it levels out to point straight ahead
        float pitch = Mathf.Lerp(readyAngle, -3f, w);
        AimOne(handKnife, anim, 1f, pitch, ref knifeRestPos, ref knifeRestRot, ref knifeRestSaved);

        AimFpv();
    }

// First person: every frame, hold the cleaver in a fixed pose relative to the view (tip forward and a bit up/in,
    // edge down) with the handle in the palm; during the thrust the tip swings to dead ahead.
    Transform fpvCam;
    void AimFpv()
    {
        if (fpvKnife == null) return;
        Transform bone = fpvKnife.transform.parent;
        if (bone == null) return;
        if (fpvCam == null) { GameObject c = GameObject.Find("FPVArmsCamera"); if (c != null) fpvCam = c.transform; }
        if (fpvCam == null) return;

        float w = aimBladeOnStab ? Mathf.Clamp01(stabW * 1.6f) : 0f;
        Vector3 tip = Vector3.Slerp(fpvCam.TransformDirection(fpvTipDirection.normalized), fpvCam.forward, w).normalized;
        Vector3 spine = Vector3.Slerp(fpvCam.TransformDirection(fpvSpineDirection.normalized), fpvCam.up, w); // stab: edge turns down
        Vector3 up = Vector3.ProjectOnPlane(spine, tip).normalized;
        Transform k = fpvKnife.transform;
        k.rotation = Quaternion.LookRotation(-tip, up); // model +Z runs blade->handle, +Y is the spine
        Vector3 palmPos = bone.position;
        if (bone.parent != null) palmPos += (bone.position - bone.parent.position).normalized * fpvPalmOffset;
        k.position += palmPos - k.TransformPoint(gripPoint);
    }


    // Puts a knife back in its normal grip, then (by weight w) turns it to point the way that rig's body faces,
    // edge down, around the handle so it stays in the hand
// Puts a knife back in its normal grip, then (by weight w) turns it to point the way that rig's body faces,
    // tilted up by pitchDeg, edge down, around the handle so it stays in the hand
    void AimOne(GameObject knife, Animator rig, float w, float pitchDeg, ref Vector3 restPos, ref Quaternion restRot, ref bool saved)
    {
        if (knife == null) return;
        Transform k = knife.transform;
        if (!saved) { restPos = k.localPosition; restRot = k.localRotation; saved = true; }
        k.localPosition = restPos; k.localRotation = restRot; // back to the normal grip every frame
        if (w <= 0f || rig == null) return;

        Transform ls = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm), rs = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (ls == null || rs == null) return;
        Vector3 facing = Vector3.Cross(rs.position - ls.position, Vector3.up); facing.y = 0f; // shoulders tell which way the body faces
        if (facing.sqrMagnitude < 0.0001f) return;
        facing.Normalize();
        float a = pitchDeg * Mathf.Deg2Rad;
        Vector3 tipDir = (facing * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)).normalized;
        Quaternion target = Quaternion.LookRotation(-tipDir, Vector3.up); // model +Z runs blade->handle, +Y is the spine

        Vector3 gripWorld = k.TransformPoint(gripPoint);
        k.rotation = Quaternion.Slerp(k.rotation, target, w);
        k.position += gripWorld - k.TransformPoint(gripPoint);
    }

    // ---------------- stabbing ----------------

    public Hider NearestVictim(float range)
    {
        Hider best = null;
        float bestD = range;
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
        {
            if (h == null || h == hider || h.IsCaught || h.IsHidden) continue;
            Vector3 d = h.transform.position - transform.position; d.y = 0f;
            if (d.magnitude < bestD) { bestD = d.magnitude; best = h; }
        }
        return best;
    }

    // human: left click
    void TryStab()
    {
        if (Time.time < cooldownUntil) return;
        StabAt(NearestVictim(stabRange));
    }

    // Swings the knife; if somebody is there (still, and close), they are stabbed. Also used by the AI.
    public bool StabAt(Hider victim)
    {
        if (Time.time < cooldownUntil || hider == null || hider.IsCaught) return false;
        cooldownUntil = Time.time + stabCooldown;
        Swing();
        if (victim != null) StartCoroutine(Hit(victim));
        return victim != null;
    }

IEnumerator Hit(Hider victim)
    {
        yield return new WaitForSeconds(hitDelay); // the moment the blade arrives (in the clown's stab clip)
        if (victim == null || victim.IsCaught || victim.IsHidden || hider == null || hider.IsCaught) yield break;
        Vector3 d = victim.transform.position - transform.position; d.y = 0f;
        if (d.magnitude > stabRange + 0.6f) yield break; // he got away: no sound
        if (stabSound != null) PlayStab();
        victim.FreezeBy(hider);
    }

    // arm swing + sound (no victim)
    static bool HasParam(Animator a, string n)
    {
        foreach (AnimatorControllerParameter p in a.parameters) if (p.name == n) return true;
        return false;
    }

    void PlayStab()
    {
        GameObject g = new GameObject("StabSound");
        g.transform.position = transform.position + Vector3.up;
        AudioSource a = g.AddComponent<AudioSource>();
        a.clip = stabSound; a.volume = 1f; a.spatialBlend = 0.5f; a.minDistance = 8f; a.maxDistance = 40f;
        a.Play();
        Destroy(g, stabSound.length + 0.2f);
    }

// arm swing only: the stab sound plays in Hit(), when the blade actually lands on someone
    public void Swing()
    {
        stabT = 0f;
        swingAt = Time.time;
        if (anim != null && HasParam(anim, "KnifeStab")) anim.SetTrigger("KnifeStab");
    }

    // ---------------- dropping ----------------

    public void OnDeath()
    {
        if (released) return;
        Vector3 pos = transform.position;
        Release();
        if (FinalHuntRound.Instance != null) FinalHuntRound.Instance.DropKnife(pos);
        Destroy(this);
    }

    void Release()
    {
        if (released) return;
        released = true;
        if (Current == this) Current = null;
        target = null;
        if (fpv != null) fpv.rightHandKick = Vector3.zero;
        if (handKnife != null) Destroy(handKnife);
        if (fpvKnife != null) Destroy(fpvKnife);
        if (cardOverlay != null) Destroy(cardOverlay);
        if (iconObj != null) Destroy(iconObj);
        GoldScoreBox(false);
        GoldPlayerColour(false);
        if (slowed && move != null) { move.extraSpeedScale = 1f; slowed = false; }
        if (cardImage != null) cardImage.color = cardOriginal;
        if (thrower != null && throwerWasOn) thrower.enabled = true;
        AIKnifeHunter hunter = GetComponent<AIKnifeHunter>();
        if (hunter != null) Destroy(hunter);
    }

    void OnDestroy() { Release(); }
}
