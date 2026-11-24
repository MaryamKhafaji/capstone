
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls Tantal's hunt gameplay in MasAR+.
/// Tantal's hunt is based on following shadow trails and identifying
/// the correct path among several misleading ones.
///
/// Gameplay sequence:
/// 1. Tantal appears in front of the player for 10 seconds and says "hehe".
/// 2. Three trails appear in different directions, but only one is real.
/// 3. The player follows the real trail by walking through its checkpoints.
/// 4. Once the correct trail is completed, Tantal appears again for
///    10 seconds and says "Catch me if you can!".
/// 5. Two new trails appear, with only one leading to Tantal.
/// 6. After the second correct trail is completed, Tantal appears
///    for 15 seconds and says "noooooooo".
/// 7. Tantal is automatically captured, the player receives crystals,
///    and the completed hunt is recorded.
///
/// Each trail consists of eight pieces created from the same 3D prefab.
/// Real and fake trails use different materials to distinguish them.
///
/// The controller also handles Tantal's positioning, visibility,
/// notifications, trail generation, player movement checks, and rewards.
/// </summary>
public class TantalHuntController : MonoBehaviour
{


    [Header("Hunt References")]

    // The player's camera is used to calculate spawn positions,
    // directions, and the distance between the player and trails.
    [SerializeField] private Transform mainCamera;

    // Tracks the player's steps and energy earned during the hunt.
    [SerializeField] private FootwearStepManager footwearStepManager;

    // Manages the crystals awarded after Tantal is captured.
    [SerializeField] private CrystalManager crystalManager;

    // Controls the UI panels used during the hunt and capture.
    [SerializeField] private MonsterHuntUIController huntUI;


    [Header("Tantal 3D Model")]

    // The actual Tantal model displayed during gameplay.
    [Tooltip("Assign Tantal's in-game 3D model.")]
    [SerializeField] private GameObject monsterVisual;

    // How far in front of the player Tantal appears.
    [Min(1f)]
    [SerializeField] private float revealDistanceFromCamera = 3f;


    [Header("Standing Monster Placement")]

    // The ground height used when positioning Tantal.
    // This keeps his model at a fixed world position vertically.
    [Tooltip("World Y position where Tantal should stand.")]
    [SerializeField] private float monsterGroundY = -2.239f;

    // Automatically corrects the model's orientation so Tantal
    // stands upright even if the imported mesh is sideways.
    [Tooltip("Automatically adjust Tantal's orientation to stand upright.")]
    [SerializeField] private bool autoStandMonsterUpright = true;

    // Allows additional rotation adjustments when needed.
    // This is useful if the automatic correction does not
    // produce the intended orientation.
    [Tooltip("Additional rotation applied after upright correction.")]
    [SerializeField] private Vector3 monsterRotationCorrection = Vector3.zero;


    [Header("Reusable Trail Piece")]

    // The single 3D trail piece used to build every path.
    // The script duplicates this prefab instead of requiring
    // a separate model for each trail.
    [Tooltip("Prefab used to generate the shadow trail pieces.")]
    [SerializeField] private GameObject trailPiecePrefab;

    // Material applied to the correct trail.
    // It should be darker and more visible.
    [Tooltip("Material used for the correct trail.")]
    [SerializeField] private Material realTrailMaterial;

    // Material applied to misleading trails.
    // It uses a similar appearance with lower visibility.
    [Tooltip("Material used for the misleading trails.")]
    [SerializeField] private Material fakeTrailMaterial;

    // Controls the overall size of every spawned trail piece.
    [Min(0.01f)]
    [SerializeField] private float trailScaleMultiplier = 1f;

    // Adds small random differences in size so the trail
    // pieces do not look like identical repeated objects.
    [Range(0f, 0.25f)]
    [SerializeField] private float trailScaleVariation = 0.08f;


    [Header("Fixed Trail Ground Height")]

    // All trail pieces are placed at this world height.
    // Their vertical position does not depend on camera height.
    [Tooltip("World Y position for the bottom of each trail piece.")]
    [SerializeField] private float trailWorldY = -2.239f;


    [Header("Trail Shape and Spacing")]

    // Every path contains eight trail pieces in both rounds.
    private const int piecesPerTrail = 8;

