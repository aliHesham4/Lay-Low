using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// Night 1's opening (lives in the Night 1 scene): once the round card is gone, the camera cuts to the
// elevator, the clown walks out and greets the family (a DialogueData conversation), then play begins.
// While it plays the player can't move and the clown doesn't hunt yet. Escape is the pause menu, so
// Space / Enter / click move through the lines, and holding Space for half a second skips the whole thing.
// Sets "metClown" (from the dialogue asset) when it ends.
public class ClownIntroCutscene : MonoBehaviour
{
    public DialogueData greeting;
    [Tooltip("Where the cutscene camera stands, and the point it looks at (in front of the elevator).")]
    public Vector3 cameraPosition = new Vector3(7.24f, 0.95f, 8.25f);
    public Vector3 lookAt = new Vector3(7.24f, 0.72f, 10.1f);
    public float fieldOfView = 45f;
    [Tooltip("Seconds each line stays before moving on by itself.")]
    public float autoAdvance = 2.4f;

    void Start() { StartCoroutine(Run()); }

    IEnumerator Run()
    {
        // wait for the house under this night to load and the round card to finish
        while (RoundManager.Instance == null || !RoundManager.Instance.IntroDone) yield return null;
        ElevatorIntro lift = FindFirstObjectByType<ElevatorIntro>();
        if (lift == null || lift.enemy == null || greeting == null) yield break;

        // the cutscene camera wins over every gameplay camera while it is on
        GameObject camGO = new GameObject("CM_ClownIntro");
        camGO.transform.position = cameraPosition;
        camGO.transform.rotation = Quaternion.LookRotation(lookAt - cameraPosition);
        CinemachineCamera cam = camGO.AddComponent<CinemachineCamera>();
        cam.Priority.Value = 200;
        LensSettings lens = cam.Lens; lens.FieldOfView = fieldOfView; cam.Lens = lens;

        // freeze the player
        Hider human = null;
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None)) if (h.isHumanControlled) human = h;
        PlayerMovement move = human != null ? human.GetComponent<PlayerMovement>() : null;
        if (move != null) move.enabled = false;
        // the round clock waits for the greeting
        RoundManager.Instance.ClockHeld = true;

        // the clown talks once the elevator doors start to open
        yield return new WaitForSeconds(lift.startDelay);
        bool done = false;
        DialogueManager.Instance.Play(greeting, () => done = true, autoAdvance, true);

        float held = 0f;
        Transform clown = lift.enemy;
        EnemyVision vision = clown.GetComponent<EnemyVision>();
        EnemyWander wander = clown.GetComponent<EnemyWander>();
        NavMeshAgent agent = clown.GetComponent<NavMeshAgent>();
        Animator anim = clown.GetComponent<Animator>();
        while (!done)
        {
            // once out of the elevator he stands there idling until the greeting is over
            if (vision != null && vision.enabled) { vision.enabled = false; if (wander) wander.enabled = false; }
            if (vision != null && agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                if (anim) anim.SetFloat("Speed", 0f);
            }
            Keyboard k = Keyboard.current;
            held = k != null && k.spaceKey.isPressed ? held + Time.unscaledDeltaTime : 0f;
            if (held > 0.5f) DialogueManager.Instance.SkipAll();
            yield return null;
        }

        // back to the game: the clock starts now
        Destroy(camGO);
        if (RoundManager.Instance != null) RoundManager.Instance.ClockHeld = false;
        if (move != null && !human.IsCaught && !human.IsHidden) move.enabled = true;
        // (if he hasn't finished walking out yet, ElevatorIntro switches him on itself)
        if (vision != null && !vision.enabled && agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = false;
            vision.enabled = true;
            if (wander) wander.enabled = true;
        }
    }
}
