using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

/// <summary>
/// ULTRA REACTOR PORTAL PLATFORM (Unity 6 + URP) — one script, no imports.
/// - High-poly procedural meshes (torus/cyl/icosphere-ish)
/// - Procedural textures (soft particle + stripe maps; 2K/4K option)
/// - Emissive LED chase (MaterialPropertyBlock)
/// - Rotating rings, hover bob, piston pump
/// - Lightning arcs (LineRenderers) + particle mist
/// - Auto URP Volume with Bloom/Vignette/Chromatic
///
/// Attach to an empty GameObject and press Play.
/// </summary>
public class UltraReactorOneScript : MonoBehaviour
{
    [Header("Quality (push it)")]
    [Range(64, 512)] public int radialSegments = 256;     // base cylinder detail
    [Range(24, 128)] public int torusMinorSegments = 32;
    [Range(64, 512)] public int torusMajorSegments = 256;

    [Header("Textures")]
    public bool generate4KStripeTexture = true;           // 4096 stripe map (heavy once at start)
    public int stripeTexSize2K = 2048;
    public int stripeTexSize4K = 4096;
    public int particleTexSize = 256;

    [Header("Look")]
    public Color ledCyan = new Color(0.15f, 0.95f, 1.0f, 1);
    public Color coreCyan = new Color(0.10f, 0.85f, 1.0f, 1);
    public Color amber = new Color(1.0f, 0.55f, 0.15f, 1);
    public Color plasticWhite = new Color(0.95f, 0.96f, 0.99f, 1);
    public Color gunmetal = new Color(0.12f, 0.13f, 0.16f, 1);

    [Header("Animation")]
    public float hoverAmp = 0.03f;
    public float hoverSpeed = 0.9f;
    public float tiltDeg = 1.2f;
    public float tiltSpeed = 0.75f;

    public float ringSpinDegPerSec = 90f;
    public float innerSpinDegPerSec = -140f;

    [Header("LEDs")]
    public int ledCount = 40;
    public float ledSpeed = 2.2f;
    public float ledSharpness = 10f;
    public float ledMin = 0.2f;
    public float ledMax = 10f;

    [Header("Lightning")]
    public int arcCount = 6;
    public int arcSegments = 24;
    public float arcJitter = 0.06f;
    public float arcSpeed = 2.5f;

    [Header("Particles")]
    public bool enableParticles = true;
    public int maxParticles = 1200;

    [Header("Auto Scene")]
    public bool createCameraAndLights = true;
    public bool createURPVolume = true;

    // internals
    Transform root, ringOuter, ringInner, core, pistonA, pistonB;
    Renderer coreR;
    List<Renderer> ledRenderers = new();
    List<LineRenderer> arcs = new();
    ParticleSystem ps;

    Material mPlastic, mMetal, mGlass, mLed, mArc, mStripeAdd;
    MaterialPropertyBlock mpb;

    Vector3 basePos;
    Quaternion baseRot;

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
        // Clean previous
        var old = transform.Find("ULTRA_REACTOR_ROOT");
        if (old) Destroy(old.gameObject);

        root = new GameObject("ULTRA_REACTOR_ROOT").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        basePos = root.localPosition;
        baseRot = root.localRotation;

        CreateMaterials();
        CreatePlatform();
        CreateLEDs();
        CreateCore();
        CreateRings();
        CreatePistons();
        CreateArcs();
        if (enableParticles) CreateParticles();

