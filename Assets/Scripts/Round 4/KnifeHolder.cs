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
    public float knifeLength = 0.4f;
    public Vector3 handPosition = new Vector3(0f, 0.03f, 0f);
    public Vector3 handEuler = new Vector3(0f, 0f, 0f);
    [Tooltip("How far the right arm swings in a stab (degrees around its own axes).")]
    public Vector3 armSwing = Vector3.zero; // the animator's stab clip does the arm now
    public float stabDuration = 0.4f;
    [Tooltip("Seconds into the stab animation when the blade lands.")] public float hitDelay = 0.5f;

    [Header("Gold card")]
    public Color goldA = new Color(0.40f, 0.27f, 0.02f);
    public Color goldB = new Color(0.46f, 0.31f, 0.03f);
    [Tooltip("Picture shown to the left of the holder's face.")] public Texture knifeIcon;
    [Tooltip("Holder moves at this fraction of normal speed.")] [Range(0.4f, 1f)] public float holderSpeed = 0.8f;
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
            handKnife = Attach(hand, -1);
        }
        if (fpv != null) StartCoroutine(AttachFpvKnife());
        if (thrower != null) { throwerWasOn = thrower.enabled; thrower.enabled = false; } // the left click is the stab now
        MakeGoldCard();
        GoldScoreBox(true);
        move = GetComponent<PlayerMovement>();
        if (move != null && hider.isHumanControlled) { move.extraSpeedScale = holderSpeed; slowed = true; }
    }

    // the first-person arms are built a moment after the round starts
    IEnumerator AttachFpvKnife()
    {
        for (int i = 0; i < 30 && fpv != null && fpv.RightHandBone == null; i++) yield return null;
        if (fpv == null || fpv.RightHandBone == null || released) yield break;
        fpvKnife = Attach(fpv.RightHandBone, LayerMask.NameToLayer("FPVArms"));
    }

    GameObject Attach(Transform bone, int layer)
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
            if (longest > 0.0001f) g.transform.localScale *= knifeLength / longest;
        }
        Vector3 ls = bone.lossyScale; g.transform.localPosition = new Vector3(handPosition.x / Mathf.Max(0.0001f, ls.x), handPosition.y / Mathf.Max(0.0001f, ls.y), handPosition.z / Mathf.Max(0.0001f, ls.z)); // handPosition is in metres
        g.transform.localRotation = Quaternion.Euler(handEuler);
        if (layer >= 0) foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
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
        if (d.magnitude > stabRange + 0.6f) yield break; // he got away
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

    public void Swing()
    {
        stabT = 0f;
        if (anim != null && HasParam(anim, "KnifeStab")) anim.SetTrigger("KnifeStab");
        if (stabSound != null) PlayStab();
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
        
        if (slowed && move != null) { move.extraSpeedScale = 1f; slowed = false; }
        if (cardImage != null) cardImage.color = cardOriginal;
        if (thrower != null && throwerWasOn) thrower.enabled = true;
        AIKnifeHunter hunter = GetComponent<AIKnifeHunter>();
        if (hunter != null) Destroy(hunter);
    }

    void OnDestroy() { Release(); }
}
