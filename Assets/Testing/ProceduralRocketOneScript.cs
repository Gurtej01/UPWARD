using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Builds a stylized rocket + visible thrust entirely in Unity (one script).
/// No imported meshes. No animations clips. Everything is runtime + scripted.
/// Works in Built-in + URP (URP gets nicer emission if Bloom enabled).
/// </summary>
[DisallowMultipleComponent]
public class ProceduralRocketOneScript : MonoBehaviour
{
    [Header("Auto Scene Setup")]
    public bool createLightsAndCamera = true;
    public bool createGround = true;

    [Header("Rocket Scale")]
    public float bodyRadius = 0.28f;
    public float bodyHeight = 2.20f;

    [Header("Animation")]
    public float hoverAmp = 0.03f;
    public float hoverSpeed = 1.0f;
    public float wobbleDeg = 1.2f;
    public float wobbleSpeed = 0.9f;

    [Header("Thrust")]
    public float thrustBase = 1.0f;          // 0..2 nice range
    public float thrustFlicker = 0.35f;      // how much it flickers
    public float thrustSpeed = 2.2f;         // flicker speed
    public bool useParticles = true;

    // Internals
    Transform _root;
    Transform _ring;
    Transform _flameMesh;
    Light _engineLight;
    ParticleSystem _ps;

    Material _matWhite, _matRed, _matMetal, _matGlass, _matFlame;

    Vector3 _rootBasePos;
    Quaternion _rootBaseRot;

    void Start()
    {
        BuildAll();
    }

    void Update()
    {
        AnimateAll(Time.time);
    }

    // ============================================================
    // Build
    // ============================================================
    void BuildAll()
    {
        DestroyExisting();

        _root = new GameObject("PROC_RocketRoot").transform;
        _root.SetParent(transform, false);
        _root.localPosition = Vector3.zero;
        _root.localRotation = Quaternion.identity;
        _rootBasePos = _root.localPosition;
        _rootBaseRot = _root.localRotation;

        CreateMaterials();
        BuildRocketGeometry();
        BuildThrust();
        if (createLightsAndCamera) SetupScene();
        if (createGround) SetupGround();
    }

    void DestroyExisting()
    {
        // Remove previous procedural child (if any)
        var existing = transform.Find("PROC_RocketRoot");
        if (existing) Destroy(existing.gameObject);

        // Remove helper camera/lights created by this script (optional)
        var cam = GameObject.Find("PROC_RocketCam");
        if (cam) Destroy(cam);

        var lightRig = GameObject.Find("PROC_LightRig");
        if (lightRig) Destroy(lightRig);
    }

    // ============================================================
    // Materials (Built-in Standard + URP Lit friendly)
    // ============================================================
    void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");

        _matWhite = new Material(shader);
        _matRed = new Material(shader);
        _matMetal = new Material(shader);
        _matGlass = new Material(shader);
        _matFlame = new Material(shader);

        SetupLitMaterial(_matWhite, new Color(0.95f, 0.96f, 0.99f), metallic: 0.02f, smoothness: 0.65f, emission: Color.black, emissionIntensity: 0f);
        SetupLitMaterial(_matRed, new Color(0.92f, 0.12f, 0.14f), metallic: 0.05f, smoothness: 0.72f, emission: Color.black, emissionIntensity: 0f);
        SetupLitMaterial(_matMetal, new Color(0.12f, 0.13f, 0.16f), metallic: 0.90f, smoothness: 0.55f, emission: Color.black, emissionIntensity: 0f);

        // Glass-ish (not real refraction, but looks good)
        SetupLitMaterial(_matGlass, new Color(0.20f, 0.55f, 1.00f), metallic: 0.0f, smoothness: 0.90f,
            emission: new Color(0.10f, 0.20f, 0.30f), emissionIntensity: 0.5f);

