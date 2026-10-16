using UnityEngine;

/// <summary>Keeps an orbit ring a roughly constant on-screen thickness however far the camera is.</summary>
[RequireComponent(typeof(LineRenderer))]
public class OrbitLine : MonoBehaviour
{
    public float orbitRadius = 50f;
    public float minWidth = 0.004f;
    public float maxWidth = 0.9f;
    [Tooltip("World width per world unit of distance to the nearest point of the ring (about 2px on screen)")]
    public float pixelFactor = 0.0024f;

    LineRenderer line;
    Camera cam;

    void Awake() { line = GetComponent<LineRenderer>(); }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position - transform.position;
        Vector3 flat = new Vector3(p.x, 0f, p.z);
        Vector3 nearestOnRing = flat.sqrMagnitude > 0.0001f ? flat.normalized * orbitRadius : new Vector3(orbitRadius, 0, 0);
        float d = Mathf.Max((p - nearestOnRing).magnitude, 1f);
        line.widthMultiplier = Mathf.Clamp(d * pixelFactor, minWidth, maxWidth);
    }
}
