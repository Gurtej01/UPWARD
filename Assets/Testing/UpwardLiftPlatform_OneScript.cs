using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

/// <summary>
/// Upward Lift Platform (2.5D side-view friendly) — ONE SCRIPT, Unity-only.
/// - Procedural rounded-rect meshes (not flat cubes)
/// - High side-view readability (layering, vents, bolts, pistons, fan, greebles)
/// - "Upward" vibe via scrolling emissive energy strip (no arrows)
/// - Scripted motion (no animation clips): hover, piston pump, LED chase, core pulse, fan spin, strip scroll
/// - Optional URP Volume with Bloom for premium glow
///
/// Attach to empty GameObject and press Play.
/// </summary>
public class UpwardLiftPlatform_OneScript : MonoBehaviour
{
    [Header("Auto Setup (optional)")]
    public bool createSideCameraIfNone = true;
    public bool createURPVolume = true;
    public bool createBasicLightsIfNone = true;

    [Header("Platform Size")]
    public float width = 3.0f;      // X
    public float depth = 1.6f;      // Z
    public float height = 0.42f;    // Y thickness
    public float cornerRadius = 0.28f;
    [Range(6, 64)] public int cornerSegments = 24;

    [Header("Detail Density")]
    [Range(6, 40)] public int boltCountPerSide = 16;
    [Range(8, 60)] public int ledCount = 28;

    [Header("Alive Motion")]
    public float hoverAmp = 0.02f;
    public float hoverSpeed = 1.0f;
    public float microTiltDeg = 1.1f;
    public float microTiltSpeed = 0.75f;

    public float fanSpinDegPerSec = 720f;
    public float ringSpinDegPerSec = 90f;

    [Header("LED / Energy")]
    public float ledSpeed = 2.4f;
    public float ledSharpness = 9f;
    public float ledMin = 0.15f;
    public float ledMax = 9.0f;

    public float corePulseMin = 2.0f;
    public float corePulseMax = 12.0f;
    public float corePulseSpeed = 1.2f;

    public float energyStripScrollSpeed = 0.18f; // scroll UP (V offset)

    [Header("Pistons")]
    public float pistonTravel = 0.09f;
    public float pistonSpeed = 1.4f;

    // hierarchy
    Transform root;
    Transform topDeck;
    Transform ring;
    Transform fan;
    Transform pistonRodL;
    Transform pistonRodR;

    // renderers
    Renderer coreRenderer;
    Renderer energyStripRenderer;
    List<Renderer> ledRenderers = new();

    // materials
    Material mPlastic;
    Material mMetal;
    Material mGlass;
    Material mLed;
    Material mEnergyStrip;

    MaterialPropertyBlock mpb;

