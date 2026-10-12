using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class FluorescentFlicker : MonoBehaviour
{
    private Light tubeLight;

    [Header("Rapid Flicker Speeds")]
    public float minFlickerSpeed = 0.05f;
    public float maxFlickerSpeed = 0.15f;

    [Header("Occasional Pauses (Light Stays On)")]
    public float minPauseTime = 0.5f;
    public float maxPauseTime = 2.5f;
    [Tooltip("Percentage chance the light will stop flickering and function normally for a bit.")]
    [Range(0, 100)] public int pauseChance = 20;

    void Start()
    {
        tubeLight = GetComponent<Light>();
        StartCoroutine(FlickerRoutine());
    }

    IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // Random chance to stop flickering and stay ON for a few seconds
            if (Random.Range(0, 100) < pauseChance)
            {
                tubeLight.enabled = true;
                yield return new WaitForSeconds(Random.Range(minPauseTime, maxPauseTime));
            }
            else
            {
                // Rapid on/off toggle
                tubeLight.enabled = !tubeLight.enabled;
                yield return new WaitForSeconds(Random.Range(minFlickerSpeed, maxFlickerSpeed));
            }
        }
    }
}