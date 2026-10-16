using UnityEngine;

/// <summary>Publishes the Sun's position and brightness to every solar-system shader.</summary>
[ExecuteAlways]
public class SunLight : MonoBehaviour
{
    [Tooltip("Overall light level reaching the planets")]
    public float brightness = 1.7f;
    [Tooltip("Distance at which the sunlight has dropped to half strength")]
    public float falloffDistance = 70f;

    static readonly int SunPos = Shader.PropertyToID("_SunPos");
    static readonly int SunParams = Shader.PropertyToID("_SunParams");

    void OnEnable() { Publish(); }
    void Update() { Publish(); }

    void Publish()
    {
        Shader.SetGlobalVector(SunPos, transform.position);
        Shader.SetGlobalVector(SunParams, new Vector4(brightness, falloffDistance, 0, 0));
    }
}
