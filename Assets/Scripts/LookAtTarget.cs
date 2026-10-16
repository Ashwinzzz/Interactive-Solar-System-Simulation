using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Camera rig. Click a body to focus it, scroll to zoom in and out anywhere in open space,
/// drag to orbit, number keys 0-9 to jump between bodies, Home to see the whole system.
/// </summary>
[RequireComponent(typeof(Camera))]
public class LookAtTarget : MonoBehaviour
{
    [Tooltip("This is the object that the script's game object will look at by default")]
    public Transform defaultTarget;

    [Tooltip("This is the object that the script's game object is currently look at based on the player clicking on a gameObject")]
    public Transform currentTarget;

    [Header("Zoom")]
    public float minDistanceMultiplier = 2.6f;   // closest approach, in body radii
    public float maxDistance = 480f;
    public float zoomSensitivity = 0.14f;
    public float zoomSmoothing = 7f;

    [Header("Orbit")]
    public float orbitSensitivity = 0.22f;
    public float focusSmoothing = 3.2f;

    [Header("Start")]
    public float startDistance = 135f;
    public float startYaw = 20f;
    public float startPitch = 24f;

    public System.Action<CelestialBody> TargetChanged;

    float distance;
    float desiredDistance;
    float yaw, pitch;
    Vector3 focusPoint;
    bool aligning;           // gliding to the sunlit side of a newly focused body
    float alignYaw, alignPitch;
    Vector3 focusOffset;     // decays to zero, so the camera glides to a new target and then follows it exactly
    float focusRadius = 12f;
    Camera cam;

    // click vs drag
    Vector2 pressPos;
    bool pressing;
    bool dragged;
    int pressButton = -1;

    public float Distance => desiredDistance;
    public float MaxDistance => maxDistance;

    void Awake()
    {
        cam = GetComponent<Camera>();
        yaw = startYaw;
        pitch = startPitch;
        distance = desiredDistance = startDistance;
    }

    void Start()
    {
        if (defaultTarget == null)
        {
            var any = FindFirstObjectByType<CelestialBody>();
            if (any != null) defaultTarget = any.transform;
        }
        if (currentTarget == null) currentTarget = defaultTarget;
        if (currentTarget != null)
        {
            focusPoint = currentTarget.position;
            var b = currentTarget.GetComponent<CelestialBody>();
            if (b != null) focusRadius = b.radius;
        }
        Debug.Log("defaultTarget changed to " + (defaultTarget != null ? defaultTarget.name : "none"));
        ApplyPose(true);
    }

    void Update()
    {
        HandleKeys();
        HandleMouse();
        HandleZoom();
        ApplyPose(false);
    }

    // ---------------------------------------------------------------- input

