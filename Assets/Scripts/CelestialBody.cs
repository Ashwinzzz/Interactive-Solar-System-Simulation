using UnityEngine;

/// <summary>Describes a clickable body (Sun, planet or moon) for the camera and the UI.</summary>
public class CelestialBody : MonoBehaviour
{
    public string displayName = "Body";
    [TextArea(2, 6)] public string description = "";
    public string facts = "";
    [Tooltip("Visual radius in world units; the camera never zooms closer than a few radii.")]
    public float radius = 1f;
    [Tooltip("Marker shown on the minimap for this body.")]
    public Transform minimapMarker;

    public static readonly System.Collections.Generic.List<CelestialBody> All = new System.Collections.Generic.List<CelestialBody>();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }
}
