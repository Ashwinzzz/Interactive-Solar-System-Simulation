using UnityEngine;

/// <summary>Spins an object around its own local Y axis (day/night rotation).</summary>
public class Spin : MonoBehaviour
{
    public float degreesPerSecond = 10f;
    public Vector3 localAxis = Vector3.up;

    void Update()
    {
        transform.Rotate(localAxis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
