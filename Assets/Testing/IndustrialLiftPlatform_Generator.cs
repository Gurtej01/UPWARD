using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Industrial Lift Platform (Unity 6 + URP) — procedural + alive, 2.5D side view (camera looks along Z).
/// - No animation clips, no scaling animations (Unity-safe)
/// - High silhouette detail for side view: panels, vents, bolts, pistons, fan, LEDs, beacons, energy strip
/// - Generates hierarchy under IND_LIFT_ROOT
/// </summary>
[ExecuteAlways]
public class IndustrialLiftPlatform_Unity6URP : MonoBehaviour
{
    [Header("Generate")]
    public bool autoRegenerateInEditor = false;
    public bool clearAndRebuildOnPlay = true;

#if UNITY_EDITOR
    [Header("Optional: Save generated meshes as assets")]
    public bool saveMeshAssets = false;
    public string saveFolder = "Assets/Generated/IndustrialLift";
#endif

    [Header("URP Helpers (optional)")]
    public bool createURPVolumeBloom = true;
    [Range(0f, 2f)] public float bloomIntensity = 0.35f;
    public bool createLightsIfNone = true;

    [Header("Platform Size")]
    public float widthX = 3.2f;    // X
    public float depthZ = 1.6f;    // Z
    public float thicknessY = 0.50f; // Y

    [Header("Rounded Hull")]
    public float cornerRadius = 0.26f;
    [Range(8, 64)] public int cornerSegments = 28;

    [Header("Detail Density")]
    [Range(8, 40)] public int boltsPerFace = 18;
    [Range(12, 60)] public int ledCount = 32;
    [Range(2, 10)] public int ventCount = 4;

    [Header("Colors")]
    public Color metalDark = new Color(0.10f, 0.11f, 0.13f, 1);
    public Color metalMid = new Color(0.18f, 0.19f, 0.22f, 1);
    public Color hazardYellow = new Color(0.95f, 0.78f, 0.15f, 1);
    public Color warningRed = new Color(1.00f, 0.22f, 0.18f, 1);
    public Color energyCyan = new Color(0.15f, 0.95f, 1.00f, 1);

    [Header("Alive Motion (NO scaling)")]
    public float hoverAmp = 0.018f;
    public float hoverSpeed = 1.00f;
    public float microTiltDeg = 0.90f;
    public float microTiltSpeed = 0.75f;

    [Header("Mechanics")]
    public float pistonTravel = 0.10f;
    public float pistonSpeed = 1.35f;
    public float fanSpinDegPerSec = 720f;

    [Header("Energy / LEDs")]
    public float energyScrollSpeed = 0.22f; // scroll UP
    public float ledSpeed = 2.4f;
    public float ledSharpness = 9f;
    public float ledMin = 0.15f;
    public float ledMax = 9.5f;

    // hierarchy refs
    Transform root;
    Transform topDeck;
    Transform pistonRodL, pistonRodR;
    Transform fan;

    // renderers & anim lists
    Renderer energyPanelRenderer;
    Renderer coreRenderer;
    readonly List<Renderer> ledRenderers = new();
    readonly List<Renderer> beaconRenderers = new();

    // materials
    Material mMetalDark, mMetalMid, mHazard, mEnergy, mLed, mCoreGlass;

    // runtime animation base
    MaterialPropertyBlock mpb;
    Vector3 basePos;
    Quaternion baseRot;
    Vector3 rodLBase, rodRBase;

    // for optional mesh saving
    readonly List<(string name, Mesh mesh)> generatedMeshes = new();

    void OnEnable()
    {
        mpb ??= new MaterialPropertyBlock();

        if (!Application.isPlaying && autoRegenerateInEditor)
            Generate();
    }

    void Start()
    {
        mpb ??= new MaterialPropertyBlock();

        if (Application.isPlaying && clearAndRebuildOnPlay)
            Generate();
    }

