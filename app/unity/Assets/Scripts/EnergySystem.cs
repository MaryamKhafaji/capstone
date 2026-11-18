using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class EnergySystem : MonoBehaviour
{
    [Header("References")]
    public Slider energyBar;
    public TMP_Text energyText;

    [FormerlySerializedAs("debugDistanceText")]
    public TMP_Text huntStepsText;

    [Header("Energy Settings")]
    public float currentEnergy = 1f;
    public float maximumEnergy = 100f;

    [Tooltip("How many real footwear steps generate one energy point.")]
    [Min(0.01f)]
    [FormerlySerializedAs("metersPerEnergy")]
    public float stepsPerEnergy = 10f;

    // =====================================================
    // DISTANCE-BASED STEP TESTING
    // =====================================================

    [Header("Distance Step Testing")]

    [Tooltip(
        "Temporary testing mode. " +
        "When enabled, walking distance is converted into steps."
    )]
    public bool simulateStepsFromDistance = true;

    [Tooltip(
        "Transform whose movement should be tracked. " +
        "Leave empty to automatically use the main camera."
    )]
    public Transform trackedTransform;

    [Tooltip(
        "Meters of movement that count as one simulated footwear step."
    )]
    [Min(0.05f)]
    public float metersPerSimulatedStep = 0.7f;

    private int huntSteps;

    private Vector3 previousPosition;
    private float accumulatedDistance;
    private bool distanceTrackingReady;

    // Historically named HuntSteps; now it is the uninterrupted AR-session total.
    public int HuntSteps => huntSteps;
    public int SessionSteps => huntSteps;

    void Start()
    {
        // Automatically use the XR/main camera if
        // nothing was manually assigned.
        if (trackedTransform == null &&
            Camera.main != null)
        {
            trackedTransform =
                Camera.main.transform;
        }

        ResetHuntSteps();
        ResetDistanceTracking();
    }

    void Update()
    {
        if (!simulateStepsFromDistance)
        {
            return;
        }

        TrackDistanceAsSteps();
    }

    // =====================================================
    // AR SESSION RESET (never call from BeginHunt)
    // =====================================================

    public void ResetHuntSteps()
    {
        huntSteps = 0;

        ResetDistanceTracking();

        UpdateDisplay();
    }

    // =====================================================
    // REAL OR SIMULATED STEP
    // =====================================================

    public void RegisterFootwearStep()
    {
        huntSteps++;

        currentEnergy +=
            1f /
            Mathf.Max(
                0.01f,
                stepsPerEnergy
            );

        currentEnergy =
            Mathf.Clamp(
                currentEnergy,
                0f,
                maximumEnergy
            );

        UpdateDisplay();
    }

    // =====================================================
    // DISTANCE → STEP TESTING
    // =====================================================

    void TrackDistanceAsSteps()
    {
        if (trackedTransform == null)
        {
            if (Camera.main != null)
            {
                trackedTransform =
                    Camera.main.transform;

                ResetDistanceTracking();
            }

            return;
        }

        Vector3 currentPosition =
            trackedTransform.position;

        if (!distanceTrackingReady)
        {
            previousPosition =
                currentPosition;

            distanceTrackingReady = true;
            return;
        }

        // Ignore vertical movement.
        // We only care about walking across the ground.
        Vector3 previousFlat =
            new Vector3(
                previousPosition.x,
                0f,
                previousPosition.z
            );

        Vector3 currentFlat =
            new Vector3(
                currentPosition.x,
                0f,
                currentPosition.z
            );

        float movedDistance =
            Vector3.Distance(
                previousFlat,
                currentFlat
            );

        previousPosition =
            currentPosition;

        // Ignore unrealistic teleport-like movement.
        // This prevents scene repositioning from
        // suddenly creating many fake steps.
        if (movedDistance > 2f)
        {
            return;
        }

        accumulatedDistance +=
            movedDistance;

        while (
            accumulatedDistance >=
            metersPerSimulatedStep
        )
        {
            accumulatedDistance -=
                metersPerSimulatedStep;

            // IMPORTANT:
            // Distance uses the SAME function as
            // an ESP32 "Pressed" message.
            RegisterFootwearStep();

            Debug.Log(
                "DISTANCE SIMULATED FOOTWEAR STEP: " +
                huntSteps
            );
        }
    }

    void ResetDistanceTracking()
    {
        accumulatedDistance = 0f;

        if (trackedTransform != null)
        {
            previousPosition =
                trackedTransform.position;

            distanceTrackingReady = true;
        }
        else
        {
            distanceTrackingReady = false;
        }
    }

    // =====================================================
    // ENERGY
    // =====================================================

    public bool SpendEnergy(float amount)
    {
        if (currentEnergy < amount)
        {
            return false;
        }

        currentEnergy -= amount;

        UpdateDisplay();

        return true;
    }

    public void AddEnergy(float amount)
    {
        currentEnergy =
            Mathf.Clamp(
                currentEnergy + amount,
                0f,
                maximumEnergy
            );

        UpdateDisplay();
    }

    // =====================================================
    // DISPLAY
    // =====================================================

    void UpdateDisplay()
    {
        if (energyBar != null)
        {
            energyBar.minValue = 0f;
            energyBar.maxValue =
                maximumEnergy;
            energyBar.value =
                currentEnergy;
        }

        if (energyText != null)
        {
            energyText.text =
                "ENERGY  " +
                Mathf.FloorToInt(
                    currentEnergy
                ) +
                " / " +
                Mathf.FloorToInt(
                    maximumEnergy
                );
        }

        if (huntStepsText != null)
        {
            huntStepsText.text =
                "STEPS  " +
                huntSteps;
        }
    }
}