using UnityEngine;

public class EditorHeadSimulator : MonoBehaviour
{
#if UNITY_EDITOR

    [Header("Editor Movement")]
    public float lookSpeed = 2f;
    public float moveSpeed = 2f;

    private float yaw;
    private float pitch;

    void Start()
    {
        Vector3 startRotation = transform.eulerAngles;

        yaw = startRotation.y;
        pitch = startRotation.x;
    }

    void Update()
    {
        // Hold RIGHT mouse button and move mouse to look around.
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSpeed;
            pitch -= Input.GetAxis("Mouse Y") * lookSpeed;

            pitch = Mathf.Clamp(pitch, -80f, 80f);

            transform.rotation =
                Quaternion.Euler(pitch, yaw, 0f);
        }

        // WASD movement.
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement =
            transform.right * horizontal +
            transform.forward * vertical;

        transform.position +=
            movement * moveSpeed * Time.deltaTime;

        // Q = down
        if (Input.GetKey(KeyCode.Q))
        {
            transform.position +=
                Vector3.down * moveSpeed * Time.deltaTime;
        }

        // E = up
        if (Input.GetKey(KeyCode.E))
        {
            transform.position +=
                Vector3.up * moveSpeed * Time.deltaTime;
        }
    }

#endif
}