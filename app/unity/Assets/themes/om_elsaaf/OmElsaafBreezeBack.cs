using UnityEngine;


/// <summary>
/// Sets the colors and movement of Om Elsaaf's background breeze.
/// It adds a wide, faint layer of drifting sand near the bottom of the HUD.
/// </summary>
[AddComponentMenu("MasAR+/Om Elsaaf/Breeze Back")]
public class OmElsaafBreezeBack : OmElsaafBreezeLayerBase
{
    /// <summary>
    /// Sets the default colors, movement, and transparency for this layer.
    /// </summary>
    private void Reset()
    {
        sandColor = new Color32(162, 147, 121, 255);
        lightColor = new Color32(213, 201, 178, 255);
        visibility = 0.16f;
        ribbonThickness = 86f;
        heightAboveBottom = 90f;
        curveAmplitude = 12f;
        centerStrength = 0.60f;
        edgeFeather = 0.16f;
        paleStreakStrength = 0.12f;
        secondsAcrossScreen = 23f;
        gustWidth = 0.40f;
        phaseOffset = 0.24f;
        curveCycles = 1.15f;
        segments = 64;
        color = Color.white;
        raycastTarget = false;
    }
}
