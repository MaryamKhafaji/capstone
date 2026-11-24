using System;
using System.Collections;
using UnityEngine;

public class BuDaryaHuntController : MonoBehaviour
{
    [Header("References")]
    public NavigationSystem navigationSystem;
    public Transform playerCamera;
    public CrystalManager crystalManager;
    public FootwearStepManager footwearStepManager;

    [Header("Catch Bu Daryah")]
    public float catchDistance = 2f;
    public int crystalReward = 2;
    public UnityEngine.Events.UnityEvent onBuDaryaCaught;

    private bool buDaryaCaught = false;
    private float nextCatchDebugTime;

    [Header("Catch Visibility Protection")]
    [Tooltip("Require the monster to be in front of the player, not behind them, before capture.")]
    public bool requireFacingMonsterToCatch = true;
    [Range(5f, 180f)] public float maxCatchViewAngle = 45f;

    // =====================================================
    // HUNT RESULT DATA
    // =====================================================

    private string currentHuntId;
    private DateTime huntStartUtc;

    private const string LegendName = "Bu Daryah";
    private const string MonsterId = "bu_daryah";

    [Header("Wrong Direction Warning")]
    public MonsterHuntUIController huntUI;

    public float wrongDirectionTime = 5f;

    [Tooltip("Player must move at least this far before movement counts.")]
    public float movementThreshold = 0.003f;

    private Vector3 previousPlayerPosition;
    private float wrongDirectionTimer;
    private bool movementTrackingStarted;

    [Header("Fake Destination")]
    public Transform fakeMelodyLocation;
    public AudioSource fakeMelodySource;

    [Header("Real Destination")]
    public Transform realMelodyLocation;
    public AudioSource realMelodySource;
    public AudioLowPassFilter realMelodyFilter;
    public Transform buDaryaVisual;

    [Header("Bu Daryah Height (Manual Testing)")]
    [Tooltip(
        "BuDaryaVisual's local Y relative to RealMelody. " +
        "Start with -2.239 based on your measurements. " +
        "This does not move the melody audio source."
    )]
    public float buDaryaVisualLocalY = -2.239f;

    [Header("Room (Unity units = meters)")]
    [Tooltip(
        "Optional room center. " +
        "If empty, the player's starting position is used."
    )]
    public Transform roomCenter;

    public float roomWidth = 10f;
    public float roomDepth = 10f;

    [Tooltip(
        "Inset from each wall for model/audio positions. " +
        "Increase for wide models."
    )]
    public float wallMargin = 1f;

    [Tooltip(
        "Bu Daryah cannot spawn closer than this " +
        "to the player at hunt start."
    )]
    public float minimumSpawnDistanceFromPlayer = 5f;

    [Tooltip(
        "Bu Daryah cannot spawn farther than this " +
        "from the player at hunt start."
    )]
    public float maximumSpawnDistanceFromPlayer = 10f;

    public float minimumFakeRealSeparation = 2f;

    [Header("Fake Target Spawn Distance")]
    [Tooltip(
        "Minimum horizontal distance from the player at hunt start."
    )]
    public float minimumFakeSpawnDistanceFromPlayer = 4f;

    [Tooltip(
        "Maximum horizontal distance from the player at hunt start."
    )]
    public float maximumFakeSpawnDistanceFromPlayer = 10f;

    [Header("Real Melody Trigger")]
    [Tooltip(
        "Player must first enter this radius around the fake melody."
    )]
    public float fakeMelodyApproachDistance = 3f;

    [Tooltip(
        "After approaching the fake melody, the real melody begins " +
        "within this distance of Bu Daryah."
    )]
    public float realMelodyStartDistance = 6f;

    private bool fakeMelodyApproached;

    [Header("Audio Distance")]
    public float hearingDistance = 100f;
    public float clearMelodyDistance = 2f;
    public float minimumVolume = 0.01f;

    [Range(0f, 1f)]
    public float maximumVolume = 0.6f;