    void Update()
    {
        if (root == null) return;

        if (Application.isPlaying)
            Animate(Time.time);
        else if (autoRegenerateInEditor)
            Generate(); // editor live update
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        mpb ??= new MaterialPropertyBlock();

        ClearGeneratedImmediate();
        CreateMaterials();

        root = new GameObject("IND_LIFT_ROOT").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        basePos = root.localPosition;
        baseRot = root.localRotation;

        BuildHull();
        BuildTopDeckAndCollider();
        BuildSideFaceDetailsForZView(); // important for 2.5D side view
        BuildBoltsOnSideFace();
        BuildRimLEDs();
        BuildCore();
        BuildPistons();
        BuildUnderFan();
        BuildCornerBeacons();
        BuildAnchors();

        if (createLightsIfNone) SetupLightsIfMissing_Unity6();
        if (createURPVolumeBloom) SetupURPVolumeBloom_Unity6();

#if UNITY_EDITOR
        if (!Application.isPlaying && saveMeshAssets)
            SaveMeshesAsAssets_EditorOnly();
#endif
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        ClearGeneratedImmediate();
    }

    void ClearGeneratedImmediate()
    {
        var old = transform.Find("IND_LIFT_ROOT");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        root = null;
        topDeck = null;
        pistonRodL = pistonRodR = null;
        fan = null;
        energyPanelRenderer = null;
        coreRenderer = null;

        ledRenderers.Clear();
        beaconRenderers.Clear();
        generatedMeshes.Clear();
    }

