using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Killer starts hidden inside the elevator. Bell rings, doors slide open, he walks out,
// doors close, then he searches normally.
public class ElevatorIntro : MonoBehaviour
{
    public Transform leftDoor;
    public Transform rightDoor;
    public AudioSource bell;
    public Transform enemy;
    public float openOffset = 0.31f;
    public float startDelay = 2.5f;
    public float openDuration = 1.2f;
    public float walkSpeed = 1.5f;
    public Vector3 insidePoint = new Vector3(7.24f, 0.06f, 10.95f);
    public Vector3 exitPoint = new Vector3(7.24f, 0.06f, 9.6f);
    public float closeAtZ = 10.1f;

    Vector3 leftClosed, rightClosed;
    bool closing;

    void Awake()
    {
        leftClosed = leftDoor.position;
        rightClosed = rightDoor.position;
        enemy.position = insidePoint;
        enemy.rotation = Quaternion.Euler(0, 180f, 0);
        SetEnemyActive(false);
    }

    void SetEnemyActive(bool on)
    {
        var w = enemy.GetComponent<EnemyWander>(); if (w) w.enabled = on;
        var v = enemy.GetComponent<EnemyVision>(); if (v) v.enabled = on;
        var a = enemy.GetComponent<NavMeshAgent>(); if (a) a.enabled = on;
    }

    void Start() { StartCoroutine(Run()); }

    IEnumerator Slide(bool open, float dur)
    {
        float t = 0;
        float from = open ? 0f : openOffset, to = open ? openOffset : 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetDoors(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur)));
            yield return null;
        }
        SetDoors(to);
    }

    void SetDoors(float o)
    {
        leftDoor.position = leftClosed + Vector3.left * o;
        rightDoor.position = rightClosed + Vector3.right * o;
    }

    IEnumerator Run()
    {
        var anim = enemy.GetComponent<Animator>();
        yield return new WaitForSeconds(startDelay);
        if (bell) bell.Play();
        yield return Slide(true, openDuration);
        yield return new WaitForSeconds(0.6f);

        while (Vector3.Distance(enemy.position, exitPoint) > 0.05f)
        {
            enemy.position = Vector3.MoveTowards(enemy.position, exitPoint, walkSpeed * Time.deltaTime);
            if (anim) anim.SetFloat("Speed", walkSpeed);
            if (!closing && enemy.position.z < closeAtZ) { closing = true; StartCoroutine(Slide(false, openDuration)); }
            yield return null;
        }
        if (anim) anim.SetFloat("Speed", 0f);

        NavMeshHit hit;
        Vector3 p = NavMesh.SamplePosition(exitPoint, out hit, 2f, NavMesh.AllAreas) ? hit.position : exitPoint;
        enemy.position = p;
        SetEnemyActive(true);
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent) agent.Warp(p);
        // He goes back to normal: wandering and searching, and chases only when he spots someone.
    }
}
