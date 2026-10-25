using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Carried by a player (human or AI) who has picked up the party whistle.
// Blowing it plays the horn at his/her position; the killer walks to where the sound came from.
public class WhistleHolder : MonoBehaviour
{
    [Header("Use")]
    public Key useKey = Key.Q;
    public string blowTriggerName = "Blow";
    [Tooltip("Seconds after the animation starts until the horn actually sounds.")]
    public float soundDelay = 0.8f;
    [Tooltip("Seconds the player stands still while blowing.")]
    public float lockDuration = 1.6f;

    [HideInInspector] public Sprite icon;
    [HideInInspector] public AudioClip hornSound;
    [HideInInspector] public float hearingRadius = 40f;
    [HideInInspector] public float hornVolume = 1f;

    Hider hider;
    Animator animator;
    PlayerHUD hud;
    PlayerMovement movement;
    AIHider ai;
    bool has;
    bool blowing;

    public bool HasWhistle { get { return has; } }
    public bool IsBlowing { get { return blowing; } }

    void Awake()
    {
        hider = GetComponent<Hider>();
        animator = GetComponent<Animator>();
        hud = GetComponent<PlayerHUD>();
        movement = GetComponent<PlayerMovement>();
        ai = GetComponent<AIHider>();
    }

    // Called by the whistle pickup. Returns false if the bag is full or she already has one.
    public bool Collect(Sprite iconSprite, AudioClip clip, float radius, float volume)
    {
        if (has || blowing) return false;
        if (hud != null && !hud.AddItem("whistle", iconSprite)) return false;
        icon = iconSprite; hornSound = clip; hearingRadius = radius; hornVolume = volume;
        has = true;
        return true;
    }

    void Update()
    {
        if (!has || blowing || hider == null || hider.IsCaught) return;
        bool human = hider.isHumanControlled;
        if (human && !hider.IsHidden && Keyboard.current != null && Keyboard.current[useKey].wasPressedThisFrame)
            Blow();
    }

    public bool CanBlow { get { return has && !blowing && hider != null && !hider.IsCaught && !hider.IsHidden; } }

    public void Blow()
    {
        if (!CanBlow) return;
        StartCoroutine(BlowRoutine());
    }

    IEnumerator BlowRoutine()
    {
        blowing = true;
        bool wasExternal = ai != null && ai.externalControl;
        if (movement != null) movement.enabled = false;
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (ai != null) { ai.externalControl = true; if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; } }

        if (animator != null && HasParameter(blowTriggerName)) animator.SetTrigger(blowTriggerName);

        yield return new WaitForSeconds(soundDelay);

        if (!hider.IsCaught)
        {
            if (hornSound != null) AudioSource.PlayClipAtPoint(hornSound, transform.position, hornVolume);
            foreach (EnemyVision e in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None))
                { try { e.HearNoise(transform.position, hearingRadius); } catch (System.Exception ex) { Debug.LogWarning(ex.Message); } }
        }
        has = false;
        if (hud != null) hud.RemoveItem("whistle");

        yield return new WaitForSeconds(Mathf.Max(0f, lockDuration - soundDelay));

        if (movement != null) movement.enabled = true;
        if (ai != null)
        {
            ai.externalControl = wasExternal;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }
        blowing = false;
    }

    bool HasParameter(string n)
    {
        foreach (AnimatorControllerParameter p in animator.parameters) if (p.name == n) return true;
        return false;
    }
}
