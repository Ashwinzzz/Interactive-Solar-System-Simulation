using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Hold Space (or the right mouse button) to shoot comets toward the point under the cursor.</summary>
public class Spawner : MonoBehaviour
{
    [Tooltip("The Prefab to be spawned into the scene.")]
    public GameObject spawnPrefab;

    [Tooltip("The time between spawns")]
    public float spawnTime = 0.18f;

    float nextSpawn;

    void Update()
    {
        if (spawnPrefab == null) return;
        bool fire = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(1) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1);
        if (!fire || Time.time < nextSpawn) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var cam = Camera.main;
        if (cam == null) return;
        nextSpawn = Time.time + spawnTime;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 aim = ray.direction;
        var hits = Physics.RaycastAll(ray, 2000f);
        float best = float.MaxValue;
        foreach (var h in hits) if (h.distance < best) { best = h.distance; aim = (h.point - (cam.transform.position + cam.transform.right * 0.6f - cam.transform.up * 0.5f)).normalized; }

        Vector3 origin = cam.transform.position + cam.transform.right * 0.6f - cam.transform.up * 0.5f + cam.transform.forward * 1.5f;
        var go = Instantiate(spawnPrefab, origin, Quaternion.LookRotation(aim));
        var p = go.GetComponent<Projectile>();
        if (p != null) p.heading = aim;
    }
}
