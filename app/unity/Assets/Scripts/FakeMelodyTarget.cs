using UnityEngine;

// Attach to GameplayRoot/BuDaryaHunt/FakeMelody.
// The AudioSource remains on FakeMelody; the assigned visual is its child.
public class FakeMelodyTarget : MonoBehaviour
{
    [Header("Fake Target Visual")]
    [Tooltip("Assign a COPY of DestinationVisual placed under FakeMelody. Do not use the original navigation object.")]
    public GameObject fakeVisual;
    [Tooltip("Adjust the marker's height relative to the fake melody emitter.")]
    public Vector3 visualLocalOffset = Vector3.zero;

    public void PlaceAt(Vector3 worldPosition)
    {
        transform.position = worldPosition;

        if (fakeVisual == null)
        {
            Debug.LogWarning("FakeMelodyTarget: Assign a fake visual under FakeMelody.", this);
            return;
        }

        if (fakeVisual.transform.parent != transform)
        {
            Debug.LogError("FakeMelodyTarget: Fake visual must be a child of FakeMelody (use a duplicate).", this);
            return;
        }

        fakeVisual.transform.localPosition = visualLocalOffset;
        fakeVisual.SetActive(true);
    }

    public void HideVisual()
    {
        if (fakeVisual != null)
            fakeVisual.SetActive(false);
    }
}
