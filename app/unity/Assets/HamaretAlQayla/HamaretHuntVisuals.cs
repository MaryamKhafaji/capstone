using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class HamaretHuntVisuals
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private readonly Transform root;
    private readonly Transform figure;
    private readonly HamaretRigPlayback rig;
    private readonly List<Material> materials = new List<Material>();
    private Mesh quad;
    private Renderer groundRenderer;
    private Texture2D owners;
    private readonly Color32[] pixels = new Color32[HamaretGroundTraversal.Resolution * HamaretGroundTraversal.Resolution];
    private int shownRevision = -1;
    private bool walking;
    private float walkDistance;
    internal HamaretHuntVisuals(Transform parent)
    {
        root = new GameObject("Hamaret world visuals").transform;
        root.SetParent(parent, false);
        figure = new GameObject("Hamaret character").transform;
        figure.SetParent(root, false);
        rig = new HamaretRigPlayback(figure);
    }

    internal void PlaceFigure(Vector3 position, Vector3 viewer)
    {
        if (walking)
            walkDistance += Vector3.Distance(figure.position, position);
        figure.position = position;
        Vector3 direction = Vector3.ProjectOnPlane(viewer - position, Vector3.up);
        if (direction.sqrMagnitude > 0.01f)
            figure.rotation = Quaternion.RotateTowards(figure.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 270f);
    }

    internal void CreateCourtyard(Vector3 start, Vector3 forward, float width, Shader shader)
    {
        var ground = new GameObject("Continuous ground ownership");
        ground.transform.SetParent(root, false);
        ground.transform.SetPositionAndRotation(start + forward * (width * 1.5f) + Vector3.up * .021f,
            Quaternion.LookRotation(forward) * Quaternion.Euler(90f, 0f, 0f));
        ground.transform.localScale = new Vector3(width * 3f, width * 3f, 1f);
        ground.AddComponent<MeshFilter>().sharedMesh = CourtyardQuad();
        groundRenderer = ground.AddComponent<MeshRenderer>();
        groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        groundRenderer.receiveShadows = false;
        var surface = new Material(shader);
        materials.Add(surface);
        surface.SetColor(ColorId, Color.white);
        surface.SetFloat("_Mode", 8f);
        owners = new Texture2D(HamaretGroundTraversal.Resolution, HamaretGroundTraversal.Resolution, TextureFormat.RGBA32, false, true);
        owners.name = "Live territory ownership";
        owners.wrapMode = TextureWrapMode.Clamp;
        owners.filterMode = FilterMode.Bilinear;
        surface.SetTexture("_Ownership", owners);
        groundRenderer.sharedMaterial = surface;
    }

    internal void UpdateCourtyard(HamaretGroundTraversal board)
    {
        if (shownRevision != board.Revision)
        {
            board.CopyPixels(pixels);
            owners.SetPixels32(pixels);
            owners.Apply(false, false);
            shownRevision = board.Revision;
        }
    }


    internal void SetWalking(bool value) => walking = value;
    internal void FadeFigure(float progress)
    {
        rig?.Dissolve(progress, figure.position.y);
        if (progress >= 1f)
            figure.gameObject.SetActive(false);
    }

    internal void Animate()
    {
        if (figure.gameObject.activeSelf)
            rig.Pose(0f, 0f, walking, walkDistance);
    }
    private Mesh CourtyardQuad()
    {
        if (quad != null)
            return quad;
        quad = new Mesh { name = "Courtyard surface" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
            new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.RecalculateNormals();
        return quad;
    }

    internal void Dispose()
    {
        rig?.Dispose();
        if (owners != null)
            Object.Destroy(owners);
        if (quad != null)
            Object.Destroy(quad);
        root.gameObject.SetActive(false);
        Object.Destroy(root.gameObject);
        foreach (var material in materials)
            Object.Destroy(material);
    }
}
