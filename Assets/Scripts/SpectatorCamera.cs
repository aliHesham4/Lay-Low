using UnityEngine;
using Unity.Cinemachine;

// Spectator camera: once your player is caught (after the death animation has played in first person),
// this group camera takes over and keeps every player still in the round in view.
// It hands back to the normal cameras when you are free again (next round).
[RequireComponent(typeof(CinemachineCamera))]
public class SpectatorCamera : MonoBehaviour
{
    [Tooltip("Seconds after being caught before switching to the spectator view.")]
    public float delayAfterCaught = 2.5f;
    public int livePriority = 100;

    CinemachineCamera cam;
    Hider human;
    float caughtSince = -1f;
    bool live;

    void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        SetLive(false);
    }

    void Update()
    {
        if (human == null || !human.isHumanControlled)
        {
            human = null;
            foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
                if (h.isHumanControlled) { human = h; break; }
        }
        bool caught = human != null && human.IsCaught;
        if (!caught) { caughtSince = -1f; if (live) SetLive(false); return; }

        if (caughtSince < 0f) caughtSince = Time.time;
        if (!live && Time.time - caughtSince >= delayAfterCaught) SetLive(true);
    }

    void SetLive(bool on)
    {
        live = on;
        cam.Priority = on ? livePriority : 0;
    }
}
