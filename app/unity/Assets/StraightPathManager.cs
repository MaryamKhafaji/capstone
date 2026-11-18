using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StraightPathManager : MonoBehaviour
{
    [Header("References")]
    public GameObject arrowPrefab;
    public Transform cameraTransform;
    public NavigationSystem navigationSystem;

    [Header("Turn UI")]
    public GameObject leftArrowUI;
    public GameObject rightArrowUI;

    [Header("Path Settings")]
    public int arrowCount = 4;
    public float spacing = 0.8f;
    public float recycleDistance = 0.2f;

    [Header("Ground Reference")]
    public float groundOffsetFromCamera = -0.8f;

    [Header("Turn Logic")]
    public float frontVisibleAngle = 35f;

    [Header("Arrow Rotation Offset")]
    public Vector3 arrowRotationOffset = new Vector3(90f, 0f, 0f);

    [Header("Rebuild Tuning")]
    public float directionRebuildThreshold = 5f;

    private List<GameObject> arrows = new List<GameObject>();
    private Vector3 pathDirection = Vector3.forward;
    private bool pathInitialized = false;
    private bool pathVisible = false;
    private Vector3 lastPathDirection = Vector3.zero;

    void Start()
    {
        SpawnArrows();
        HideAllTurnArrows();
        HidePath();
        StartCoroutine(InitializePathAfterDelay());
    }

    IEnumerator InitializePathAfterDelay()
    {
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);
        pathInitialized = true;
    }

    void Update()
    {
        if (!pathInitialized || cameraTransform == null || navigationSystem == null)
            return;

        // Hide everything once arrived
        if (navigationSystem.hasArrived)
        {
            HidePath();
            HideAllTurnArrows();
            return;
        }

        UpdateNavigationState();
    }

    Vector3 GetUserGroundPosition()
    {
        return cameraTransform.position + Vector3.up * groundOffsetFromCamera;
    }

    void UpdateNavigationState()
    {
        Vector3 userForward = cameraTransform.forward;
        userForward.y = 0f;
        userForward.Normalize();

        Vector3 userGroundPos = GetUserGroundPosition();

        Vector3 toDestination = navigationSystem.fixedDestination - userGroundPos;
        toDestination.y = 0f;

        if (toDestination.sqrMagnitude < 0.001f)
            return;

        toDestination.Normalize();

        float signedAngle = Vector3.SignedAngle(userForward, toDestination, Vector3.up);

        // Destination is visible enough in front
        if (Mathf.Abs(signedAngle) <= frontVisibleAngle)
        {
            pathDirection = toDestination;
            HideAllTurnArrows();

            if (!pathVisible)
            {
                ShowPath();
                RebuildPath();
                lastPathDirection = pathDirection;
            }
            else
            {
                // Only rebuild fully if direction changed enough
                float dirChange = Vector3.Angle(lastPathDirection, pathDirection);

                if (dirChange > directionRebuildThreshold)
                {
                    RebuildPath();
                    lastPathDirection = pathDirection;
                }
                else
                {
                    UpdateArrowRotations();
                    RecycleArrows();
                }
            }
        }
        else
        {
            HidePath();

            if (signedAngle < 0f)
                ShowLeftArrow();
            else
                ShowRightArrow();
        }
    }

    void SpawnArrows()
    {
        ClearArrows();

        if (arrowPrefab == null)
            return;

        for (int i = 0; i < arrowCount; i++)
        {
            GameObject arrow = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity, transform);
            arrows.Add(arrow);
        }
    }

    void PlaceInitialArrows()
    {
        if (cameraTransform == null || arrows.Count == 0)
            return;

        Vector3 startPos = GetUserGroundPosition();

        for (int i = 0; i < arrows.Count; i++)
        {
            Vector3 pos = startPos + pathDirection * (i * spacing);
            arrows[i].transform.position = pos;
            ApplyArrowRotation(arrows[i]);
        }
    }

    void ApplyArrowRotation(GameObject arrow)
    {
        if (arrow == null) return;

        arrow.transform.rotation =
            Quaternion.LookRotation(pathDirection) *
            Quaternion.Euler(arrowRotationOffset);
    }

    void UpdateArrowRotations()
    {
        foreach (GameObject arrow in arrows)
        {
            if (arrow != null && arrow.activeSelf)
                ApplyArrowRotation(arrow);
        }
    }

    void RecycleArrows()
    {
        Vector3 userPos = GetUserGroundPosition();

        foreach (GameObject arrow in arrows)
        {
            if (arrow == null || !arrow.activeSelf)
                continue;

            Vector3 toArrow = arrow.transform.position - userPos;
            float forwardDot = Vector3.Dot(toArrow, pathDirection);

            // If arrow is under / behind the user, move it to the front
            if (forwardDot < recycleDistance)
            {
                MoveToFront(arrow);
            }
        }
    }

    void MoveToFront(GameObject arrow)
    {
        GameObject front = GetFrontArrow();
        if (front == null || arrow == null)
            return;

        arrow.transform.position = front.transform.position + pathDirection * spacing;
        ApplyArrowRotation(arrow);
    }

    GameObject GetFrontArrow()
    {
        GameObject best = null;
        float maxDot = float.MinValue;
        Vector3 userPos = GetUserGroundPosition();

        foreach (GameObject arrow in arrows)
        {
            if (arrow == null || !arrow.activeSelf)
                continue;

            Vector3 toArrow = arrow.transform.position - userPos;
            float dot = Vector3.Dot(toArrow, pathDirection);

            if (dot > maxDot)
            {
                maxDot = dot;
                best = arrow;
            }
        }

        return best;
    }

    public void RebuildPath()
    {
        PlaceInitialArrows();
        UpdateArrowRotations();
    }

    void ShowPath()
    {
        pathVisible = true;

        foreach (GameObject arrow in arrows)
        {
            if (arrow != null)
                arrow.SetActive(true);
        }
    }

    void HidePath()
    {
        pathVisible = false;

        foreach (GameObject arrow in arrows)
        {
            if (arrow != null)
                arrow.SetActive(false);
        }
    }

    void ShowLeftArrow()
    {
        if (leftArrowUI != null) leftArrowUI.SetActive(true);
        if (rightArrowUI != null) rightArrowUI.SetActive(false);
    }

    void ShowRightArrow()
    {
        if (leftArrowUI != null) leftArrowUI.SetActive(false);
        if (rightArrowUI != null) rightArrowUI.SetActive(true);
    }

    void HideAllTurnArrows()
    {
        if (leftArrowUI != null) leftArrowUI.SetActive(false);
        if (rightArrowUI != null) rightArrowUI.SetActive(false);
    }

    void ClearArrows()
    {
        foreach (GameObject arrow in arrows)
        {
            if (arrow != null)
                Destroy(arrow);
        }

        arrows.Clear();
    }
}