    // Distance from the player to the first trail piece.
    [Min(0.5f)]
    [SerializeField] private float firstPieceDistance = 2.0f;

    // Distance between consecutive pieces in the same path.
    [Min(0.5f)]
    [SerializeField] private float distanceBetweenPieces = 1.55f;

    // Angle used to separate the three paths in round one.
    [Range(25f, 80f)]
    [SerializeField] private float firstRoundAngle = 55f;

    // Angle used to separate the two paths in round two.
    [Range(20f, 75f)]
    [SerializeField] private float secondRoundAngle = 48f;

    // Adds small sideways offsets to the trail pieces.
    // This gives each path a less artificial appearance.
    [Range(0f, 0.8f)]
    [SerializeField] private float sidewaysVariation = 0.18f;

    // Randomly rotates individual trail pieces to make
    // the repeated model look more natural.
    [Range(0f, 30f)]
    [SerializeField] private float pieceRotationVariation = 12f;


    [Header("Following a Real Trail")]

    // Maximum horizontal distance allowed between the player
    // and the next checkpoint for it to count as reached.
    [Tooltip("Distance required to reach a trail checkpoint.")]
    [Min(0.3f)]
    [SerializeField] private float trailCheckpointRadius = 1.1f;

    // Small delay before checkpoints can be reached.
    // This prevents an immediate detection when trails spawn.
    [Tooltip("Delay before trail checkpoints become active.")]
    [Range(0f, 2f)]
    [SerializeField] private float checkpointArmDelay = 0.45f;


    [Header("Tantal's Appearance and Dialogue")]

    // Delay before the hunt sequence begins.
    [Min(0f)]
    [SerializeField] private float initialDelay = 1.0f;

    // Duration of each appearance during the hunt.
    private const float FirstAppearanceSeconds = 10f;
    private const float SecondAppearanceSeconds = 10f;
    private const float FinalAppearanceSeconds = 15f;

    // Text component used to display Tantal's dialogue.
    [Tooltip("UI text used for Tantal's messages.")]
    [SerializeField] private TMP_Text cueText;

    // Panel containing the dialogue text.
    [Tooltip("Panel used to display Tantal's messages.")]
    [SerializeField] private GameObject cuePanel;

    // Messages displayed during Tantal's three appearances.
    [SerializeField] private string firstCue = "hehe";
    [SerializeField] private string secondCue = "Catch me if you can!";
    [SerializeField] private string finalCue = "noooooooo";


    [Header("Capture and Rewards")]

    // Number of crystals awarded for completing Tantal's hunt.
    [Min(0)]
    [SerializeField] private int crystalReward = 2;

    // Information used when recording the completed hunt.
    [SerializeField] private string monsterId = "tantal";
    [SerializeField] private string monsterLegend = "Tantal";

    // Event triggered after Tantal has been successfully captured.
    public UnityEvent onTantalCaught;

    // The phases describe which part of the hunt is currently active.
    // This helps control when trails are checked and when Tantal appears.
    private enum Phase
    {
        None,
        Opening,
        ThreeTrails,
        MiddleReveal,
        TwoTrails,
        FinalReveal,
        Finished
    }

    // Stores the information needed for one complete trail.
    private sealed class TrailBranch
    {
        // Determines whether this is the correct path.
        public bool isReal;

        // Positions the player must reach while following the trail.
        public readonly List<Vector3> checkpoints = new List<Vector3>();
    }

    // Stores all paths generated during the current round.
    private readonly List<TrailBranch> branches = new List<TrailBranch>();

    // Stores Tantal's renderers so his model can be hidden or revealed.
    private readonly List<Renderer> monsterRenderers = new List<Renderer>();

    // Parent object containing the generated trail pieces.
    private GameObject trailContainer;

    // Current stage of the hunt.
    private Phase phase = Phase.None;

    // Index of the correct trail in the branches list.
    private int correctBranchIndex = -1;

    // Index of the next checkpoint the player must reach.
    private int nextCheckpoint;

    // Time when the player can begin reaching checkpoints.
    private float checkpointsArmedAt;

    // Reference to the currently running hunt coroutine.
    private Coroutine sequence;

