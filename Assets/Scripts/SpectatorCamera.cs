using UnityEngine;
using Unity.Cinemachine;

// Camera help while you play:
//  - After your player is caught (once the death animation has played): this group camera keeps
//    every player still in the round in view.
//  - Aiming a stone (T) or a door lock (E): the whole-house overview camera, and you can't walk
//    until you throw / lock or cancel.
// Throwing / locking / cancelling hands straight back to your normal camera and lets you walk again.
[RequireComponent(typeof(CinemachineCamera))]
public class SpectatorCamera : MonoBehaviour
{
    [Tooltip("Seconds after being caught before switching to the spectator view.")]
    public float delayAfterCaught = 2.5f;
    public int livePriority = 100;
    [Tooltip("Fixed camera that shows the whole house; used while you pick a doorway to lock.")]
    public CinemachineCamera houseOverviewCamera;

    CinemachineCamera cam;
    Hider human;
    float caughtSince = -1f;
    bool live, overviewLive;

    PlayerStoneThrower stone;
    DoorLockHolder doorLock;
    PlayerMovement movement;
    bool movementLocked;

    void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        SetLive(false);
        SetOverview(false);
    }

    void Update()
    {
        if (human == null || !human.isHumanControlled)
        {
            human = null;
            foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
                if (h.isHumanControlled) { human = h; break; }
            stone = null; doorLock = null; movement = null;
        }
        if (human != null)
        {
            if (stone == null) stone = human.GetComponent<PlayerStoneThrower>();
            if (doorLock == null) doorLock = human.GetComponent<DoorLockHolder>();
            if (movement == null) movement = human.GetComponent<PlayerMovement>();
        }

        bool caught = human != null && human.IsCaught;
        if (!caught) caughtSince = -1f;
        else if (caughtSince < 0f) caughtSince = Time.time;

        bool aimingStone = !caught && stone != null && stone.enabled && stone.IsTargeting;
        bool aimingLock = !caught && doorLock != null && doorLock.IsAiming;

        bool wantSpectator = caught && Time.time - caughtSince >= delayAfterCaught;
        bool wantOverview = aimingStone || aimingLock;
        if (wantSpectator != live) SetLive(wantSpectator);
        if (wantOverview != overviewLive) SetOverview(wantOverview);

        LockMovement(aimingStone || aimingLock);
    }

    // no walking while you choose a target
    void LockMovement(bool lockIt)
    {
        if (movement == null) { movementLocked = false; return; }
        if (lockIt && !movementLocked)
        {
            if (!movement.enabled) return; // already off (hiding etc.): nothing to do
            movement.enabled = false;
            movementLocked = true;
            Animator a = human.GetComponent<Animator>();
            if (a != null) a.SetFloat("Speed", 0f); // stand still instead of running on the spot
        }
        else if (!lockIt && movementLocked)
        {
            movementLocked = false;
            if (!human.IsHidden && !human.IsCaught) movement.enabled = true;
        }
    }

    void SetLive(bool on)
    {
        live = on;
        cam.Priority = on ? livePriority : 0;
    }

    void SetOverview(bool on)
    {
        overviewLive = on;
        if (houseOverviewCamera != null) houseOverviewCamera.Priority = on ? livePriority + 10 : 0;
    }

    void OnDisable()
    {
        LockMovement(false);
    }
}