    [Header("Fade")]
    public float realMelodyFadeDuration = 3f;

    private bool huntReady;
    private bool realMelodyStarted;

    private float realFadeAmount;

    private Coroutine exposeRoutine;

    private float fakeMelodyMultiplier = 1f;
    private Coroutine initializationRoutine;
    private bool travellingToPin;

    // One initialization per activation. Unity no longer implicitly calls Start()
    // as a second coroutine while OnEnable() starts another one.
    private void OnEnable()
    {
        huntReady = false;
        buDaryaCaught = false;
        travellingToPin = false;
        initializationRoutine = StartCoroutine(InitializeHunt());
    }

    private void OnDisable()
    {
        huntReady = false;
        movementTrackingStarted = false;

        if (initializationRoutine != null)
        {
            StopCoroutine(initializationRoutine);
            initializationRoutine = null;
        }

        if (exposeRoutine != null)
        {
            StopCoroutine(exposeRoutine);
            exposeRoutine = null;
        }

        if (fakeMelodySource != null) fakeMelodySource.Stop();
        if (realMelodySource != null) realMelodySource.Stop();
    }

    private IEnumerator InitializeHunt()
    {
        yield return new WaitForSeconds(1.2f);

        if (playerCamera == null ||
            fakeMelodyLocation == null ||
            realMelodyLocation == null ||
            fakeMelodySource == null ||
            realMelodySource == null)
        {
            Debug.LogWarning(
                "Bu Darya Hunt Controller has missing references."
            );

            yield break;
        }

        // Fallback in case CrystalManager was not manually assigned.
        if (crystalManager == null)
        {
            crystalManager = CrystalManager.Instance;
        }

        if (footwearStepManager == null)
        {
            footwearStepManager =
                FindFirstObjectByType<FootwearStepManager>();
        }

        if (crystalManager == null)
        {
            Debug.LogWarning(
                "CrystalManager is not assigned. " +
                "Crystals will not be rewarded until it is assigned."
            );
        }

        // Start a new hunt with both melody emitters
        // inside the same room area.
        Vector3 center =
            roomCenter != null
                ? roomCenter.position
                : playerCamera.position;

        float groundY =
            navigationSystem != null
                ? navigationSystem.destinationGroundY
                : center.y;

        // Snapshot player position at hunt start.
        Vector3 huntStartPosition =
            playerCamera.position;

        Vector3 realDestination;

        bool closeRange = EmbeddedHunt.Active && EmbeddedHunt.ReadState().closeRangeDebug;
        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        if (closeRange)
            realDestination = new Vector3(huntStartPosition.x + forward.x * 3f, groundY, huntStartPosition.z + forward.z * 3f);
        else if (HuntLocationGuide.Active)
            realDestination = new Vector3(huntStartPosition.x + forward.x * 6f, groundY, huntStartPosition.z + forward.z * 6f);
        else if (!TryRandomBuDaryaSpawn(
            center,
            groundY,
            huntStartPosition,
            out realDestination))
        {
            Debug.LogError(
                "Bu Daryah: No valid spawn point 5-10m from " +
                "the starting position inside the room. " +
                "Adjust room center, size, wall margin, " +
                "or spawn distances."
            );

            yield break;
        }

        Vector3 fakeDestination;

        if (closeRange || HuntLocationGuide.Active)
        {
            Vector3 direction = Quaternion.Euler(0f, 35f, 0f) * forward;
            fakeDestination = new Vector3(huntStartPosition.x + direction.x * 2.5f, groundY, huntStartPosition.z + direction.z * 2.5f);
        }
        else if (!TryRandomFakeSpawn(
            center,
            groundY,
            huntStartPosition,
            realDestination,
            out fakeDestination))
        {
            Debug.LogError(
                "Bu Daryah: No valid FAKE spawn inside the room " +
                "within the configured fake distance range and " +
                "separation. Adjust fake distances, room center/size, " +
                "wall margin, or separation."
            );

            yield break;
        }

        // BuDaryaVisual is a child of RealMelody.
        realMelodyLocation.position =
            realDestination;

        fakeMelodyLocation.position =
            fakeDestination;

        if (buDaryaVisual != null)
        {
            Vector3 visualLocalPosition =
                buDaryaVisual.localPosition;

            visualLocalPosition.y =
                buDaryaVisualLocalY;

            buDaryaVisual.localPosition =
                visualLocalPosition;

            buDaryaVisual.gameObject.SetActive(true);

            foreach (
                Renderer meshRenderer
                in buDaryaVisual.GetComponentsInChildren<Renderer>(true))
            {
                meshRenderer.enabled = true;
            }
        }

        Debug.Log(
            $"Bu Daryah hunt positions: " +
            $"REAL {realDestination}, FAKE {fakeDestination}"
        );

        PrepareAudioSource(
            fakeMelodySource
        );

        PrepareAudioSource(
            realMelodySource
        );

        fakeMelodySource.loop = true;
        realMelodySource.loop = true;

        fakeMelodySource.Play();
        realMelodySource.Stop();

        realMelodyStarted = false;
        fakeMelodyApproached = false;
        fakeMelodyMultiplier = 1f;
        realFadeAmount = 0f;

        buDaryaCaught = false;

        huntReady = true;
        travellingToPin = HuntLocationGuide.Active && !HuntLocationGuide.AtDestination;

        previousPlayerPosition =
            playerCamera.position;

        wrongDirectionTimer = 0f;

        movementTrackingStarted = true;

        // =================================================
        // BEGIN SESSION DATA
        // =================================================

        currentHuntId =
            Guid.NewGuid().ToString("N");

        huntStartUtc =
            DateTime.UtcNow;

        Debug.Log(
            "BU DARYA HUNT STARTED: " +
            currentHuntId
        );

        if (footwearStepManager != null)
        {
            footwearStepManager.BeginHunt();
        }
        else
        {
            Debug.LogWarning(
                "FootwearStepManager is not assigned. " +
                "Footwear steps will not be counted."
            );
        }
    }

