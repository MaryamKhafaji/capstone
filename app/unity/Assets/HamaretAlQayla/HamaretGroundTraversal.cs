using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class HamaretGroundTraversal
{
    internal const int Resolution = 144;
    private const int Area = Resolution * Resolution;
    private readonly byte[] owners = new byte[Area];
    private readonly int[] counts = { Area, 0, 0 };
    private readonly Trail[] trails = { null, new Trail(), new Trail() };
    private readonly bool[] before = new bool[Area];
    private readonly bool[] after = new bool[Area];
    private readonly int[] queue = new int[Area];

    private sealed class Trail
    {
        internal readonly bool[] Cells = new bool[Area];
        internal readonly List<int> Active = new List<int>();
        internal Vector2? Previous;
        internal bool Armed;
    }

    internal int Revision { get; private set; }
    internal int Pending => trails[1].Active.Count;
    internal int LastBank { get; private set; }
    internal byte Winner { get; private set; }
    private byte cutSide;
    internal float LightShare => counts[1] / (float)Area;
    internal float DarkShare => counts[2] / (float)Area;
    internal byte OwnerAt(Vector2 point) => owners[Cell(point)];

    internal void Seed(Vector2 center, byte side)
    {
        const float radius = .078f;
        for (int y = 0; y < Resolution; y++)
        for (int x = 0; x < Resolution; x++)
        {
            if ((Center(x + y * Resolution) - center).sqrMagnitude <= radius * radius)
                Assign(x + y * Resolution, side);
        }
        Revision++;
    }

    internal Vector2 NearestOwned(Vector2 point, byte side)
    {
        if (OwnerAt(point) == side) return point;
        float best = float.PositiveInfinity;
        Vector2 nearest = point;
        for (int i = 0; i < Area; i++)
        {
            if (owners[i] != side) continue;
            Vector2 candidate = Center(i);
            float distance = (candidate - point).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            nearest = candidate;
        }
        return nearest;
    }

    internal int Advance(Vector2 point, byte side)
    {
        if (Winner != 0) return 0;
        Trail trail = trails[side];
        byte opponent = (byte)(3 - side);
        Vector2? previous = trail.Previous;
        trail.Previous = point;
        if (!previous.HasValue)
        {
            trail.Armed = OwnerAt(point) == side;
            return 0;
        }

        Vector2 delta = point - previous.Value;
        int steps = Mathf.CeilToInt(delta.magnitude * Resolution * 3f);
        int banked = 0;
        int last = Cell(previous.Value);
        for (int step = 1; step <= steps; step++)
        {
            int index = Cell(previous.Value + delta * ((float)step / steps));
            if (index == last) continue;
            last = index;
            if (trails[opponent].Cells[index])
            {
                // Keep the pose and allow a fresh trail from that point.
                Clear(trails[opponent]);
                cutSide = opponent;
            }
            if (owners[index] == side)
            {
                if (trail.Active.Count > 0)
                    banked += Capture(side, trail);
                trail.Armed = true;
                continue;
            }
            if (!trail.Armed || trail.Cells[index]) continue;
            trail.Cells[index] = true;
            trail.Active.Add(index);
            Revision++;
        }
        if (side == 1) LastBank = banked;
        if (counts[opponent] == 0) Winner = side;
        return banked;
    }

    internal byte TakeCutNotice()
    {
        byte notice = cutSide;
        cutSide = 0;
        return notice;
    }

    internal void Interrupt()
    {
        Trail trail = trails[1];
        Clear(trail);
        trail.Previous = null;
        trail.Armed = false;
    }

    private void Clear(Trail trail)
    {
        if (trail.Active.Count == 0) return;
        foreach (int index in trail.Active) trail.Cells[index] = false;
        trail.Active.Clear();
        Revision++;
    }

    private int Capture(byte side, Trail trail)
    {
        MarkOutside(side, null, before);
        MarkOutside(side, trail.Cells, after);
        int changed = 0;
        for (int index = 0; index < Area; index++)
        {
            if (owners[index] == side || (!trail.Cells[index] && (!before[index] || after[index]))) continue;
            Assign(index, side);
            changed++;
        }
        Clear(trail);
        return changed;
    }

    private void MarkOutside(byte side, bool[] trail, bool[] seen)
    {
        Array.Clear(seen, 0, Area);
        int read = 0, write = 0;
        void Visit(int index)
        {
            if (seen[index] || owners[index] == side || (trail != null && trail[index])) return;
            seen[index] = true;
            queue[write++] = index;
        }
        for (int edge = 0; edge < Resolution; edge++)
        {
            Visit(edge);
            Visit(Area - Resolution + edge);
            Visit(edge * Resolution);
            Visit(edge * Resolution + Resolution - 1);
        }
        while (read < write)
        {
            int index = queue[read++];
            int x = index % Resolution;
            if (x > 0) Visit(index - 1);
            if (x < Resolution - 1) Visit(index + 1);
            if (index >= Resolution) Visit(index - Resolution);
            if (index < Area - Resolution) Visit(index + Resolution);
        }
    }

    private void Assign(int index, byte side)
    {
        if (owners[index] == side) return;
        counts[owners[index]]--;
        owners[index] = side;
        counts[side]++;
    }

    internal void CopyPixels(Color32[] destination)
    {
        for (int index = 0; index < Area; index++)
            destination[index] = new Color32(owners[index] == 1 ? (byte)255 : (byte)0,
                trails[1].Cells[index] ? (byte)255 : (byte)0, owners[index] == 2 ? (byte)255 : (byte)0,
                trails[2].Cells[index] ? (byte)255 : (byte)0);
    }

    private static Vector2 Center(int index) =>
        new Vector2((index % Resolution + .5f) / Resolution, (index / Resolution + .5f) / Resolution);
    private static int Cell(Vector2 point) => Coordinate(point.x) + Coordinate(point.y) * Resolution;
    private static int Coordinate(float value) => Mathf.Clamp(Mathf.FloorToInt(value * Resolution), 0, Resolution - 1);
}
