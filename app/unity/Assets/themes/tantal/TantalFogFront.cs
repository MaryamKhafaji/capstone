using UnityEngine;


/// <summary>
/// Sets the appearance of Tantal's thin foreground fog.
/// Its lighter wisps add movement without blocking the player's view.
/// </summary>
[AddComponentMenu("MasAR+/Tantal/Fog Front")]
public class TantalFogFront : TantalFogLayerBase
{
    /// <summary>
    /// Sets the default colors, movement, and transparency for this layer.
    /// </summary>
    private void Reset()
    {
        darkFog = new Color32(47, 49, 57, 255);
        mistFog = new Color32(143, 146, 153, 255);
        visibility = 0.12f;
        fogThickness = 76f;
        heightAboveBottom = 144f;
        shapeVariation = 18f;
        centerStrength = 0.42f;
        edgeFeather = 0.17f;
        fogBands = 2;
        spacingBetweenBands = 25f;
        secondsAcrossScreen = 21f;
        cloudWidth = 0.27f;
        phaseOffset = 0.81f;
        curveCycles = 1.60f;
        segments = 64;
        color = Color.white;
        raycastTarget = false;
    }
}
