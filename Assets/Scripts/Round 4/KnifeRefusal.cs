using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// Night 4's choice (sits on the "Final Hunt (Round 4)" object).
// The player holding the golden knife can press T to throw it away: he refuses to play by the clown's rules.
// The knife vanishes for good, and as punishment a second clown rides up in the elevator and hunts with the first.
//   refusedKnife  he threw the knife away
//   tookKnife     he kept it to the end of the night, or stabbed someone with it
// Only the human player decides; AI holders always keep the knife. It can only happen once per match.
// (Submission-only feature: delete this component to remove it; nothing else depends on it.)
public class KnifeRefusal : MonoBehaviour
{
    [Tooltip("Seconds between throwing the knife away and the elevator bell.")]
    public float arrivalDelay = 3f;
    [Tooltip("Said when the second clown arrives.")]
    public string clownLine = "You won't play by my rules? Then we play by mine.";

    bool hintShown, decided, spawned;
    Hider lastHolder;

    void Update()
    {
        if (!FinalHuntRound.ThisRound || RoundManager.Instance == null) return;
        StoryStateManager story = StoryStateManager.Instance;

        // the night is over: did the player keep the knife, or use it?
        if (RoundManager.Instance.RoundOver)
        {
            if (!decided) { decided = true; if (PlayerUsedKnife()) story.SetFlag("tookKnife"); }
            return;
        }

        Hider holder = KnifeHolder.CurrentHider;
        // tell the player about the choice the moment he grabs the knife
        if (holder != lastHolder)
        {
            lastHolder = holder;
            if (holder != null && holder.isHumanControlled && !hintShown && !story.HasFlag("refusedKnife"))
            {
                hintShown = true;
                RoundManager.Instance.Toast("Click to stab  -  or press T to throw the knife away");
            }
        }

        if (holder == null || !holder.isHumanControlled || holder.IsCaught || PauseMenu.IsOpen || Time.timeScale <= 0f) return;
        if (Keyboard.current == null || !Keyboard.current.tKey.wasPressedThisFrame) return;
        if (story.HasFlag("refusedKnife")) return;

        // the knife vanishes (removing the holder doesn't drop it, unlike dying)
        Destroy(KnifeHolder.Current);
        story.SetFlag("refusedKnife");
        RoundManager.Instance.Toast(holder.displayName + " threw the golden knife away...");
        if (!spawned) { spawned = true; StartCoroutine(SecondClown()); }
    }

    bool PlayerUsedKnife()
    {
        Hider holder = KnifeHolder.CurrentHider;
        if (holder != null && holder.isHumanControlled) return true;
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
            if (h.KilledBy != null && h.KilledBy.isHumanControlled) return true;
        return false;
    }

    // A copy of the clown rides up in the elevator, walks out and joins the hunt.
    IEnumerator SecondClown()
    {
        yield return new WaitForSeconds(arrivalDelay);
        if (RoundManager.Instance == null || RoundManager.Instance.RoundOver) yield break;

        ElevatorIntro lift = FindFirstObjectByType<ElevatorIntro>();
        if (lift == null || lift.enemy == null) yield break;

        // a fresh clown inside the elevator, switched off until he steps out
        GameObject clown = Instantiate(lift.enemy.gameObject, lift.insidePoint, Quaternion.Euler(0f, 180f, 0f));
        clown.name = "Enemy 2";
        NavMeshAgent agent = clown.GetComponent<NavMeshAgent>();
        EnemyWander wander = clown.GetComponent<EnemyWander>();
        EnemyVision vision = clown.GetComponent<EnemyVision>();
        if (agent) agent.enabled = false;
        if (wander) wander.enabled = false;
        if (vision) vision.enabled = false;
        clown.transform.position = lift.insidePoint;
        Animator anim = clown.GetComponent<Animator>();

        // bell, doors open
        if (lift.bell) lift.bell.Play();
        RoundManager.Instance.Toast("\"" + clownLine + "\"");
        Vector3 leftClosed = lift.leftDoor.position, rightClosed = lift.rightDoor.position;
        yield return Doors(lift, leftClosed, rightClosed, true);
        yield return new WaitForSeconds(0.4f);

        // he walks out, the doors close behind him
        bool closing = false;
        while (Vector3.Distance(clown.transform.position, lift.exitPoint) > 0.05f)
        {
            clown.transform.position = Vector3.MoveTowards(clown.transform.position, lift.exitPoint, lift.walkSpeed * Time.deltaTime);
            if (anim) anim.SetFloat("Speed", lift.walkSpeed);
            if (!closing && clown.transform.position.z < lift.closeAtZ) { closing = true; StartCoroutine(Doors(lift, leftClosed, rightClosed, false)); }
            yield return null;
        }

        // hand him to the NavMesh and let him hunt
        NavMeshHit hit;
        Vector3 p = NavMesh.SamplePosition(lift.exitPoint, out hit, 2f, NavMesh.AllAreas) ? hit.position : lift.exitPoint;
        if (agent)
        {
            agent.enabled = true;
            agent.Warp(p);
            clown.transform.position = p + Vector3.up * agent.baseOffset * clown.transform.lossyScale.y;
        }
        if (wander) wander.enabled = true;
        if (vision) vision.enabled = true;
    }

    IEnumerator Doors(ElevatorIntro lift, Vector3 leftClosed, Vector3 rightClosed, bool open)
    {
        float from = open ? 0f : lift.openOffset, to = open ? lift.openOffset : 0f;
        for (float t = 0f; t < lift.openDuration; t += Time.deltaTime)
        {
            float o = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / lift.openDuration));
            lift.leftDoor.position = leftClosed + Vector3.left * o;
            lift.rightDoor.position = rightClosed + Vector3.right * o;
            yield return null;
        }
        lift.leftDoor.position = leftClosed + Vector3.left * to;
        lift.rightDoor.position = rightClosed + Vector3.right * to;
    }
}
