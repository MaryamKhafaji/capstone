using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class HamaretHudMist : MaskableGraphic
{
    private float nextFrame;

    private void Update()
    {
        if (Time.unscaledTime < nextFrame)
            return;
        nextFrame = Time.unscaledTime + 0.083f;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect frame = rectTransform.rect;
        float time = Time.unscaledTime * 0.42f;
        AddVeil(mesh, frame, time, 0.10f, new Color(.22f,.209f,.193f,.12f));
        AddVeil(mesh, frame, time + 2.4f, 0.067f, new Color(.44f,.41f,.37f,.09f));
        AddVeil(mesh, frame, time + 4.8f, 0.035f, new Color(.86f,.57f,.31f,.11f));
    }

    private static void AddVeil(VertexHelper mesh, Rect frame, float phase, float height, Color tint)
    {
        int start = mesh.currentVertCount;
        const int segments = 24;
        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            float edge = Mathf.Pow(Mathf.Abs(u * 2f - 1f), 2f);
            float x = Mathf.Lerp(frame.xMin, frame.xMax, u);
            float center = frame.yMin + frame.height *
                (0.018f + edge * height + Mathf.Sin(u * 12f + phase) * 0.009f);
            float thickness = frame.height * (0.014f + edge * height * 0.45f);
            Color glow = tint;
            glow.a *= 0.4f + edge * 0.6f;
            Color transparent = new Color(glow.r, glow.g, glow.b, 0f);
            mesh.AddVert(new Vector3(x, center - thickness), transparent, Vector2.zero);
            mesh.AddVert(new Vector3(x, center), glow, Vector2.zero);
            mesh.AddVert(new Vector3(x, center + thickness), transparent, Vector2.zero);
            if (i == 0)
                continue;
            int current = start + i * 3;
            for (int row = 0; row < 2; row++)
            {
                int v = current + row;
                mesh.AddTriangle(v - 3, v - 2, v);
                mesh.AddTriangle(v - 2, v + 1, v);
            }
        }
    }
}
