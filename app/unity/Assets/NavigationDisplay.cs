using TMPro;
using UnityEngine;

public class NavigationDisplay : MonoBehaviour
{
    public NavigationSystem navigationSystem;

    public TMP_Text destinationText;
    public TMP_Text remainText;
    public TMP_Text etaText;
    public TMP_Text walkedText;
    public TMP_Text stepsText;
    public TMP_Text alertText;

    void Update()
    {
        if (navigationSystem == null)
            return;

        if (destinationText != null)
        {
            if (navigationSystem.hasArrived)
                destinationText.text = "Dest: Arrived";
            else
                destinationText.text = "Dest: " + navigationSystem.destinationName;
        }

        if (remainText != null)
            remainText.text = navigationSystem.remainingDistanceKm.ToString("0.00") + " km";

        if (etaText != null)
            etaText.text = "ETA: " + navigationSystem.etaMinutes + " min";

        if (walkedText != null)
            walkedText.text = navigationSystem.walkedDistanceKm.ToString("0.00") + " km";

        if (stepsText != null)
            stepsText.text = navigationSystem.steps.ToString("N0") + " steps";

        if (alertText != null)
            alertText.text = navigationSystem.currentAlert;
    }
}