    void Update()
    {
        if (!huntReady ||
            playerCamera == null ||
            buDaryaCaught)
        {
            return;
        }

        if (travellingToPin)
        {
            if (!HuntLocationGuide.HasFix)
            {
                fakeMelodySource.volume = 0f;
                realMelodySource.volume = 0f;
                return;
            }
            Vector3 direction = HuntLocationGuide.Direction;
            if (!HuntLocationGuide.AtDestination)
            {
                // Audible nearby cue points toward the actual saved destination,
                // rather than a random room position. Capture is unavailable en route.
                fakeMelodyLocation.position = playerCamera.position + direction * 6f;
                fakeMelodySource.volume = maximumVolume * 0.6f;
                realMelodySource.volume = 0f;
                if (buDaryaVisual != null)
                    foreach (Renderer mesh in buDaryaVisual.GetComponentsInChildren<Renderer>(true)) mesh.enabled = false;
                return;
            }
            travellingToPin = false;
            float ground = navigationSystem != null ? navigationSystem.destinationGroundY : playerCamera.position.y;
            Vector3 real = playerCamera.position + direction * 6f;
            Vector3 fake = playerCamera.position + Quaternion.Euler(0, 35, 0) * direction * 4f;
            real.y = fake.y = ground;
            realMelodyLocation.position = real;
            fakeMelodyLocation.position = fake;
            if (buDaryaVisual != null)
                foreach (Renderer mesh in buDaryaVisual.GetComponentsInChildren<Renderer>(true)) mesh.enabled = true;
            previousPlayerPosition = playerCamera.position;
        }

        if (!realMelodyStarted)
        {
            UpdateFakeStage();
        }
        else
        {
            UpdateTwoMelodyStage();
        }

        UpdateWrongDirectionDetection();

        CheckForBuDaryaCatch();
    }

