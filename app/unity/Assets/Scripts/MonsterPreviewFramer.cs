using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class MonsterPreviewFramer : MonoBehaviour
{
    [Header("Current model (set by MonsterHuntUIController)")]
    public Transform monsterRoot;

    [Header("Monster intro framing (existing setting)")]
    [Min(0.1f)] public float padding = 1.3f;

    [Header("Reward models — drag from MonsterPreviewStage")]
    [SerializeField] private Transform oudPreview;
    [SerializeField] private Transform saafPreview;

    [Header("Reward framing — higher = smaller on screen")]
    [Min(0.1f)][SerializeField] private float oudPadding = 1.8f;
    [Min(0.1f)][SerializeField] private float saafPadding = 1.8f;

    private Camera previewCamera;

    private void OnEnable()
    {
        previewCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        FrameMonster();
    }

    private float GetPaddingForCurrentModel()
    {
        // The controller already changes monsterRoot when switching previews.
        // Keep normal monster intros on the existing padding; override only rewards.
        if (Matches(monsterRoot, oudPreview)) return oudPadding;
        if (Matches(monsterRoot, saafPreview)) return saafPadding;
        return padding;
    }

    private static bool Matches(Transform current, Transform target)
    {
        return current != null && target != null &&
               (current == target || current.IsChildOf(target));
    }

    private void FrameMonster()
    {
        if (monsterRoot == null) return;

        Renderer[] renderers = monsterRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        if (previewCamera == null)
            previewCamera = GetComponent<Camera>();
        if (previewCamera == null) return;

        float halfVerticalFov = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float verticalDistance = bounds.extents.y / Mathf.Tan(halfVerticalFov);

        float horizontalFov = 2f * Mathf.Atan(
            Mathf.Tan(halfVerticalFov) * previewCamera.aspect);
        float horizontalDistance = bounds.extents.x / Mathf.Tan(horizontalFov * 0.5f);

        float selectedPadding = Mathf.Max(0.1f, GetPaddingForCurrentModel());
        float distance = Mathf.Max(verticalDistance, horizontalDistance) * selectedPadding
                         + bounds.extents.z;

        // Preserve the original camera positioning and facing logic.
        transform.position = bounds.center + Vector3.forward * distance;
        transform.LookAt(bounds.center);
    }
}