    // Prevents the hunt from being completed more than once.
    private bool huntFinished;

    // Start time and unique identifier used when saving the hunt.
    private DateTime huntStartTime;
    private string huntId;

    // Stores the original model rotation before the script
    // applies its own positioning and orientation adjustments.
    private Quaternion authoredMonsterRotation;

    // Ensures the original rotation is recorded only once.
    private bool capturedMonsterRotation;


    /// <summary>
    /// Stores Tantal's original rotation when the component initializes.
    /// </summary>
    private void Awake()
    {
        CaptureAuthoredMonsterRotation();
    }


    /// <summary>
    /// Saves the model's original world rotation before changing it
    /// during Tantal's appearances.
    /// </summary>
    private void CaptureAuthoredMonsterRotation()
    {
        // Avoid replacing the original rotation with an adjusted one.
        if (capturedMonsterRotation) return;

        // Use the assigned model when available.
        // Otherwise, use the object holding this controller.
        Transform root = monsterVisual != null ? monsterVisual.transform : transform;

        authoredMonsterRotation = root.rotation;
        capturedMonsterRotation = true;
    }


    /// <summary>
    /// Resets the hunt whenever the TantalHunt object becomes active.
    /// </summary>
    private void OnEnable()
    {
        // Prepare a fresh hunt and reset its progress.
        huntFinished = false;
        phase = Phase.Opening;
        correctBranchIndex = -1;
        nextCheckpoint = 0;

        // Give this hunt a unique ID for session recording.
        huntId = Guid.NewGuid().ToString("N");

        // Prepare Tantal's model and remove any previous visuals.
        CaptureAuthoredMonsterRotation();
        HideMonsterVisual();
        HideCue();
        ClearTrails();

        // Start Tantal's gameplay sequence.
        sequence = StartCoroutine(RunHunt());
    }


    /// <summary>
    /// Prepares the required systems, starts step tracking,
    /// and begins Tantal's first appearance and trail round.
    /// </summary>
    private IEnumerator RunHunt()
    {
        // Wait until the player's camera becomes available.
        // This is important because the trails are positioned
        // relative to the player's current location.
        while (mainCamera == null)
        {
            Camera camera = Camera.main;

            if (camera != null)
                mainCamera = camera.transform;

            yield return null;
        }

        // The hunt cannot generate trails without a trail prefab.
        if (trailPiecePrefab == null)
        {
            Debug.LogError(
                "TANTAL: Assign the Trail Piece Prefab before starting the hunt.",
                this
            );

            yield break;
        }

        // Warn if either trail material has not been assigned.
        if (realTrailMaterial == null || fakeTrailMaterial == null)
            Debug.LogWarning(
                "TANTAL: For faded fake trails, assign BOTH Real and Fake transparent materials.",
                this
            );

        // Locate the required managers if they were not assigned
        // through the Inspector.
        if (footwearStepManager == null)
            footwearStepManager = FindFirstObjectByType<FootwearStepManager>();

        if (crystalManager == null)
            crystalManager = CrystalManager.Instance;

        if (huntUI == null)
            huntUI = FindFirstObjectByType<MonsterHuntUIController>();

        // Apply the initial delay before starting the hunt.
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        // Begin recording the player's steps for this hunt.
        if (footwearStepManager != null)
            footwearStepManager.BeginHunt();
        else
            Debug.LogWarning(
                "TANTAL: FootwearStepManager is not assigned.",
                this
            );

        // Save the starting time for the completed hunt record.
        huntStartTime = DateTime.UtcNow;

        // The app has already selected the hunt. In location mode guide the
        // player to the configured destination before spawning local trails.
        while (HuntLocationGuide.Active && !HuntLocationGuide.AtDestination)
            yield return new WaitForSeconds(0.5f);

        // Show Tantal for 10 seconds with his first message.
        yield return ShowTantalWithCue(firstCue, FirstAppearanceSeconds);

        // Stop if the hunt has already ended.
        if (huntFinished)
            yield break;

        // Start the first round with three different paths.
        CreateTrails(3);
        phase = Phase.ThreeTrails;

        Debug.Log(
            "TANTAL: Round 1 started. Find the real path out of 3.",
            this
        );

        sequence = null;
    }