    void UpdateWrongDirectionDetection()
    {
        if (!movementTrackingStarted ||
            !realMelodyStarted ||
            huntUI == null ||
            realMelodyLocation == null)
        {
            return;
        }

        Vector3 currentPosition =
            playerCamera.position;

        currentPosition.y = 0f;

        Vector3 previousPosition =
            previousPlayerPosition;

        previousPosition.y = 0f;

        Vector3 movement =
            currentPosition -
            previousPosition;

        previousPlayerPosition =
            playerCamera.position;

        // Standing still pauses timer.
        if (movement.magnitude <
            movementThreshold)
        {
            return;
        }

        Vector3 directionToMonster =
            realMelodyLocation.position -
            currentPosition;

        directionToMonster.y = 0f;

        if (directionToMonster.sqrMagnitude <
            0.001f)
        {
            return;
        }

        movement.Normalize();
        directionToMonster.Normalize();

        float directionDot =
            Vector3.Dot(
                movement,
                directionToMonster
            );

        bool movingWrongDirection =
            directionDot < 0f;

        if (movingWrongDirection)
        {
            if (!huntUI.IsWarningShowing())
            {
                wrongDirectionTimer +=
                    Time.deltaTime;

                if (wrongDirectionTimer >=
                    wrongDirectionTime)
                {
                    wrongDirectionTimer = 0f;

                    huntUI.ShowWrongDirectionWarning();
                }
            }
        }
        else
        {
            wrongDirectionTimer = 0f;
        }
    }

    void UpdateFakeStage()
    {
        float distanceToFake =
            GetGroundDistance(
                playerCamera.position,
                fakeMelodyLocation.position
            );

        float fakeVolume =
            CalculateVolume(
                distanceToFake
            );

        fakeMelodySource.volume =
            fakeVolume *
            fakeMelodyMultiplier;

        if (!fakeMelodyApproached &&
            distanceToFake <=
            fakeMelodyApproachDistance)
        {
            fakeMelodyApproached = true;

            Debug.Log(
                "Fake melody approached. " +
                "Real melody can now unlock nearby."
            );
        }

        float distanceToReal =
            GetGroundDistance(
                playerCamera.position,
                realMelodyLocation.position
            );

        if (fakeMelodyApproached &&
            distanceToReal <=
            realMelodyStartDistance)
        {
            realMelodyStarted = true;

            realFadeAmount = 0f;

            realMelodySource.volume = 0f;

            realMelodySource.Play();

            Debug.Log(
                "Real melody is fading in."
            );
        }
    }

    void UpdateTwoMelodyStage()
    {
        float distanceToReal =
            GetGroundDistance(
                playerCamera.position,
                realMelodyLocation.position
            );

        float realDistanceVolume =
            CalculateVolume(
                distanceToReal
            );

        float distanceToFake =
            GetGroundDistance(
                playerCamera.position,
                fakeMelodyLocation.position
            );

        fakeMelodySource.volume =
            CalculateVolume(
                distanceToFake
            ) *
            fakeMelodyMultiplier;

        realFadeAmount =
            Mathf.MoveTowards(
                realFadeAmount,
                1f,
                Time.deltaTime /
                Mathf.Max(
                    0.01f,
                    realMelodyFadeDuration
                )
            );

        realMelodySource.volume =
            realDistanceVolume *
            realFadeAmount;

        float realStrength =
            CalculateStrength(
                distanceToReal
            );

        if (realMelodyFilter != null)
        {
            realMelodyFilter.cutoffFrequency =
                Mathf.Lerp(
                    2500f,
                    22000f,
                    realStrength
                );
        }
    }

