using UnityEngine;

// When the player is caught, the first-person camera rides on the head bone so you see the
// death animation from your own eyes (falling, tilting, hitting the floor).
// It moves the FPV camera's target (PlayerHeadTarget); the camera itself is locked to that target.
[RequireComponent(typeof(Hider))]
public class FPVDeathCamera : MonoBehaviour
{
    [Tooltip("The FPV camera's tracking target (PlayerHeadTarget).")]
    public Transform headTarget;
    [Tooltip("Animator of the visible body (the one that plays Die).")]
    public Animator bodyAnimator;
    [Tooltip("How quickly the camera catches up with the head (higher = stiffer).")]
    public float followSharpness = 18f;
    [Tooltip("0 = keep the horizon level, 1 = roll fully with the head.")]
    [Range(0f, 1f)] public float rollAmount = 0.6f;

    Hider hider;
    Transform head;
    Vector3 posInHead;       // camera target position in head-bone space, taken when caught
    Quaternion rotInHead;    // camera target rotation in head-bone space
    Vector3 startLocalPos;
    Quaternion startLocalRot;
    bool following;

    void Awake()
    {
        hider = GetComponent<Hider>();
        if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
        if (headTarget == null) headTarget = transform.Find("PlayerHeadTarget");
        if (headTarget != null) { startLocalPos = headTarget.localPosition; startLocalRot = headTarget.localRotation; }
    }

    void LateUpdate()
    {
        if (headTarget == null || bodyAnimator == null || hider == null) return;

        if (hider.IsCaught && !following) Begin();
        else if (!hider.IsCaught && following) End();
        if (!following || head == null) return;

        Vector3 wantPos = head.TransformPoint(posInHead);
        Quaternion wantRot = head.rotation * rotInHead;

        // keep some of the roll out so it does not get nauseating
        Vector3 fwd = wantRot * Vector3.forward;
        Quaternion level = Quaternion.LookRotation(fwd, Vector3.up);
        wantRot = Quaternion.Slerp(level, wantRot, rollAmount);

        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        headTarget.position = Vector3.Lerp(headTarget.position, wantPos, k);
        headTarget.rotation = Quaternion.Slerp(headTarget.rotation, wantRot, k);
    }

    void Begin()
    {
        head = bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;
        // remember where the camera sits relative to the head right now, so the view starts where it was
        posInHead = head.InverseTransformPoint(headTarget.position);
        rotInHead = Quaternion.Inverse(head.rotation) * headTarget.rotation;
        following = true;
    }

    void End()
    {
        following = false;
        headTarget.localPosition = startLocalPos;
        headTarget.localRotation = startLocalRot;
    }
}
