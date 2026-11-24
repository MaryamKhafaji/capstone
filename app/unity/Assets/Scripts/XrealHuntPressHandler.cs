using NRKernal;
using UnityEngine;
using UnityEngine.EventSystems;

// Pinches are often released after the hand ray has drifted off the button.
// Start the selected hunt on the XREAL press; the UI controller prevents a
// second start if the normal click event also fires on release.
public sealed class XrealHuntPressHandler : MonoBehaviour, IPointerDownHandler
{
    internal MonsterHuntUIController huntUI;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData is NRPointerEventData && huntUI != null)
            huntUI.AcceptHunt();
    }
}