        // Flame emissive (cyan/blue flame look)
        SetupLitMaterial(_matFlame, new Color(0.05f, 0.20f, 0.25f), metallic: 0f, smoothness: 0.2f,
            emission: new Color(0.15f, 0.95f, 1.0f), emissionIntensity: 8.0f);
    }

    void SetupLitMaterial(Material m, Color baseColor, float metallic, float smoothness, Color emission, float emissionIntensity)
    {
        if (!m) return;

        // Base color (Standard uses _Color; URP Lit uses _BaseColor)
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);

        // Metallic + Smoothness (URP Lit: _Metallic, _Smoothness; Standard: _Metallic, _Glossiness)
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

        // Emission
        Color em = emission * Mathf.Max(0f, emissionIntensity);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", em);
        }
    }

    // ============================================================
    // Geometry
    // ============================================================
    void BuildRocketGeometry()
    {
        float halfBody = bodyHeight * 0.5f;

        // Body cylinder
        var body = CreatePrimitive(PrimitiveType.Cylinder, "Body", _root);
        body.localScale = new Vector3(bodyRadius * 2f, halfBody, bodyRadius * 2f);
        body.localPosition = new Vector3(0, halfBody, 0);
        SetRendererMat(body, _matWhite);

        // Red stripe band
        var stripe = CreatePrimitive(PrimitiveType.Cylinder, "Stripe", _root);
        stripe.localScale = new Vector3(bodyRadius * 2.01f, 0.18f, bodyRadius * 2.01f);
        stripe.localPosition = new Vector3(0, 1.05f, 0);
        SetRendererMat(stripe, _matRed);

        // Nose cone (procedural)
        var nose = CreateMeshObject("NoseCone", _root, CreateConeMesh(bodyRadius * 0.95f, 0.02f, 0.72f, 56, capBottom: true));
        nose.localPosition = new Vector3(0, bodyHeight + 0.36f, 0);
        nose.localRotation = Quaternion.identity;
        SetRendererMat(nose, _matRed);

        // Engine section
        var eng = CreatePrimitive(PrimitiveType.Cylinder, "EngineSection", _root);
        eng.localScale = new Vector3(bodyRadius * 1.84f, 0.21f, bodyRadius * 1.84f);
        eng.localPosition = new Vector3(0, 0.22f, 0);
        SetRendererMat(eng, _matMetal);

        // Engine bell (truncated cone)
        var bell = CreateMeshObject("EngineBell", _root, CreateConeMesh(bodyRadius * 0.55f, bodyRadius * 0.28f, 0.34f, 56, capBottom: false));
        bell.localPosition = new Vector3(0, 0.02f, 0);
        bell.localRotation = Quaternion.identity;
        SetRendererMat(bell, _matMetal);

        // Bell rim (torus)
        var rim = CreateMeshObject("BellRim", _root, CreateTorusMesh(majorRadius: bodyRadius * 0.28f, minorRadius: 0.015f, majorSeg: 64, minorSeg: 16));
        rim.localPosition = new Vector3(0, -0.15f, 0);
        rim.localRotation = Quaternion.Euler(90f, 0, 0);
        SetRendererMat(rim, _matMetal);

        // Window (sphere) + frame torus
        var window = CreatePrimitive(PrimitiveType.Sphere, "Window", _root);
        window.localScale = Vector3.one * 0.20f;
        window.localPosition = new Vector3(0, 1.52f, bodyRadius * 0.78f);
        SetRendererMat(window, _matGlass);

        var wframe = CreateMeshObject("WindowFrame", _root, CreateTorusMesh(0.115f, 0.012f, 48, 14));
        wframe.localPosition = window.localPosition;
        wframe.localRotation = Quaternion.Euler(90f, 0, 0);
        SetRendererMat(wframe, _matMetal);

        // Fins (4)
        float finLen = 0.35f, finW = 0.10f, finH = 0.42f;
        float finZ = 0.42f;
        for (int i = 0; i < 4; i++)
        {
            float ang = 90f * i;
            float rad = bodyRadius * 0.92f;
            Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.right;
            var fin = CreatePrimitive(PrimitiveType.Cube, $"Fin_{i:00}", _root);
            fin.localScale = new Vector3(finLen, finH, finW);
            fin.localPosition = new Vector3(dir.x * rad, finZ, dir.z * rad);
            fin.localRotation = Quaternion.Euler(0, ang, 0);
            SetRendererMat(fin, _matRed);
        }

        // Decorative ring (for “alive” rotation)
        _ring = CreateMeshObject("SpinRing", _root, CreateTorusMesh(majorRadius: bodyRadius * 0.95f, minorRadius: 0.03f, majorSeg: 64, minorSeg: 16));
        _ring.localPosition = new Vector3(0, 1.10f, 0);
        _ring.localRotation = Quaternion.Euler(90f, 0, 0);
        SetRendererMat(_ring, _matMetal);
    }

    // ============================================================
    // Thrust: mesh flame + particle system + light
    // ============================================================
    void BuildThrust()
    {
        // Flame mesh (emissive cone) — always visible
        _flameMesh = CreateMeshObject("FlameMesh", _root, CreateConeMesh(0.18f, 0.02f, 1.10f, 40, capBottom: false));
        _flameMesh.localPosition = new Vector3(0, -0.85f, 0);
        _flameMesh.localRotation = Quaternion.identity;
        SetRendererMat(_flameMesh, _matFlame);

        // Engine glow light
        var lightGO = new GameObject("EngineGlowLight");
        lightGO.transform.SetParent(_root, false);
        lightGO.transform.localPosition = new Vector3(0, -0.25f, 0);
        _engineLight = lightGO.AddComponent<Light>();
        _engineLight.type = LightType.Point;
        _engineLight.range = 3.5f;
        _engineLight.intensity = 5.0f;
        _engineLight.color = new Color(0.2f, 0.95f, 1.0f);

        if (!useParticles) return;

        // Particle flame
        var psGO = new GameObject("FlameParticles");
        psGO.transform.SetParent(_root, false);
        psGO.transform.localPosition = new Vector3(0, -0.30f, 0);
        psGO.transform.localRotation = Quaternion.identity;

        _ps = psGO.AddComponent<ParticleSystem>();
        var main = _ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.35f;
        main.startSpeed = 2.4f;
        main.startSize = 0.18f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 1.0f, 1.0f, 0.9f),
            new Color(0.1f, 0.8f, 1.0f, 0.2f)
        );

        var emission = _ps.emission;
        emission.rateOverTime = 90f;

        var shape = _ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.05f;
        shape.length = 0.10f;
        shape.rotation = new Vector3(180f, 0f, 0f); // point downward

        var col = _ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.95f,0.95f,1.0f), 0f),
                new GradientColorKey(new Color(0.20f,0.90f,1.0f), 0.35f),
                new GradientColorKey(new Color(0.08f,0.25f,0.35f), 1f),
            },
            new[] {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.55f, 0.25f),
                new GradientAlphaKey(0.00f, 1f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var size = _ps.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve c = new AnimationCurve(
            new Keyframe(0f, 0.9f),
            new Keyframe(0.2f, 1.1f),
            new Keyframe(1f, 0.0f)
        );
        size.size = new ParticleSystem.MinMaxCurve(1f, c);

        var rend = _ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;

        // Try to use an unlit particle shader if available
        Shader psShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!psShader) psShader = Shader.Find("Particles/Standard Unlit");
        if (!psShader) psShader = Shader.Find("Legacy Shaders/Particles/Additive");

        if (psShader)
        {
            var psMat = new Material(psShader);
            if (psMat.HasProperty("_BaseColor")) psMat.SetColor("_BaseColor", new Color(0.2f, 0.95f, 1.0f, 1f));
            if (psMat.HasProperty("_Color")) psMat.SetColor("_Color", new Color(0.2f, 0.95f, 1.0f, 1f));
            rend.material = psMat;
        }

        _ps.Play();
    }

    // ============================================================
    // Animation (script-only)
    // ============================================================
    void AnimateAll(float t)
    {
        // Rocket hover + wobble
        float bob = Mathf.Sin(t * hoverSpeed) * hoverAmp;
        float wobX = Mathf.Sin(t * wobbleSpeed) * wobbleDeg;
        float wobZ = Mathf.Sin(t * (wobbleSpeed * 1.23f)) * wobbleDeg * 0.65f;

        _root.localPosition = _rootBasePos + new Vector3(0, bob, 0);
        _root.localRotation = _rootBaseRot * Quaternion.Euler(wobX, 0, wobZ);

        // Spin ring (gives “alive” detail even from side view)
        if (_ring) _ring.localRotation *= Quaternion.Euler(0f, 0f, 90f * Time.deltaTime);

        // Thrust flicker (intensity + flame length)
        float flick = 1f
            + thrustFlicker * (0.5f + 0.5f * Mathf.Sin(t * thrustSpeed))
            + 0.18f * Mathf.Sin(t * (thrustSpeed * 3.1f));

        float thrust = Mathf.Max(0.05f, thrustBase * flick);

        // Flame mesh scaling (length)
        if (_flameMesh)
        {
            Vector3 s = _flameMesh.localScale;
            s.y = Mathf.Lerp(0.8f, 1.35f, Mathf.Clamp01(thrust / 1.6f));
            s.x = 1f + 0.05f * Mathf.Sin(t * 9.0f);
            s.z = 1f + 0.05f * Mathf.Sin(t * 7.5f);
            _flameMesh.localScale = s;
        }

        // Emission intensity (material)
        if (_matFlame && _matFlame.HasProperty("_EmissionColor"))
        {
            Color baseEm = new Color(0.15f, 0.95f, 1.0f);
            float emI = 6.0f + 10.0f * Mathf.Clamp01(thrust / 1.6f);
            _matFlame.SetColor("_EmissionColor", baseEm * emI);
        }

        // Light pulse
        if (_engineLight)
        {
            _engineLight.intensity = 2.5f + 6.0f * Mathf.Clamp01(thrust / 1.6f);
        }

        // Particle tuning
        if (_ps)
        {
            var main = _ps.main;
            var emission = _ps.emission;

            main.startSpeed = 1.9f + 2.3f * Mathf.Clamp01(thrust / 1.6f);
            main.startSize = 0.14f + 0.16f * Mathf.Clamp01(thrust / 1.6f);
            emission.rateOverTime = 60f + 140f * Mathf.Clamp01(thrust / 1.6f);
        }
    }

    // ============================================================
    // Scene helpers
    // ============================================================
    void SetupScene()
    {
        // Light rig
        var rig = new GameObject("PROC_LightRig");
        var key = new GameObject("Key").AddComponent<Light>();
        key.transform.SetParent(rig.transform, false);
        key.type = LightType.Directional;
        key.intensity = 1.1f;
        key.transform.rotation = Quaternion.Euler(55f, 35f, 0f);

        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.transform.SetParent(rig.transform, false);
        fill.type = LightType.Directional;
        fill.intensity = 0.55f;
        fill.color = new Color(0.85f, 0.92f, 1.0f);
        fill.transform.rotation = Quaternion.Euler(75f, -30f, 0f);

        // Camera
        if (!Camera.main)
        {
            var camGO = new GameObject("PROC_RocketCam");
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(2.6f, 2.0f, -4.0f);
            cam.transform.rotation = Quaternion.Euler(25f, 155f, 0f);
            cam.fieldOfView = 55f;
        }
    }

    void SetupGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "PROC_Ground";
        ground.transform.position = new Vector3(0, 0, 0);
        ground.transform.localScale = Vector3.one * 5f;

        // Dark matte ground
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        var m = new Material(s);
        SetupLitMaterial(m, new Color(0.06f, 0.06f, 0.07f), metallic: 0f, smoothness: 0.1f, emission: Color.black, emissionIntensity: 0f);
        ground.GetComponent<Renderer>().sharedMaterial = m;
    }

    // ============================================================
    // Utility: create objects
    // ============================================================
    Transform CreatePrimitive(PrimitiveType type, string name, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);

        // Remove collider (we’re building visuals)
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        return go.transform;
    }

    void SetRendererMat(Transform t, Material m)
    {
        if (!t) return;
        var r = t.GetComponent<Renderer>();
        if (r) r.sharedMaterial = m;
    }

    Transform CreateMeshObject(string name, Transform parent, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        // material assigned by caller
        return go.transform;
    }

    // ============================================================
    // Procedural Mesh: Cone / Torus
    // ============================================================
    Mesh CreateConeMesh(float rBottom, float rTop, float height, int seg, bool capBottom)
    {
        // Cone aligned along Y, centered at mesh origin (pivot at center of height)
        float y0 = -height * 0.5f;
        float y1 = height * 0.5f;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // Ring vertices (bottom + top)
        for (int i = 0; i <= seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            float ca = Mathf.Cos(a);
            float sa = Mathf.Sin(a);

            Vector3 vb = new Vector3(ca * rBottom, y0, sa * rBottom);
            Vector3 vt = new Vector3(ca * rTop, y1, sa * rTop);

            verts.Add(vb);
            verts.Add(vt);

            // Approx normal for side
            Vector3 n = new Vector3(ca, (rBottom - rTop) / Mathf.Max(0.0001f, height), sa).normalized;
            norms.Add(n);
            norms.Add(n);

            uvs.Add(new Vector2(i / (float)seg, 0f));
            uvs.Add(new Vector2(i / (float)seg, 1f));
        }

        // Side triangles
        for (int i = 0; i < seg; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int i3 = i0 + 3;

            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i2); tris.Add(i1); tris.Add(i3);
        }

        // Bottom cap (optional)
        if (capBottom && rBottom > 0.0001f)
        {
            int centerIndex = verts.Count;
            verts.Add(new Vector3(0, y0, 0));
            norms.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ringStart = 0;
            for (int i = 0; i < seg; i++)
            {
                int v0 = ringStart + i * 2;
                int v1 = ringStart + (i + 1) * 2;

                tris.Add(centerIndex);
                tris.Add(v1);
                tris.Add(v0);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "PROC_Cone";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    Mesh CreateTorusMesh(float majorRadius, float minorRadius, int majorSeg, int minorSeg)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int i = 0; i <= majorSeg; i++)
        {
            float u = (i / (float)majorSeg) * Mathf.PI * 2f;
            Vector3 majorCenter = new Vector3(Mathf.Cos(u) * majorRadius, 0, Mathf.Sin(u) * majorRadius);

            for (int j = 0; j <= minorSeg; j++)
            {
                float v = (j / (float)minorSeg) * Mathf.PI * 2f;

                // Minor circle around the major ring
                Vector3 minor = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                Vector3 pos = majorCenter + minor * minorRadius;

                verts.Add(pos);
                norms.Add((pos - majorCenter).normalized);
                uvs.Add(new Vector2(i / (float)majorSeg, j / (float)minorSeg));
            }
        }

        int ring = minorSeg + 1;
        for (int i = 0; i < majorSeg; i++)
        {
            for (int j = 0; j < minorSeg; j++)
            {
                int a = (i * ring) + j;
                int b = ((i + 1) * ring) + j;
                int c = ((i + 1) * ring) + (j + 1);
                int d = (i * ring) + (j + 1);

                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "PROC_Torus";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
