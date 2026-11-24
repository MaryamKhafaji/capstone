using UnityEngine;


/// <summary>
/// Sets the appearance of Bu Daryah's main moving wave.
/// This layer gives the sea most of its visible motion and color.
/// </summary>
[AddComponentMenu("MasAR+/Bu Daryah/Wave Mid")]
public class BuDaryahWaveMid : BuDaryahWaveLayerBase
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
        seaColor = new Color32(133, 172, 187, 255);
        reflectionColor = new Color32(199, 220, 224, 255);
        visibility = 0.38f;
        waterHeight = 140f;
        frontRestingHeight = 65f;
        curvedSurface = 8f;
        crestVisibility = 0.22f;
        crestWidth = 3.5f;
        centerStrength = 0.77f;
        secondsPerCycle = 6f;
        approachDistance = 38f;
        phaseOffset = 0f;
        waveCycles = 1.65f;
        segments = 80;
        RefreshWave();
    }
}