    /// <summary>
    /// Checks whether the player has reached the next checkpoint
    /// along the correct trail.
    ///
    /// Checkpoints must be reached in order. Completing all eight
    /// checkpoints moves the hunt to its next stage.
    /// </summary>
    private void Update()
    {
        // Do not check movement if the hunt has ended,
        // the camera is unavailable, or checkpoints are not ready.
        if (huntFinished || mainCamera == null || Time.time < checkpointsArmedAt)
            return;

        // Checkpoints are only active while following trails.
        if (phase != Phase.ThreeTrails && phase != Phase.TwoTrails)
            return;

        // Make sure the correct path index is valid.
        if (correctBranchIndex < 0 || correctBranchIndex >= branches.Count)
            return;

        // Only the real trail is checked for progress.
        // Reaching a fake trail does not advance the hunt.
        TrailBranch realTrail = branches[correctBranchIndex];

        // Stop checking if all checkpoints have already been reached.
        if (nextCheckpoint >= realTrail.checkpoints.Count)
            return;

        // Get the player's position and the next checkpoint position.
        Vector3 player = mainCamera.position;
        Vector3 marker = realTrail.checkpoints[nextCheckpoint];

        // Ignore height differences when measuring distance.
        // The player only needs to reach the checkpoint horizontally.
        player.y = 0f;
        marker.y = 0f;

        // The player must enter the checkpoint's allowed radius.
        if (Vector3.Distance(player, marker) > trailCheckpointRadius)
            return;

        // Mark the current checkpoint as completed.
        nextCheckpoint++;

        Debug.Log(
            "TANTAL: Real trail checkpoint " + nextCheckpoint + "/" +
            realTrail.checkpoints.Count,
            this
        );

        // Continue following the path if more checkpoints remain.
        if (nextCheckpoint < realTrail.checkpoints.Count)
            return;

        // The completed path determines what happens next.
        if (phase == Phase.ThreeTrails)
        {
            // The first round is complete.
            // Reveal Tantal and prepare the second round.
            phase = Phase.MiddleReveal;
            sequence = StartCoroutine(BeginSecondRound());
        }
        else
        {
            // The second round is complete.
            // Begin Tantal's final appearance and automatic capture.
            phase = Phase.FinalReveal;
            sequence = StartCoroutine(FinalRevealAndCapture());
        }
    }

    /// <summary>
    /// Starts the second stage after the player completes
    /// the first correct trail.
    /// </summary>
    private IEnumerator BeginSecondRound()
    {
        // Remove the first round's trails.
        ClearTrails();

        // Tantal appears for 10 seconds and challenges the player again.
        yield return ShowTantalWithCue(secondCue, SecondAppearanceSeconds);

        if (huntFinished)
            yield break;

        // Generate two new paths from the player's current position.
        // Only one of these paths will be correct.
        CreateTrails(2);
        phase = Phase.TwoTrails;

        Debug.Log(
            "TANTAL: Round 2 started. Find the real path out of 2.",
            this
        );

        sequence = null;
    }


    /// <summary>
    /// Shows Tantal for the final 15 seconds after the second
    /// correct trail is completed, then captures him automatically.
    /// </summary>
    private IEnumerator FinalRevealAndCapture()
    {
        // Remove the remaining trail pieces.
        ClearTrails();

        // Position Tantal in front of the player and reveal his model.
        PlaceMonsterInFrontOfPlayer();
        RevealMonsterVisual();

        // Display his final message.
        ShowCue(finalCue);

        Debug.Log(
            "TANTAL: Final reveal for " + FinalAppearanceSeconds +
            " seconds. Cue: " + finalCue,
            this
        );

        // Keep Tantal visible before completing the capture.
        yield return new WaitForSeconds(FinalAppearanceSeconds);

        // Capture Tantal without requiring another player action.
        CompleteTantalHunt();

        sequence = null;
    }

    /// <summary>
    /// Shows Tantal with a given message for a specified duration.
    /// Used for his first and second appearances.
    /// </summary>
    private IEnumerator ShowTantalWithCue(string message, float duration)
    {
        // Move Tantal into the player's view.
        PlaceMonsterInFrontOfPlayer();

        // Show his model and dialogue.
        RevealMonsterVisual();
        ShowCue(message);

        Debug.Log("TANTAL CUE: " + message, this);

        // Keep him visible for the requested duration.
        yield return new WaitForSeconds(duration);

        // Hide Tantal before the next stage begins.
        HideMonsterVisual();
        HideCue();
    }


