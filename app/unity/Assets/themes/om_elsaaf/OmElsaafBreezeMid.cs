using UnityEngine;


/// <summary>
/// Sets the appearance of Om Elsaaf's main sandy breeze.
/// This layer carries most of the visible wind movement.
/// </summary>
[AddComponentMenu("MasAR+/Om Elsaaf/Breeze Mid")]
public class OmElsaafBreezeMid : OmElsaafBreezeLayerBase
{
    /// <summary>
    /// Sets the default colors, movement, and transparency for this layer.
    /// </summary>
    private void Reset()
    {
        sandColor = new Color32(190, 163, 124, 255);
        lightColor = new Color32(229, 211, 180, 255);
        visibility = 0.22f;
        ribbonThickness = 59f;
        heightAboveBottom = 140f;
        curveAmplitude = 17f;
        centerStrength = 0.65f;
        edgeFeather = 0.14f;
        paleStreakStrength = 0.25f;
        secondsAcrossScreen = 17f;
        gustWidth = 0.32f;
        phaseOffset = 0.72f;
        curveCycles = 1.45f;
        segments = 64;
        color = Color.white;
        raycastTarget = false;
    }
}