    void HandleKeys()
    {
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                FocusIndex(i);
        }
        if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.O))
            ShowWholeSystem();
        if (Input.GetKeyDown(KeyCode.Escape))
            ShowWholeSystem();
    }

    void FocusIndex(int index)
    {
        // 0 = Sun, 1..8 = planets in the order they were created
        var ordered = new System.Collections.Generic.List<CelestialBody>();
        foreach (var b in CelestialBody.All)
        {
            if (b.transform.parent != null && b.transform.parent.GetComponent<CelestialBody>() != null) continue;   // skip moons
            ordered.Add(b);
        }
        Vector3 sun = defaultTarget != null ? defaultTarget.position : Vector3.zero;
        ordered.Sort((a, b) => Vector3.Distance(a.transform.position, sun).CompareTo(Vector3.Distance(b.transform.position, sun)));
        CelestialBody found = index < ordered.Count ? ordered[index] : null;
        if (found != null) SetTarget(found.transform);
    }

    void HandleMouse()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        for (int b = 0; b < 3; b++)
        {
            if (b != 1 && Input.GetMouseButtonDown(b) && !overUI)   // right button is reserved for firing comets
            {
                pressing = true; dragged = false; pressPos = Input.mousePosition; pressButton = b;
            }
        }

        if (pressing && Input.GetMouseButton(pressButton))
        {
            Vector2 delta = (Vector2)Input.mousePosition - pressPos;
            if (!dragged && delta.sqrMagnitude > 25f) dragged = true;
            if (dragged)
            {
                Vector2 d = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                yaw += d.x * orbitSensitivity * 10f;
                pitch -= d.y * orbitSensitivity * 10f;
                pitch = Mathf.Clamp(pitch, -85f, 85f);
                aligning = false;
            }
        }

        if (pressing && Input.GetMouseButtonUp(pressButton))
        {
            if (!dragged && pressButton == 0) TryPick(Input.mousePosition);
            pressing = false;
            pressButton = -1;
        }
    }

    void TryPick(Vector2 screenPos)
    {
        // Pick in screen space: the body whose on-screen disc (with a generous minimum size) is closest to the click.
        CelestialBody best = null;
        float bestScore = float.MaxValue;
        float pxPerUnitAtUnitDistance = Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        foreach (var b in CelestialBody.All)
        {
            Vector3 sp = cam.WorldToScreenPoint(b.transform.position);
            if (sp.z <= 0f) continue;
            float discPx = b.radius * pxPerUnitAtUnitDistance / sp.z;
            float hitPx = Mathf.Max(discPx * 1.05f, Screen.height * 0.022f);
            float d = Vector2.Distance(new Vector2(sp.x, sp.y), screenPos);
            if (d > hitPx) continue;
            float score = d / hitPx;
            if (score < bestScore) { bestScore = score; best = b; }
        }
        if (best != null) SetTarget(best.transform);
    }

    void HandleZoom()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        float scroll = overUI ? 0f : Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            // WebGL reports very small wheel deltas, desktop reports whole notches: normalise both to a similar feel
            float step = Mathf.Sign(scroll) * Mathf.Clamp(Mathf.Abs(scroll) * 10f, 0.15f, 3f);
            ZoomBy(step);
        }

        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.PageUp))
            ZoomBy(2.2f * Time.deltaTime * 6f);
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.PageDown))
            ZoomBy(-2.2f * Time.deltaTime * 6f);

        distance = Mathf.Lerp(distance, desiredDistance, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
    }

    /// <summary>Positive = zoom in, negative = zoom out. Used by scroll wheel, keys and UI buttons.</summary>
    public void ZoomBy(float amount)
    {
        desiredDistance *= Mathf.Exp(-amount * zoomSensitivity);
        desiredDistance = Mathf.Clamp(desiredDistance, MinDistance, maxDistance);
    }

    float MinDistance => Mathf.Max(1.2f, focusRadius * minDistanceMultiplier);

    // --------------------------------------------------------------- focusing

    public void SetTarget(Transform t)
    {
        if (t == null) return;
        focusOffset = focusPoint - t.position;
        currentTarget = t;
        var body = t.GetComponent<CelestialBody>();
        focusRadius = body != null ? body.radius : 1f;
        // bring the camera to a comfortable framing distance for the chosen body
        desiredDistance = Mathf.Clamp(focusRadius * 7.5f, MinDistance, maxDistance);
        if (body != null && t == defaultTarget) desiredDistance = Mathf.Max(desiredDistance, 60f);
        if (defaultTarget != null && t != defaultTarget)
        {
            // settle the camera on the sunlit side of the body, a little off-axis so a soft terminator shows
            Vector3 toSun = defaultTarget.position - t.position; toSun.y = 0f;
            if (toSun.sqrMagnitude > 0.01f)
            {
                Vector3 dir = Quaternion.AngleAxis(38f, Vector3.up) * toSun.normalized;
                alignYaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                alignPitch = 13f;
                aligning = true;
            }
        }
        Debug.Log("defaultTarget changed to " + t.name);
        TargetChanged?.Invoke(body);
    }

    public void ShowWholeSystem()
    {
        if (defaultTarget != null)
        {
            focusOffset = focusPoint - defaultTarget.position;
            currentTarget = defaultTarget;
            var b = defaultTarget.GetComponent<CelestialBody>();
            focusRadius = b != null ? b.radius : 10f;
            TargetChanged?.Invoke(b);
        }
        desiredDistance = startDistance * 2.2f;
        alignYaw = startYaw; alignPitch = startPitch; aligning = true;
    }

    void ApplyPose(bool instant)
    {
        if (currentTarget != null)
        {
            focusOffset = instant ? Vector3.zero : focusOffset * Mathf.Exp(-focusSmoothing * Time.deltaTime);
            focusPoint = currentTarget.position + focusOffset;
        }
        if (aligning)
        {
            float k = instant ? 1f : 1f - Mathf.Exp(-2.4f * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, alignYaw, k);
            pitch = Mathf.Lerp(pitch, alignPitch, k);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, alignYaw)) < 0.15f && Mathf.Abs(pitch - alignPitch) < 0.15f) aligning = false;
        }
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = focusPoint + rot * new Vector3(0f, 0f, -distance);
        transform.rotation = rot;
    }
}
