using System.Collections;
using UnityEngine;

public class NavigationSystem : MonoBehaviour
{
    [Header("Destination")]
    public string destinationName = "check point";
    public Transform userTransform;
    public float destinationDistanceMeters = 6f;
    public float destinationGroundY = 0.5f;

    [Header("Destination Visual")]
    public Transform destinationVisual;

    [Header("Live Data")]
    public float remainingDistanceMeters;
    public float remainingDistanceKm;
    public int etaMinutes;
    public float walkedDistanceKm = 0f;
    public int steps = 0;
    public string currentAlert = "";

    [Header("Arrival")]
    public float arrivalThresholdMeters = 1.0f;
    public bool hasArrived = false;

    [Header("Debug / Read Only")]
    public Vector3 fixedDestination;

    private Vector3 lastUserPosition;
    private bool hasLastPosition = false;
    private bool destinationInitialized = false;

    private float walkingSpeedKmH = 5f;

    void Start()
    {
        StartCoroutine(InitializeDestinationAfterDelay());
    }

    IEnumerator InitializeDestinationAfterDelay()
    {
        // Wait a little so AR camera/user pose becomes stable
        yield return new WaitForSeconds(1.0f);

        if (userTransform == null)
            yield break;

        Vector3 flatForward = userTransform.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        else
            flatForward.Normalize();

        Vector3 startPos = userTransform.position;
        fixedDestination = startPos + flatForward * destinationDistanceMeters;
        fixedDestination.y = destinationGroundY;

        destinationInitialized = true;

        if (destinationVisual != null)
        {
            destinationVisual.gameObject.SetActive(true);
            destinationVisual.position = fixedDestination;
        }
    }

    void Update()
    {
        if (userTransform == null || !destinationInitialized)
            return;

        if (HuntLocationGuide.Active && !HuntLocationGuide.AtDestination)
        {
            if (!HuntLocationGuide.HasFix) return;
            var location = HuntLocationGuide.Current;
            destinationName = location.siteName;
            remainingDistanceMeters = location.distanceMeters;
            remainingDistanceKm = remainingDistanceMeters / 1000f;
            etaMinutes = Mathf.CeilToInt(remainingDistanceKm / 4f * 60f);
            hasArrived = false;
            fixedDestination = userTransform.position + HuntLocationGuide.Direction * 6f;
            fixedDestination.y = destinationGroundY;
            if (destinationVisual != null) destinationVisual.position = fixedDestination;
            return;
        }

        UpdateNavigationData();

        if (destinationVisual != null)
        {
            destinationVisual.gameObject.SetActive(true);
            destinationVisual.position = fixedDestination;
        }
    }

    void UpdateNavigationData()
    {
        if (hasArrived)
            return;

        Vector3 userPos = userTransform.position;
        Vector3 destinationPos = fixedDestination;

        // Ground distance only
        userPos.y = 0f;
        destinationPos.y = 0f;

        remainingDistanceMeters = Vector3.Distance(userPos, destinationPos);
        float demoScale = 10f; // 6m → 60m → 0.06 km
        remainingDistanceKm = (remainingDistanceMeters * demoScale) / 1000f;

        if (hasLastPosition)
        {
            Vector3 lastFlat = lastUserPosition;
            Vector3 currentFlat = userTransform.position;

            lastFlat.y = 0f;
            currentFlat.y = 0f;

            float segmentMeters = Vector3.Distance(lastFlat, currentFlat);

            if (segmentMeters > 0.02f)
            {
                walkedDistanceKm += segmentMeters / 1000f;
            }
        }

        lastUserPosition = userTransform.position;
        hasLastPosition = true;

        etaMinutes = Mathf.RoundToInt((remainingDistanceKm / walkingSpeedKmH) * 60f);
        steps = Mathf.RoundToInt((walkedDistanceKm * 1000f) / 0.75f);

        CheckArrival();
    }

    void CheckArrival()
    {
        if (remainingDistanceMeters <= arrivalThresholdMeters)
        {
            hasArrived = true;
            remainingDistanceMeters = 0f;
            remainingDistanceKm = 0f;
            etaMinutes = 0;
            currentAlert = "You are here!";
        }
        else
        {
            currentAlert = "";
        }
    }
}
