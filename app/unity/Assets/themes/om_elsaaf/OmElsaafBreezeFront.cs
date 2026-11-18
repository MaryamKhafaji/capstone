using UnityEngine;

// Soft pale foreground wisps, still transparent. Attach to OmElsaafTheme > frontBreeze.
[AddComponentMenu("MasAR+/Om Elsaaf/Breeze Front")]
public class OmElsaafBreezeFront : OmElsaafBreezeLayerBase
{
    private void Reset()
    {
        sandColor = new Color32(205, 183, 149, 255);
        lightColor = new Color32(240, 223, 196, 255);
        visibility = 0.17f;
        ribbonThickness = 31f;
        heightAboveBottom = 182f;
        curveAmplitude = 11f;
        centerStrength = 0.48f;
        edgeFeather = 0.13f;
        paleStreakStrength = 0.38f;
        secondsAcrossScreen = 13f;
        gustWidth = 0.25f;
        phaseOffset = 1.15f;
        curveCycles = 1.70f;
        segments = 64;
        color = Color.white;
        raycastTarget = false;
    }
}
