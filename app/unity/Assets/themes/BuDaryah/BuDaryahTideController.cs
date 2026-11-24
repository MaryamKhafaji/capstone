using UnityEngine;


/// <summary>
/// Controls the timing and reach of Bu Daryah's incoming waves.
/// Each tide moves in, retreats, and pauses briefly before the next one.
/// The wave layers read this controller to move together naturally.
/// </summary>
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

    /// <summary>
    /// Begins a fresh tide cycle when the theme is enabled.
    /// </summary>
    private void OnEnable()
    {
        StartNextCycle(Time.time);
    }

    /// <summary>
    /// Starts the next tide after the current wave and pause finish.
    /// </summary>
    private void Update()
    {
        if (!initialized) StartNextCycle(Time.time);
        if (Time.time - startTime >= cycleDuration + pauseDuration)
            StartNextCycle(Time.time);
    }

    /// <summary>
    /// Chooses the next wave's reach, duration, and pause.
    /// </summary>
    private void StartNextCycle(float atTime)
    {
        startTime = atTime;
        // Vary each wave slightly so the motion does not repeat exactly.
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


    /// <summary>
    /// Returns how far the tide has advanced, from 0 to 1.
    /// Each layer can use a small delay so the waves do not overlap exactly.
    /// </summary>
    public float GetAdvance01(float layerDelaySeconds = 0f)
    {
        if (!initialized) return 0f;
        // Account for the small timing difference between the wave layers.
        float elapsed = Time.time - startTime - Mathf.Max(0f, layerDelaySeconds);
        if (elapsed <= 0f || elapsed >= cycleDuration) return 0f;

        float t = elapsed / cycleDuration;
        float reach;
        // The wave approaches first, then retreats for the rest of the cycle.
        if (t < incomingPart)
            reach = Smooth01(t / Mathf.Max(0.01f, incomingPart));
        else
            reach = 1f - Smooth01((t - incomingPart) / Mathf.Max(0.01f, 1f - incomingPart));
        return Mathf.Clamp01(reach);
    }

    /// <summary>
    /// Smooths the movement so each wave accelerates and slows gently.
    /// </summary>
    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Keeps minimum and maximum Inspector values within valid ranges.
    /// </summary>
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
