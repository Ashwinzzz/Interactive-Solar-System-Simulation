using UnityEngine;

/// <summary>Moves this object along a circular orbit around <see cref="target"/>.</summary>
public class RotateAround : MonoBehaviour
{
    [Tooltip("This is the object that the script's game object will rotate around")]
    public Transform target;

    [Tooltip("This is the speed at which the object rotates (degrees per second)")]
    public float speed = 10f;

    public Vector3 axis = Vector3.up;

    [Tooltip("Starting angle on the orbit, in degrees")]
    public float startAngle;

    float radius;
    Vector3 startDirection;
    float angle;
    bool local;   // orbiting our own parent (e.g. the Moon around Earth): work in local space so there is no frame lag

    void Awake()
    {
        if (target == null)
        {
            Debug.Log("RotateAround target not specified. Defaulting to this GameObject");
            target = transform;
        }
    }

    void Start()
    {
        local = target == transform.parent;
        Vector3 offset = local ? transform.localPosition : transform.position - target.position;
        offset = Vector3.ProjectOnPlane(offset, axis.normalized);
        radius = offset.magnitude;
        startDirection = radius > 0.0001f ? offset / radius : Vector3.right;
        angle = startAngle;
        Apply();
    }

    void Update()
    {
        if (target == transform) return;
        angle += speed * Time.deltaTime;
        Apply();
    }

    void Apply()
    {
        Vector3 dir = Quaternion.AngleAxis(angle, axis.normalized) * startDirection;
        if (local) transform.localPosition = dir * radius;
        else transform.position = target.position + dir * radius;
    }
}