    // --------------------------------------------------------------------
    // MATERIALS + procedural energy texture
    // --------------------------------------------------------------------
    void CreateMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!lit) lit = Shader.Find("Standard");

        mMetalDark = new Material(lit);
        SetLit(mMetalDark, metalDark, 0.92f, 0.55f, Color.black, 0);

        mMetalMid = new Material(lit);
        SetLit(mMetalMid, metalMid, 0.78f, 0.45f, Color.black, 0);

        mHazard = new Material(lit);
        SetLit(mHazard, hazardYellow, 0.05f, 0.35f, Color.black, 0);

        mLed = new Material(lit);
        SetLit(mLed, new Color(0.05f, 0.06f, 0.08f, 1), 0.0f, 0.25f, energyCyan, 2.0f);

        mEnergy = new Material(lit);
        SetLit(mEnergy, new Color(0.08f, 0.09f, 0.11f, 1), 0.15f, 0.25f, energyCyan, 4.0f);
        AssignBaseMap(mEnergy, CreateEnergyTexture(1024));

        mCoreGlass = new Material(lit);
        SetLit(mCoreGlass, new Color(0.15f, 0.55f, 1.0f, 0.75f), 0.0f, 0.92f, new Color(0.08f, 0.15f, 0.20f, 1), 1.2f);
        ForceTransparentURP(mCoreGlass);
    }

    void SetLit(Material m, Color baseColor, float metallic, float smoothness, Color emission, float emissionIntensity)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);

        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission * Mathf.Max(0f, emissionIntensity));
        }
    }

    void ForceTransparentURP(Material m)
    {
        // URP Lit supports _Surface; this is stable in Unity 6.
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // 1 = Transparent
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    void AssignBaseMap(Material m, Texture2D tex)
    {
        if (!tex) return;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
    }

    Texture2D CreateEnergyTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 6;

        Color[] px = new Color[size * size];
        float inv = 1f / (size - 1);

        for (int y = 0; y < size; y++)
        {
            float v = y * inv;
            for (int x = 0; x < size; x++)
            {
                float u = x * inv;

                // diagonal flow + bars + scanlines (works well when scrolling V upward)
                float diag = Mathf.Sin((u * 18f + v * 32f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                diag = Mathf.Pow(diag, 6f);

                float bars = Mathf.Sin(u * Mathf.PI * 2f * 36f) * 0.5f + 0.5f;
                bars = Mathf.Pow(bars, 10f);

                float scan = Mathf.Sin(v * Mathf.PI * 2f * 8f) * 0.5f + 0.5f;
                scan = Mathf.Lerp(0.65f, 1.0f, scan);

                float val = Mathf.Clamp01(diag * 0.65f + bars * 0.55f) * scan;
                px[y * size + x] = new Color(val, val, val, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    // --------------------------------------------------------------------
    // BUILD GEOMETRY
    // --------------------------------------------------------------------
    void BuildHull()
    {
        // Main hull (rounded)
        var hull = CreateMeshGO("Hull");
        var mf = hull.AddComponent<MeshFilter>();
        var mr = hull.AddComponent<MeshRenderer>();

        Mesh hullMesh = CreateRoundedRectPrism(widthX, depthZ, thicknessY, cornerRadius, cornerSegments);
        mf.sharedMesh = hullMesh;
        mr.sharedMaterial = mMetalMid;

        hull.transform.localPosition = new Vector3(0, thicknessY * 0.5f, 0);

        // Underframe (darker)
        var under = CreateMeshGO("UnderFrame");
        var uf = under.AddComponent<MeshFilter>();
        var ur = under.AddComponent<MeshRenderer>();

        Mesh underMesh = CreateRoundedRectPrism(widthX * 0.86f, depthZ * 0.78f, thicknessY * 0.34f, cornerRadius * 0.75f, cornerSegments);
        uf.sharedMesh = underMesh;
        ur.sharedMaterial = mMetalDark;
        under.transform.localPosition = new Vector3(0, thicknessY * 0.18f, 0);

        // Hazard edge plate (front face at z = -depth/2)
        float faceZ = -depthZ * 0.5f + 0.03f;

        var hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazard.name = "HazardEdgePlate";
        hazard.transform.SetParent(root, false);
        hazard.transform.localScale = new Vector3(widthX * 0.92f, thicknessY * 0.12f, 0.06f);
        hazard.transform.localPosition = new Vector3(0, thicknessY * 0.92f, faceZ);
        DestroyCollider(hazard);
        hazard.GetComponent<Renderer>().sharedMaterial = mHazard;

        // Dark diagonal bars
        for (int i = 0; i < 9; i++)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = $"HazBar_{i:00}";
            bar.transform.SetParent(hazard.transform, false);
            bar.transform.localScale = new Vector3(0.055f, 0.98f, 0.55f);
            bar.transform.localPosition = new Vector3(Mathf.Lerp(-0.42f, 0.42f, i / 8f), 0, 0);
            bar.transform.localRotation = Quaternion.Euler(0, 0, 25f);
            DestroyCollider(bar);
            bar.GetComponent<Renderer>().sharedMaterial = mMetalDark;
        }
    }

    void BuildTopDeckAndCollider()
    {
        var deck = CreateMeshGO("TopDeck");
        topDeck = deck.transform;

        var df = deck.AddComponent<MeshFilter>();
        var dr = deck.AddComponent<MeshRenderer>();

        Mesh deckMesh = CreateRoundedRectPrism(widthX * 0.90f, depthZ * 0.82f, thicknessY * 0.12f, cornerRadius * 0.70f, cornerSegments);
        df.sharedMesh = deckMesh;
        dr.sharedMaterial = mMetalMid;

        topDeck.localPosition = new Vector3(0, thicknessY * 0.90f, 0);

        // Ribs
        var ribs = new GameObject("DeckRibs").transform;
        ribs.SetParent(topDeck, false);
        ribs.localPosition = new Vector3(0, thicknessY * 0.06f, 0);

        int ribCount = 9;
        for (int i = 0; i < ribCount; i++)
        {
            float x = Mathf.Lerp(-widthX * 0.36f, widthX * 0.36f, i / (float)(ribCount - 1));
            var rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rib.name = $"Rib_{i:00}";
            rib.transform.SetParent(ribs, false);
            rib.transform.localScale = new Vector3(widthX * 0.018f, 0.012f, depthZ * 0.70f);
            rib.transform.localPosition = new Vector3(x, 0.02f, 0);
            DestroyCollider(rib);
            rib.GetComponent<Renderer>().sharedMaterial = mMetalDark;
        }

        // Player top collider
        var colGO = new GameObject("TopCollider");
        colGO.transform.SetParent(root, false);
        colGO.transform.localPosition = new Vector3(0, thicknessY * 1.03f, 0);
        var bc = colGO.AddComponent<BoxCollider>();
        bc.size = new Vector3(widthX * 0.88f, 0.06f, depthZ * 0.80f);
    }

    void BuildSideFaceDetailsForZView()
    {
        // camera looks along Z -> detail the face at z = -depth/2
        float faceZ = -depthZ * 0.5f + 0.02f;

        // main side panel
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "SidePanel_Main";
        panel.transform.SetParent(root, false);
        panel.transform.localScale = new Vector3(widthX * 0.78f, thicknessY * 0.46f, 0.05f);
        panel.transform.localPosition = new Vector3(0, thicknessY * 0.55f, faceZ);
        DestroyCollider(panel);
        panel.GetComponent<Renderer>().sharedMaterial = mMetalDark;

        // energy conveyor (scrolls up)
        var energy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        energy.name = "EnergyConveyorPanel";
        energy.transform.SetParent(root, false);
        energy.transform.localScale = new Vector3(widthX * 0.62f, thicknessY * 0.22f, 0.045f);
        energy.transform.localPosition = new Vector3(0, thicknessY * 0.55f, faceZ + 0.018f);
        DestroyCollider(energy);

        energyPanelRenderer = energy.GetComponent<Renderer>();
        energyPanelRenderer.sharedMaterial = mEnergy;

        // vents
        for (int v = 0; v < ventCount; v++)
        {
            var vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = $"Vent_{v:00}";
            vent.transform.SetParent(root, false);
            vent.transform.localScale = new Vector3(widthX * 0.12f, thicknessY * 0.16f, 0.06f);
            vent.transform.localPosition = new Vector3(
                Mathf.Lerp(-widthX * 0.30f, widthX * 0.30f, v / Mathf.Max(1f, ventCount - 1f)),
                thicknessY * 0.40f,
                faceZ
            );
            DestroyCollider(vent);
            vent.GetComponent<Renderer>().sharedMaterial = mMetalMid;

            // slats
            for (int s = 0; s < 5; s++)
            {
                var slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = $"Slat_{v:00}_{s:00}";
                slat.transform.SetParent(vent.transform, false);
                slat.transform.localScale = new Vector3(0.86f, 0.06f, 0.55f);
                slat.transform.localPosition = new Vector3(0, Mathf.Lerp(-0.30f, 0.30f, s / 4f), 0);
                DestroyCollider(slat);
                slat.GetComponent<Renderer>().sharedMaterial = mMetalDark;
            }
        }
    }

    void BuildBoltsOnSideFace()
    {
        float z = -depthZ * 0.5f + 0.04f;
        float y = thicknessY * 0.34f;

        for (int i = 0; i < boltsPerFace; i++)
        {
            float x = Mathf.Lerp(-widthX * 0.44f, widthX * 0.44f, i / (float)(boltsPerFace - 1));
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bolt.name = $"Bolt_{i:00}";
            bolt.transform.SetParent(root, false);
            bolt.transform.localScale = new Vector3(0.032f, 0.010f, 0.032f);
            bolt.transform.localPosition = new Vector3(x, y, z);
            bolt.transform.localRotation = Quaternion.Euler(90, 0, 0);
            DestroyCollider(bolt);
            bolt.GetComponent<Renderer>().sharedMaterial = mMetalDark;
        }
    }

    void BuildRimLEDs()
    {
        float rX = widthX * 0.44f;
        float rZ = depthZ * 0.40f;
        float y = thicknessY * 0.94f;

        for (int i = 0; i < ledCount; i++)
        {
            float a = (i / (float)ledCount) * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * rX;
            float z = Mathf.Sin(a) * rZ;

            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
            led.name = $"LED_{i:00}";
            led.transform.SetParent(root, false);
            led.transform.localScale = new Vector3(0.08f, 0.03f, 0.03f);
            led.transform.localPosition = new Vector3(x, y, z);
            led.transform.localRotation = Quaternion.LookRotation(new Vector3(x, 0, z).normalized, Vector3.up);
            DestroyCollider(led);

            var rr = led.GetComponent<Renderer>();
            rr.sharedMaterial = mLed;
            ledRenderers.Add(rr);
        }
    }

    void BuildCore()
    {
        var coreGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreGO.name = "LiftCore";
        coreGO.transform.SetParent(root, false);
        coreGO.transform.localScale = Vector3.one * 0.34f;
        coreGO.transform.localPosition = new Vector3(0, thicknessY * 0.48f, 0);
        DestroyCollider(coreGO);

        coreRenderer = coreGO.GetComponent<Renderer>();
        coreRenderer.sharedMaterial = mCoreGlass;

        // small point light so it "self illuminates" a bit
        var lgo = new GameObject("CoreLight");
        lgo.transform.SetParent(root, false);
        lgo.transform.localPosition = coreGO.transform.localPosition;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 4.5f;
        l.intensity = 1.8f;
        l.color = energyCyan;
    }

    void BuildPistons()
    {
        var pL = new GameObject("Piston_L").transform;
        pL.SetParent(root, false);
        pL.localPosition = new Vector3(-widthX * 0.40f, thicknessY * 0.22f, 0.0f);

        var pR = new GameObject("Piston_R").transform;
        pR.SetParent(root, false);
        pR.localPosition = new Vector3(widthX * 0.40f, thicknessY * 0.22f, 0.0f);

        pistonRodL = MakePiston(pL, +1);
        pistonRodR = MakePiston(pR, -1);

        rodLBase = pistonRodL.localPosition;
        rodRBase = pistonRodR.localPosition;
    }

    Transform MakePiston(Transform parent, int dir)
    {
        var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube";
        tube.transform.SetParent(parent, false);
        tube.transform.localScale = new Vector3(0.11f, 0.19f, 0.11f);
        tube.transform.localRotation = Quaternion.Euler(0, 0, 90);
        DestroyCollider(tube);
        tube.GetComponent<Renderer>().sharedMaterial = mMetalMid;

        var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rod.name = "Rod";
        rod.transform.SetParent(parent, false);
        rod.transform.localScale = new Vector3(0.075f, 0.26f, 0.075f);
        rod.transform.localRotation = Quaternion.Euler(0, 0, 90);
        rod.transform.localPosition = new Vector3(0.18f * dir, 0, 0);
        DestroyCollider(rod);
        rod.GetComponent<Renderer>().sharedMaterial = mMetalDark;

        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "GlowCap";
        cap.transform.SetParent(parent, false);
        cap.transform.localScale = new Vector3(0.13f, 0.055f, 0.13f);
        cap.transform.localPosition = new Vector3(0.36f * dir, 0, 0);
        DestroyCollider(cap);

        var capMat = new Material(mMetalDark);
        SetLit(capMat, metalDark, 0.0f, 0.25f, energyCyan, 8.0f);
        cap.GetComponent<Renderer>().sharedMaterial = capMat;

        return rod.transform;
    }

    void BuildUnderFan()
    {
        fan = new GameObject("UnderFan").transform;
        fan.SetParent(root, false);
        fan.localPosition = new Vector3(0, thicknessY * 0.10f, -depthZ * 0.12f);

        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "Hub";
        hub.transform.SetParent(fan, false);
        hub.transform.localScale = new Vector3(0.18f, 0.03f, 0.18f);
        DestroyCollider(hub);
        hub.GetComponent<Renderer>().sharedMaterial = mMetalMid;

        int blades = 8;
        for (int i = 0; i < blades; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = $"Blade_{i:00}";
            b.transform.SetParent(fan, false);
            b.transform.localScale = new Vector3(0.48f, 0.012f, 0.07f);
            b.transform.localPosition = new Vector3(0.23f, 0, 0);
            b.transform.localRotation = Quaternion.Euler(0, i * (360f / blades), 14f);
            DestroyCollider(b);
            b.GetComponent<Renderer>().sharedMaterial = mMetalDark;
        }
    }

    void BuildCornerBeacons()
    {
        Vector3[] corners =
        {
            new Vector3(-widthX*0.42f, thicknessY*0.98f, -depthZ*0.42f),
            new Vector3( widthX*0.42f, thicknessY*0.98f, -depthZ*0.42f),
            new Vector3(-widthX*0.42f, thicknessY*0.98f,  depthZ*0.42f),
            new Vector3( widthX*0.42f, thicknessY*0.98f,  depthZ*0.42f),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = $"Beacon_{i:00}";
            b.transform.SetParent(root, false);
            b.transform.localScale = Vector3.one * 0.10f;
            b.transform.localPosition = corners[i];
            DestroyCollider(b);

            var r = b.GetComponent<Renderer>();
            var mat = new Material(mMetalDark);
            SetLit(mat, metalDark, 0.0f, 0.25f, warningRed, 6.0f);
            r.sharedMaterial = mat;
            beaconRenderers.Add(r);
        }
    }

    void BuildAnchors()
    {
        var top = new GameObject("TopAnchor");
        top.transform.SetParent(root, false);
        top.transform.localPosition = new Vector3(0, thicknessY * 1.18f, 0);

        var bottom = new GameObject("BottomAnchor");
        bottom.transform.SetParent(root, false);
        bottom.transform.localPosition = new Vector3(0, 0, 0);
    }

    // --------------------------------------------------------------------
    // ANIMATION (no clips)
    // --------------------------------------------------------------------
    void Animate(float t)
    {
        float bob = Mathf.Sin(t * hoverSpeed) * hoverAmp;
        float tiltX = Mathf.Sin(t * microTiltSpeed) * microTiltDeg;
        float tiltZ = Mathf.Sin(t * (microTiltSpeed * 1.18f)) * microTiltDeg * 0.65f;

        root.localPosition = basePos + new Vector3(0, bob, 0);
        root.localRotation = baseRot * Quaternion.Euler(tiltX, 0, tiltZ);

        // piston slide
        float p = (Mathf.Sin(t * pistonSpeed) * 0.5f + 0.5f) * pistonTravel;
        if (pistonRodL) pistonRodL.localPosition = rodLBase + new Vector3(p, 0, 0);
        if (pistonRodR) pistonRodR.localPosition = rodRBase + new Vector3(-p, 0, 0);

        // fan spin
        if (fan) fan.Rotate(0, fanSpinDegPerSec * Time.deltaTime, 0, Space.Self);

        // energy panel scroll UP (V offset)
        if (mEnergy && mEnergy.HasProperty("_BaseMap_ST"))
        {
            Vector4 st = mEnergy.GetVector("_BaseMap_ST");
            st.w = t * energyScrollSpeed;
            mEnergy.SetVector("_BaseMap_ST", st);
        }

        // LED chase
        for (int i = 0; i < ledRenderers.Count; i++)
        {
            float x = Mathf.Sin(t * ledSpeed - i * 0.42f);
            float v = Mathf.Pow((x * 0.5f + 0.5f), ledSharpness);
            float intensity = Mathf.Lerp(ledMin, ledMax, v);

            var r = ledRenderers[i];
            r.GetPropertyBlock(mpb);
            if (r.sharedMaterial && r.sharedMaterial.HasProperty("_EmissionColor"))
                mpb.SetColor("_EmissionColor", energyCyan * intensity);
            r.SetPropertyBlock(mpb);
        }

        // core pulse
        if (coreRenderer && coreRenderer.sharedMaterial && coreRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            float s = Mathf.Sin(t * 1.2f) * 0.5f + 0.5f;
            float intensity = Mathf.Lerp(2.5f, 12.0f, s) + 1.0f * Mathf.Sin(t * 5.6f);
            coreRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", energyCyan * Mathf.Max(0f, intensity));
            coreRenderer.SetPropertyBlock(mpb);
        }

        // beacon blink
        for (int i = 0; i < beaconRenderers.Count; i++)
        {
            float blink = Mathf.Sin(t * 3.6f + i * 0.9f) > 0.2f ? 1f : 0.1f;
            var r = beaconRenderers[i];
            r.GetPropertyBlock(mpb);
            if (r.sharedMaterial && r.sharedMaterial.HasProperty("_EmissionColor"))
                mpb.SetColor("_EmissionColor", warningRed * (6.0f * blink));
            r.SetPropertyBlock(mpb);
        }
    }

    // --------------------------------------------------------------------
    // MESH: rounded rectangle prism
    // --------------------------------------------------------------------
    Mesh CreateRoundedRectPrism(float w, float d, float h, float r, int seg)
    {
        r = Mathf.Clamp(r, 0.0001f, Mathf.Min(w, d) * 0.49f);
        int corner = Mathf.Max(2, seg);

        List<Vector3> outline = new();
        Vector2[] centers =
        {
            new Vector2( w*0.5f - r,  d*0.5f - r),
            new Vector2(-w*0.5f + r,  d*0.5f - r),
            new Vector2(-w*0.5f + r, -d*0.5f + r),
            new Vector2( w*0.5f - r, -d*0.5f + r),
        };
        float[] startAng = { 0, 90, 180, 270 };

        for (int c = 0; c < 4; c++)
        {
            for (int i = 0; i <= corner; i++)
            {
                float a = (startAng[c] + (i / (float)corner) * 90f) * Mathf.Deg2Rad;
                float x = centers[c].x + Mathf.Cos(a) * r;
                float z = centers[c].y + Mathf.Sin(a) * r;
                outline.Add(new Vector3(x, 0, z));
            }
        }

        if (outline.Count > 1 && (outline[0] - outline[outline.Count - 1]).sqrMagnitude < 1e-6f)
            outline.RemoveAt(outline.Count - 1);

        int n = outline.Count;

        List<Vector3> v = new();
        List<Vector3> nrm = new();
        List<Vector2> uv = new();
        List<int> tri = new();

        for (int i = 0; i < n; i++)
        {
            Vector3 p = outline[i];
            v.Add(new Vector3(p.x, 0, p.z));
            v.Add(new Vector3(p.x, h, p.z));

            Vector3 nn = new Vector3(p.x, 0, p.z).normalized;
            nrm.Add(nn); nrm.Add(nn);

            float uu = i / (float)n;
            uv.Add(new Vector2(uu, 0));
            uv.Add(new Vector2(uu, 1));
        }

        for (int i = 0; i < n; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int j0 = ((i + 1) % n) * 2;
            int j1 = j0 + 1;

            tri.Add(i0); tri.Add(i1); tri.Add(j0);
            tri.Add(j0); tri.Add(i1); tri.Add(j1);
        }

        int topCenter = v.Count;
        v.Add(new Vector3(0, h, 0));
        nrm.Add(Vector3.up);
        uv.Add(new Vector2(0.5f, 0.5f));

        int botCenter = v.Count;
        v.Add(new Vector3(0, 0, 0));
        nrm.Add(Vector3.down);
        uv.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < n; i++)
        {
            int a = i * 2 + 1;
            int b = ((i + 1) % n) * 2 + 1;
            tri.Add(topCenter); tri.Add(a); tri.Add(b);
        }

        for (int i = 0; i < n; i++)
        {
            int a = ((i + 1) % n) * 2;
            int b = i * 2;
            tri.Add(botCenter); tri.Add(a); tri.Add(b);
        }

        Mesh m = new Mesh();
        m.name = "RoundedRectPrism";
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        m.SetVertices(v);
        m.SetNormals(nrm);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();

        generatedMeshes.Add((m.name, m));
        return m;
    }

    GameObject CreateMeshGO(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        return go;
    }

    void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c == null) return;
        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }

    // --------------------------------------------------------------------
    // URP Volume + Lights (Unity 6 APIs)
    // --------------------------------------------------------------------
    void SetupLightsIfMissing_Unity6()
    {
        if (Object.FindFirstObjectByType<Light>() != null) return;

        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.0f;
        key.transform.rotation = Quaternion.Euler(55f, 35f, 0f);

        var fill = new GameObject("FillLight").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.45f;
        fill.color = new Color(0.85f, 0.92f, 1.0f);
        fill.transform.rotation = Quaternion.Euler(75f, -25f, 0f);
    }

    void SetupURPVolumeBloom_Unity6()
    {
        if (Object.FindFirstObjectByType<Volume>() != null) return;

        var go = new GameObject("IND_LIFT_URP_VOLUME");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.value = 1.0f;
        bloom.scatter.value = 0.75f;
    }

#if UNITY_EDITOR
    void SaveMeshesAsAssets_EditorOnly()
    {
        if (string.IsNullOrWhiteSpace(saveFolder)) return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");

        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            string parent = System.IO.Path.GetDirectoryName(saveFolder).Replace("\\", "/");
            string name = System.IO.Path.GetFileName(saveFolder);
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", "Generated");
            AssetDatabase.CreateFolder(parent, name);
        }

        for (int i = 0; i < generatedMeshes.Count; i++)
        {
            var (n, mesh) = generatedMeshes[i];
            if (!mesh) continue;

            string path = $"{saveFolder}/{gameObject.name}_{n}_{i:00}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) continue;

            AssetDatabase.CreateAsset(Object.Instantiate(mesh), path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
#endif
}
