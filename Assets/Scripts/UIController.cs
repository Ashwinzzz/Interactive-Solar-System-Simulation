using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD: focus info panel, zoom buttons, music toggle and the "click to begin" intro (which also unlocks WebGL audio).</summary>
public class UIController : MonoBehaviour
{
    public LookAtTarget cameraRig;
    public Text bodyName;
    public Text bodyDescription;
    public Text bodyFacts;
    public Text zoomReadout;
    public Button zoomInButton;
    public Button zoomOutButton;
    public Button overviewButton;
    public Toggle musicToggle;
    public AudioSource music;
    public CanvasGroup introGroup;

    bool started;
    float introFade = 1f;
    float musicVolume = 0.55f;

    void Start()
    {
        if (cameraRig != null) cameraRig.TargetChanged += Show;
        if (zoomInButton != null) zoomInButton.onClick.AddListener(() => cameraRig.ZoomBy(2.5f));
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(() => cameraRig.ZoomBy(-2.5f));
        if (overviewButton != null) overviewButton.onClick.AddListener(() => cameraRig.ShowWholeSystem());
        if (musicToggle != null) musicToggle.onValueChanged.AddListener(on => { if (music != null) music.mute = !on; });
        if (cameraRig != null && cameraRig.currentTarget != null) Show(cameraRig.currentTarget.GetComponent<CelestialBody>());
        if (introGroup != null) { introGroup.alpha = 1f; introGroup.blocksRaycasts = true; }
    }

    void Update()
    {
        if (!started && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            Begin();

        if (started && introGroup != null && introGroup.alpha > 0f)
        {
            introFade = Mathf.MoveTowards(introFade, 0f, Time.deltaTime * 1.4f);
            introGroup.alpha = introFade;
            if (introFade <= 0f) introGroup.gameObject.SetActive(false);
        }

        if (music != null && started)
            music.volume = Mathf.MoveTowards(music.volume, musicVolume, Time.deltaTime * 0.15f);

        if (zoomReadout != null && cameraRig != null)
            zoomReadout.text = "Distance  " + cameraRig.Distance.ToString("0") + " u";
    }

    void Begin()
    {
        started = true;
        if (introGroup != null) introGroup.blocksRaycasts = false;
        if (music != null)
        {
            music.volume = 0f;
            music.loop = true;
            music.Play();
        }
    }

    void Show(CelestialBody b)
    {
        if (b == null) return;
        if (bodyName != null) bodyName.text = b.displayName;
        if (bodyDescription != null) bodyDescription.text = b.description;
        if (bodyFacts != null) bodyFacts.text = b.facts;
    }
}
