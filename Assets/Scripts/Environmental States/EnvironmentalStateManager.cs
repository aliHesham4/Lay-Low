using System.Collections.Generic;
using UnityEngine;

// Environmental states: in round 2 (only), at least one room malfunctions at a random moment.
// Each room is an EnvironmentalHazard; add more rooms to the list and they join the draw.
public class EnvironmentalStateManager : MonoBehaviour
{
    [Tooltip("The round in which rooms malfunction.")]
    public int activeRound = 2;
    public List<EnvironmentalHazard> hazards = new List<EnvironmentalHazard>();
    [Tooltip("How many rooms malfunction that round (at least 1, at most this many).")]
    public int maxHazardsPerRound = 1;
    [Tooltip("Seconds into the round when the first alert may come (random between the two).")]
    public Vector2 firstAlertTime = new Vector2(12f, 30f);
    [Tooltip("Seconds between two rooms, when more than one goes wrong.")]
    public Vector2 gapBetweenHazards = new Vector2(12f, 20f);

    void Start()
    {
        if (RoundManager.CurrentRound != activeRound) return;
        if (hazards.Count == 0) hazards.AddRange(FindObjectsByType<EnvironmentalHazard>(FindObjectsSortMode.None));
        hazards.RemoveAll(h => h == null);
        if (hazards.Count == 0) return;

        // shuffle, then take 1..max of them
        for (int i = hazards.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = hazards[i]; hazards[i] = hazards[j]; hazards[j] = t; }
        int count = Random.Range(1, Mathf.Clamp(maxHazardsPerRound, 1, hazards.Count) + 1);

        float when = Random.Range(firstAlertTime.x, firstAlertTime.y);
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(BeginAfter(hazards[i], when));
            when += Random.Range(gapBetweenHazards.x, gapBetweenHazards.y);
        }
    }

    System.Collections.IEnumerator BeginAfter(EnvironmentalHazard h, float seconds)
    {
        yield return new WaitForSeconds(seconds); // game time: waits through the round intro too
        if (h != null) h.Begin();
    }
}
