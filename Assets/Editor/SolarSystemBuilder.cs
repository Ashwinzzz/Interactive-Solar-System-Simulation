using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the whole Solar System project (assets, URP setup, scene, UI) from code.</summary>
public static class SolarSystemBuilder
{
    const string Root = "Assets";
    const string TexDir = "Assets/Textures";
    const string MatDir = "Assets/Materials";
    const string MeshDir = "Assets/Meshes";
    const string PrefabDir = "Assets/Prefabs";
    const string SettingsDir = "Assets/Settings";
    const string ScenePath = "Assets/Scenes/SolarSystem.unity";
    const int MinimapLayer = 8;

    static Font uiFont;

    // ============================================================ stage 1: raw generated files

    [MenuItem("Solar System/1 - Generate Textures And Music")]
    public static void GenerateAssets()
    {
        string tex = Path.Combine(Application.dataPath, "Textures");
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        void Step(string label, System.Action a) { a(); Debug.Log($"[Generate] {label} done at {sw.Elapsed.TotalSeconds:0.0}s"); }

        Step("sun", () => ProceduralTextures.Sun(tex));
        Step("mercury", () => ProceduralTextures.Mercury(tex));
        Step("venus", () => ProceduralTextures.Venus(tex));
        Step("earth", () => ProceduralTextures.Earth(tex));
        Step("moon", () => ProceduralTextures.Moon(tex));
        Step("mars", () => ProceduralTextures.Mars(tex));
        Step("jupiter", () => ProceduralTextures.Jupiter(tex));
        Step("saturn", () => ProceduralTextures.Saturn(tex));
        Step("uranus", () => ProceduralTextures.Uranus(tex));
        Step("neptune", () => ProceduralTextures.Neptune(tex));
        Step("rings", () => ProceduralTextures.Rings(tex));
        Step("stars", () => ProceduralTextures.Stars(tex));
        Step("glow", () => ProceduralTextures.GlowDot(tex));
        Step("music", () => MusicGenerator.Generate(Path.Combine(Application.dataPath, "Audio/space_ambient.wav")));
        AssetDatabase.Refresh();
    }

    // ============================================================ stage 2: project + scene