    bool TryRandomBuDaryaSpawn(
        Vector3 center,
        float y,
        Vector3 startPosition,
        out Vector3 result)
    {
        result =
            default(Vector3);

        if (roomWidth <=
                2f * wallMargin ||
            roomDepth <=
                2f * wallMargin ||
            minimumSpawnDistanceFromPlayer < 0f ||
            maximumSpawnDistanceFromPlayer <
                minimumSpawnDistanceFromPlayer)
        {
            return false;
        }

        float halfX =
            roomWidth * 0.5f -
            wallMargin;

        float halfZ =
            roomDepth * 0.5f -
            wallMargin;

        float minSqr =
            minimumSpawnDistanceFromPlayer *
            minimumSpawnDistanceFromPlayer;

        float maxSqr =
            maximumSpawnDistanceFromPlayer *
            maximumSpawnDistanceFromPlayer;

        for (int i = 0;
             i < 2000;
             i++)
        {
            float x =
                UnityEngine.Random.Range(
                    -halfX,
                    halfX
                );

            float z =
                UnityEngine.Random.Range(
                    -halfZ,
                    halfZ
                );

            float dx =
                center.x +
                x -
                startPosition.x;

            float dz =
                center.z +
                z -
                startPosition.z;

            float distanceSqr =
                dx * dx +
                dz * dz;

            // Keep the nearby monster in front of the player at the start
            // when the room is centered on the player's current position.
            if (roomCenter == null && playerCamera != null)
            {
                Vector3 facing = playerCamera.forward;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.001f &&
                    Vector3.Dot(facing, new Vector3(dx, 0f, dz)) < 0f)
                    continue;
            }

            if (distanceSqr >= minSqr &&
                distanceSqr <= maxSqr)
            {
                result =
                    new Vector3(
                        center.x + x,
                        y,
                        center.z + z
                    );

                return true;
            }
        }

