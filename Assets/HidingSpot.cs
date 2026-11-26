using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class HidingSpot : MonoBehaviour
{
    [Header("Visual swap")]
    public GameObject normalVisual;
    public GameObject hiddenVisual;
    [Tooltip("Uncheck this for bathtubs so the player mesh stays visible inside.")]
    public bool hidePlayerMesh = true;

    [Header("VFX")]
    [Tooltip("Cartoon dust particle system (can be a child object in the scene or a prefab).")]
    public ParticleSystem hideDustVFX;

    [Header("Bathtub Settings (Laying Down)")]
    public bool layFlatInside = false;
    [Tooltip("Move everyone lying here up (+) or down (-) in the tub, in metres.")]
    public float lyingHeightOffset = 0.08f;

    // Pose copied from the reference hider (Player 1) so every character lies the same way
    [HideInInspector] public string lyingPoseData;

    // Lying pose, measured on Player 1. Directions are relative to the Inside Point.
    static readonly Vector3 LyingHeadDir = new Vector3(0.972f, 0.231f, -0.035f);   // head points along the tub
    static readonly Vector3 LyingFaceDir = new Vector3(-0.233f, 0.964f, -0.117f);  // face points up
    static readonly Vector3 LyingHips = new Vector3(0.156f, -0.009f, 0f);          // where the hips sit

    [Header("Player Positions (Optional)")]
    public Transform insidePoint; // Place inside the bathtub (UPRIGHT, just lower Y)
    public Transform exitPoint;   // Place on the floor outside the bathtub

    [Header("Camera (same two objects for every hiding spot)")]
    public CinemachineCamera fpvCamera;
    public GameObject hideCamera;

    [Header("Close-up camera placement")]
    public Transform cameraAnchor;
    public Vector3 cameraOffset = new Vector3(0f, 1.5f, -2.5f);
    public float lookAtHeight = 0.5f;

    [Header("Looking around while hidden")]
    public float lookSpeed = 90f;
    public float maxYaw = 60f;

    [Header("Fridge (optional)")]
    [Tooltip("Only for the fridge: animates its doors and light.")]
    public FridgeDoors fridge;

    [Header("AI hider (optional)")]
    public Transform approachPoint;

    [Header("Disguise (optional, e.g. the floor lamp)")]
    [Tooltip("The hider stays in the open, standing still in the idle pose, instead of whatever pose he walked in with.")]
    public bool standStill = false;
    [Tooltip("Put on the hider's head while hidden (the lamp shade). Its pivot must be the centre of its bottom opening.")]
    public Transform wornOnHead;
    [Tooltip("How far below the chin the worn object's opening sits, in metres. It is sized so the whole head (face included) fits inside.")]
    public float wornDrop = 0.02f;
    [Tooltip("Size of the worn object compared to a snug fit around the head (1 = just covers the whole head).")]
    public float wornSize = 1f;
    [Tooltip("Sideways tilt of the worn object, in degrees, so it sits a little crooked.")]
    public float wornTilt = 4f;
    [Tooltip("Stretches the hider while hidden here: below 1 on X/Z makes him thinner, above 1 on Y makes him taller (lamp-shaped). 1,1,1 = unchanged.")]
    public Vector3 disguiseStretch = Vector3.one;
    [Tooltip("Head size while hidden here, compared to normal (smaller head = smaller worn object). 1 = unchanged.")]
    [Range(0.4f, 1f)] public float disguiseHeadSize = 1f;
    [Tooltip("Shown at the bottom of the screen to the player hiding here. Leave empty for none.")]
    [TextArea(1, 3)] public string hiddenCaption = "";

    // where the worn object lives while nobody is wearing it
    Transform wornHome;
    Vector3 wornHomePos, wornHomeScale;
    Quaternion wornHomeRot;
    float wornRadius = 0.2f, wornTopRadius = 0.135f, wornHeight = 0.25f;  // its openings and height, measured from its mesh
    // the hider's own size, put back when he comes out
    Hider stretched;
    Vector3 stretchHome;
    Transform stretchedHead;
    Vector3 stretchedHeadHome;

    public bool IsFree => occupant == null;
    public Hider Occupant => occupant;
    private Hider occupant;
    private Hider humanInRange;
    private bool switchedCamera = false;
    private Quaternion baseRotation;
    private float yaw = 0f;
    private Collider[] triggers;

    void Awake()
    {
        triggers = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => c.isTrigger);
        if (wornOnHead != null) MeasureWorn();
    }

    void Start()
    {
        if (normalVisual != null) normalVisual.SetActive(true);
        if (hiddenVisual != null) hiddenVisual.SetActive(false);
        if (hideCamera != null) hideCamera.SetActive(false);
    }

    [Header("On-screen prompt (Press H)")]
    [Tooltip("Optional. Where the prompt floats. Defaults to just above the trigger box.")]
    public Transform promptAnchor;

    // every hiding spot, so a key press is answered by one spot only (two spots can share the player)
    static readonly System.Collections.Generic.List<HidingSpot> all = new System.Collections.Generic.List<HidingSpot>();
    static int hHandledFrame = -1;
    // the human knife holder can't hide while opening a spot, or for a moment after the kill
    static float hunterBusyUntil = -1f;
    float stabPromptAt = -9f; // last time this spot offered the knife holder 'to open and stab'

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }

    void Update()
    {
        if (humanInRange != null && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && hHandledFrame != Time.frameCount)
        {
            bool hunter = KnifeHolder.IsHolder(humanInRange);
            if (occupant == null)
            {
                // the knife holder never ends up hidden by mistake: no hiding while busy opening a spot,
                // and an occupied spot next to this one is opened instead
                // (also when the one inside slipped out just as he pressed: the prompt still said 'to open and stab')
                if (!(hunter && (Time.time < hunterBusyUntil || OccupiedSpotFor(humanInRange) != null || Time.time - stabPromptAt < 0.6f)))
                {
                    hHandledFrame = Time.frameCount;
                    TryEnter(humanInRange);
                }
            }
            else if (occupant == humanInRange) { hHandledFrame = Time.frameCount; Exit(humanInRange); }
            else if (hunter) { hHandledFrame = Time.frameCount; HunterOpen(humanInRange, 1.2f); } // golden knife: open it and stab whoever is inside
        }

        // safety net: if whoever is inside got caught or un-hidden without coming out through Exit,
        // still put the spot back the way it was (furniture shown, worn object returned, size restored)
        if (occupant != null && (occupant.IsCaught || !occupant.IsHidden) && !openingByHunter)
            Exit(occupant);

        if (occupant != null && occupant.isHumanControlled && switchedCamera)
            HandleHideLook();

        UpdatePrompt();
    }

    // another spot the player is standing at, with someone (else) hiding in it
    HidingSpot OccupiedSpotFor(Hider h)
    {
        foreach (HidingSpot s in all)
            if (s != null && s != this && s.humanInRange == h && s.occupant != null && s.occupant != h && !s.openingByHunter) return s;
        return null;
    }

    void OnDisable()
    {
        all.Remove(this);
        HidePrompt.Hide(this);
        HideCaption.Hide(this);
    }

    // Shows "Press [H] to hide", "Press [H] to come out" or "A player is hiding here"
    void UpdatePrompt()
    {
        bool show = humanInRange != null && !humanInRange.IsCaught && Time.timeScale > 0f && !openingByHunter;
        // the knife holder gets no 'to hide' prompt while opening a spot or just after a kill
        if (show && occupant == null && KnifeHolder.IsHolder(humanInRange) && (Time.time < hunterBusyUntil || OccupiedSpotFor(humanInRange) != null)) show = false;
        if (!show)
        {
            HidePrompt.Hide(this);
            return;
        }

        HidePrompt.Mode mode;
        if (occupant == null) mode = HidePrompt.Mode.Hide;
        else if (occupant == humanInRange) mode = HidePrompt.Mode.ComeOut;
        else if (KnifeHolder.IsHolder(humanInRange)) { mode = HidePrompt.Mode.Stab; stabPromptAt = Time.time; }
        else mode = HidePrompt.Mode.Occupied;

        HidePrompt.Instance.Show(this, GetPromptPosition(), mode);
    }

    Vector3 GetPromptPosition()
    {
        if (promptAnchor != null) return promptAnchor.position;

        if (triggers != null && triggers.Length > 0 && triggers[0] != null)
        {
            Bounds b = triggers[0].bounds;
            return new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        }
        return transform.position + Vector3.up * 0.6f;
    }

    void OnTriggerEnter(Collider other)
    {
        Hider h = other.GetComponentInParent<Hider>();
        if (h != null && h.isHumanControlled) humanInRange = h;
    }

    void OnTriggerExit(Collider other)
    {
        Hider h = other.GetComponentInParent<Hider>();
        if (h != null && h == humanInRange && occupant != h) humanInRange = null;
    }

    public bool TryEnter(Hider h)
    {
        if (!Application.isPlaying) return false; // never change the saved scene from the editor
        if (h == null || h.IsCaught || occupant != null) return false;

        // Trigger dust at the hider's feet BEFORE teleporting them to insidePoint
        PlayHideDust(h.transform.position);

        occupant = h;
        h.SetHidden(true, hidePlayerMesh);

        // An AI hider's NavMeshAgent would drag it back onto the floor while it lies in the tub
        UnityEngine.AI.NavMeshAgent agent = h.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled) agent.enabled = false;

        if (insidePoint != null)
        {
            h.transform.position = insidePoint.position;
            // 1. Keep the invisible physics root standing strictly upright
            h.transform.rotation = Quaternion.Euler(0f, insidePoint.eulerAngles.y, 0f);
        }

        // 2. Tilt ONLY the visual mesh backward to lay down
        if (layFlatInside)
        {
            Animator anim = h.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.transform.localRotation = Quaternion.Euler(90f, 90f, 0f); // fallback if the skeleton can't be found
                ApplyLyingPose(h);
                AlignLyingPose(h);
            }
        }

        if (normalVisual != null) normalVisual.SetActive(false);
        if (hiddenVisual != null) hiddenVisual.SetActive(true);

        // disguise: stand still in the open and wear part of the furniture
        if (standStill) FreezeStanding(h);
        // size the worn object on his normal head first, then stretch him
        Transform head = HeadOf(h);
        Vector3 wornAt = Vector3.zero;
        float wornScale = 0f;
        bool fits = wornOnHead != null && head != null && MeasureHead(h, head, out wornAt, out wornScale);
        Vector3 headBefore = head != null ? head.position : Vector3.zero;
        if (disguiseStretch != Vector3.one || disguiseHeadSize != 1f) Stretch(h, head);
        // the head shrinks around the neck, so the worn object shrinks and moves with it
        if (fits) PutOnHead(h, head.position + (wornAt - headBefore) * disguiseHeadSize, wornScale * disguiseHeadSize);
        // the caption, with the hider's name on it, shows the first time only (once per game)
        if (h.isHumanControlled && !string.IsNullOrEmpty(hiddenCaption) && StoryStateManager.Instance.SetFlag("hidIn_" + name))
            HideCaption.Show(this, hiddenCaption, h.displayName);

        if (h.isHumanControlled && hideCamera != null)
        {
            PlaceHideCamera();
            hideCamera.SetActive(true);
            switchedCamera = true;
        }
        if (fridge != null) fridge.PlayEnter();
        return true;
    }

    void PlayHideDust(Vector3 footPosition)
    {
        if (hideDustVFX == null) return;

        Vector3 spawnPos = footPosition + Vector3.up * 0.2f;

        if (hideDustVFX.gameObject.scene.IsValid())
        {
            hideDustVFX.transform.position = spawnPos;
            hideDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hideDustVFX.Play();
        }
        else
        {
            ParticleSystem burst = Instantiate(hideDustVFX, spawnPos, Quaternion.identity);
            burst.Play();
            Destroy(burst.gameObject, burst.main.duration + burst.main.startLifetime.constantMax);
        }
    }

    public void Exit(Hider h)
    {
        if (occupant == null || occupant != h) return;

        // back to his own size before he steps out
        if (stretched == h)
        {
            if (stretchedHead != null) stretchedHead.localScale = stretchedHeadHome;
            h.transform.localScale = stretchHome;
            stretched = null; stretchedHead = null;
        }

        if (exitPoint != null)
        {
            h.transform.position = exitPoint.position;
        }

        UnityEngine.AI.NavMeshAgent exitAgent = h.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (exitAgent != null && !exitAgent.enabled)
        {
            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(h.transform.position, out navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                h.transform.position = navHit.position;
        }

        foreach (Animator a in h.GetComponentsInChildren<Animator>(true)) a.enabled = true;

        // Snap the visual mesh back upright so the character walks normally
        if (layFlatInside)
        {
            Animator anim = h.GetComponentInChildren<Animator>();
            if (anim != null) anim.transform.localRotation = Quaternion.identity;
        }

        h.transform.rotation = Quaternion.Euler(0f, h.transform.eulerAngles.y, 0f);

        TakeOffHead();
        HideCaption.Hide(this);

        occupant = null;
        h.SetHidden(false);
        if (exitAgent != null && !exitAgent.enabled) exitAgent.enabled = true;

        if (normalVisual != null) normalVisual.SetActive(true);
        if (hiddenVisual != null) hiddenVisual.SetActive(false);

        if (switchedCamera && hideCamera != null)
        {
            hideCamera.SetActive(false);
            switchedCamera = false;
        }
        if (fridge != null) fridge.PlayExit();
    }

    // The player holding the golden knife opens this spot; whoever is hiding inside is pulled out and stabbed.
    bool openingByHunter;
    public bool HunterOpen(Hider hunter, float duration)
    {
        if (hunter == null || hunter.IsCaught || occupant == null || occupant == hunter || openingByHunter) return false;
        if (hunter.isHumanControlled) hunterBusyUntil = Time.time + duration + 1.2f;
        StartCoroutine(HunterOpenRoutine(hunter, duration));
        return true;
    }

    System.Collections.IEnumerator HunterOpenRoutine(Hider hunter, float duration)
    {
        openingByHunter = true;
        NotifyEnemyOpening(duration); // fridge doors swing open
        yield return new WaitForSeconds(duration);
        Hider victim = occupant;
        if (victim != null && victim != hunter && !victim.IsCaught && hunter != null && !hunter.IsCaught)
        {
            Exit(victim); // pulled out of the spot...
            KnifeHolder k = hunter.GetComponent<KnifeHolder>();
            if (k != null) k.StabPulledOut(victim); // ...then stabbed, with the swing
            else victim.FreezeBy(hunter);
        }
        openingByHunter = false;
    }

    // Called by the enemy when he starts opening this spot.
    public void NotifyEnemyOpening(float duration)
    {
        if (fridge != null) fridge.EnemyOpens(duration);
    }

    // ---------- Disguise ----------

    // Snaps the hider into the first frame of his idle and holds it there, so he stands up straight and still
    void FreezeStanding(Hider h)
    {
        int idle = Animator.StringToHash("Idle");
        foreach (Animator a in h.GetComponentsInChildren<Animator>())
        {
            if (!a.isActiveAndEnabled || a.runtimeAnimatorController == null || !a.HasState(0, idle)) continue;
            a.speed = 1f;
            a.SetFloat("Speed", 0f);
            a.Play(idle, 0, 0f);
            a.Update(0f);
            a.speed = 0f;
            a.enabled = false; // nothing rewrites the pose (or the stretch) while he stands there; Exit turns it back on
        }

        // turn him so his face (not just his root) looks the way the Inside Point faces
        // (each character's skeleton is named differently, so ask the humanoid rig for the upper arms)
        Animator rig = h.GetComponentInChildren<Animator>();
        Transform ls = null, rs = null;
        if (rig != null && rig.isHuman) { ls = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm); rs = rig.GetBoneTransform(HumanBodyBones.RightUpperArm); }
        if (ls == null || rs == null) { ls = FindBone(h, "LeftShoulder"); rs = FindBone(h, "RightShoulder"); }
        if (insidePoint == null || ls == null || rs == null) return;
        Vector3 face = Vector3.Cross(rs.position - ls.position, Vector3.up);
        face.y = 0f;
        if (face.sqrMagnitude < 1e-6f) return;
        Vector3 want = insidePoint.forward; want.y = 0f;
        float turn = Vector3.SignedAngle(face, want, Vector3.up);
        h.transform.RotateAround(insidePoint.position, Vector3.up, turn);
    }

    // The head bone, from the humanoid rig if there is one (bone names differ per character)
    static Transform HeadOf(Hider h)
    {
        Animator rig = h.GetComponentInChildren<Animator>();
        if (rig != null && rig.isHuman) { Transform t = rig.GetBoneTransform(HumanBodyBones.Head); if (t != null) return t; }
        return FindBone(h, "Head");
    }

    // Size of the worn object's bottom opening and its height, in its own units
    void MeasureWorn()
    {
        wornHome = wornOnHead.parent;
        wornHomePos = wornOnHead.localPosition;
        wornHomeRot = wornOnHead.localRotation;
        wornHomeScale = wornOnHead.localScale;

        MeshFilter mf = wornOnHead.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null) return;
        Bounds b = mf.sharedMesh.bounds;
        wornHeight = Mathf.Max(0.01f, b.max.y);
        wornRadius = Mathf.Max(0.01f, Mathf.Min(b.extents.x, b.extents.z));
        // the narrower top opening: the widest point near the top
        float best = 0f;
        foreach (Vector3 v in mf.sharedMesh.vertices)
            if (v.y > wornHeight * 0.85f) best = Mathf.Max(best, new Vector2(v.x, v.z).magnitude);
        if (best > 0.01f) wornTopRadius = Mathf.Min(best, wornRadius);
    }

    // Measures the posed head: where the worn object's opening goes (just under the neck) and how big it must be
    bool MeasureHead(Hider h, Transform head, out Vector3 at, out float scale)
    {
        at = Vector3.zero; scale = 1f;

        // the posed head's vertices, in world space: every vertex mostly moved by the head bone (or its hair, eyes...)
        List<Vector3> verts = new List<Vector3>();
        Mesh baked = new Mesh();
        foreach (SkinnedMeshRenderer smr in h.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!smr.enabled || smr.sharedMesh == null) continue;
            smr.BakeMesh(baked, false); // the skinned result is already at world size; only its position and rotation are added below
            Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Vector3[] vs = baked.vertices;
            BoneWeight[] bw = smr.sharedMesh.boneWeights;
            Transform[] bones = smr.bones;
            bool[] onHead = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++) onHead[b] = bones[b] != null && (bones[b] == head || bones[b].IsChildOf(head));
            bool weighted = bw.Length == vs.Length;
            for (int i = 0; i < vs.Length; i++)
            {
                Vector3 w = m.MultiplyPoint3x4(vs[i]);
                if (weighted) { int b = bw[i].boneIndex0; if (b < 0 || b >= onHead.Length || !onHead[b]) continue; }
                else if (w.y < head.position.y) continue; // no skin weights: fall back to 'above the neck'
                verts.Add(w);
            }
        }
        Destroy(baked);
        if (verts.Count == 0) return false;

        // the opening sits just under the chin, so the whole head, face included, ends up inside
        float rim = float.MaxValue;
        foreach (Vector3 v in verts) rim = Mathf.Min(rim, v.y);
        rim -= wornDrop;
        float top = rim, minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 v in verts)
        {
            top = Mathf.Max(top, v.y);
            minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
            minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
        }
        if (minX > maxX) return false;
        Vector3 centre = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

        // grow it until the head fits everywhere: tall enough for the crown, wide enough at every height
        // (it narrows towards the top, so the width it needs depends on how tall it already is)
        for (int i = 0; i < 6; i++)
        {
            float need = (top - rim) / (wornHeight * 0.92f);
            foreach (Vector3 v in verts)
            {
                float along = Mathf.Clamp01((v.y - rim) / (wornHeight * scale));
                float room = Mathf.Lerp(wornRadius, wornTopRadius, along) * 0.97f;
                float d = new Vector2(v.x - centre.x, v.z - centre.z).magnitude;
                need = Mathf.Max(need, d / room);
            }
            scale = Mathf.Clamp(need, 0.4f, 4f);
        }
        scale *= wornSize;
        at = new Vector3(centre.x, rim, centre.z);
        return true;
    }

    // Puts the worn object on the hider's head
    void PutOnHead(Hider h, Vector3 at, float scale)
    {
        // it stays a child of the spot (he can't move while hidden), so his stretched size never skews it
        wornOnHead.SetParent(wornHome, true);
        wornOnHead.position = at;
        wornOnHead.rotation = Quaternion.Euler(0f, h.transform.eulerAngles.y, 0f) * Quaternion.Euler(0f, 0f, wornTilt);
        Vector3 ps = wornHome != null ? wornHome.lossyScale : Vector3.one;
        wornOnHead.localScale = new Vector3(scale / ps.x, scale / ps.y, scale / ps.z);
        wornOnHead.gameObject.SetActive(true);
    }

    // Makes the body long and thin (lamp-shaped) while the head keeps its own shape (at disguiseHeadSize)
    void Stretch(Hider h, Transform head)
    {
        stretched = h;
        stretchHome = h.transform.localScale;
        Vector3 before = head != null ? AxisLengths(head) : Vector3.one;
        h.transform.localScale = Vector3.Scale(stretchHome, disguiseStretch);
        stretchedHead = head;
        if (head == null) return;
        stretchedHeadHome = head.localScale;
        Vector3 after = AxisLengths(head);
        float k = disguiseHeadSize;
        head.localScale = new Vector3(head.localScale.x * k * before.x / after.x, head.localScale.y * k * before.y / after.y, head.localScale.z * k * before.z / after.z);
    }

    // How long each of a transform's own axes is in the world (its real size along them)
    static Vector3 AxisLengths(Transform t)
    {
        return new Vector3(t.TransformVector(Vector3.right).magnitude, t.TransformVector(Vector3.up).magnitude, t.TransformVector(Vector3.forward).magnitude);
    }

    // Puts the worn object back where it lives
    void TakeOffHead()
    {
        if (wornOnHead == null || wornHome == null) return;
        wornOnHead.SetParent(wornHome, false);
        wornOnHead.localPosition = wornHomePos;
        wornOnHead.localRotation = wornHomeRot;
        wornOnHead.localScale = wornHomeScale;
        if (hiddenVisual == null || hiddenVisual.transform != wornOnHead) wornOnHead.gameObject.SetActive(false);
    }

    // Freezes the animation and copies the stored skeleton pose onto the character
    void ApplyLyingPose(Hider h)
    {
        if (string.IsNullOrEmpty(lyingPoseData)) return;
        foreach (Animator a in h.GetComponentsInChildren<Animator>()) a.enabled = false;

        foreach (string entry in lyingPoseData.Split(';'))
        {
            string[] kv = entry.Trim().Split('|');
            if (kv.Length != 2) continue;
            Transform bone = FindBone(h, kv[0]);
            string[] q = kv[1].Split(',');
            if (bone == null || q.Length != 4) continue;
            bone.localRotation = new Quaternion(
                float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(q[2], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(q[3], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    static Transform FindBone(Hider h, string suffix)
    {
        foreach (Transform t in h.GetComponentsInChildren<Transform>())
            if (t.name.EndsWith(suffix)) return t;
        return null;
    }

    // Rotates/moves the hider so its skeleton matches the lying pose, whatever the model's own orientation
    void AlignLyingPose(Hider h)
    {
        Transform hips = FindBone(h, "Hips"), head = FindBone(h, "Head");
        Transform ls = FindBone(h, "LeftShoulder"), rs = FindBone(h, "RightShoulder");
        if (hips == null || head == null || ls == null || rs == null) return; // keep the plain layFlatRotation

        Vector3 up = (head.position - hips.position).normalized;
        Vector3 right = (rs.position - ls.position).normalized;
        Vector3 face = Vector3.Cross(right, up);
        if (up.sqrMagnitude < 0.5f || face.sqrMagnitude < 0.0001f) return;

        Quaternion yawFrame = Quaternion.Euler(0f, insidePoint != null ? insidePoint.eulerAngles.y : 0f, 0f);
        Quaternion src = Quaternion.LookRotation(up, face);
        Quaternion dst = Quaternion.LookRotation(yawFrame * LyingHeadDir.normalized, yawFrame * LyingFaceDir.normalized);
        h.transform.rotation = dst * Quaternion.Inverse(src) * h.transform.rotation;

        // Put the hips at the same spot for every character
        Vector3 hipsTarget = (insidePoint != null ? insidePoint.position : h.transform.position) + yawFrame * LyingHips + Vector3.up * lyingHeightOffset;
        h.transform.position += hipsTarget - hips.position;
    }

    public Vector3 GetApproachPoint()
    {
        if (approachPoint != null) return approachPoint.position;
        if (triggers != null && triggers.Length > 0 && triggers[0] != null) return triggers[0].bounds.center;
        return transform.position;
    }

    public bool IsReachedBy(Vector3 pos)
    {
        if (triggers == null) return false;
        foreach (Collider c in triggers)
        {
            if (c == null) continue;
            if ((c.ClosestPoint(pos) - pos).sqrMagnitude <= 0.04f) return true;
        }
        return false;
    }

    void PlaceHideCamera()
    {
        if (cameraAnchor != null)
        {
            hideCamera.transform.SetPositionAndRotation(cameraAnchor.position, cameraAnchor.rotation);
        }
        else
        {
            Vector3 camPos = transform.TransformPoint(cameraOffset);
            Vector3 lookTarget = transform.position + Vector3.up * lookAtHeight;
            hideCamera.transform.position = camPos;
            hideCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos);
        }

        baseRotation = hideCamera.transform.rotation;
        yaw = 0f;
    }

    void HandleHideLook()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float yawInput = 0f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) yawInput += 1f;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) yawInput -= 1f;

        yaw = Mathf.Clamp(yaw + yawInput * lookSpeed * Time.deltaTime, -maxYaw, maxYaw);

        if (hideCamera != null)
            hideCamera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
    }
}