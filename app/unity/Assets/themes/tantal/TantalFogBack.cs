using UnityEngine;


/// <summary>
/// Sets the appearance of Tantal's distant background fog.
/// This wide, slow-moving layer gives the theme a soft shadowy base.
/// </summary>
[AddComponentMenu("MasAR+/Tantal/Fog Back")]
public class TantalFogBack : TantalFogLayerBase
{
    /// <summary>
    /// Sets the default colors, movement, and transparency for this layer.
    /// </summary>
    private void Reset()
    {
        darkFog = new Color32(38, 40, 46, 255);
        mistFog = new Color32(95, 97, 105, 255);
        visibility = 0.15f;
        fogThickness = 145f;
        heightAboveBottom = 55f;
        shapeVariation = 18f;
        centerStrength = 0.40f;
        edgeFeather = 0.19f;
        fogBands = 2;
        spacingBetweenBands = 35f;
        secondsAcrossScreen = 35f;
        cloudWidth = 0.49f;
        phaseOffset = 0.48f;
        curveCycles = 0.95f;
        segments = 64;
        color = Color.white;
        raycastTarget = false;
    }
}
