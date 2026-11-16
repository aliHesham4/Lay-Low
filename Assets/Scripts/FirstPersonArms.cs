using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// First-person "viewmodel" hands for Player 1.
// The character is chibi (huge head, short arms), so its real hands can never reach in front of the FPV camera.
// Instead, a copy of the character's own model is drawn in front of the camera, by an overlay camera so it
// never clips into walls. Only its arms are kept (head and legs are shrunk away). It plays exactly the same
// animation state as the real body (idle, run, throw, horn, laugh) and, while idle or running, IK holds the
// hands up in a nervous "ready" pose so they show at the bottom of the screen.
public class FirstPersonArms : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator that drives the real body (on Player1).")]
    public Animator sourceAnimator;
    [Tooltip("Model to copy for the arms (the character mesh + skeleton).")]
    public Transform modelToCopy;
    [Tooltip("Controller for the copy: same as the player's but with IK Pass enabled.")]
    public RuntimeAnimatorController armsController;
    public CameraSwitcher cameraSwitcher;

    [Header("Placement (camera space, metres)")]
    [Tooltip("Where the copy's feet/root sit relative to the camera.")]
    public Vector3 bodyOffset = new Vector3(0f, -0.95f, -0.05f);
    [Tooltip("Size of the arms copy, relative to the real model.")]
    public float scale = 1.6f;
    [Tooltip("Ready-pose hand positions relative to the camera.")]
    public Vector3 leftHand = new Vector3(-0.16f, -0.2f, 0.32f);
    public Vector3 rightHand = new Vector3(0.16f, -0.2f, 0.32f);
    [Tooltip("Tilt of the hands in the ready pose (degrees, camera space).")]
    public Vector3 leftHandEuler = new Vector3(0f, 0f, 90f);
    public Vector3 rightHandEuler = new Vector3(0f, 0f, -90f);
    [Range(0f, 1f)] public float handRotationWeight = 0f;

    [Header("Animation")]
    [Tooltip("States where the hands keep the ready pose. Other states (throw, horn, laugh) play freely.")]
    public string[] readyPoseStates = { "Idle", "Run" };
    public float poseBlendSpeed = 6f;
    [Header("Running arm swing")]
    [Tooltip("Swings per second (radians). Higher = faster pumping.")]
    public float runSwingSpeed = 10f;
    [Tooltip("How far forward/back each hand swings (metres).")]
    public float runSwingForward = 0.07f;
    [Tooltip("How much a hand rises when it swings forward.")]
    public float runSwingUp = 0.05f;
    [Tooltip("How much a hand drops when it swings back (enough to dip out of view).")]
    public float runSwingDown = 0.09f;
    [Tooltip("Hands sit a bit lower while running.")]
    public Vector3 runLower = new Vector3(0f, -0.03f, -0.02f);
    [Tooltip("Hands move a bit toward the centre while running.")]
    public float runInward = 0.02f;
    [Tooltip("Lines the run clip up with the swing (fraction of the clip). The run clip has the left arm furthest forward at 0.67.")]
    [Range(0f, 1f)] public float runClipOffset = 0.42f;

    [Header("Rendering")]
    public string armsLayerName = "FPVArms";
    [Tooltip("Brightness and colour of the light on the hands.")]
    public float handLightIntensity = 0.45f;
    public Color handLightColor = new Color(1f, 0.8f, 0.62f);

    Hider hider;
    Transform cam;
    Transform view, rig;      // fixed off-map rig: 'view' is the arms camera, hand positions are relative to it
    CinemachineBrain brain;
    Camera overlayCam;
    GameObject arms;
    Animator armsAnimator;
    FirstPersonArmsIK ik;
    int armsLayer = -1;
    float poseWeight;
    float runWeight, runPhase;

    Vector3 baseScale = Vector3.one;


    void Start()
    {
        hider = GetComponent<Hider>();
        if (sourceAnimator == null) sourceAnimator = GetComponent<Animator>();
        if (cameraSwitcher == null) cameraSwitcher = GetComponent<CameraSwitcher>();
        armsLayer = LayerMask.NameToLayer(armsLayerName);
        if (armsLayer < 0) { Debug.LogWarning("FirstPersonArms: layer '" + armsLayerName + "' is missing."); enabled = false; return; }
        if (modelToCopy == null || sourceAnimator == null) { enabled = false; return; }
        Build();
    }

    void Build()
    {
        Camera main = Camera.main;
        if (main == null) { enabled = false; return; }
        cam = main.transform;

        // main camera must not draw the arms; an overlay camera draws them on top of everything
        main.cullingMask &= ~(1 << armsLayer);
        // The arms and their camera live in a fixed spot far below the map instead of riding on the main camera.
        // Nothing there ever moves, so turning the player cannot drag or smear the hands across the view.
        brain = main.GetComponent<CinemachineBrain>();
        rig = new GameObject("FPV Arms Rig").transform;
        rig.position = new Vector3(0f, -500f, 0f);
        var go = new GameObject("FPVArmsCamera");
        go.transform.SetParent(rig, false);
        view = go.transform;
        overlayCam = go.AddComponent<Camera>();
        overlayCam.clearFlags = CameraClearFlags.Depth;
        overlayCam.cullingMask = 1 << armsLayer;
        overlayCam.nearClipPlane = 0.01f;
        overlayCam.farClipPlane = 5f;
        overlayCam.fieldOfView = main.fieldOfView;
        var od = overlayCam.GetUniversalAdditionalCameraData();
        od.renderType = CameraRenderType.Overlay;
        od.renderShadows = false;
        var md = main.GetUniversalAdditionalCameraData();
        if (!md.cameraStack.Contains(overlayCam)) md.cameraStack.Add(overlayCam);

        // the room lights do not reach the rig, so the hands get their own soft warm light
        var lightGo = new GameObject("FPV Arms Light");
        lightGo.transform.SetParent(rig, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.25f, 0.05f);
        var armsLight = lightGo.AddComponent<Light>();
        armsLight.type = LightType.Point;
        armsLight.range = 3f;
        armsLight.intensity = handLightIntensity;
        armsLight.color = handLightColor;
        armsLight.shadows = LightShadows.None;

        // the copy
        arms = Instantiate(modelToCopy.gameObject, rig);
        arms.name = "FPV Arms";
        baseScale = modelToCopy.localScale; // the model itself is scaled inside the player (e.g. 70 x 60 x 70)
        arms.transform.localScale = baseScale * scale;
        foreach (Transform t in arms.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = armsLayer;
        foreach (var r in arms.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
        }
        // nothing on the copy should run gameplay code or collide
        foreach (var c in arms.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var mb in arms.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);

        armsAnimator = arms.GetComponent<Animator>();
        if (armsAnimator == null) armsAnimator = arms.AddComponent<Animator>();
        armsAnimator.avatar = sourceAnimator.avatar;
        armsAnimator.runtimeAnimatorController = armsController != null ? armsController : sourceAnimator.runtimeAnimatorController;
        armsAnimator.applyRootMotion = false;
        armsAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        armsAnimator.Rebind();

        ik = arms.AddComponent<FirstPersonArmsIK>();
        ik.owner = this;
    }

    void Update()
    {
        if (arms == null) return;

        bool inFpv = cameraSwitcher != null && cameraSwitcher.IsFirstPerson;
        // only once the camera has fully arrived in first person (not while it is still blending in or out)
        if (inFpv && brain != null)
            inFpv = !brain.IsBlending && (brain.ActiveVirtualCamera as CinemachineCamera) == cameraSwitcher.fpvCamera;
        bool show = inFpv && hider != null && !hider.IsHidden && !hider.IsCaught;
        if (arms.activeSelf != show) arms.SetActive(show);
        if (overlayCam != null) overlayCam.fieldOfView = cam.GetComponent<Camera>().fieldOfView;
        if (!show) return;

        // play exactly what the real body plays
        AnimatorStateInfo st = sourceAnimator.IsInTransition(0) ? sourceAnimator.GetNextAnimatorStateInfo(0) : sourceAnimator.GetCurrentAnimatorStateInfo(0);
        // while running, the run clip is driven by the swing's own phase instead of its own clock,
        // so the clip's arm motion (wrists, elbows, shoulders) and the scripted up/down swing land together on every stroke
        if (st.IsName("Run")) armsAnimator.Play(st.fullPathHash, 0, Mathf.Repeat(runPhase / (2f * Mathf.PI) + runClipOffset, 1f));
        else armsAnimator.Play(st.fullPathHash, 0, st.normalizedTime);
        foreach (var p in sourceAnimator.parameters)
            if (p.type == AnimatorControllerParameterType.Float) armsAnimator.SetFloat(p.nameHash, sourceAnimator.GetFloat(p.nameHash));

        bool ready = false;
        foreach (var n in readyPoseStates) if (st.IsName(n)) ready = true;
        poseWeight = Mathf.MoveTowards(poseWeight, ready ? 1f : 0f, poseBlendSpeed * Time.deltaTime);

        arms.transform.localPosition = bodyOffset;
        arms.transform.localRotation = Quaternion.identity;
        arms.transform.localScale = baseScale * scale;
    }

    // called by FirstPersonArmsIK during the copy's IK pass
    public void ApplyIK(Animator a)
    {
        if (view == null) return;
        // running: arms pump in opposition (one forward and up while the other goes back and down)
        float speed = sourceAnimator.GetFloat("Speed");
        runWeight = Mathf.MoveTowards(runWeight, speed > 0.1f ? 1f : 0f, Time.deltaTime * 5f);
        runPhase += Time.deltaTime * runSwingSpeed; // constant tempo, so every stroke takes the same time
        float s = Mathf.Sin(runPhase);

        Vector3 l = leftHand + runWeight * (runLower + SwingOffset(s));
        Vector3 r = rightHand + runWeight * (runLower + SwingOffset(-s));
        l.x += runWeight * runInward; r.x -= runWeight * runInward;

        SetHand(a, AvatarIKGoal.LeftHand, l, leftHandEuler);
        SetHand(a, AvatarIKGoal.RightHand, r, rightHandEuler);
    }

    // s = +1: hand swung forward (closer to centre of view and higher); s = -1: swung back (down, out of view)
    Vector3 SwingOffset(float s)
    {
        // one smooth curve (no kink in the middle of the stroke): +1 -> runSwingUp, -1 -> -runSwingDown
        float up = s * (runSwingUp + runSwingDown) * 0.5f + (runSwingUp - runSwingDown) * 0.5f;
        return new Vector3(0f, up, s * runSwingForward);
    }

    void SetHand(Animator a, AvatarIKGoal goal, Vector3 camLocal, Vector3 euler)
    {
        a.SetIKPositionWeight(goal, poseWeight);
        a.SetIKRotationWeight(goal, poseWeight * handRotationWeight);
        a.SetIKPosition(goal, view.TransformPoint(camLocal));
        a.SetIKRotation(goal, view.rotation * Quaternion.Euler(euler));
    }

    // hide everything but the arms (after animation + IK)
    const float Tiny = 0.001f;
    Transform spineBone, leftShoulder, rightShoulder;
    Transform[] chain;          // bones between the spine and the shoulders (chest, upper chest)
    Vector3[] chainPos;
    Vector3 leftShoulderPos, rightShoulderPos;
    bool cached, usable;

    public void HideBody(Animator a)
    {
        // legs and head away
        Shrink(a, HumanBodyBones.LeftUpperLeg);
        Shrink(a, HumanBodyBones.RightUpperLeg);

        // whole torso + neck + head away (shrink the spine), but keep the shoulders/arms full size and in place
        if (!cached)
        {
            cached = true;
            spineBone = a.GetBoneTransform(HumanBodyBones.Spine);
            leftShoulder = a.GetBoneTransform(HumanBodyBones.LeftShoulder) ?? a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightShoulder = a.GetBoneTransform(HumanBodyBones.RightShoulder) ?? a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (spineBone != null && leftShoulder != null && rightShoulder != null && leftShoulder.parent == rightShoulder.parent && leftShoulder.IsChildOf(spineBone))
            {
                var list = new System.Collections.Generic.List<Transform>();
                for (Transform t = leftShoulder.parent; t != null && t != spineBone; t = t.parent) list.Insert(0, t);
                chain = list.ToArray();
                chainPos = new Vector3[chain.Length];
                for (int i = 0; i < chain.Length; i++) chainPos[i] = chain[i].localPosition;
                leftShoulderPos = leftShoulder.localPosition;
                rightShoulderPos = rightShoulder.localPosition;
                usable = true;
            }
        }
        if (!usable) { Shrink(a, HumanBodyBones.Head); Shrink(a, HumanBodyBones.Neck); return; }

        // every bone under the spine inherits the tiny scale, so its offset from its parent is divided back out
        spineBone.localScale = Vector3.one * Tiny;
        for (int i = 0; i < chain.Length; i++) chain[i].localPosition = chainPos[i] / Tiny;
        leftShoulder.localPosition = leftShoulderPos / Tiny;
        rightShoulder.localPosition = rightShoulderPos / Tiny;
        leftShoulder.localScale = Vector3.one / Tiny;
        rightShoulder.localScale = Vector3.one / Tiny;
    }

    static void Shrink(Animator a, HumanBodyBones b)
    {
        Transform t = a.GetBoneTransform(b);
        if (t != null) t.localScale = Vector3.one * 0.001f;
    }

    void OnDestroy()
    {
        if (overlayCam != null && cam != null)
        {
            var main = cam.GetComponent<Camera>();
            if (main != null)
            {
                var md = main.GetUniversalAdditionalCameraData();
                if (md != null) md.cameraStack.Remove(overlayCam);
            }
        }
        if (rig != null) Destroy(rig.gameObject);
    }
}
