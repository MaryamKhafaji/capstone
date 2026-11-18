using UnityEngine;

public class PreviewAutoRotate : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 0f, 1f);
    [SerializeField] private float rotationSpeed = 35f;

    void Update()
    {
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime, Space.Self);
    }
}