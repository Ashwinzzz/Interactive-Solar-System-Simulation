using UnityEngine;

/// <summary>Keeps the ring shader aware of its planet so the planet can cast a shadow onto the ring.</summary>
[ExecuteAlways]
public class RingPlanetTracker : MonoBehaviour
{
    public Transform planetCenter;
    public float radius = 1f;
    Renderer rend;
    MaterialPropertyBlock block;

    void LateUpdate()
    {
        if (planetCenter == null) return;
        if (rend == null) rend = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetVector("_PlanetCenter", planetCenter.position);
        block.SetFloat("_PlanetRadius", radius);
        rend.SetPropertyBlock(block);
    }
}
