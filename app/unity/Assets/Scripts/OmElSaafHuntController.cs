using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class OmElSaafHuntController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private FootwearStepManager footwearStepManager;
    [SerializeField] private CrystalManager crystalManager;
    [SerializeField] private MonsterHuntUIController huntUI;

    [Header("Monster Spawn")]
    [SerializeField] private float minSpawnDistance = 6f;
    [SerializeField] private float maxSpawnDistance = 9f;
    [SerializeField] private float monsterHeightBelowCamera = 1.4f;
    [SerializeField] private float maxSpawnAngle = 35f;
    [Tooltip("Optional: assign only the 3D monster model if other objects live under this root. If empty, the existing renderers under this root are hidden until reveal.")]
    [SerializeField] private GameObject monsterVisual;

    [Header("Palm Frond Collectibles")]
    [Tooltip("Drag the om_elsaaf_Fregblend prefab here, not the FBX file if you want to edit scale/materials in the prefab.")]
    [SerializeField] private GameObject palmFrondPrefab;
    [Min(1)][SerializeField] private int leafCount = 5;
    [Tooltip("In location mode, place one collectible per approximate walking distance.")]
    [Min(1f)][SerializeField] private float metresPerLeaf = 50f;
    [Tooltip("Multiplies the prefab's existing scale. Begin with 1.")]
    [Min(0.01f)][SerializeField] private float frondScaleMultiplier = 1f;
    [SerializeField] private float leafHeightBelowCamera = 0.75f;
    [SerializeField] private float sidewaysVariation = 0.35f;
    [Header("Sequential Frond Placement")]
    [Tooltip("Distance ahead of the user's current camera pose for EACH new frond.")]
    [Min(2f)][SerializeField] private float nextFrondSpawnDistance = 4f;
    [Tooltip("Random direction offset (degrees). Keep small so each item is discoverable.")]
    [Range(0f, 35f)][SerializeField] private float frondSpawnAngle = 12f;
    [Tooltip("Adjust only if the FBX model's forward axis is unusual.")]
    [SerializeField] private Vector3 frondRotationOffset = Vector3.zero;
    [Min(0f)][SerializeField] private float pickupArmDelay = 0.45f;
    [Tooltip("Horizontal collection radius in meters. Does not require colliders.")]
    [Min(0.2f)][SerializeField] private float collectionDistance = 1.25f;
    [Min(0f)][SerializeField] private float nextFrondDelay = 0.35f;
    [Tooltip("Optional: if all fronds should face the player, enable this.")]
    [SerializeField] private bool faceFrondTowardPlayer = true;

    [Header("Capture After Five Fronds")]
    [Min(0f)][SerializeField] private float revealDuration = 4f;
    [Header("Visible Monster Reveal")]
    [Tooltip("Om El-Saaf moves into the player's forward view for the final reveal.")]
    [SerializeField] private float revealDistanceFromCamera = 2.5f;
    [Tooltip("Vertical distance below the camera for the center of the revealed monster.")]
    [SerializeField] private float revealCenterBelowCamera = 0.35f;
    [Min(0)][SerializeField] private int crystalReward = 2;
    [SerializeField] private string monsterId = "om_el_saaf";
    [SerializeField] private string monsterLegend = "Om El-Saaf";
    public UnityEvent onOmElSaafCaught;

    private readonly List<Renderer> hiddenMonsterRenderers = new List<Renderer>();
    private GameObject currentFrond;
    private Vector3 currentFrondVisualCenter;
    private float frondPickupAllowedAt;
    private Coroutine huntRoutine;
    private int collectedCount;
    private int requiredLeafCount;
    private bool huntReady;
    private bool huntFinished;
    private DateTime huntStartTime;
    private string huntId;
    private bool frondIsGuidance;

    internal static int CountForDistance(float distance, float spacing)
    {
        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, distance) / Mathf.Max(1f, spacing)));
    }

    public int RequiredLeafCount => HuntLocationGuide.Active
        ? CountForDistance(HuntLocationGuide.InitialDistance, metresPerLeaf)
        : Mathf.Max(1, leafCount);

    private void OnEnable()
    {
        huntReady = false;
        huntFinished = false;
        collectedCount = 0;
        requiredLeafCount = RequiredLeafCount;
        huntId = Guid.NewGuid().ToString("N");
        HideMonsterVisual();
        huntRoutine = StartCoroutine(InitializeHunt());
    }

    private IEnumerator InitializeHunt()
    {
        while (mainCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null) mainCamera = cam.transform;
            yield return null;
        }

        if (palmFrondPrefab == null)
        {
            Debug.LogError("OM EL-SAAF: Palm Frond Prefab is not assigned!", this);
            yield break;
        }

        DestroyCurrentFrond();

        // Match the original short delay for XR camera tracking to settle.
        yield return new WaitForSeconds(2f);

        SpawnMonsterPosition();

        if (footwearStepManager == null)
            footwearStepManager = FindFirstObjectByType<FootwearStepManager>();
        if (crystalManager == null)
            crystalManager = CrystalManager.Instance;
        if (huntUI == null)
            huntUI = FindFirstObjectByType<MonsterHuntUIController>();

        // Preserve the existing hunt baseline / step tracking behavior.
        if (footwearStepManager != null)
        {
            footwearStepManager.BeginHunt();
            Debug.Log("OM EL-SAAF: Footwear hunt started.");
        }
        else
        {
            Debug.LogWarning("OM EL-SAAF: FootwearStepManager not found!", this);
        }

        huntStartTime = DateTime.UtcNow;
        huntReady = true;
        SpawnCurrentFrond();
        huntRoutine = null;
        Debug.Log("OM EL-SAAF: Hunt started; frond 1/" + requiredLeafCount);
    }

    private void SpawnMonsterPosition()
    {
        Vector3 forward = mainCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        float low = Mathf.Min(minSpawnDistance, maxSpawnDistance);
        float high = Mathf.Max(minSpawnDistance, maxSpawnDistance);
        float distance = UnityEngine.Random.Range(low, high);
        Vector3 spawnPosition = mainCamera.position + forward * distance;
        spawnPosition.y = mainCamera.position.y - monsterHeightBelowCamera;

        // Preserve the old controller's placement convention.
        transform.position = spawnPosition;

        Vector3 towardPlayer = mainCamera.position - transform.position;
        towardPlayer.y = 0f;
        if (towardPlayer.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(towardPlayer);

        Debug.Log("OM EL-SAAF: Monster waiting hidden at " + transform.position);
    }

    private void HideMonsterVisual()
    {
        hiddenMonsterRenderers.Clear();
        if (monsterVisual != null)
        {
            foreach (Renderer renderer in monsterVisual.GetComponentsInChildren<Renderer>(true))
            {
                hiddenMonsterRenderers.Add(renderer);
                renderer.enabled = false;
            }
        }
        else
        {
            // No visual assigned: hide ONLY renderers present before fronds spawn.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                hiddenMonsterRenderers.Add(renderer);
                renderer.enabled = false;
            }
        }
    }

    private void RevealMonsterVisual()
    {
        foreach (Renderer renderer in hiddenMonsterRenderers)
            if (renderer != null) renderer.enabled = true;
        Debug.Log("OM EL-SAAF: Monster revealed!");
    }

    private void SpawnCurrentFrond()
    {
        if (!huntReady || huntFinished || collectedCount >= requiredLeafCount || currentFrond != null)
            return;

        // Spawn each new frond relative to the camera NOW, not relative to a
        // fixed set of positions recorded when the hunt began.
        Vector3 forward = mainCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();
        if (HuntLocationGuide.Active && HuntLocationGuide.HasFix && !HuntLocationGuide.AtDestination)
            forward = HuntLocationGuide.Direction;
        forward = Quaternion.Euler(0f,
            UnityEngine.Random.Range(-frondSpawnAngle, frondSpawnAngle), 0f) * forward;

        // Even if collectionDistance is still 3 in an old Inspector, never
        // spawn within immediate pickup range.
        bool closeRange = EmbeddedHunt.Active && EmbeddedHunt.ReadState().closeRangeDebug;
        float spawnDistance = closeRange ? 2f : Mathf.Max(nextFrondSpawnDistance, collectionDistance + 1.8f);
        Vector3 intendedCenter = mainCamera.position + forward * spawnDistance;
        intendedCenter.y = mainCamera.position.y - leafHeightBelowCamera;

        Quaternion rotation = palmFrondPrefab.transform.rotation;
        if (faceFrondTowardPlayer)
        {
            Vector3 towardPlayer = -forward;
            rotation = Quaternion.LookRotation(towardPlayer) *
                Quaternion.Euler(frondRotationOffset);
        }

        currentFrond = Instantiate(palmFrondPrefab, intendedCenter, rotation);
        frondIsGuidance = HuntLocationGuide.Active && !ReachedFrondMilestone();
        currentFrond.name = "OmElSaafPalmFrond_" + (collectedCount + 1);
        currentFrond.transform.localScale *= frondScaleMultiplier;

        // An imported FBX can have a pivot nowhere near the visible artwork.
        // Recenter the actual rendered mesh on the intended location.
        Vector3 center;
        if (TryGetFrondVisualCenter(out center))
        {
            currentFrond.transform.position += intendedCenter - center;
            TryGetFrondVisualCenter(out center);
            currentFrondVisualCenter = center;
        }
        else
        {
            currentFrondVisualCenter = currentFrond.transform.position;
            Debug.LogWarning("OM EL-SAAF: The frond model has no enabled Renderer. Check its FBX and materials.");
        }

        frondPickupAllowedAt = Time.time + pickupArmDelay;
        Debug.Log("OM EL-SAAF: Frond " + (collectedCount + 1) + "/" + requiredLeafCount +
                  " appeared, visual center=" + currentFrondVisualCenter +
                  ", camera=" + mainCamera.position +
                  ", pickup radius=" + collectionDistance);
    }

    private bool TryGetFrondVisualCenter(out Vector3 center)
    {
        center = Vector3.zero;
        if (currentFrond == null) return false;
        Renderer[] renderers = currentFrond.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (found) center = bounds.center;
        return found;
    }

    private void Update()
    {
        if (!huntReady || huntFinished || mainCamera == null || currentFrond == null)
            return;
        if (Time.time < frondPickupAllowedAt) return;

        if (HuntLocationGuide.Active)
        {
            if (!HuntLocationGuide.HasFix) return;
            if (frondIsGuidance)
            {
                if (!ReachedFrondMilestone())
                {
                    Vector3 guide = mainCamera.position + HuntLocationGuide.Direction * 4f;
                    guide.y = mainCamera.position.y - leafHeightBelowCamera;
                    if (TryGetFrondVisualCenter(out Vector3 visualCenter))
                        currentFrond.transform.position += guide - visualCenter;
                    if (faceFrondTowardPlayer)
                        currentFrond.transform.rotation = Quaternion.LookRotation(-HuntLocationGuide.Direction) * Quaternion.Euler(frondRotationOffset);
                    return;
                }
                // Freeze the collectible when the player reaches this part of the walk.
                frondIsGuidance = false;
                frondPickupAllowedAt = Time.time + pickupArmDelay;
                return;
            }
        }

        Vector3 center;
        if (TryGetFrondVisualCenter(out center)) currentFrondVisualCenter = center;

        // Measure to the visible model, not the imported FBX pivot.
        Vector3 player = mainCamera.position;
        Vector3 target = currentFrondVisualCenter;
        player.y = 0f;
        target.y = 0f;
        float horizontalDistance = Vector3.Distance(player, target);
        float pickupDistance = EmbeddedHunt.Active && EmbeddedHunt.ReadState().closeRangeDebug ? 1f : collectionDistance;
        if (horizontalDistance <= pickupDistance &&
            (!HuntLocationGuide.Active || collectedCount + 1 < requiredLeafCount || HuntLocationGuide.AtDestination))
            CollectCurrentFrond();
    }

    private void CollectCurrentFrond()
    {
        if (currentFrond == null || huntFinished) return;

        DestroyCurrentFrond();
        collectedCount++;
        Debug.Log("OM EL-SAAF: Frond collected " + collectedCount + "/" + requiredLeafCount);

        if (collectedCount >= requiredLeafCount)
        {
            huntReady = false;
            huntRoutine = StartCoroutine(RevealAndCapture());
        }
        else
        {
            huntRoutine = StartCoroutine(SpawnNextFrond());
        }
    }

    private bool ReachedFrondMilestone()
    {
        if (!HuntLocationGuide.Active) return true;
        if (!HuntLocationGuide.HasFix) return false;
        if (HuntLocationGuide.AtDestination) return true;
        if (collectedCount + 1 >= requiredLeafCount) return false;
        float remainingAtMilestone = HuntLocationGuide.InitialDistance *
            (1f - (float)(collectedCount + 1) / requiredLeafCount);
        return HuntLocationGuide.Current.distanceMeters <= remainingAtMilestone;
    }

    private IEnumerator SpawnNextFrond()
    {
        if (nextFrondDelay > 0f)
            yield return new WaitForSeconds(nextFrondDelay);
        SpawnCurrentFrond();
        huntRoutine = null;
    }

    private IEnumerator RevealAndCapture()
    {
        // The last frond can be far away or off to the side. Place the reveal
        // in front of the player so it cannot be missed.
        if (mainCamera != null)
        {
            Vector3 direction = mainCamera.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            direction.Normalize();

            Vector3 position = mainCamera.position +
                direction * Mathf.Max(1.5f, revealDistanceFromCamera);
            position.y = mainCamera.position.y - monsterHeightBelowCamera;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(-direction);
        }

        RevealMonsterVisual();
        // The imported visual has a large offset from its root. Align its
        // actual visible center with the intended position in front of the player.
        if (mainCamera != null)
        {
            Bounds visualBounds = new Bounds();
            bool hasBounds = false;
            foreach (Renderer renderer in hiddenMonsterRenderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!hasBounds) { visualBounds = renderer.bounds; hasBounds = true; }
                else visualBounds.Encapsulate(renderer.bounds);
            }
            if (hasBounds)
            {
                Vector3 forward = Vector3.ProjectOnPlane(mainCamera.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                Vector3 target = mainCamera.position + forward.normalized * Mathf.Max(1.5f, revealDistanceFromCamera);
                target.y = mainCamera.position.y - revealCenterBelowCamera;
                transform.position += target - visualBounds.center;
            }
        }
        Debug.Log("OM EL-SAAF: Final monster reveal in front of player at " + transform.position);
        if (revealDuration > 0f)
            yield return new WaitForSeconds(revealDuration);

        CompleteOmElSaafHunt();
        huntRoutine = null;
    }

    private void CompleteOmElSaafHunt()
    {
        if (huntFinished || collectedCount < requiredLeafCount) return;
        huntFinished = true;

        int completedSteps = 0;
        int energyEarned = 0;
        if (footwearStepManager != null)
        {
            completedSteps = footwearStepManager.CompleteHunt();
            if (footwearStepManager.energySystem != null)
            {
                float rate = Mathf.Max(0.01f, footwearStepManager.energySystem.stepsPerEnergy);
                energyEarned = Mathf.FloorToInt(completedSteps / rate);
            }
        }

        int before = 0;
        int after = 0;
        if (crystalManager != null)
        {
            before = crystalManager.CurrentCrystals;
            after = crystalManager.AddCrystals(crystalReward);
        }
        else
        {
            Debug.LogWarning("OM EL-SAAF: CrystalManager missing; reward panel may not reflect crystals.", this);
        }

        int actualCrystalsEarned = Mathf.Max(0, after - before);
        HuntRewardActions.RecordCompletedHunt(
            huntId, monsterId, completedSteps, actualCrystalsEarned,
            energyEarned, monsterLegend, huntStartTime, DateTime.UtcNow);

        // Hide the monster before showing the caught/reward panel.
        foreach (Renderer renderer in hiddenMonsterRenderers)
            if (renderer != null) renderer.enabled = false;

        if (huntUI != null)
            huntUI.ShowCaughtMonster("Om El-Saaf", crystalReward, before, after);
        else
            Debug.LogError("OM EL-SAAF: MonsterHuntUIController is missing!", this);

        onOmElSaafCaught?.Invoke();
        Debug.Log("OM EL-SAAF: CAUGHT; " + completedSteps + " steps, +" + crystalReward + " crystals; session hunt saved.");
    }

    private void DestroyCurrentFrond()
    {
        if (currentFrond != null)
            Destroy(currentFrond);
        currentFrond = null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        huntRoutine = null;
        huntReady = false;
        DestroyCurrentFrond();
    }
}