    [MenuItem("Solar System/2 - Build Project And Scene")]
    public static void BuildEverything()
    {
        Directory.CreateDirectory(MatDir); Directory.CreateDirectory(MeshDir); Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(SettingsDir); Directory.CreateDirectory("Assets/Scenes");
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        ConfigureTextureImports();
        ConfigureAudioImport();
        SetupLayers();
        SetupRenderPipeline();
        BuildScene();
        ConfigurePlayerSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Builder] Scene and project settings built.");
    }

    // ------------------------------------------------------------ imports

    static void ConfigureTextureImports()
    {
        foreach (string path in Directory.GetFiles(TexDir, "*.png"))
        {
            string p = path.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) { AssetDatabase.ImportAsset(p); ti = AssetImporter.GetAtPath(p) as TextureImporter; }
            if (ti == null) continue;
            string n = Path.GetFileNameWithoutExtension(p);

            bool normal = n.EndsWith("_normal");
            bool linearMask = n.EndsWith("_spec") || n.EndsWith("_clouds");
            bool ring = n.EndsWith("_ring");
            bool stars = n == "stars";

            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !(normal || linearMask);
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = stars ? 2 : 8;
            ti.wrapModeU = ring ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.maxTextureSize = stars ? 4096 : 2048;
            ti.textureCompression = ring ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.CompressedHQ;
            ti.crunchedCompression = false;
            ti.alphaSource = ring || n == "glow_dot" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = ring;
            ti.npotScale = TextureImporterNPOTScale.None;
            if (normal) ti.convertToNormalmap = false;
            ti.SaveAndReimport();
        }
    }

    static void ConfigureAudioImport()
    {
        string p = "Assets/Audio/space_ambient.wav";
        var ai = AssetImporter.GetAtPath(p) as AudioImporter;
        if (ai == null) return;
        var s = new AudioImporterSampleSettings
        {
            loadType = AudioClipLoadType.CompressedInMemory,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = 0.55f,
            sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
        };
        ai.defaultSampleSettings = s;
        ai.SetOverrideSampleSettings("WebGL", s);
        ai.forceToMono = false;
        ai.loadInBackground = false;
        ai.SaveAndReimport();
    }

    static void SetupLayers()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("layers");
        layers.GetArrayElementAtIndex(MinimapLayer).stringValue = "Minimap";
        tagManager.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------ render pipeline

    static UniversalRenderPipelineAsset urpAsset;

    static void SetupRenderPipeline()
    {
        string rendererPath = SettingsDir + "/SolarSystem_Renderer.asset";
        string assetPath = SettingsDir + "/SolarSystem_URP.asset";
        AssetDatabase.DeleteAsset(rendererPath);
        AssetDatabase.DeleteAsset(assetPath);

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);
        urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
        urpAsset.supportsHDR = true;
        urpAsset.msaaSampleCount = 4;
        urpAsset.renderScale = 1f;
        urpAsset.supportsCameraDepthTexture = false;
        urpAsset.supportsCameraOpaqueTexture = false;
        urpAsset.shadowDistance = 0f;
        urpAsset.colorGradingMode = ColorGradingMode.HighDynamicRange;
        urpAsset.colorGradingLutSize = 32;
        AssetDatabase.CreateAsset(urpAsset, assetPath);

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urpAsset;
            QualitySettings.antiAliasing = 0;
            QualitySettings.vSyncCount = 1;
        }
        QualitySettings.SetQualityLevel(current, false);
        EditorUtility.SetDirty(urpAsset);
    }

    // ------------------------------------------------------------ mesh helpers

    static Mesh sphereMesh, quadMesh;

    static Mesh MakeSphere(string path, int lonSeg = 160, int latSeg = 80)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int y = 0; y <= latSeg; y++)
        {
            float v = y / (float)latSeg;
            float lat = v * Mathf.PI - Mathf.PI * 0.5f;
            for (int x = 0; x <= lonSeg; x++)
            {
                float u = x / (float)lonSeg;
                float lon = u * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                verts.Add(p * 0.5f); norms.Add(p); uvs.Add(new Vector2(u, v));
            }
        }
        for (int y = 0; y < latSeg; y++)
            for (int x = 0; x < lonSeg; x++)
            {
                int i0 = y * (lonSeg + 1) + x, i1 = i0 + 1, i2 = i0 + lonSeg + 1, i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i3);
                tris.Add(i0); tris.Add(i3); tris.Add(i1);
            }
        var m = new Mesh { name = "HiResSphere" };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Mesh MakeRing(string path, float innerFraction, int seg = 192)
    {
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            verts.Add(dir * 0.5f * innerFraction); uvs.Add(new Vector2(0, i / (float)seg));
            verts.Add(dir * 0.5f); uvs.Add(new Vector2(1, i / (float)seg));
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
        var m = new Mesh { name = "Ring" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Mesh GetQuad()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var m = go.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(go);
        return m;
    }

    // ------------------------------------------------------------ materials

    static Texture2D Tex(string name) { return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png"); }

    static Material NewMat(string name, string shader)
    {
        string path = $"{MatDir}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        var s = Shader.Find(shader);
        if (s == null) throw new System.Exception("Shader not found: " + shader);
        var m = new Material(s) { name = name };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    class PlanetDef
    {
        public string name, tex, desc, facts;
        public float radius, orbit, orbitSpeed, spin, tilt, startAngle;
        public bool normal;
        public Color atmo; public float atmoIntensity = 0.6f, atmoPower = 3f;
        public Color shell; public float shellIntensity = 0f;
        public float ambient = 0.012f; public float brightness = 1f;
        public Color marker = Color.white;
        public string ring; public float ringInner, ringOuter; public float ringOpacity = 1f;
        public bool clouds, earth;
        public float normalStrength = 1f;
    }

    static List<PlanetDef> Planets()
    {
        return new List<PlanetDef> {
            new PlanetDef { name="Mercury", tex="mercury", normal=true, normalStrength=1.4f, radius=1.0f, orbit=27f, orbitSpeed=11.8f, spin=4f, tilt=0.03f, startAngle=20f,
                atmo=Color.black, atmoIntensity=0f, marker=new Color(0.75f,0.72f,0.68f),
                desc="The smallest planet and the closest to the Sun. A heavily cratered, airless world.",
                facts="Orbit: 88 days   |   Day: 59 Earth days\nSurface: -180 C to 430 C\nMoons: 0" },
            new PlanetDef { name="Venus", tex="venus", radius=1.9f, orbit=36f, orbitSpeed=6.6f, spin=-2.4f, tilt=177f, startAngle=140f,
                atmo=new Color(1f,0.78f,0.45f), atmoIntensity=0.9f, atmoPower=2.2f, shell=new Color(1f,0.72f,0.38f), shellIntensity=1.9f, marker=new Color(0.96f,0.8f,0.5f),
                desc="Wrapped in thick sulphuric clouds, Venus is the hottest planet in the Solar System.",
                facts="Orbit: 225 days   |   Day: 243 Earth days\nSurface: 465 C   |   Spins backwards\nMoons: 0" },
            new PlanetDef { name="Earth", tex="earth", normal=true, normalStrength=1.1f, radius=2.0f, orbit=46f, orbitSpeed=5f, spin=26f, tilt=23.4f, startAngle=250f, clouds=true, earth=true,
                atmo=new Color(0.32f,0.58f,1f), atmoIntensity=1.5f, atmoPower=3.2f, shell=new Color(0.26f,0.52f,1f), shellIntensity=3.0f, marker=new Color(0.3f,0.65f,1f),
                desc="Our home. The only world known to host life, with liquid oceans and a protective atmosphere.",
                facts="Orbit: 365 days   |   Day: 24 hours\nSurface: about 15 C average\nMoons: 1 (the Moon)" },
            new PlanetDef { name="Mars", tex="mars", normal=true, normalStrength=1.2f, radius=1.45f, orbit=57f, orbitSpeed=3.4f, spin=25f, tilt=25.2f, startAngle=60f,
                atmo=new Color(0.95f,0.55f,0.32f), atmoIntensity=0.5f, atmoPower=3.5f, shell=new Color(0.95f,0.5f,0.28f), shellIntensity=0.9f, marker=new Color(0.95f,0.4f,0.2f),
                desc="The red planet: cold desert plains, giant volcanoes and the deepest canyon in the Solar System.",
                facts="Orbit: 687 days   |   Day: 24.6 hours\nSurface: -63 C average\nMoons: 2 (Phobos, Deimos)" },
            new PlanetDef { name="Jupiter", tex="jupiter", radius=6.6f, orbit=84f, orbitSpeed=1.14f, spin=58f, tilt=3.1f, startAngle=310f,
                atmo=new Color(0.85f,0.72f,0.55f), atmoIntensity=0.35f, atmoPower=3f, shell=new Color(0.85f,0.7f,0.5f), shellIntensity=0.55f, marker=new Color(0.9f,0.7f,0.5f),
                desc="The giant of the Solar System. Its Great Red Spot is a storm larger than Earth.",
                facts="Orbit: 11.9 years   |   Day: 9.9 hours\nCloud tops: -110 C\nMoons: 95 known" },
            new PlanetDef { name="Saturn", tex="saturn", radius=5.6f, orbit=114f, orbitSpeed=0.66f, spin=52f, tilt=26.7f, startAngle=95f,
                atmo=new Color(0.95f,0.82f,0.55f), atmoIntensity=0.35f, atmoPower=3f, shell=new Color(0.95f,0.8f,0.5f), shellIntensity=0.55f, marker=new Color(0.95f,0.85f,0.55f),
                ring="saturn_ring", ringInner=1.24f, ringOuter=2.35f, ringOpacity=0.95f,
                desc="Famous for its spectacular ring system made of ice and rock, Saturn is a gas giant.",
                facts="Orbit: 29.5 years   |   Day: 10.7 hours\nCloud tops: -140 C\nMoons: 146 known" },
            new PlanetDef { name="Uranus", tex="uranus", radius=3.6f, orbit=142f, orbitSpeed=0.35f, spin=-32f, tilt=97.8f, startAngle=200f,
                atmo=new Color(0.55f,0.88f,0.95f), atmoIntensity=1.0f, atmoPower=2.6f, shell=new Color(0.5f,0.85f,0.95f), shellIntensity=1.8f, marker=new Color(0.55f,0.9f,0.95f),
                ring="uranus_ring", ringInner=1.55f, ringOuter=2.2f, ringOpacity=0.9f,
                desc="An ice giant that rolls around the Sun on its side, tilted almost 98 degrees.",
                facts="Orbit: 84 years   |   Day: 17 hours\nCloud tops: -195 C\nMoons: 28 known" },
            new PlanetDef { name="Neptune", tex="neptune", radius=3.5f, orbit=168f, orbitSpeed=0.23f, spin=34f, tilt=28.3f, startAngle=345f,
                atmo=new Color(0.25f,0.45f,1f), atmoIntensity=1.1f, atmoPower=2.8f, shell=new Color(0.22f,0.42f,1f), shellIntensity=2.0f, marker=new Color(0.3f,0.45f,1f),
                desc="The windiest planet, a deep-blue ice giant at the dark edge of the Solar System.",
                facts="Orbit: 165 years   |   Day: 16 hours\nWinds: up to 2,100 km/h\nMoons: 16 known" },
        };
    }

    // ------------------------------------------------------------ scene

    static Material lineMat, minimapLineMat, markerMat, glowMat;

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        sphereMesh = MakeSphere(MeshDir + "/HiResSphere.asset");
        quadMesh = GetQuad();

        // ---- render settings
        var skyMat = NewMat("Stars", "SolarSystem/StarSky");
        skyMat.SetTexture("_MainTex", Tex("stars"));
        skyMat.SetFloat("_MilkyWay", 1.0f);
        skyMat.SetFloat("_StarBrightness", 1.0f);
        skyMat.SetFloat("_Exposure", 1.0f);
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.04f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.fog = false;

        lineMat = NewMat("OrbitLine", "SolarSystem/Line"); lineMat.SetColor("_Color", new Color(0.55f, 0.72f, 1f, 0.20f));
        minimapLineMat = NewMat("MinimapOrbitLine", "SolarSystem/Line"); minimapLineMat.SetColor("_Color", new Color(0.45f, 0.65f, 1f, 0.55f));
        markerMat = NewMat("MinimapMarker", "SolarSystem/Line"); markerMat.SetColor("_Color", Color.white);

        // ---- sun
        var sunRoot = new GameObject("Sun");
        sunRoot.transform.position = Vector3.zero;
        float sunR = 14f;
        var sunBody = sunRoot.AddComponent<CelestialBody>();
        sunBody.displayName = "The Sun";
        sunBody.radius = sunR;
        sunBody.description = "A G-type main-sequence star holding 99.8% of the mass of the entire Solar System.";
        sunBody.facts = "Diameter: 1.39 million km (109 Earths)\nSurface: 5,500 C   |   Core: 15 million C\nAge: 4.6 billion years";
        var sunLight = sunRoot.AddComponent<SunLight>();
        var sunCol = sunRoot.AddComponent<SphereCollider>(); sunCol.radius = 0.55f * 2f * sunR;

        var sunMat = NewMat("Sun", "SolarSystem/Sun");
        sunMat.SetTexture("_BaseMap", Tex("sun_albedo"));
        sunMat.SetColor("_Color", new Color(3.4f, 3.0f, 2.3f, 1f));

        var surf = MakeMeshObject("Surface", sunRoot.transform, sphereMesh, sunMat);
        surf.transform.localScale = Vector3.one * sunR * 2f;
        var sunSpin = surf.AddComponent<Spin>(); sunSpin.degreesPerSecond = 1.6f;
        MakeGlow("Corona Inner", sunRoot.transform, sunR * 4.0f, new Color(1.0f, 0.62f, 0.28f) * 0.9f, 3.6f, 0.0f);
        MakeGlow("Corona Outer", sunRoot.transform, sunR * 12f, new Color(1.0f, 0.5f, 0.2f) * 0.17f, 5.0f, 0.0f);
        MakeGlow("Corona Core", sunRoot.transform, sunR * 2.55f, new Color(1.0f, 0.82f, 0.5f) * 1.8f, 6f, 0.0f);
        MakeMarker(sunRoot.transform, sunBody, new Color(1f, 0.85f, 0.35f), 15f);

        // ---- planets
        var orbitsRoot = new GameObject("Orbits");
        var bodyTransforms = new List<Transform> { sunRoot.transform };
        Transform earthRoot = null;
        foreach (var d in Planets())
        {
            var root = new GameObject(d.name);
            root.transform.position = new Vector3(d.orbit, 0, 0);
            var cb = root.AddComponent<CelestialBody>();
            cb.displayName = d.name; cb.radius = d.radius; cb.description = d.desc; cb.facts = d.facts;
            var col = root.AddComponent<SphereCollider>(); col.radius = 0.5f * 2f * 1.18f * d.radius;
            var orbit = root.AddComponent<RotateAround>();
            orbit.target = sunRoot.transform; orbit.speed = d.orbitSpeed; orbit.axis = Vector3.up; orbit.startAngle = d.startAngle;

            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(root.transform, false);
            tilt.localRotation = Quaternion.Euler(0, 0, d.tilt);

            var pm = NewMat(d.name, "SolarSystem/Planet");
            pm.SetTexture("_BaseMap", Tex(d.tex + "_albedo"));
            if (d.normal) { pm.SetTexture("_BumpMap", Tex(d.tex + "_normal")); pm.SetFloat("_BumpScale", d.normalStrength); }
            if (d.earth)
            {
                pm.SetTexture("_SpecMap", Tex("earth_spec"));
                pm.SetTexture("_NightMap", Tex("earth_night"));
                pm.SetFloat("_SpecPower", 230f); pm.SetFloat("_SpecIntensity", 0.2f);
            }
            pm.SetColor("_AtmoColor", d.atmo); pm.SetFloat("_AtmoIntensity", d.atmoIntensity); pm.SetFloat("_AtmoPower", d.atmoPower);
            pm.SetFloat("_Ambient", d.ambient); pm.SetFloat("_Brightness", d.brightness);

            var body = MakeMeshObject("Surface", tilt, sphereMesh, pm);
            body.transform.localScale = Vector3.one * d.radius * 2f;
            var spin = body.AddComponent<Spin>(); spin.degreesPerSecond = d.spin;

            if (d.clouds)
            {
                var cm = NewMat("EarthClouds", "SolarSystem/Clouds");
                cm.SetTexture("_BaseMap", Tex("earth_clouds")); cm.SetFloat("_Opacity", 0.78f);
                var cl = MakeMeshObject("Clouds", tilt, sphereMesh, cm);
                cl.transform.localScale = Vector3.one * d.radius * 2f * 1.012f;
                var cs = cl.AddComponent<Spin>(); cs.degreesPerSecond = d.spin * 1.18f;
            }
            if (d.shellIntensity > 0f)
            {
                var am = NewMat(d.name + "Atmosphere", "SolarSystem/Atmosphere");
                am.SetColor("_Color", d.shell); am.SetFloat("_Intensity", d.shellIntensity);
                am.SetFloat("_Power", 3.0f); am.SetFloat("_Sharp", 1.9f);
                var shell = MakeMeshObject("Atmosphere", tilt, sphereMesh, am);
                shell.transform.localScale = Vector3.one * d.radius * 2f * 1.11f;
            }
            if (!string.IsNullOrEmpty(d.ring))
            {
                var rm = NewMat(d.name + "Ring", "SolarSystem/Ring");
                rm.SetTexture("_BaseMap", Tex(d.ring)); rm.SetFloat("_Opacity", d.ringOpacity);
                rm.SetFloat("_PlanetRadius", d.radius);
                var ringMesh = MakeRing($"{MeshDir}/{d.name}Ring.asset", d.ringInner / d.ringOuter);
                var ring = MakeMeshObject("Rings", tilt, ringMesh, rm);
                ring.transform.localScale = Vector3.one * d.radius * 2f * d.ringOuter;
                var track = ring.AddComponent<RingPlanetTracker>(); track.planetCenter = root.transform; track.radius = d.radius;
            }

            MakeMarker(root.transform, cb, d.marker, 8.5f);
            MakeOrbitLine(orbitsRoot.transform, sunRoot.transform, d.name, d.orbit);
            bodyTransforms.Add(root.transform);
            if (d.name == "Earth") earthRoot = root.transform;
        }

        // ---- the Moon
        if (earthRoot != null)
        {
            var moonRoot = new GameObject("Moon");
            moonRoot.transform.SetParent(earthRoot, false);
            moonRoot.transform.localPosition = new Vector3(5.6f, 0, 0);
            var mcb = moonRoot.AddComponent<CelestialBody>();
            mcb.displayName = "The Moon"; mcb.radius = 0.55f;
            mcb.description = "Earth's only natural satellite, locked in a synchronous orbit so we always see the same face.";
            mcb.facts = "Orbit: 27.3 days   |   Distance: 384,400 km\nSurface: -170 C to 120 C\nCircumference: 10,921 km";
            var mcol = moonRoot.AddComponent<SphereCollider>(); mcol.radius = 0.5f * 2f * 1.3f * 0.55f;
            var mo = moonRoot.AddComponent<RotateAround>();
            mo.target = earthRoot; mo.speed = 38f; mo.axis = Vector3.up; mo.startAngle = 20f;
            var mm = NewMat("Moon", "SolarSystem/Planet");
            mm.SetTexture("_BaseMap", Tex("moon_albedo")); mm.SetTexture("_BumpMap", Tex("moon_normal")); mm.SetFloat("_BumpScale", 1.3f);
            mm.SetFloat("_AtmoIntensity", 0f); mm.SetFloat("_Ambient", 0.01f);
            var mb = MakeMeshObject("Surface", moonRoot.transform, sphereMesh, mm);
            mb.transform.localScale = Vector3.one * 0.55f * 2f;
            var ms = mb.AddComponent<Spin>(); ms.degreesPerSecond = 38f;   // tidally locked: spin matches orbit
        }

        // ---- camera
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 42f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 4000f;
        cam.allowHDR = true;
        cam.cullingMask = ~(1 << MinimapLayer);
        camGo.AddComponent<AudioListener>();
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.None;
        camData.renderShadows = false;
        var rig = camGo.AddComponent<LookAtTarget>();
        rig.defaultTarget = sunRoot.transform;
        rig.currentTarget = sunRoot.transform;
        camGo.transform.position = new Vector3(0, 60, -120);
        camGo.transform.LookAt(Vector3.zero);

        var spawner = camGo.AddComponent<Spawner>();
        spawner.spawnPrefab = BuildCometPrefab();

        // ---- post processing
        BuildVolume();

        // ---- minimap
        var rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "MinimapRT", antiAliasing = 2 };
        AssetDatabase.CreateAsset(rt, SettingsDir + "/MinimapRT.renderTexture");
        var mapCamGo = new GameObject("Minimap Camera");
        mapCamGo.transform.position = new Vector3(0, 600, 0);
        mapCamGo.transform.rotation = Quaternion.Euler(90, 0, 0);
        var mapCam = mapCamGo.AddComponent<Camera>();
        mapCam.orthographic = true; mapCam.orthographicSize = 188f;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0.012f, 0.02f, 0.05f, 1f);
        mapCam.cullingMask = 1 << MinimapLayer;
        mapCam.targetTexture = rt; mapCam.depth = -5; mapCam.allowHDR = false; mapCam.nearClipPlane = 1; mapCam.farClipPlane = 1500;
        var mapData = mapCamGo.AddComponent<UniversalAdditionalCameraData>();
        mapData.renderPostProcessing = false; mapData.renderShadows = false; mapData.antialiasing = AntialiasingMode.None;

        // ---- UI + audio
        var music = new GameObject("Music").AddComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/space_ambient.wav");
        music.loop = true; music.playOnAwake = false; music.spatialBlend = 0f; music.volume = 0f;
        BuildUI(rig, mapCam, rt, music);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        string sample = "Assets/Scenes/SampleScene.unity";
        if (File.Exists(sample)) AssetDatabase.DeleteAsset(sample);
    }

    static GameObject MakeMeshObject(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return go;
    }

    static void MakeGlow(string name, Transform parent, float size, Color hdr, float power, float core)
    {
        var m = NewMat("Glow_" + name.Replace(" ", ""), "SolarSystem/Glow");
        m.SetColor("_Color", hdr); m.SetFloat("_Power", power); m.SetFloat("_Core", Mathf.Max(core, 0.001f));
        var g = MakeMeshObject(name, parent, quadMesh, m);
        g.transform.localScale = Vector3.one * size;
        g.layer = 0;
    }

    static void MakeMarker(Transform parent, CelestialBody body, Color color, float worldSize)
    {
        var m = NewMat("Marker_" + parent.name, "SolarSystem/Line");
        m.SetColor("_Color", color);
        var go = MakeMeshObject("Minimap Marker", parent, sphereMesh, m);
        go.layer = MinimapLayer;
        go.transform.localScale = Vector3.one * worldSize;
        var mk = go.AddComponent<MinimapMarker>(); mk.baseScale = worldSize;
        body.minimapMarker = go.transform;
    }

    static void MakeOrbitLine(Transform orbitsRoot, Transform center, string name, float radius)
    {
        // main-view line
        var go = new GameObject(name + " Orbit");
        go.transform.SetParent(orbitsRoot, false);
        go.transform.position = center.position;
        var lr = go.AddComponent<LineRenderer>();
        ConfigureCircle(lr, radius, lineMat, 0.1f);
        lr.gameObject.AddComponent<OrbitLine>().orbitRadius = radius;
        lr.shadowCastingMode = ShadowCastingMode.Off;

        // minimap line (own layer, thicker)
        var go2 = new GameObject(name + " Orbit (Minimap)");
        go2.transform.SetParent(orbitsRoot, false);
        go2.transform.position = center.position;
        go2.layer = MinimapLayer;
        var lr2 = go2.AddComponent<LineRenderer>();
        ConfigureCircle(lr2, radius, minimapLineMat, 1.0f);
        lr2.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void ConfigureCircle(LineRenderer lr, float radius, Material mat, float width)
    {
        int n = 361;
        lr.useWorldSpace = false; lr.loop = true; lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)(n - 1) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
        }
        lr.widthMultiplier = width;
        lr.sharedMaterial = mat;
        lr.alignment = LineAlignment.View;
        lr.numCornerVertices = 0;
        lr.startColor = lr.endColor = Color.white;
        lr.textureMode = LineTextureMode.Stretch;
    }

    static GameObject BuildCometPrefab()
    {
        var root = new GameObject("Comet");
        root.AddComponent<Projectile>();
        MakeGlow("Head", root.transform, 1.3f, new Color(0.7f, 0.9f, 1f) * 2.0f, 2.2f, 0.12f);
        var trailMat = NewMat("CometTrail", "SolarSystem/Line");
        trailMat.SetColor("_Color", new Color(0.55f, 0.85f, 1f, 1f));
        var tr = root.AddComponent<TrailRenderer>();
        tr.time = 1.4f; tr.minVertexDistance = 0.2f; tr.widthMultiplier = 0.28f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1) },
                  new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0f, 1) });
        tr.colorGradient = g;
        tr.sharedMaterial = trailMat;
        tr.shadowCastingMode = ShadowCastingMode.Off;
        string path = PrefabDir + "/Comet.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void BuildVolume()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "SolarSystemPost";

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.72f);
        bloom.clamp.Override(60f);
        bloom.highQualityFiltering.Override(true);
        bloom.tint.Override(new Color(1f, 0.96f, 0.92f));

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.35f);
        ca.contrast.Override(10f);
        ca.saturation.Override(10f);

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.28f);
        vig.smoothness.Override(0.55f);

        AssetDatabase.CreateAsset(profile, SettingsDir + "/SolarSystemPost.asset");
        var go = new GameObject("Global Volume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
        vol.priority = 1;
    }

    // ------------------------------------------------------------ UI

    static GameObject UIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = UIObject(name, parent);
        var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
        return img;
    }

    static Text MakeText(string name, Transform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        var go = UIObject(name, parent);
        var t = go.AddComponent<Text>();
        t.font = uiFont; t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = anchor; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false; t.supportRichText = true;
        var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.75f); sh.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }

    static Button MakeButton(string name, Transform parent, string label, int fontSize)
    {
        var go = UIObject(name, parent);
        var img = go.AddComponent<Image>(); img.color = new Color(0.07f, 0.11f, 0.2f, 0.82f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(0.7f, 0.9f, 1f); cb.pressedColor = new Color(0.45f, 0.7f, 1f);
        cb.selectedColor = Color.white; cb.fadeDuration = 0.08f; btn.colors = cb;
        var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0.4f, 0.65f, 1f, 0.65f); outline.effectDistance = new Vector2(1.5f, -1.5f);
        var t = MakeText("Label", go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.88f, 0.95f, 1f));
        Stretch(t.rectTransform);
        return btn;
    }

    static Toggle MakeToggle(string name, Transform parent, string label, Vector2 size)
    {
        var go = UIObject(name, parent);
        var toggle = go.AddComponent<Toggle>();
        var bgImg = go.AddComponent<Image>(); bgImg.color = new Color(0.06f, 0.1f, 0.18f, 0.82f);
        var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0.4f, 0.65f, 1f, 0.6f); outline.effectDistance = new Vector2(1.5f, -1.5f);

        var box = UIObject("Box", go.transform);
        var boxImg = box.AddComponent<Image>(); boxImg.color = new Color(0.02f, 0.04f, 0.09f, 1f);
        var brt = box.GetComponent<RectTransform>(); Place(brt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(26, 26));
        var boxOutline = box.AddComponent<Outline>(); boxOutline.effectColor = new Color(0.5f, 0.75f, 1f, 0.9f); boxOutline.effectDistance = new Vector2(1.2f, -1.2f);

        var check = UIObject("Checkmark", box.transform);
        var checkImg = check.AddComponent<Image>(); checkImg.color = new Color(0.45f, 0.85f, 1f);
        Stretch(check.GetComponent<RectTransform>(), 6, 6, 6, 6);

        var lab = MakeText("Label", go.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.88f, 0.95f, 1f));
        Place(lab.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(size.x - 56, size.y));

        toggle.targetGraphic = bgImg; toggle.graphic = checkImg; toggle.isOn = true;
        var colors = toggle.colors;
        colors.highlightedColor = new Color(0.75f, 0.92f, 1f); colors.pressedColor = new Color(0.5f, 0.75f, 1f); colors.fadeDuration = 0.08f;
        toggle.colors = colors;
        go.GetComponent<RectTransform>().sizeDelta = size;
        return toggle;
    }

    static void BuildUI(LookAtTarget rig, Camera mapCam, RenderTexture rt, AudioSource music)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        Color ink = new Color(0.9f, 0.96f, 1f);
        Color accent = new Color(0.5f, 0.78f, 1f);
        var ui = canvasGo.AddComponent<UIController>();
        ui.cameraRig = rig; ui.music = music;

        // ---- title + controls (top-left)
        var title = MakeText("Title", canvasGo.transform, "SOLAR SYSTEM", 44, FontStyle.Bold, TextAnchor.UpperLeft, ink);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -26), new Vector2(700, 60));
        var controls = MakeText("Controls", canvasGo.transform,
            "<color=#80C8FF>Scroll</color> or <color=#80C8FF>+ / -</color>   Zoom in and out\n" +
            "<color=#80C8FF>Click</color> a planet   Focus on it\n" +
            "<color=#80C8FF>Drag</color>   Orbit the camera\n" +
            "<color=#80C8FF>Space</color> / right-click   Fire comets\n" +
            "<color=#80C8FF>0-8</color>   Sun and planets     <color=#80C8FF>Home</color>   Whole system\n" +
            "<color=#80C8FF>M</color>   Show / hide the map",
            20, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.82f, 0.9f, 1f, 0.92f));
        controls.lineSpacing = 1.15f;
        Place(controls.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -86), new Vector2(760, 200));

        // ---- focus info (top-right)
        var info = UIObject("Info Panel", canvasGo.transform);
        var infoBg = info.AddComponent<Image>(); infoBg.color = new Color(0.03f, 0.06f, 0.12f, 0.72f); infoBg.raycastTarget = false;
        var infoOutline = info.AddComponent<Outline>(); infoOutline.effectColor = new Color(0.4f, 0.65f, 1f, 0.55f); infoOutline.effectDistance = new Vector2(1.5f, -1.5f);
        Place(info.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -30), new Vector2(520, 330));
        ui.bodyName = MakeText("Name", info.transform, "The Sun", 42, FontStyle.Bold, TextAnchor.UpperLeft, ink);
        Place(ui.bodyName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(470, 56));
        ui.bodyDescription = MakeText("Description", info.transform, "", 22, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.85f, 0.92f, 1f));
        Place(ui.bodyDescription.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -76), new Vector2(472, 110));
        ui.bodyFacts = MakeText("Facts", info.transform, "", 19, FontStyle.Normal, TextAnchor.UpperLeft, accent);
        Place(ui.bodyFacts.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -190), new Vector2(472, 100));
        ui.zoomReadout = MakeText("Zoom", info.transform, "", 18, FontStyle.Italic, TextAnchor.LowerRight, new Color(0.7f, 0.82f, 1f, 0.8f));
        Place(ui.zoomReadout.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 10), new Vector2(300, 28));

        // ---- minimap (bottom-left) with its own visibility toggle
        var mapToggle = MakeToggle("Minimap Toggle", canvasGo.transform, "Show map (M)", new Vector2(256, 46));
        Place(mapToggle.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 28), new Vector2(256, 46));

        var panel = UIObject("Minimap Panel", canvasGo.transform);
        var frame = panel.AddComponent<Image>(); frame.color = new Color(0.4f, 0.65f, 1f, 0.75f); frame.raycastTarget = false;
        Place(panel.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 86), new Vector2(344, 368));
        var raw = UIObject("Map", panel.transform);
        var rawImg = raw.AddComponent<RawImage>(); rawImg.texture = rt; rawImg.raycastTarget = false;
        Place(raw.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 5), new Vector2(334, 334));
        var cap = MakeImage("Caption Bar", panel.transform, new Color(0.03f, 0.06f, 0.12f, 0.95f));
        Place(cap.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -5), new Vector2(334, 24));
        var capText = MakeText("Caption", cap.transform, "SOLAR SYSTEM MAP", 15, FontStyle.Bold, TextAnchor.MiddleCenter, accent);
        Stretch(capText.rectTransform);

        var mini = canvasGo.AddComponent<MinimapController>();
        mini.minimapCamera = mapCam; mini.minimapPanel = panel; mini.visibilityToggle = mapToggle; mini.cameraRig = rig;

        // ---- zoom + view buttons (bottom-right)
        var plus = MakeButton("Zoom In", canvasGo.transform, "+", 40);
        Place(plus.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 250), new Vector2(66, 66));
        var minus = MakeButton("Zoom Out", canvasGo.transform, "-", 44);
        Place(minus.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 176), new Vector2(66, 66));
        var all = MakeButton("Whole System", canvasGo.transform, "ALL", 22);
        Place(all.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 102), new Vector2(66, 66));
        var musicToggle = MakeToggle("Music Toggle", canvasGo.transform, "Music", new Vector2(190, 46));
        Place(musicToggle.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 28), new Vector2(190, 46));
        ui.zoomInButton = plus; ui.zoomOutButton = minus; ui.overviewButton = all; ui.musicToggle = musicToggle;

        // ---- intro overlay
        var intro = UIObject("Intro", canvasGo.transform);
        var introImg = intro.AddComponent<Image>(); introImg.color = new Color(0.0f, 0.01f, 0.03f, 0.74f);
        Stretch(intro.GetComponent<RectTransform>());
        var cg = intro.AddComponent<CanvasGroup>();
        var introTitle = MakeText("Title", intro.transform, "SOLAR SYSTEM", 110, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
        Place(introTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1500, 150));
        var introSub = MakeText("Subtitle", intro.transform, "An interactive journey from the Sun to Neptune", 32, FontStyle.Normal, TextAnchor.MiddleCenter, accent);
        Place(introSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1500, 60));
        var introGo = MakeText("Prompt", intro.transform, "Click anywhere to begin", 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        Place(introGo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(1500, 60));
        var introNote = MakeText("Note", intro.transform, "Sound on for the full experience", 22, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.7f, 0.8f, 0.95f, 0.8f));
        Place(introNote.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(1500, 40));
        ui.introGroup = cg;
        intro.transform.SetAsLastSibling();
    }

    // ------------------------------------------------------------ player settings

    static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "Ashwin Chakraborty";
        PlayerSettings.productName = "Solar System";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.template = "PROJECT:SolarSystem";
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.stripEngineCode = true;
    }

    // ============================================================ build

    [MenuItem("Solar System/3 - Build WebGL")]
    public static void BuildWebGL()
    {
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/WebGL"));
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[Build] Result: " + report.summary.result + "  size=" + (report.summary.totalSize / (1024 * 1024)) + " MB  time=" + report.summary.totalTime);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    // ============================================================ preview renders (editor only)

    public static void CapturePreviews()
    {
        EditorSceneManager.OpenScene(ScenePath);
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Previews"));
        Directory.CreateDirectory(outDir);
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        Object.FindFirstObjectByType<SunLight>().SendMessage("OnEnable");
        foreach (var ra in Object.FindObjectsByType<RotateAround>(FindObjectsSortMode.None)) if (ra.target != ra.transform.parent) ra.SendMessage("Start");
        foreach (var ra in Object.FindObjectsByType<RotateAround>(FindObjectsSortMode.None)) if (ra.target == ra.transform.parent) ra.SendMessage("Start");
        foreach (var rt2 in Object.FindObjectsByType<RingPlanetTracker>(FindObjectsSortMode.None)) rt2.SendMessage("LateUpdate");
        foreach (var ol in Object.FindObjectsByType<OrbitLine>(FindObjectsSortMode.None)) ol.SendMessage("Awake");

        var views = new (string name, string target, float dist, float yaw, float pitch)[] {
            ("overview", "Sun", 190f, 20f, 28f),
            ("neptune", "Neptune", 18f, 150f, 10f),
            ("venus", "Venus", 11f, 120f, 8f),
            ("moon", "Moon", 4f, 120f, 8f),
            ("earth", "Earth", 14f, 130f, 12f),
            ("saturn", "Saturn", 46f, 35f, 14f),
            ("jupiter", "Jupiter", 40f, 60f, 8f),
            ("sun", "Sun", 80f, 200f, 10f),
            ("mars", "Mars", 11f, 90f, 5f),
            ("whole", "Sun", 420f, 20f, 40f),
        };
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        foreach (var v in views)
        {
            var t = GameObject.Find(v.target).transform;
            float yawUse = v.yaw;
            if (v.target != "Sun")
            {
                Vector3 toSun = GameObject.Find("Sun").transform.position - t.position; toSun.y = 0f;
                Vector3 dirSun = Quaternion.AngleAxis(38f, Vector3.up) * toSun.normalized;
                yawUse = Mathf.Atan2(-dirSun.x, -dirSun.z) * Mathf.Rad2Deg;
            }
            var rot = Quaternion.Euler(v.pitch, yawUse, 0);
            cam.transform.position = t.position + rot * new Vector3(0, 0, -v.dist);
            cam.transform.rotation = rot;
            foreach (var ol in Object.FindObjectsByType<OrbitLine>(FindObjectsSortMode.None)) ol.SendMessage("LateUpdate");
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, v.name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        Debug.Log("[Preview] saved to " + outDir);
        EditorApplication.Exit(0);
    }
}