        return false;
    }

    bool TryRandomFakeSpawn(
        Vector3 center,
        float y,
        Vector3 startPosition,
        Vector3 realPosition,
        out Vector3 result)
    {
        result =
            default(Vector3);

        if (roomWidth <=
                2f * wallMargin ||
            roomDepth <=
                2f * wallMargin ||
            minimumFakeSpawnDistanceFromPlayer < 0f ||
            maximumFakeSpawnDistanceFromPlayer <
                minimumFakeSpawnDistanceFromPlayer ||
            minimumFakeRealSeparation < 0f)
        {
            return false;
        }

        float halfX =
            roomWidth * 0.5f -
            wallMargin;

        float halfZ =
            roomDepth * 0.5f -
            wallMargin;

        float minSqr =
            minimumFakeSpawnDistanceFromPlayer *
            minimumFakeSpawnDistanceFromPlayer;

        float maxSqr =
            maximumFakeSpawnDistanceFromPlayer *
            maximumFakeSpawnDistanceFromPlayer;

        float separationSqr =
            minimumFakeRealSeparation *
            minimumFakeRealSeparation;

        for (int i = 0;
             i < 2000;
             i++)
        {
            Vector3 candidate =
                new Vector3(
                    center.x +
                    UnityEngine.Random.Range(
                        -halfX,
                        halfX
                    ),
                    y,
                    center.z +
                    UnityEngine.Random.Range(
                        -halfZ,
                        halfZ
                    )
                );

            float dx =
                candidate.x -
                startPosition.x;

            float dz =
                candidate.z -
                startPosition.z;

            float distanceSqr =
                dx * dx +
                dz * dz;

            float rx =
                candidate.x -
                realPosition.x;

            float rz =
                candidate.z -
                realPosition.z;

            float realSeparationSqr =
                rx * rx +
                rz * rz;

            if (distanceSqr >= minSqr &&
                distanceSqr <= maxSqr &&
                realSeparationSqr >=
                separationSqr)
            {
                result = candidate;

                return true;
            }
        }

        return false;
    }

    void CheckForBuDaryaCatch()
    {
        if (buDaryaCaught ||
            !huntReady ||
            playerCamera == null ||
            buDaryaVisual == null ||
            realMelodyLocation == null)
        {
            return;
        }

        float distanceToBuDarya =
            GetGroundDistance(
                playerCamera.position,
                buDaryaVisual.position
            );

        if (distanceToBuDarya <=
                Mathf.Max(
                    catchDistance + 2f,
                    4f
                ) &&
            Time.time >=
                nextCatchDebugTime)
        {
            nextCatchDebugTime =
                Time.time + 1f;

            Debug.Log(
                $"BU DARYA CATCH CHECK: " +
                $"distance={distanceToBuDarya:F2}m, " +
                $"catchRadius={catchDistance:F2}m, " +
                $"fakeApproached={fakeMelodyApproached}, " +
                $"realMelodyStarted={realMelodyStarted}"
            );
        }

        if (!realMelodyStarted)
        {
            return;
        }

        if (distanceToBuDarya > catchDistance)
            return;

        // Do not collect an invisible or off-screen monster by accident.
        bool visualIsVisible = false;
        foreach (Renderer visualRenderer in
            buDaryaVisual.GetComponentsInChildren<Renderer>(true))
        {
            if (visualRenderer.enabled && visualRenderer.gameObject.activeInHierarchy)
            {
                visualIsVisible = true;
                break;
            }
        }
        if (!visualIsVisible)
            return;

        if (requireFacingMonsterToCatch)
        {
            Vector3 toMonster = buDaryaVisual.position - playerCamera.position;
            Vector3 facing = playerCamera.forward;
            toMonster.y = 0f;
            facing.y = 0f;
            if (toMonster.sqrMagnitude > 0.001f && facing.sqrMagnitude > 0.001f &&
                Vector3.Angle(facing, toMonster) > maxCatchViewAngle)
                return;
        }

        CatchBuDarya();
    }

    void CatchBuDarya()
    {
        if (buDaryaCaught)
        {
            return;
        }

        buDaryaCaught = true;

        huntReady = false;

        movementTrackingStarted = false;

        wrongDirectionTimer = 0f;

        // =================================================
        // FINAL HUNT STEPS
        // =================================================

        int completedHuntSteps = 0;

        if (footwearStepManager != null)
        {
            completedHuntSteps =
                footwearStepManager.CompleteHunt();
        }

        Debug.Log(
            "BU DARYA HUNT STEPS: " +
            completedHuntSteps
        );

        // Stop fake melody.
        if (fakeMelodySource != null)
        {
            fakeMelodySource.Stop();
        }

        // Stop real melody.
        if (realMelodySource != null)
        {
            realMelodySource.Stop();
        }

        // Stop active expose effect.
        if (exposeRoutine != null)
        {
            StopCoroutine(
                exposeRoutine
            );

            exposeRoutine = null;
        }

        // =================================================
        // CRYSTALS
        // =================================================

        int crystalsBefore = 0;
        int crystalsAfter = 0;
        int actualCrystalsEarned = 0;

        if (crystalManager == null)
        {
            crystalManager =
                CrystalManager.Instance;
        }

        if (crystalManager != null)
        {
            crystalsBefore =
                crystalManager.CurrentCrystals;

            crystalsAfter =
                crystalManager.AddCrystals(
                    crystalReward
                );

            actualCrystalsEarned =
                Mathf.Max(
                    0,
                    crystalsAfter -
                    crystalsBefore
                );

            Debug.Log(
                "CRYSTAL REWARD: " +
                crystalsBefore +
                " -> " +
                crystalsAfter
            );
        }
        else
        {
            Debug.LogError(
                "CrystalManager is NOT assigned! " +
                "No crystals were awarded."
            );
        }

        // =================================================
        // ENERGY EARNED THIS HUNT
        // =================================================

        int energyEarned = 0;

        if (footwearStepManager != null &&
            footwearStepManager.energySystem != null)
        {
            float stepsPerEnergy =
                Mathf.Max(
                    0.01f,
                    footwearStepManager
                        .energySystem
                        .stepsPerEnergy
                );

            energyEarned =
                Mathf.FloorToInt(
                    completedHuntSteps /
                    stepsPerEnergy
                );
        }

        Debug.Log(
            "HUNT ENERGY EARNED: " +
            energyEarned
        );

        // =================================================
        // END TIME
        // =================================================

        DateTime huntEndUtc =
            DateTime.UtcNow;

        // =================================================
        // SEND COMPLETE RESULT TO FLUTTER
        // =================================================

        // The session ledger is stored by the EXISTING HuntRewardActions.
        // Never send a single hunt result or credit cumulative totals here.
        HuntRewardActions.RecordCompletedHunt(
            currentHuntId,
            MonsterId,
            completedHuntSteps,
            actualCrystalsEarned,
            energyEarned,
            LegendName,
            huntStartUtc,
            huntEndUtc
        );

        // Hide real 3D monster.
        if (buDaryaVisual != null)
        {
            buDaryaVisual.gameObject.SetActive(
                false
            );
        }

        // Show reward screen.
        if (huntUI != null)
        {
            huntUI.ShowCaughtBuDaryah(
                crystalReward,
                crystalsBefore,
                crystalsAfter
            );
        }
        else
        {
            Debug.LogError(
                "Hunt UI is NOT assigned on BuDaryaHunt!"
            );
        }

        // Fire optional Unity event.
        onBuDaryaCaught?.Invoke();

        Debug.Log(
            "BU DARYA CAUGHT! RESULT STORED IN LOCAL AR SESSION."
        );
    }

    // =====================================================
    // UNITY -> FLUTTER RESULT
    // =====================================================

    float CalculateVolume(
        float distance)
    {
        if (distance >=
            hearingDistance)
        {
            return 0f;
        }

        float strength =
            CalculateStrength(
                distance
            );

        float smoothStrength =
            strength *
            strength *
            strength;

        return Mathf.Lerp(
            minimumVolume,
            maximumVolume,
            smoothStrength
        );
    }

    float CalculateStrength(
        float distance)
    {
        return Mathf.InverseLerp(
            hearingDistance,
            clearMelodyDistance,
            distance
        );
    }

    float GetGroundDistance(
        Vector3 first,
        Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;

        return Vector3.Distance(
            first,
            second
        );
    }

    void PrepareAudioSource(
        AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.spatialBlend = 1f;

        source.dopplerLevel = 0f;

        source.rolloffMode =
            AudioRolloffMode.Custom;

        source.SetCustomCurve(
            AudioSourceCurveType.CustomRolloff,
            AnimationCurve.Linear(
                0f,
                1f,
                1f,
                1f
            )
        );
    }

    public void ActivateExpose()
    {
        if (!huntReady ||
            fakeMelodySource == null ||
            buDaryaCaught)
        {
            return;
        }

        if (exposeRoutine != null)
        {
            StopCoroutine(
                exposeRoutine
            );
        }

        exposeRoutine =
            StartCoroutine(
                ExposeRoutine()
            );
    }

    private IEnumerator ExposeRoutine()
    {
        float elapsed = 0f;

        float fadeOutDuration = 0.5f;

        while (elapsed <
            fadeOutDuration)
        {
            elapsed +=
                Time.deltaTime;

            fakeMelodyMultiplier =
                Mathf.Lerp(
                    1f,
                    0f,
                    elapsed /
                    fadeOutDuration
                );

            yield return null;
        }

        fakeMelodyMultiplier = 0f;

        yield return new WaitForSeconds(
            15f
        );

        elapsed = 0f;

        float fadeInDuration = 1.5f;

        while (elapsed <
            fadeInDuration)
        {
            elapsed +=
                Time.deltaTime;

            fakeMelodyMultiplier =
                Mathf.Lerp(
                    0f,
                    1f,
                    elapsed /
                    fadeInDuration
                );

            yield return null;
        }

        fakeMelodyMultiplier = 1f;

        exposeRoutine = null;
    }
}
