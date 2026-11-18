using UnityEngine;

// Put this on BuDaryahTheme. It drives ALL three wave layers as one tide.
// Every incoming wave has its own reach, duration, and short calm pause.
// The shore stays completely stationary.
[AddComponentMenu("MasAR+/Bu Daryah/Natural Tide Controller")]
public class BuDaryahTideController : MonoBehaviour
{
    [Header("How far different waves reach")]
    [Min(0.1f)] public float minimumReach = 0.60f;
    [Min(0.1f)] public float maximumReach = 1.35f;

    [Header("Wave timing (seconds)")]
    [Min(0.5f)] public float minimumCycle = 4.4f;
    [Min(0.5f)] public float maximumCycle = 7.2f;
    [Range(0.2f, 0.8f)] public float incomingPart = 0.43f;

    [Header("Quiet pause between waves (seconds)")]
    [Min(0f)] public float minimumPause = 0.45f;
    [Min(0f)] public float maximumPause = 1.20f;

    public float ReachMultiplier { get; private set; } = 1f;

    private float startTime;
    private float cycleDuration;
    private float pauseDuration;
    private bool initialized;

    private void OnEnable()
    {
        StartNextCycle(Time.time);
    }

    private void Update()
    {
        if (!initialized) StartNextCycle(Time.time);
        if (Time.time - startTime >= cycleDuration + pauseDuration)
            StartNextCycle(Time.time);
    }

    private void StartNextCycle(float atTime)
    {
        startTime = atTime;
        ReachMultiplier = Random.Range(
            Mathf.Max(0.1f, minimumReach),
            Mathf.Max(minimumReach, maximumReach));
        cycleDuration = Random.Range(
            Mathf.Max(0.5f, minimumCycle),
            Mathf.Max(minimumCycle, maximumCycle));
        pauseDuration = Random.Range(
            Mathf.Max(0f, minimumPause),
            Mathf.Max(minimumPause, maximumPause));
        initialized = true;
    }

    // A small optional layer delay lets the highlight, middle, and back layers
    // follow the SAME wave without sitting perfectly on top of one another.
    public float GetAdvance01(float layerDelaySeconds = 0f)
    {
        if (!initialized) return 0f;
        float elapsed = Time.time - startTime - Mathf.Max(0f, layerDelaySeconds);
        if (elapsed <= 0f || elapsed >= cycleDuration) return 0f;

        float t = elapsed / cycleDuration;
        float reach;
        if (t < incomingPart)
            reach = Smooth01(t / Mathf.Max(0.01f, incomingPart));
        else
            reach = 1f - Smooth01((t - incomingPart) / Mathf.Max(0.01f, 1f - incomingPart));
        return Mathf.Clamp01(reach);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void OnValidate()
    {
        minimumReach = Mathf.Max(0.1f, minimumReach);
        maximumReach = Mathf.Max(minimumReach, maximumReach);
        minimumCycle = Mathf.Max(0.5f, minimumCycle);
        maximumCycle = Mathf.Max(minimumCycle, maximumCycle);
        minimumPause = Mathf.Max(0f, minimumPause);
        maximumPause = Mathf.Max(minimumPause, maximumPause);
    }
}
