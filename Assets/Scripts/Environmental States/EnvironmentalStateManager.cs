using System.Collections.Generic;
using UnityEngine;

// Environmental states: in round 2 (only), between min and max rooms malfunction at random moments.
// Each room is an EnvironmentalHazard; add more rooms to the list and they join the draw.
public class EnvironmentalStateManager : MonoBehaviour
{
    [Tooltip("The round in which rooms malfunction.")]
    public int activeRound = 2;
    public List<EnvironmentalHazard> hazards = new List<EnvironmentalHazard>();
    [Tooltip("Fewest rooms that malfunction that round.")]
    public int minHazardsPerRound = 2;
    [Tooltip("Most rooms that malfunction that round (a random number between min and max).")]
    public int maxHazardsPerRound = 3;
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
        int most = Mathf.Clamp(maxHazardsPerRound, 1, hazards.Count);
        int least = Mathf.Clamp(minHazardsPerRound, 1, most);
        int count = Random.Range(least, most + 1);

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