    /// <summary>
    /// Displays Tantal's dialogue in the assigned UI panel.
    /// </summary>
    private void ShowCue(string message)
    {
        // Update the displayed message.
        if (cueText != null)
            cueText.text = message;

        // Show the dialogue panel when available.
        if (cuePanel != null)
            cuePanel.SetActive(true);
        else if (cueText != null)
            cueText.gameObject.SetActive(true);
    }


    /// <summary>
    /// Hides Tantal's dialogue when an appearance ends.
    /// </summary>
    private void HideCue()
    {
        if (cuePanel != null)
            cuePanel.SetActive(false);
        else if (cueText != null)
            cueText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Generates the paths for the current round.
    ///
    /// Each path contains eight trail pieces positioned in sequence.
    /// One path is randomly selected as correct, while the remaining
    /// paths use the fake trail material.
    ///
    /// The paths are generated relative to the player's position
    /// and facing direction when the round begins.
    /// </summary>
    private void CreateTrails(int trailCount)
    {
        // Remove existing trails and reset the path information.
        ClearTrails();
        branches.Clear();

        // Randomly select the correct path.
        correctBranchIndex = UnityEngine.Random.Range(0, trailCount);

        // Start tracking from the first checkpoint.
        nextCheckpoint = 0;

        // Create a parent object to organize all generated pieces.
        trailContainer = new GameObject(
            "Tantal_Trails_Round" + (trailCount == 3 ? "1" : "2")
        );

        // Use the camera's current position as the starting point.
        Vector3 origin = mainCamera.position;

        // Use a horizontal forward direction to keep paths on the ground.
        Vector3 forward = FlatForward(mainCamera.forward);

        // Use the fixed ground height for all trail pieces.
        float floorY = trailWorldY;

        // Choose the angle between paths based on the current round.
        float spread = trailCount == 3 ? firstRoundAngle : secondRoundAngle;

        // Create each path individually.
        for (int path = 0; path < trailCount; path++)
        {
            // Identify whether this is the randomly selected correct path.
            bool real = path == correctBranchIndex;

            // Spread the paths in different directions.
            // Round one places three paths around the forward direction.
            // Round two places one path on each side.
            float angle = trailCount == 3
                ? (path - 1) * spread
                : (path == 0 ? -spread : spread);

            // Rotate the player's forward direction to obtain
            // the direction of this particular path.
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;

            // Calculate a sideways direction for small position variations.
            Vector3 sideways = Vector3.Cross(Vector3.up, direction);

            // Create the information used to track this path.
            TrailBranch branch = new TrailBranch { isReal = real };
            branches.Add(branch);

            // Build the path using eight individual trail pieces.
            for (int part = 0; part < piecesPerTrail; part++)
            {
                // Move each new piece farther along the path.
                float distance =
                    firstPieceDistance + part * distanceBetweenPieces;

                // Add a small random sideways offset.
                // The first piece remains centered on the path.
                float offset = part == 0
                    ? 0f
                    : UnityEngine.Random.Range(
                        -sidewaysVariation,
                        sidewaysVariation
                    );

                // Calculate the position of the current piece.
                Vector3 marker =
                    origin + direction * distance + sideways * offset;

                // Keep the piece at the required ground height.
                marker.y = floorY;

                // Store the position as a checkpoint.
                branch.checkpoints.Add(marker);

                // Create the visible 3D trail piece.
                SpawnTrailPiece(marker, direction, real, path, part);
            }
        }

        // Allow a short delay before the player can reach checkpoints.
        checkpointsArmedAt = Time.time + checkpointArmDelay;

        Debug.Log(
            "TANTAL: Generated " + trailCount + " paths (" +
            piecesPerTrail + " pieces each). Exactly one is real.",
            this
        );
    }


    /// <summary>
    /// Creates one trail piece at the given position.
    ///
    /// The piece receives the correct material, scale, and rotation.
    /// Its visible mesh is then aligned with the intended ground
    /// position to account for possible model pivot offsets.
    /// </summary>
    private void SpawnTrailPiece(
        Vector3 intendedPosition,
        Vector3 direction,
        bool isReal,
        int pathIndex,
        int pieceIndex)
    {
        // Create a holder to control the piece's position and rotation.
        // The name also identifies its path, order, and type.
        GameObject holder = new GameObject(
            "Trail_" + (pathIndex + 1) +
            "_Piece_" + (pieceIndex + 1) +
            (isReal ? "_Real" : "_Fake")
        );

        // Place the holder inside the current trail container.
        holder.transform.SetParent(trailContainer.transform, false);
        holder.transform.position = intendedPosition;

        // Point the holder along the trail direction and add
        // a small random rotation.
        holder.transform.rotation =
            Quaternion.LookRotation(direction, Vector3.up) *
            Quaternion.Euler(
                0f,
                UnityEngine.Random.Range(
                    -pieceRotationVariation,
                    pieceRotationVariation
                ),
                0f
            );

        // Create an instance of the trail prefab.
        GameObject piece = Instantiate(trailPiecePrefab, holder.transform);
        piece.name = "TantalTrailPiece";

        // Keep the prefab's existing local orientation.
        piece.transform.localPosition = Vector3.zero;
        piece.transform.localRotation = trailPiecePrefab.transform.localRotation;

        // Apply the overall scale with a small random variation.
        float sizeVariation = UnityEngine.Random.Range(
            1f - trailScaleVariation,
            1f + trailScaleVariation
        );

        piece.transform.localScale =
            trailPiecePrefab.transform.localScale *
            trailScaleMultiplier *
            sizeVariation;

        // Make sure the spawned piece is visible.
        piece.SetActive(true);

        // Select the correct material based on the trail type.
        // Both types use the same mesh, but their transparency differs.
        Material selectedMaterial = isReal
            ? realTrailMaterial
            : fakeTrailMaterial;

        // Get all renderers belonging to the trail piece.
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);

        // Apply the selected material to every renderer and material slot.
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || selectedMaterial == null)
                continue;

            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
                materials[i] = selectedMaterial;

