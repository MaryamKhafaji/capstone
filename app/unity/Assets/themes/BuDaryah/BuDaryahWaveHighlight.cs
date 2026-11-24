using UnityEngine;


/// <summary>
/// Sets the appearance of the pale foam on Bu Daryah's waves.
/// The foam becomes wider and brighter as a wave reaches the shore.
/// </summary>
[AddComponentMenu("MasAR+/Bu Daryah/Wave Highlight")]
public class BuDaryahWaveHighlight : BuDaryahWaveLayerBase
{
    /// <summary>
    /// Applies the default wave settings when the component is added.
    /// </summary>
    private void Reset() => ApplyShorelineDefaults();

    /// <summary>
    /// Sets the colors, shape, speed, and transparency for this wave layer.
    /// </summary>
    [ContextMenu("Apply Shoreline Defaults")]
    private void ApplyShorelineDefaults()
    {
        seaColor = new Color32(174, 206, 214, 255);
        reflectionColor = new Color32(228, 235, 233, 255);
        visibility = 0.30f;
        waterHeight = 100f;
        frontRestingHeight = 58f;
        curvedSurface = 7.5f;
        crestVisibility = 0.58f;
        crestWidth = 3f;
        foamExtraWidthAtShore = 18f;
        foamOpacityBoostAtShore = 0.75f;
        foamStartsAtTide = 0.50f;
        centerStrength = 0.70f;
        secondsPerCycle = 6f;
        approachDistance = 38f;
        phaseOffset = 0.13f;
        waveCycles = 1.45f;
        segments = 80;
        RefreshWave();
    }
}
