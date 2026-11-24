using UnityEngine;


/// <summary>
/// Sets the colors, thickness, and movement of Tantal's main fog layer.
/// This is the darkest layer and gives the theme most of its shadowy appearance.
/// </summary>
[AddComponentMenu("MasAR+/Tantal/Fog Mid")]
public class TantalFogMid : TantalFogLayerBase
{
    /// <summary>
    /// Sets the default colors, movement, and transparency for this layer.
    /// </summary>
    private void Reset()
    {
        darkFog = new Color32(25, 27, 32, 255);
        mistFog = new Color32(119, 122, 129, 255);
        visibility = 0.20f;
        fogThickness = 112f;
        heightAboveBottom = 95f;
        shapeVariation = 24f;
        centerStrength = 0.53f;
        edgeFeather = 0.17f;
        fogBands = 2;
        spacingBetweenBands = 24f;
        secondsAcrossScreen = 27f;
        cloudWidth = 0.35f;
        phaseOffset = 0.63f;
        curveCycles = 1.25f;
        segments = 72;
        color = Color.white;
        raycastTarget = false;
    }
}
