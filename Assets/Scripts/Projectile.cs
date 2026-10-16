using UnityEngine;

/// <summary>A glowing comet fired from the camera. It flies straight and removes itself once far away.</summary>
public class Projectile : MonoBehaviour
{
    [Tooltip("The distance this projectile will move each second in meters.")]
    public float projectileSpeed = 70f;

    [Tooltip("How far away from the main camera before destroying the projectile gameobject in meters.")]
    public float destroyDistance = 700f;

    public Vector3 heading = Vector3.forward;

    void Update()
    {
        MoveProjectile();
    }

    void MoveProjectile()
    {
        transform.position += heading * projectileSpeed * Time.deltaTime;
        var cam = Camera.main;
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > destroyDistance)
            Destroy(gameObject);
    }
}