        if (createCameraAndLights) SetupCameraAndLights();
        if (createURPVolume) SetupURPVolume();
    }

    // ------------------------------------------------------------
    // Materials / Textures
    // ------------------------------------------------------------
    void CreateMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!lit) lit = Shader.Find("Standard");

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (!unlit) unlit = Shader.Find("Unlit/Color");

        Shader particlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!particlesUnlit) particlesUnlit = Shader.Find("Particles/Standard Unlit");

        // Base materials
        mPlastic = new Material(lit);
        SetLit(mPlastic, plasticWhite, metallic: 0.02f, smoothness: 0.72f, emission: Color.black, emissionIntensity: 0);

        mMetal = new Material(lit);
        SetLit(mMetal, gunmetal, metallic: 0.90f, smoothness: 0.58f, emission: Color.black, emissionIntensity: 0);

        mGlass = new Material(lit);
        SetLit(mGlass, new Color(0.18f, 0.55f, 1.0f, 0.8f), metallic: 0.0f, smoothness: 0.92f,
               emission: new Color(0.05f, 0.10f, 0.12f), emissionIntensity: 1.0f);
        ForceTransparent(mGlass);

        // LED shared material (emission intensity controlled via MPB)
        mLed = new Material(lit);
        SetLit(mLed, new Color(0.06f, 0.07f, 0.09f, 1), metallic: 0.0f, smoothness: 0.35f, emission: ledCyan, emissionIntensity: 2.0f);

        // Arc material (unlit additive-ish)
        mArc = new Material(unlit);
        SetUnlitColor(mArc, ledCyan);
        ForceAdditiveLike(mArc);

        // Stripe emissive material (lit + emission + scrolling texture)
        mStripeAdd = new Material(lit);
        SetLit(mStripeAdd, new Color(0.06f, 0.07f, 0.09f, 1), metallic: 0.2f, smoothness: 0.35f,
               emission: ledCyan, emissionIntensity: 3.0f);

        // Create stripe texture (2K/4K)
        int size = generate4KStripeTexture ? stripeTexSize4K : stripeTexSize2K;
        Texture2D stripes = CreateStripeTexture(size);
        AssignBaseMap(mStripeAdd, stripes);

        // We also generate a soft particle texture for later (for cleaner particles)
        Texture2D softParticle = CreateSoftParticleTexture(particleTexSize);

        // If particles shader exists, we’ll use it later in CreateParticles()
        // (we keep softParticle accessible via a field? simplest: store as global)
        _softParticleTex = softParticle;
        _particlesShader = particlesUnlit;
    }

    Shader _particlesShader;
    Texture2D _softParticleTex;

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

    void SetUnlitColor(Material m, Color c)
    {
        if (!m) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    void AssignBaseMap(Material m, Texture2D tex)
    {
        if (!m || !tex) return;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
    }

    void ForceTransparent(Material m)
    {
        if (!m) return;
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    // Best-effort “additive-like” for URP/Unlit or fallback unlit
    void ForceAdditiveLike(Material m)
    {
        if (!m) return;
        // Not all shaders expose blend properties; this still helps.
        m.renderQueue = 3000;
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // URP often: 2 = Additive
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_EMISSION");
    }

    Texture2D CreateSoftParticleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[size * size];
        float inv = 1f / (size - 1);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x * inv * 2f - 1f;
                float v = y * inv * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);

                float a = SmoothStep(1.0f, 0.0f, r);      // soft disk
                a = Mathf.Pow(a, 2.2f);                   // tighter core
                Color c = new Color(1, 1, 1, a);
                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        return tex;
    }

    Texture2D CreateStripeTexture(int size)
    {
        // High-res emissive stripe map (2K/4K) — generated once at Start
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;

        Color[] px = new Color[size * size];
        float inv = 1f / (size - 1);

        for (int y = 0; y < size; y++)
        {
            float v = y * inv;
            for (int x = 0; x < size; x++)
            {
                float u = x * inv;

                // stripes + micro pattern
                float stripes = Mathf.Sin((u * 50f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                stripes = Mathf.Pow(stripes, 10f);

                float scan = Mathf.Sin((v * 6f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                scan = Mathf.Lerp(0.6f, 1.0f, scan);

                float micro = Mathf.Sin((u * 420f + v * 180f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                micro = Mathf.Lerp(0.85f, 1.0f, micro);

                float val = stripes * scan * micro;
                // store as grayscale in RGB
                px[y * size + x] = new Color(val, val, val, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        return tex;
    }

    float SmoothStep(float a, float b, float t)
    {
        t = Mathf.Clamp01((t - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // ------------------------------------------------------------
    // Geometry creation
    // ------------------------------------------------------------
    void CreatePlatform()
    {
        // Base: chamfered cylinder (high radialSegments)
        var baseGO = CreateMeshGO("Base_ChamferCylinder", root);
        var baseMF = baseGO.AddComponent<MeshFilter>();
        var baseMR = baseGO.AddComponent<MeshRenderer>();
        baseMF.sharedMesh = CreateChamferCylinder(radius: 1.35f, height: 0.35f, chamfer: 0.08f, seg: radialSegments);
        baseMR.sharedMaterial = mPlastic;
        baseGO.transform.localPosition = new Vector3(0, 0.18f, 0);

        // Underframe
        var under = CreateMeshGO("UnderFrame", root);
        var uf = under.AddComponent<MeshFilter>();
        var ur = under.AddComponent<MeshRenderer>();
        uf.sharedMesh = CreateChamferCylinder(radius: 1.05f, height: 0.22f, chamfer: 0.06f, seg: radialSegments);
        ur.sharedMaterial = mMetal;
        under.transform.localPosition = new Vector3(0, 0.10f, 0);

        // Top plate (slightly inset)
        var top = CreateMeshGO("TopPlate", root);
        var tf = top.AddComponent<MeshFilter>();
        var tr = top.AddComponent<MeshRenderer>();
        tf.sharedMesh = CreateChamferCylinder(radius: 1.10f, height: 0.10f, chamfer: 0.04f, seg: radialSegments);
        tr.sharedMaterial = mPlastic;
        top.transform.localPosition = new Vector3(0, 0.34f, 0);

        // Side stripe panel
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "StripePanel";
        panel.transform.SetParent(root, false);
        panel.transform.localScale = new Vector3(1.6f, 0.28f, 0.06f);
        panel.transform.localPosition = new Vector3(0.0f, 0.20f, -1.30f);
        Destroy(panel.GetComponent<Collider>());
        panel.GetComponent<Renderer>().sharedMaterial = mStripeAdd;
    }

    void CreateLEDs()
    {
        float r = 1.18f;
        float y = 0.40f;

        for (int i = 0; i < ledCount; i++)
        {
            float ang = (i / (float)ledCount) * Mathf.PI * 2f;
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;

            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
            led.name = $"LED_{i:00}";
            led.transform.SetParent(root, false);
            led.transform.localScale = new Vector3(0.10f, 0.035f, 0.035f);
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
        // Core sphere (higher detail than primitive sphere by using a denser UV sphere mesh)
        var coreGO = CreateMeshGO("EnergyCore", root);
        var mf = coreGO.AddComponent<MeshFilter>();
        var mr = coreGO.AddComponent<MeshRenderer>();
        mf.sharedMesh = CreateUVSphere(radius: 0.33f, lon: 128, lat: 64);
        mr.sharedMaterial = mGlass;
        coreGO.transform.localPosition = new Vector3(0, 0.55f, 0);
        core = coreGO.transform;
        coreR = mr;

        // Inner glow shell (slightly bigger, emissive)
        var glow = CreateMeshGO("CoreGlowShell", root);
        var gf = glow.AddComponent<MeshFilter>();
        var gr = glow.AddComponent<MeshRenderer>();
        gf.sharedMesh = CreateUVSphere(radius: 0.37f, lon: 128, lat: 64);
        var glowMat = new Material(mMetal);
        SetLit(glowMat, gunmetal, 0.0f, 0.3f, coreCyan, 8.0f);
        ForceTransparent(glowMat);
        gr.sharedMaterial = glowMat;
        glow.transform.localPosition = coreGO.transform.localPosition;

        // A point light to sell energy (small, not environment lighting)
        var lgo = new GameObject("CoreLight");
        lgo.transform.SetParent(root, false);
        lgo.transform.localPosition = coreGO.transform.localPosition;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 6f;
        l.intensity = 2.5f;
        l.color = coreCyan;
    }

    void CreateRings()
    {
        // Outer ring
        ringOuter = CreateMeshGO("RingOuter", root).transform;
        var of = ringOuter.gameObject.AddComponent<MeshFilter>();
        var or = ringOuter.gameObject.AddComponent<MeshRenderer>();
        of.sharedMesh = CreateTorus(majorRadius: 0.95f, minorRadius: 0.06f, majorSeg: torusMajorSegments, minorSeg: torusMinorSegments);
        or.sharedMaterial = mMetal;
        ringOuter.localPosition = new Vector3(0, 0.50f, 0);
        ringOuter.localRotation = Quaternion.Euler(90, 0, 0);

        // Inner ring (emissive stripe look)
        ringInner = CreateMeshGO("RingInner", root).transform;
        var inf = ringInner.gameObject.AddComponent<MeshFilter>();
        var inr = ringInner.gameObject.AddComponent<MeshRenderer>();
        inf.sharedMesh = CreateTorus(majorRadius: 0.72f, minorRadius: 0.05f, majorSeg: torusMajorSegments, minorSeg: torusMinorSegments);
        inr.sharedMaterial = mStripeAdd; // scrolling base map + emission
        ringInner.localPosition = new Vector3(0, 0.50f, 0);
        ringInner.localRotation = Quaternion.Euler(90, 0, 0);
    }

    void CreatePistons()
    {
        pistonA = new GameObject("PistonA").transform;
        pistonA.SetParent(root, false);
        pistonA.localPosition = new Vector3(-0.85f, 0.18f, 0.65f);

        pistonB = new GameObject("PistonB").transform;
        pistonB.SetParent(root, false);
        pistonB.localPosition = new Vector3(0.85f, 0.18f, 0.65f);

        MakePiston(pistonA);
        MakePiston(pistonB);
    }

    void MakePiston(Transform parent)
    {
        var tube = CreateMeshGO("Tube", parent);
        tube.AddComponent<MeshFilter>().sharedMesh = CreateChamferCylinder(0.06f, 0.28f, 0.02f, seg: 64);
        tube.AddComponent<MeshRenderer>().sharedMaterial = mMetal;
        tube.transform.localPosition = new Vector3(0, 0.12f, 0);
        tube.transform.localRotation = Quaternion.Euler(0, 0, 90);

        var rod = CreateMeshGO("Rod", parent);
        rod.AddComponent<MeshFilter>().sharedMesh = CreateChamferCylinder(0.04f, 0.34f, 0.015f, seg: 64);
        rod.AddComponent<MeshRenderer>().sharedMaterial = mMetal;
        rod.transform.localPosition = new Vector3(0, 0.12f, 0);
        rod.transform.localRotation = Quaternion.Euler(0, 0, 90);

        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "Cap";
        cap.transform.SetParent(parent, false);
        cap.transform.localScale = new Vector3(0.10f, 0.04f, 0.10f);
        cap.transform.localPosition = new Vector3(0.18f, 0.12f, 0);
        Destroy(cap.GetComponent<Collider>());
        var capMat = new Material(mMetal);
        SetLit(capMat, gunmetal, 0.0f, 0.25f, coreCyan, 8.0f);
        cap.GetComponent<Renderer>().sharedMaterial = capMat;
    }

    void CreateArcs()
    {
        for (int i = 0; i < arcCount; i++)
        {
            var go = new GameObject($"Arc_{i:00}");
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = arcSegments;
            lr.widthMultiplier = 0.02f;
            lr.useWorldSpace = false;
            lr.material = mArc;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            arcs.Add(lr);
        }
    }

    void CreateParticles()
    {
        var go = new GameObject("EnergyMistParticles");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0, 0.55f, 0);

        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = maxParticles;
        main.startLifetime = 1.1f;
        main.startSpeed = 0.55f;
        main.startSize = 0.10f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 1.0f, 1.0f, 0.35f),
            new Color(0.1f, 0.8f, 1.0f, 0.0f)
        );

        var emission = ps.emission;
        emission.rateOverTime = Mathf.Min(1200, maxParticles);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.95f,0.98f,1.0f), 0f),
                new GradientColorKey(new Color(0.15f,0.90f,1.0f), 0.4f),
                new GradientColorKey(new Color(0.06f,0.2f,0.25f), 1f),
            },
            new[] {
                new GradientAlphaKey(0.0f, 0f),
                new GradientAlphaKey(0.45f, 0.15f),
                new GradientAlphaKey(0.0f, 1f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve c = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.2f, 1.0f),
            new Keyframe(1f, 0.0f)
        );
        size.size = new ParticleSystem.MinMaxCurve(1f, c);

        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;

        if (_particlesShader)
        {
            var pm = new Material(_particlesShader);
            AssignBaseMap(pm, _softParticleTex);
            ForceAdditiveLike(pm);
            pr.material = pm;
        }

        ps.Play();
    }

    // ------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------
    void Animate(float t)
    {
        // Hover + tilt
        float bob = Mathf.Sin(t * hoverSpeed) * hoverAmp;
        float tx = Mathf.Sin(t * tiltSpeed) * tiltDeg;
        float tz = Mathf.Sin(t * (tiltSpeed * 1.23f)) * (tiltDeg * 0.65f);
        root.localPosition = basePos + new Vector3(0, bob, 0);
        root.localRotation = baseRot * Quaternion.Euler(tx, 0, tz);

        // Rings spin
        if (ringOuter) ringOuter.Rotate(0, 0, ringSpinDegPerSec * Time.deltaTime, Space.Self);
        if (ringInner) ringInner.Rotate(0, 0, innerSpinDegPerSec * Time.deltaTime, Space.Self);

        // Scroll stripe texture UV offset (URP Lit uses _BaseMap_ST)
        if (mStripeAdd && mStripeAdd.HasProperty("_BaseMap_ST"))
        {
            Vector4 st = mStripeAdd.GetVector("_BaseMap_ST");
            st.z = t * 0.12f; // offset X
            mStripeAdd.SetVector("_BaseMap_ST", st);
        }

        // LED chase via MPB (per LED)
        int n = ledRenderers.Count;
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Sin(t * ledSpeed - i * 0.42f);
            float v = Mathf.Pow((x * 0.5f + 0.5f), ledSharpness);
            float intensity = Mathf.Lerp(ledMin, ledMax, v);

            var r = ledRenderers[i];
            r.GetPropertyBlock(mpb);

            if (r.sharedMaterial && r.sharedMaterial.HasProperty("_EmissionColor"))
            {
                mpb.SetColor("_EmissionColor", ledCyan * intensity);
            }
            r.SetPropertyBlock(mpb);
        }

        // Core pulse
        if (coreR && coreR.sharedMaterial && coreR.sharedMaterial.HasProperty("_EmissionColor"))
        {
            float pulse = 6f + 6f * (0.5f + 0.5f * Mathf.Sin(t * 1.2f)) + 2f * Mathf.Sin(t * 5.7f);
            coreR.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", coreCyan * pulse);
            coreR.SetPropertyBlock(mpb);
        }

        // Pistons pump
        float p = 0.06f * (0.5f + 0.5f * Mathf.Sin(t * 1.8f));
        if (pistonA) pistonA.GetChild(1).localPosition = new Vector3(p, 0.12f, 0); // Rod
        if (pistonB) pistonB.GetChild(1).localPosition = new Vector3(0.06f - p, 0.12f, 0);

        // Lightning arcs
        UpdateArcs(t);
    }

    void UpdateArcs(float t)
    {
        // Anchors: between ring and core area
        Vector3 a0 = new Vector3(0.75f, 0.52f, 0.0f);
        Vector3 b0 = new Vector3(0.0f, 0.55f, 0.0f);

        for (int i = 0; i < arcs.Count; i++)
        {
            float ang = (i / (float)arcs.Count) * Mathf.PI * 2f;
            Vector3 a = Quaternion.Euler(0, ang * Mathf.Rad2Deg, 0) * a0;
            Vector3 b = b0;

            var lr = arcs[i];
            for (int s = 0; s < arcSegments; s++)
            {
                float u = s / (float)(arcSegments - 1);
                Vector3 p = Vector3.Lerp(a, b, u);

                // noise jitter along a perpendicular-ish direction
                float n1 = Mathf.PerlinNoise(u * 6f + i * 10f, t * arcSpeed);
                float n2 = Mathf.PerlinNoise(u * 6f + 33f + i * 10f, t * arcSpeed * 0.9f);
                Vector3 j = new Vector3((n1 - 0.5f), (n2 - 0.5f), (n1 - 0.5f)) * arcJitter;

                // taper jitter near ends
                float taper = Mathf.Sin(u * Mathf.PI);
                lr.SetPosition(s, p + j * taper);
            }

            // slight width pulse
            lr.widthMultiplier = 0.014f + 0.010f * (0.5f + 0.5f * Mathf.Sin(t * 3.5f + i));
        }
    }

    // ------------------------------------------------------------
    // Scene setup (optional)
    // ------------------------------------------------------------
    void SetupCameraAndLights()
    {
        if (!Camera.main)
        {
            var camGO = new GameObject("ULTRA_ReactorCam");
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(3.4f, 2.2f, -4.2f);
            cam.transform.rotation = Quaternion.Euler(18f, 145f, 0f);
            cam.fieldOfView = 55f;
            cam.allowHDR = true;
        }

        var rig = GameObject.Find("ULTRA_LightRig");
        if (!rig)
        {
            rig = new GameObject("ULTRA_LightRig");
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(rig.transform, false);
            key.type = LightType.Directional;
            key.intensity = 1.0f;
            key.transform.rotation = Quaternion.Euler(55f, 35f, 0f);

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(rig.transform, false);
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.85f, 0.92f, 1.0f);
            fill.transform.rotation = Quaternion.Euler(75f, -30f, 0f);
        }
    }

    void SetupURPVolume()
    {
        var volGO = GameObject.Find("ULTRA_Volume");
        if (volGO) return;

        volGO = new GameObject("ULTRA_Volume");
        var vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        // Bloom
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 0.35f;
        bloom.threshold.value = 1.0f;
        bloom.scatter.value = 0.75f;

        // Vignette (subtle)
        var vig = profile.Add<Vignette>(true);
        vig.intensity.value = 0.18f;

        // Chromatic Aberration (tiny)
        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.value = 0.06f;
    }

    // ------------------------------------------------------------
    // Mesh generators
    // ------------------------------------------------------------
    GameObject CreateMeshGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    Mesh CreateChamferCylinder(float radius, float height, float chamfer, int seg)
    {
        // 4 rings: bottom outer, bottom chamfer, top chamfer, top outer
        float y0 = 0f;
        float y1 = chamfer;
        float y2 = height - chamfer;
        float y3 = height;

        float r0 = radius;
        float r1 = Mathf.Max(0.0001f, radius - chamfer);

        List<Vector3> v = new();
        List<Vector3> n = new();
        List<Vector2> uv = new();
        List<int> t = new();

        // rings
        int Rings = 4;
        for (int ring = 0; ring < Rings; ring++)
        {
            float y = ring switch
            {
                0 => y0,
                1 => y1,
                2 => y2,
                _ => y3
            };
            float r = (ring == 0 || ring == 3) ? r0 : r1;

            for (int i = 0; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                v.Add(new Vector3(ca * r, y, sa * r));

                // normal approximation
                Vector3 nn = new Vector3(ca, 0, sa);
                if (ring == 1) nn = (new Vector3(ca, 1, sa)).normalized;
                if (ring == 2) nn = (new Vector3(ca, -1, sa)).normalized;
                n.Add(nn);

                uv.Add(new Vector2(i / (float)seg, y / Mathf.Max(0.0001f, height)));
            }
        }

        int ringStride = seg + 1;

        // side triangles between rings
        for (int ring = 0; ring < Rings - 1; ring++)
        {
            int startA = ring * ringStride;
            int startB = (ring + 1) * ringStride;

            for (int i = 0; i < seg; i++)
            {
                int a0 = startA + i;
                int a1 = startA + i + 1;
                int b0 = startB + i;
                int b1 = startB + i + 1;

                t.Add(a0); t.Add(b0); t.Add(a1);
                t.Add(a1); t.Add(b0); t.Add(b1);
            }
        }

        // caps
        int bottomCenter = v.Count;
        v.Add(new Vector3(0, y0, 0));
        n.Add(Vector3.down);
        uv.Add(new Vector2(0.5f, 0.5f));

        int topCenter = v.Count;
        v.Add(new Vector3(0, y3, 0));
        n.Add(Vector3.up);
        uv.Add(new Vector2(0.5f, 0.5f));

        // bottom fan uses ring 0
        int bottomRingStart = 0;
        for (int i = 0; i < seg; i++)
        {
            int a = bottomRingStart + i;
            int b = bottomRingStart + i + 1;
            t.Add(bottomCenter); t.Add(b); t.Add(a);
        }

        // top fan uses ring 3
        int topRingStart = (Rings - 1) * ringStride;
        for (int i = 0; i < seg; i++)
        {
            int a = topRingStart + i;
            int b = topRingStart + i + 1;
            t.Add(topCenter); t.Add(a); t.Add(b);
        }

        Mesh m = new Mesh();
        m.indexFormat = (v.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    Mesh CreateTorus(float majorRadius, float minorRadius, int majorSeg, int minorSeg)
    {
        List<Vector3> v = new();
        List<Vector3> n = new();
        List<Vector2> uv = new();
        List<int> t = new();

        for (int i = 0; i <= majorSeg; i++)
        {
            float u = (i / (float)majorSeg) * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * majorRadius, 0, Mathf.Sin(u) * majorRadius);

            for (int j = 0; j <= minorSeg; j++)
            {
                float vAng = (j / (float)minorSeg) * Mathf.PI * 2f;
                Vector3 minor = new Vector3(Mathf.Cos(u) * Mathf.Cos(vAng), Mathf.Sin(vAng), Mathf.Sin(u) * Mathf.Cos(vAng));
                Vector3 pos = center + minor * minorRadius;

                v.Add(pos);
                n.Add((pos - center).normalized);
                uv.Add(new Vector2(i / (float)majorSeg, j / (float)minorSeg));
            }
        }

        int ring = minorSeg + 1;
        for (int i = 0; i < majorSeg; i++)
        {
            for (int j = 0; j < minorSeg; j++)
            {
                int a = i * ring + j;
                int b = (i + 1) * ring + j;
                int c = (i + 1) * ring + (j + 1);
                int d = i * ring + (j + 1);

                t.Add(a); t.Add(b); t.Add(d);
                t.Add(b); t.Add(c); t.Add(d);
            }
        }

        Mesh m = new Mesh();
        m.indexFormat = (v.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    Mesh CreateUVSphere(float radius, int lon, int lat)
    {
        List<Vector3> v = new();
        List<Vector3> n = new();
        List<Vector2> uv = new();
        List<int> t = new();

        for (int y = 0; y <= lat; y++)
        {
            float v01 = y / (float)lat;
            float phi = v01 * Mathf.PI;

            for (int x = 0; x <= lon; x++)
            {
                float u01 = x / (float)lon;
                float theta = u01 * Mathf.PI * 2f;

                float sx = Mathf.Sin(phi) * Mathf.Cos(theta);
                float sy = Mathf.Cos(phi);
                float sz = Mathf.Sin(phi) * Mathf.Sin(theta);

                Vector3 p = new Vector3(sx, sy, sz) * radius;
                v.Add(p);
                n.Add(p.normalized);
                uv.Add(new Vector2(u01, v01));
            }
        }

        int stride = lon + 1;
        for (int y = 0; y < lat; y++)
        {
            for (int x = 0; x < lon; x++)
            {
                int a = y * stride + x;
                int b = (y + 1) * stride + x;
                int c = (y + 1) * stride + (x + 1);
                int d = y * stride + (x + 1);

                t.Add(a); t.Add(b); t.Add(d);
                t.Add(b); t.Add(c); t.Add(d);
            }
        }

        Mesh m = new Mesh();
        m.indexFormat = (v.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }
}
