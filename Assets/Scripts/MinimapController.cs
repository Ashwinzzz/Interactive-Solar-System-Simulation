using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The overview map in the bottom-left corner. A UI toggle (or the M key) shows and hides it;
/// the minimap camera is switched off while hidden so it costs nothing.
/// </summary>
public class MinimapController : MonoBehaviour
{
    public Camera minimapCamera;
    public GameObject minimapPanel;
    public Toggle visibilityToggle;
    public LookAtTarget cameraRig;
    public bool startVisible = true;

    const string PrefKey = "SolarSystem.MinimapVisible";

    void Start()
    {
        bool visible = startVisible;
        if (PlayerPrefs.HasKey(PrefKey)) visible = PlayerPrefs.GetInt(PrefKey) == 1;
        if (visibilityToggle != null)
        {
            visibilityToggle.SetIsOnWithoutNotify(visible);
            visibilityToggle.onValueChanged.AddListener(SetVisible);
        }
        SetVisible(visible);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M) && visibilityToggle != null)
            visibilityToggle.isOn = !visibilityToggle.isOn;

        // keep the minimap markers readable: the current target's marker pulses larger
        if (minimapPanel != null && minimapPanel.activeSelf && cameraRig != null)
        {
            foreach (var b in CelestialBody.All)
            {
                if (b.minimapMarker == null) continue;
                bool selected = cameraRig.currentTarget == b.transform;
                float target = selected ? 1.9f + 0.25f * Mathf.Sin(Time.time * 5f) : 1f;
                var s = b.minimapMarker.localScale;
                float want = markerBase(b) * target;
                b.minimapMarker.localScale = Vector3.Lerp(s, Vector3.one * want, 10f * Time.deltaTime);
            }
        }
    }

    float markerBase(CelestialBody b)
    {
        var m = b.minimapMarker.GetComponent<MinimapMarker>();
        return m != null ? m.baseScale : 1f;
    }

    public void SetVisible(bool visible)
    {
        if (minimapPanel != null) minimapPanel.SetActive(visible);
        if (minimapCamera != null) minimapCamera.enabled = visible;
        PlayerPrefs.SetInt(PrefKey, visible ? 1 : 0);
    }
}
