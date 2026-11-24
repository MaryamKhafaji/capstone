using UnityEngine;


/// <summary>
/// Sets the appearance of Bu Daryah's distant water layer.
/// This wide, faint wave gives depth to the moving sea effect.
/// </summary>
[AddComponentMenu("MasAR+/Bu Daryah/Wave Back")]
public class BuDaryahWaveBack : BuDaryahWaveLayerBase
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
        seaColor = new Color32(117, 155, 171, 255);
        reflectionColor = new Color32(186, 210, 217, 255);
        visibility = 0.24f;
        waterHeight = 170f;
        frontRestingHeight = 78f;
        curvedSurface = 5f;
        crestVisibility = 0.07f;
        crestWidth = 5f;
        centerStrength = 0.72f;
        secondsPerCycle = 6f;
        approachDistance = 39f;
        phaseOffset = -0.22f;
        waveCycles = 1.40f;
        segments = 80;
        RefreshWave();
    }
}