    // animation base
    Vector3 basePos;
    Quaternion baseRot;
    Vector3 rodLBase;
    Vector3 rodRBase;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        Build();
    }

    void Update()
    {
        Animate(Time.time);
    }

    // ------------------------------------------------------------
    // Build
    // ------------------------------------------------------------
    void Build()
    {
        // clear previous
        var old = transform.Find("UP_LIFT_ROOT");
        if (old) Destroy(old.gameObject);

        root = new GameObject("UP_LIFT_ROOT").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        basePos = root.localPosition;
        baseRot = root.localRotation;

        CreateMaterials();
        CreateMainBody();
        CreateTopDeckAndCollider();
        CreateSideEnergyStrip();
        CreateBolts();
        CreateLEDs();
        CreateCore();
        CreateRingAndFan();
        CreatePistons();
        CreateAnchors();

        if (createBasicLightsIfNone) SetupLightsIfMissing();
        if (createSideCameraIfNone) SetupSideCameraIfMissing();
        if (createURPVolume) SetupURPVolume();
    }

    // ------------------------------------------------------------
    // Materials / Textures
    // ------------------------------------------------------------
    void CreateMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!lit) lit = Shader.Find("Standard");

        mPlastic = new Material(lit);
        SetLit(mPlastic, new Color(0.95f, 0.96f, 0.99f, 1), 0.03f, 0.74f, Color.black, 0);

        mMetal = new Material(lit);
        SetLit(mMetal, new Color(0.12f, 0.13f, 0.16f, 1), 0.88f, 0.55f, Color.black, 0);

        mGlass = new Material(lit);
        SetLit(mGlass, new Color(0.18f, 0.60f, 1.0f, 0.75f), 0.0f, 0.92f, new Color(0.08f, 0.15f, 0.20f, 1), 1.0f);
        ForceTransparent(mGlass);

        // LED material (emission controlled by MPB)
        mLed = new Material(lit);
        SetLit(mLed, new Color(0.06f, 0.07f, 0.09f, 1), 0.0f, 0.35f, new Color(0.15f, 0.95f, 1.0f, 1), 2.0f);

        // Energy strip material: lit + emissive + texture scroll
        mEnergyStrip = new Material(lit);
        SetLit(mEnergyStrip, new Color(0.08f, 0.09f, 0.12f, 1), 0.15f, 0.35f, new Color(0.15f, 0.95f, 1.0f, 1), 3.5f);

        // procedural stripe texture (no imports)
        Texture2D stripeTex = CreateStripeTexture(1024);
        AssignBaseMap(mEnergyStrip, stripeTex);
    }

    void SetLit(Material m, Color baseColor, float metallic, float smoothness, Color emission, float emissionIntensity)
    {
        if (!m) return;
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

    void ForceTransparent(Material m)
    {
        if (!m) return;
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    void AssignBaseMap(Material m, Texture2D tex)
    {
        if (!m || !tex) return;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
    }

    Texture2D CreateStripeTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false);
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

                // thin vertical bars + scanlines + micro noise
                float bars = Mathf.Sin(u * Mathf.PI * 2f * 40f) * 0.5f + 0.5f;
                bars = Mathf.Pow(bars, 10f);

                float scan = Mathf.Sin(v * Mathf.PI * 2f * 6f) * 0.5f + 0.5f;
                scan = Mathf.Lerp(0.65f, 1.0f, scan);

                float micro = Mathf.Sin((u * 260f + v * 140f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                micro = Mathf.Lerp(0.85f, 1.0f, micro);

                float val = bars * scan * micro;
                px[y * size + x] = new Color(val, val, val, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    // ------------------------------------------------------------
    // Geometry
    // ------------------------------------------------------------
    void CreateMainBody()
    {
        // Main hull (rounded rectangle prism)
        var hull = CreateMeshGO("Hull", root);
        var mf = hull.AddComponent<MeshFilter>();
        var mr = hull.AddComponent<MeshRenderer>();

        mf.sharedMesh = CreateRoundedRectPrism(width, depth, height, cornerRadius, cornerSegments);
        mr.sharedMaterial = mPlastic;

        hull.transform.localPosition = new Vector3(0, height * 0.5f, 0);

        // Underframe (smaller + metal)
        var under = CreateMeshGO("UnderFrame", root);
        var uf = under.AddComponent<MeshFilter>();
        var ur = under.AddComponent<MeshRenderer>();
        uf.sharedMesh = CreateRoundedRectPrism(width * 0.86f, depth * 0.78f, height * 0.35f, cornerRadius * 0.75f, cornerSegments);
        ur.sharedMaterial = mMetal;
        under.transform.localPosition = new Vector3(0, height * 0.18f, 0);
    }

    void CreateTopDeckAndCollider()
    {
        // Top deck inset (player stands here)
        topDeck = CreateMeshGO("TopDeck", root).transform;
        var df = topDeck.gameObject.AddComponent<MeshFilter>();
        var dr = topDeck.gameObject.AddComponent<MeshRenderer>();

        df.sharedMesh = CreateRoundedRectPrism(width * 0.90f, depth * 0.82f, height * 0.12f, cornerRadius * 0.7f, cornerSegments);
        dr.sharedMaterial = mPlastic;
        topDeck.localPosition = new Vector3(0, height * 0.88f, 0);

        // Grip grooves (visual only, gives side/readable detail)
        var grooves = new GameObject("Grooves").transform;
        grooves.SetParent(topDeck, false);
        grooves.localPosition = new Vector3(0, height * 0.07f, 0);

        int gCount = 7;
        for (int i = 0; i < gCount; i++)
        {
            float x = Mathf.Lerp(-width * 0.36f, width * 0.36f, i / (float)(gCount - 1));
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = $"Groove_{i:00}";
            g.transform.SetParent(grooves, false);
            g.transform.localScale = new Vector3(width * 0.02f, 0.01f, depth * 0.72f);
            g.transform.localPosition = new Vector3(x, 0.02f, 0);
            Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = mMetal;
        }

        // Collider for player standing
        var colGO = new GameObject("TopCollider");
        colGO.transform.SetParent(root, false);
        colGO.transform.localPosition = new Vector3(0, height * 1.02f, 0);
        var bc = colGO.AddComponent<BoxCollider>();
        bc.size = new Vector3(width * 0.88f, 0.05f, depth * 0.80f);
    }

    void CreateSideEnergyStrip()
    {
        // Back side panel (shows well in side view camera looking along Z)
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "EnergyStripPanel";
        panel.transform.SetParent(root, false);

        // Put it on the "back" edge (negative Z) so side camera sees it as a stripe on the silhouette
        panel.transform.localScale = new Vector3(width * 0.82f, height * 0.42f, 0.045f);
        panel.transform.localPosition = new Vector3(0, height * 0.55f, -depth * 0.50f + 0.02f);
        Destroy(panel.GetComponent<Collider>());

        energyStripRenderer = panel.GetComponent<Renderer>();
        energyStripRenderer.sharedMaterial = mEnergyStrip;

        // Add some vent blocks next to it (greeble)
        for (int i = 0; i < 2; i++)
        {
            var vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = $"VentBlock_{i:00}";
            vent.transform.SetParent(root, false);
            vent.transform.localScale = new Vector3(width * 0.12f, height * 0.18f, 0.06f);
            vent.transform.localPosition = new Vector3((i == 0 ? -1f : 1f) * width * 0.34f, height * 0.50f, -depth * 0.50f + 0.02f);
            Destroy(vent.GetComponent<Collider>());
            vent.GetComponent<Renderer>().sharedMaterial = mMetal;
        }
    }

    void CreateBolts()
    {
        // Bolts along back edge — extremely readable in side view
        float y = height * 0.34f;
        float z = -depth * 0.50f + 0.035f;

        for (int i = 0; i < boltCountPerSide; i++)
        {
            float x = Mathf.Lerp(-width * 0.42f, width * 0.42f, i / (float)(boltCountPerSide - 1));
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bolt.name = $"Bolt_{i:00}";
            bolt.transform.SetParent(root, false);
            bolt.transform.localScale = new Vector3(0.03f, 0.008f, 0.03f);
            bolt.transform.localPosition = new Vector3(x, y, z);
            bolt.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Destroy(bolt.GetComponent<Collider>());
            bolt.GetComponent<Renderer>().sharedMaterial = mMetal;
        }
    }

    void CreateLEDs()
    {
        // LED “capsules” around the rim
        float rX = width * 0.44f;
        float rZ = depth * 0.40f;
        float y = height * 0.92f;

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
            Destroy(led.GetComponent<Collider>());

            var rr = led.GetComponent<Renderer>();
            rr.sharedMaterial = mLed;
            ledRenderers.Add(rr);
        }
    }

    void CreateCore()
    {
        // Glowing core under the deck (visible from side, not blocking player)
        var coreGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        coreGO.name = "LiftCore";
        coreGO.transform.SetParent(root, false);
        coreGO.transform.localScale = Vector3.one * 0.32f;
        coreGO.transform.localPosition = new Vector3(0, height * 0.45f, 0);
        Destroy(coreGO.GetComponent<Collider>());
        coreRenderer = coreGO.GetComponent<Renderer>();
        coreRenderer.sharedMaterial = mGlass;
    }

    void CreateRingAndFan()
    {
        // Thin ring above deck edge (alive detail from side view)
        ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        ring.name = "RimRing";
        ring.SetParent(root, false);
        ring.localScale = new Vector3(width * 0.32f, 0.01f, depth * 0.32f);
        ring.localPosition = new Vector3(0, height * 1.02f, 0);
        ring.localRotation = Quaternion.identity;
        Destroy(ring.GetComponent<Collider>());
        ring.GetComponent<Renderer>().sharedMaterial = mMetal;

        // Spinning fan under the platform (side readable)
        fan = new GameObject("UnderFan").transform;
        fan.SetParent(root, false);
        fan.localPosition = new Vector3(0, height * 0.08f, -depth * 0.18f);

        var fanHub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fanHub.name = "Hub";
        fanHub.transform.SetParent(fan, false);
        fanHub.transform.localScale = new Vector3(0.18f, 0.03f, 0.18f);
        fanHub.transform.localRotation = Quaternion.identity;
        Destroy(fanHub.GetComponent<Collider>());
        fanHub.GetComponent<Renderer>().sharedMaterial = mMetal;

        int blades = 7;
        for (int i = 0; i < blades; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = $"Blade_{i:00}";
            b.transform.SetParent(fan, false);
            b.transform.localScale = new Vector3(0.42f, 0.01f, 0.06f);
            b.transform.localPosition = new Vector3(0.20f, 0, 0);
            b.transform.localRotation = Quaternion.Euler(0, i * (360f / blades), 12f);
            Destroy(b.GetComponent<Collider>());
            b.GetComponent<Renderer>().sharedMaterial = mMetal;
        }
    }

    void CreatePistons()
    {
        // Pistons visible from side (left/right), pumping upward “lift” vibe
        var pL = new GameObject("Piston_L").transform;
        pL.SetParent(root, false);
        pL.localPosition = new Vector3(-width * 0.38f, height * 0.22f, 0.0f);

        var pR = new GameObject("Piston_R").transform;
        pR.SetParent(root, false);
        pR.localPosition = new Vector3(width * 0.38f, height * 0.22f, 0.0f);

        pistonRodL = MakePiston(pL, +1);
        pistonRodR = MakePiston(pR, -1);

        rodLBase = pistonRodL.localPosition;
        rodRBase = pistonRodR.localPosition;
    }

    Transform MakePiston(Transform parent, int dir)
    {
        // tube
        var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tube.name = "Tube";
        tube.transform.SetParent(parent, false);
        tube.transform.localScale = new Vector3(0.10f, 0.18f, 0.10f);
        tube.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Destroy(tube.GetComponent<Collider>());
        tube.GetComponent<Renderer>().sharedMaterial = mMetal;

        // rod
        var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rod.name = "Rod";
        rod.transform.SetParent(parent, false);
        rod.transform.localScale = new Vector3(0.07f, 0.24f, 0.07f);
        rod.transform.localRotation = Quaternion.Euler(0, 0, 90);
        rod.transform.localPosition = new Vector3(0.18f * dir, 0, 0);
        Destroy(rod.GetComponent<Collider>());
        rod.GetComponent<Renderer>().sharedMaterial = mMetal;

        // glow cap
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "GlowCap";
        cap.transform.SetParent(parent, false);
        cap.transform.localScale = new Vector3(0.12f, 0.05f, 0.12f);
        cap.transform.localPosition = new Vector3(0.35f * dir, 0, 0);
        Destroy(cap.GetComponent<Collider>());
        var capMat = new Material(mMetal);
        SetLit(capMat, new Color(0.10f, 0.11f, 0.13f, 1), 0.0f, 0.25f, new Color(0.15f, 0.95f, 1.0f, 1), 8.0f);
        cap.GetComponent<Renderer>().sharedMaterial = capMat;

        return rod.transform;
    }

    void CreateAnchors()
    {
        // Useful in your game: where the next platform should appear above
        var top = new GameObject("TopAnchor");
        top.transform.SetParent(root, false);
        top.transform.localPosition = new Vector3(0, height * 1.18f, 0);

        var bottom = new GameObject("BottomAnchor");
        bottom.transform.SetParent(root, false);
        bottom.transform.localPosition = new Vector3(0, 0, 0);
    }

    // ------------------------------------------------------------
    // Animate (no clips, no scaling)
    // ------------------------------------------------------------
    void Animate(float t)
    {
        // hover + micro tilt
        float bob = Mathf.Sin(t * hoverSpeed) * hoverAmp;
        float tiltX = Mathf.Sin(t * microTiltSpeed) * microTiltDeg;
        float tiltZ = Mathf.Sin(t * (microTiltSpeed * 1.18f)) * microTiltDeg * 0.65f;

        root.localPosition = basePos + new Vector3(0, bob, 0);
        root.localRotation = baseRot * Quaternion.Euler(tiltX, 0, tiltZ);

        // ring spin
        if (ring) ring.Rotate(0, ringSpinDegPerSec * Time.deltaTime, 0, Space.Self);

        // fan spin
        if (fan) fan.Rotate(0, fanSpinDegPerSec * Time.deltaTime, 0, Space.Self);

        // piston pump (local slide)
        float p = (Mathf.Sin(t * pistonSpeed) * 0.5f + 0.5f) * pistonTravel;
        if (pistonRodL) pistonRodL.localPosition = rodLBase + new Vector3(p, 0, 0);
        if (pistonRodR) pistonRodR.localPosition = rodRBase + new Vector3(-p, 0, 0);

        // energy strip scroll UP (offsetY)
        if (mEnergyStrip && mEnergyStrip.HasProperty("_BaseMap_ST"))
        {
            Vector4 st = mEnergyStrip.GetVector("_BaseMap_ST");
            st.w = t * energyStripScrollSpeed; // offsetY
            mEnergyStrip.SetVector("_BaseMap_ST", st);
        }

        // LED chase
        int n = ledRenderers.Count;
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Sin(t * ledSpeed - i * 0.42f);
            float v = Mathf.Pow((x * 0.5f + 0.5f), ledSharpness);
            float intensity = Mathf.Lerp(ledMin, ledMax, v);

            var r = ledRenderers[i];
            r.GetPropertyBlock(mpb);
            if (r.sharedMaterial && r.sharedMaterial.HasProperty("_EmissionColor"))
                mpb.SetColor("_EmissionColor", new Color(0.15f, 0.95f, 1.0f, 1) * intensity);
            r.SetPropertyBlock(mpb);
        }

        // core pulse
        if (coreRenderer && coreRenderer.sharedMaterial && coreRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            float s = Mathf.Sin(t * corePulseSpeed) * 0.5f + 0.5f;
            float intensity = Mathf.Lerp(corePulseMin, corePulseMax, s) + 1.5f * Mathf.Sin(t * 5.6f);
            coreRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", new Color(0.10f, 0.85f, 1.0f, 1) * Mathf.Max(0.0f, intensity));
            coreRenderer.SetPropertyBlock(mpb);
        }
    }

    // ------------------------------------------------------------
    // Side camera / lighting / URP Volume (optional)
    // ------------------------------------------------------------
    void SetupSideCameraIfMissing()
    {
        if (Camera.main) return;

        var camGO = new GameObject("SideCamera_2p5D");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 2.2f;
        cam.transform.position = new Vector3(0, 1.1f, -6.0f); // looking along +Z
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.72f, 0.83f, 0.96f, 1);
        cam.allowHDR = true;
        camGO.tag = "MainCamera";
    }

    void SetupLightsIfMissing()
    {
        if (FindObjectOfType<Light>() != null) return;

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

    void SetupURPVolume()
    {
        // One global volume for glow/readability (you can delete it later)
        var existing = GameObject.Find("UP_LIFT_URP_VOLUME");
        if (existing) return;

        var go = new GameObject("UP_LIFT_URP_VOLUME");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 0.35f;
        bloom.threshold.value = 1.0f;
        bloom.scatter.value = 0.75f;

        var vig = profile.Add<Vignette>(true);
        vig.intensity.value = 0.12f;
    }

    // ------------------------------------------------------------
    // Procedural rounded-rect prism mesh (high side-view quality)
    // ------------------------------------------------------------
    GameObject CreateMeshGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    Mesh CreateRoundedRectPrism(float w, float d, float h, float r, int seg)
    {
        // Generates a rounded-rectangle outline in XZ, then extrudes in Y.
        // Pivot: bottom at Y=0, top at Y=h.

        r = Mathf.Clamp(r, 0.0001f, Mathf.Min(w, d) * 0.49f);
        int corner = Mathf.Max(2, seg);

        List<Vector3> outline = new List<Vector3>();
        // corners: (+x,+z), (-x,+z), (-x,-z), (+x,-z)
        Vector2[] centers = new Vector2[]
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

        // remove duplicate last point
        if (outline.Count > 1 && (outline[0] - outline[outline.Count - 1]).sqrMagnitude < 1e-6f)
            outline.RemoveAt(outline.Count - 1);

        int n = outline.Count;

        List<Vector3> v = new List<Vector3>();
        List<Vector3> nrm = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> tri = new List<int>();

        // side verts (bottom + top per outline point)
        for (int i = 0; i < n; i++)
        {
            Vector3 p = outline[i];
            v.Add(new Vector3(p.x, 0, p.z));
            v.Add(new Vector3(p.x, h, p.z));

            // side normal approx from center
            Vector3 nn = new Vector3(p.x, 0, p.z).normalized;
            nrm.Add(nn);
            nrm.Add(nn);

            float u = i / (float)n;
            uv.Add(new Vector2(u, 0));
            uv.Add(new Vector2(u, 1));
        }

        // side tris
        for (int i = 0; i < n; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int j0 = ((i + 1) % n) * 2;
            int j1 = j0 + 1;

            tri.Add(i0); tri.Add(i1); tri.Add(j0);
            tri.Add(j0); tri.Add(i1); tri.Add(j1);
        }

        // top cap (fan triangulation around center)
        int topCenter = v.Count;
        v.Add(new Vector3(0, h, 0));
        nrm.Add(Vector3.up);
        uv.Add(new Vector2(0.5f, 0.5f));

        int bottomCenter = v.Count;
        v.Add(new Vector3(0, 0, 0));
        nrm.Add(Vector3.down);
        uv.Add(new Vector2(0.5f, 0.5f));

        // top fan
        for (int i = 0; i < n; i++)
        {
            int a = i * 2 + 1;
            int b = ((i + 1) % n) * 2 + 1;
            tri.Add(topCenter); tri.Add(a); tri.Add(b);
        }

        // bottom fan
        for (int i = 0; i < n; i++)
        {
            int a = ((i + 1) % n) * 2;
            int b = i * 2;
            tri.Add(bottomCenter); tri.Add(a); tri.Add(b);
        }

        Mesh m = new Mesh();
        m.name = "RoundedRectPrism";
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        m.SetVertices(v);
        m.SetNormals(nrm);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }
}
