using System.Collections;
using UnityEngine;

// Put on any player (added automatically). When tickled with the feather the player stops, plays the
// laugh animation and sound, and the killer runs to the spot where the laugh came from.
public class LaughEffect : MonoBehaviour
{
    public string laughTriggerName = "Laugh";
    [Tooltip("Seconds after the animation starts until the laugh is heard.")]
    public float soundDelay = 0.15f;
    [Tooltip("Seconds the tickled player stands still laughing.")]
    public float lockDuration = 2.5f;

    Hider hider;
    Animator animator;
    PlayerMovement movement;
    AIHider ai;
    bool laughing;

    public bool IsLaughing { get { return laughing; } }

    void Awake()
    {
        hider = GetComponent<Hider>();
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
        ai = GetComponent<AIHider>();
    }

    public bool Laugh(AudioClip clip, float radius, float volume)
    {
        if (laughing || hider == null || hider.IsCaught) return false;
        StartCoroutine(LaughRoutine(clip, radius, volume));
        return true;
    }

    IEnumerator LaughRoutine(AudioClip clip, float radius, float volume)
    {
        laughing = true;
        bool hiddenNow = hider.IsHidden; // somebody in a hiding spot cannot move or animate, but is still heard
        bool wasExternal = ai != null && ai.externalControl;
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (!hiddenNow)
        {
            if (movement != null) movement.enabled = false;
            if (ai != null)
            {
                ai.externalControl = true;
                if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            }
            if (animator != null && HasParameter(laughTriggerName)) animator.SetTrigger(laughTriggerName);
        }

        yield return new WaitForSeconds(soundDelay);

        if (!hider.IsCaught)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            foreach (EnemyVision e in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None))
            {
                try { e.HearNoise(transform.position, radius, true); }
                catch (System.Exception ex) { Debug.LogWarning(ex.Message); }
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0f, lockDuration - soundDelay));

        if (!hiddenNow && !hider.IsCaught && !hider.IsHidden)
        {
            if (movement != null) movement.enabled = true;
            if (ai != null)
            {
                ai.externalControl = wasExternal;
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            }
        }
        laughing = false;
    }

    bool HasParameter(string n)
    {
        foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == n) return true;
        return false;
    }
}
