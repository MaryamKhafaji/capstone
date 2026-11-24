using UnityEngine;

internal sealed class HamaretPreviewPerformance : MonoBehaviour
{
    private HamaretHuntVisuals visuals;
    internal void Initialize(HamaretHuntVisuals model) => visuals = model;
    private void LateUpdate() => visuals?.Animate();
}
