using UnityEngine;
using NRKernal;

public class HideHandLaser : MonoBehaviour
{
    void Start()
    {
        NRInput.LaserVisualActive = false;
    }
}