            renderer.sharedMaterials = materials;
        }

        // Imported 3D models may have pivots that do not match
        // the center of their visible mesh.
        // The bounds are used to correct the final position.
        Bounds bounds;

        if (TryGetBounds(renderers, out bounds))
        {
            // Center the piece horizontally and place its bottom
            // at the fixed trail height.
            piece.transform.position += new Vector3(
                intendedPosition.x - bounds.center.x,
                intendedPosition.y - bounds.min.y,
                intendedPosition.z - bounds.center.z
            );
        }
    }

    /// <summary>
    /// Clears the generated trails and resets checkpoint progress.
    /// Called when changing rounds, completing the hunt, or stopping it.
    /// </summary>
    private void ClearTrails()
    {
        // Remove the stored path data.
        branches.Clear();
        correctBranchIndex = -1;
        nextCheckpoint = 0;

        // Destroy the parent object containing all trail pieces.
        if (trailContainer != null)
        {
            if (Application.isPlaying)
                Destroy(trailContainer);
            else
                DestroyImmediate(trailContainer);

            trailContainer = null;
        }
    }


    /// <summary>
    /// Hides Tantal's 3D model without destroying it.
    /// The renderers are stored so they can be enabled again later.
    /// </summary>
    private void HideMonsterVisual()
    {
        monsterRenderers.Clear();

        if (monsterVisual != null)
        {
            // Keep the object active so its renderers can be accessed.
            monsterVisual.SetActive(true);

            // Collect all renderers belonging to Tantal's model.
            monsterRenderers.AddRange(
                monsterVisual.GetComponentsInChildren<Renderer>(true)
            );
        }
        else
        {
            // Use the controller's child renderers if no model is assigned.
            monsterRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }

        // Disable the collected renderers to hide Tantal.
        foreach (Renderer renderer in monsterRenderers)
            if (renderer != null)
                renderer.enabled = false;
    }


    /// <summary>
    /// Makes Tantal's 3D model visible during his appearances.
    /// </summary>
    private void RevealMonsterVisual()
    {
        // A model cannot be shown without available renderers.
        if (monsterRenderers.Count == 0)
        {
            Debug.LogWarning(
                "TANTAL: Assign Monster Visual to show his appearance.",
                this
            );

            return;
        }

        // Ensure the model object is active.
        if (monsterVisual != null)
            monsterVisual.SetActive(true);

        // Enable all stored renderers.
        foreach (Renderer renderer in monsterRenderers)
            if (renderer != null)
                renderer.enabled = true;
    }


    /// <summary>
    /// Places Tantal in front of the player during a reveal.
    ///
    /// The method uses the player's horizontal facing direction,
    /// corrects the model's orientation, and positions the bottom
    /// of the visible model at the selected ground height.
    /// </summary>
    private void PlaceMonsterInFrontOfPlayer()
    {
        if (mainCamera == null)
            return;

        // Make sure the model's original rotation has been recorded.
        CaptureAuthoredMonsterRotation();

        // Find the player's horizontal facing direction.
        Vector3 direction = FlatForward(mainCamera.forward);

        // Calculate where Tantal should appear in front of the player.
        Vector3 target =
            mainCamera.position + direction * revealDistanceFromCamera;

        target.y = monsterGroundY;

        // Use Tantal's model root when assigned.
        // Otherwise, use the controller's own Transform.
        Transform targetRoot =
            monsterVisual != null ? monsterVisual.transform : transform;

        // Rotate the model toward the player while keeping
        // its original imported rotation as the starting orientation.
        Quaternion facingPlayer =
            Quaternion.LookRotation(-direction, Vector3.up);

        Quaternion baseRotation =
            facingPlayer * authoredMonsterRotation;

        targetRoot.position = target;

        // Get the visible geometry used to determine Tantal's orientation.
        Renderer[] renderers = monsterVisual != null
            ? monsterVisual.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);

        // Start without any additional upright rotation.
        Quaternion uprightCorrection = Quaternion.identity;

        // Find the orientation that makes the model stand vertically.
        if (autoStandMonsterUpright && renderers.Length > 0)
            uprightCorrection =
                FindUprightRotation(targetRoot, baseRotation, renderers);

        // Apply any manual adjustment selected in the Inspector.
        Quaternion manualCorrection =
            Quaternion.Euler(monsterRotationCorrection);

        // Combine the original rotation, automatic correction,
        // and manual adjustment.
        targetRoot.rotation =
            manualCorrection * uprightCorrection * baseRotation;

        // Use the rendered model's bounds to correct its position.
        Bounds bounds;

        if (TryGetBounds(renderers, out bounds))
        {
            // Align the model horizontally with the target
            // and place the bottom of the mesh on the ground.
            targetRoot.position += new Vector3(
                target.x - bounds.center.x,
                monsterGroundY - bounds.min.y,
                target.z - bounds.center.z
            );
        }
    }

    /// <summary>
    /// Finds a rotation that makes Tantal's visible model stand upright.
    ///
    /// Several possible rotations are tested. The one that gives
    /// the model the greatest vertical size compared with its
    /// width and depth is selected.
    /// </summary>
    private static Quaternion FindUprightRotation(
        Transform modelRoot,
        Quaternion baseRotation,
        Renderer[] renderers)
    {
        // Possible orientation corrections for an imported model.
        Quaternion[] candidates =
        {
            Quaternion.identity,
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(0f, 0f, -90f),
            Quaternion.Euler(90f, 0f, 0f),
            Quaternion.Euler(-90f, 0f, 0f)
        };

        // Store the best correction found during testing.
        Quaternion best = Quaternion.identity;
        float bestScore = float.NegativeInfinity;

        // Test each possible rotation.
        for (int i = 0; i < candidates.Length; i++)
        {
            modelRoot.rotation = candidates[i] * baseRotation;

            // Read the visible model's dimensions after rotation.
            Bounds bounds;

            if (!TryGetBounds(renderers, out bounds))
                continue;

            Vector3 size = bounds.size;

            // A standing model should be taller than it is wide or deep.
            // Dividing the height by the larger horizontal dimension
            // gives a score for how upright the model appears.
            float score =
                size.y / Mathf.Max(0.001f, Mathf.Max(size.x, size.z));

            // Slightly prefer the original orientation when the scores
            // are nearly equal.
            if (i != 0)
                score -= 0.02f;

            // Keep the rotation with the highest score.
            if (score > bestScore)
            {
                bestScore = score;
                best = candidates[i];
            }
        }

        return best;
    }

    /// <summary>
    /// Calculates the combined bounds of a group of renderers.
    ///
    /// These bounds are used to position trail pieces and
    /// Tantal's model based on their visible geometry.
    /// </summary>
    private static bool TryGetBounds(
        Renderer[] renderers,
        out Bounds bounds)
    {
        // Start with empty bounds.
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;

        // Include every available renderer in the combined bounds.
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        // Return whether any valid renderer was found.
        return found;
    }


    /// <summary>
    /// Returns a normalized horizontal direction.
    /// Removes vertical movement so trail directions stay level.
    /// </summary>
    private static Vector3 FlatForward(Vector3 forward)
    {
        // Ignore the vertical component of the direction.
        forward.y = 0f;

        // Use a default direction if the vector is too small.
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        // Return the horizontal direction with a length of one.
        return forward.normalized;
    }


    /// <summary>
    /// Completes Tantal's hunt after the final reveal.
    ///
    /// The method stops the gameplay, calculates the player's
    /// steps and energy, awards crystals, saves the hunt record,
    /// and displays the capture reward panel.
    /// </summary>
    private void CompleteTantalHunt()
    {
        // Prevent the same hunt from being completed twice.
        if (huntFinished)
            return;

        huntFinished = true;
        phase = Phase.Finished;

        // Remove all remaining gameplay visuals.
        HideMonsterVisual();
        HideCue();
        ClearTrails();

        // Values used to record player activity.
        int completedSteps = 0;
        int energyEarned = 0;

        if (footwearStepManager != null)
        {
            // Finish step tracking and retrieve the hunt's step count.
            completedSteps = footwearStepManager.CompleteHunt();

            if (footwearStepManager.energySystem != null)
            {
                // Convert completed steps into earned energy.
                float rate = Mathf.Max(
                    0.01f,
                    footwearStepManager.energySystem.stepsPerEnergy
                );

                energyEarned =
                    Mathf.FloorToInt(completedSteps / rate);
            }
        }

        // Store crystal totals before and after the reward.
        int before = 0;
        int after = 0;

        if (crystalManager != null)
        {
            before = crystalManager.CurrentCrystals;
            after = crystalManager.AddCrystals(crystalReward);
        }
        else
        {
            Debug.LogWarning(
                "TANTAL: CrystalManager missing.",
                this
            );
        }

        // Calculate the number of crystals actually added.
        int actualEarned = Mathf.Max(0, after - before);

        // Save the completed hunt with its rewards,
        // step count, energy, and start/end times.
        HuntRewardActions.RecordCompletedHunt(
            huntId,
            monsterId,
            completedSteps,
            actualEarned,
            energyEarned,
            monsterLegend,
            huntStartTime,
            DateTime.UtcNow
        );

        // Display Tantal's capture panel and crystal rewards.
        if (huntUI != null)
            huntUI.ShowCaughtMonster(
                "Tantal",
                crystalReward,
                before,
                after
            );
        else
            Debug.LogError(
                "TANTAL: MonsterHuntUIController not assigned.",
                this
            );

        // Notify any other systems listening for Tantal's capture.
        onTantalCaught?.Invoke();

        Debug.Log(
            "TANTAL: CAUGHT automatically; " +
            completedSteps + " steps, +" +
            crystalReward + " crystals.",
            this
        );
    }
    /// <summary>
    /// Stops the hunt and clears temporary objects when
    /// the TantalHunt object is disabled.
    /// </summary>
    private void OnDisable()
    {
        // Stop any active hunt or reveal sequence.
        StopAllCoroutines();
        sequence = null;
        phase = Phase.None;

        // Remove dialogue and trail objects.
        HideCue();
        ClearTrails();

        // Hide Tantal's model without deleting it.
        foreach (Renderer renderer in monsterRenderers)
            if (renderer != null)
                renderer.enabled = false;
    }
}
