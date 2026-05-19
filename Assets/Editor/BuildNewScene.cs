using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BuildNewScene : EditorWindow
{
    static Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    [MenuItem("Tools/Build New Scene")]
    public static void Run()
    {
        _mats.Clear();

        // --- DELETE old environment ---
        string[] toDelete = { "Restaurant_Cantina" };
        foreach (string name in toDelete)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) { Undo.DestroyObjectImmediate(go); Debug.Log("Deleted: " + name); }
        }
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name == "Plate" && go.transform.parent == null)
                Undo.DestroyObjectImmediate(go);

        // --- DIRECTIONAL LIGHT (sunset) ---
        GameObject lightGO = GameObject.Find("Directional Light");
        if (lightGO != null)
        {
            Light l = lightGO.GetComponent<Light>();
            l.color = new Color(1f, 0.78f, 0.46f);
            l.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(32f, -28f, 0f);
        }

        // --- SKYBOX ---
        SetupSkybox();

        // --- FLOOR ---
        GameObject floor = MakeCube("Floor", null,
            new Vector3(0f, -0.05f, 0f), new Vector3(20f, 0.1f, 20f), "floor");
        ApplyMat(floor, MakeMat("floor", new Color(0.23f, 0.20f, 0.17f)));

        // --- WALL WITH WINDOW ---
        CreateWallWithWindow(new Vector3(0f, 0f, 3.5f));

        // --- KISSING SILHOUETTES ---
        CreateKissingSilhouettes(new Vector3(0f, 0f, 4.0f));

        // --- CAMERA PROP ---
        CreateCameraProp(new Vector3(1.6f, 0.95f, -0.8f));

        // Save new materials
        SavePendingMats();

        EditorUtility.SetDirty(lightGO);
        Debug.Log("[BuildNewScene] Done.");
    }

    // =========================================================================
    static void SetupSkybox()
    {
        Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SkyboxSunset.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, "Assets/Materials/SkyboxSunset.mat");
        }
        sky.SetFloat("_SunSize", 0.045f);
        sky.SetFloat("_SunSizeConvergence", 8f);
        sky.SetFloat("_AtmosphereThickness", 1.05f);
        sky.SetColor("_SkyTint", new Color(0.38f, 0.52f, 1f));
        sky.SetColor("_GroundColor", new Color(0.36f, 0.32f, 0.28f));
        sky.SetFloat("_Exposure", 1.2f);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        DynamicGI.UpdateEnvironment();
    }

    // =========================================================================
    static void CreateWallWithWindow(Vector3 pos)
    {
        GameObject root = new GameObject("WallWithWindow");
        root.transform.position = pos;

        const float wallW = 7f, wallH = 3.8f, wallT = 0.22f;
        const float winW = 2.2f, winH = 2.0f, winY = 1.4f;
        Color plaster = new Color(0.76f, 0.71f, 0.62f);
        Color frame   = new Color(0.18f, 0.13f, 0.09f);

        float sideW = (wallW - winW) / 2f;
        float botH  = winY - winH / 2f;
        float topH  = wallH - (winY + winH / 2f);

        // Wall panels
        Panel("WallL",   root, new Vector3(-(winW / 2f + sideW / 2f), wallH / 2f, 0f), new Vector3(sideW, wallH, wallT), plaster);
        Panel("WallR",   root, new Vector3( winW / 2f + sideW / 2f,  wallH / 2f, 0f), new Vector3(sideW, wallH, wallT), plaster);
        Panel("WallBot", root, new Vector3(0f, botH / 2f, 0f),                          new Vector3(winW, botH,  wallT), plaster);
        Panel("WallTop", root, new Vector3(0f, winY + winH / 2f + topH / 2f, 0f),       new Vector3(winW, topH,  wallT), plaster);

        // Window frame
        float ft = 0.055f, fz = -wallT / 2f - 0.005f;
        Panel("FrTop",   root, new Vector3(0f,               winY + winH / 2f + ft / 2f, fz), new Vector3(winW + ft * 2f, ft, 0.05f), frame);
        Panel("FrBot",   root, new Vector3(0f,               winY - winH / 2f - ft / 2f, fz), new Vector3(winW + ft * 2f, ft, 0.05f), frame);
        Panel("FrLeft",  root, new Vector3(-(winW / 2f + ft / 2f), winY,                 fz), new Vector3(ft, winH + ft * 2f, 0.05f), frame);
        Panel("FrRight", root, new Vector3( winW / 2f + ft / 2f,  winY,                 fz), new Vector3(ft, winH + ft * 2f, 0.05f), frame);

        // Cross bar
        Panel("FrCrossH", root, new Vector3(0f,   winY,           fz), new Vector3(winW, ft * 0.8f, 0.04f), frame);
        Panel("FrCrossV", root, new Vector3(0f,   winY,           fz), new Vector3(ft * 0.8f, winH, 0.04f), frame);

        // Glass
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WindowGlass.mat");
        if (glassMat == null)
        {
            glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glassMat.SetFloat("_Surface", 1f);
            glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassMat.renderQueue = 3000;
            AssetDatabase.CreateAsset(glassMat, "Assets/Materials/WindowGlass.mat");
        }
        glassMat.color = new Color(0.75f, 0.9f, 1f, 0.18f);
        GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glass.name = "Glass";
        glass.transform.SetParent(root.transform, false);
        glass.transform.localPosition = new Vector3(0f, winY, fz - 0.01f);
        glass.transform.localScale = new Vector3(winW, winH, 1f);
        glass.GetComponent<Renderer>().sharedMaterial = glassMat;
    }

    // =========================================================================
    static void CreateKissingSilhouettes(Vector3 pos)
    {
        GameObject root = new GameObject("KissingSilhouettes");
        root.transform.position = pos;

        Color sil = new Color(0.07f, 0.05f, 0.04f);
        Material silMat = GetOrMakeMat("silhouette", sil);

        // Person A - left, leaning slightly right
        GameObject pA = new GameObject("PersonA");
        pA.transform.SetParent(root.transform, false);
        pA.transform.localPosition = new Vector3(-0.25f, 0f, 0f);
        pA.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
        BuildHumanoid(pA.transform, silMat, 1f);

        // Person B - right, leaning slightly left (slightly smaller)
        GameObject pB = new GameObject("PersonB");
        pB.transform.SetParent(root.transform, false);
        pB.transform.localPosition = new Vector3(0.25f, 0f, 0f);
        pB.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
        BuildHumanoid(pB.transform, silMat, 0.92f);

        // Warm back-light
        GameObject blGO = new GameObject("Backlight");
        blGO.transform.SetParent(root.transform, false);
        blGO.transform.localPosition = new Vector3(0f, 1.4f, 0.6f);
        Light bl = blGO.AddComponent<Light>();
        bl.type = LightType.Point;
        bl.color = new Color(1f, 0.82f, 0.45f);
        bl.intensity = 2.2f;
        bl.range = 5f;
    }

    static void BuildHumanoid(Transform parent, Material mat, float s)
    {
        // torso
        AddPrimMat(PrimitiveType.Capsule, "Torso", parent,
            new Vector3(0f, 0.72f * s, 0f), new Vector3(0.27f * s, 0.38f * s, 0.14f * s),
            Quaternion.identity, mat);
        // head
        AddPrimMat(PrimitiveType.Sphere, "Head", parent,
            new Vector3(0f, 1.62f * s, 0f), Vector3.one * 0.21f * s,
            Quaternion.identity, mat);
        // left arm (extended toward the other person)
        AddPrimMat(PrimitiveType.Capsule, "ArmL", parent,
            new Vector3(-0.28f * s, 0.9f * s, 0f),
            new Vector3(0.09f * s, 0.28f * s, 0.09f * s),
            Quaternion.Euler(0f, 0f, 50f), mat);
        // right arm
        AddPrimMat(PrimitiveType.Capsule, "ArmR", parent,
            new Vector3(0.28f * s, 0.9f * s, 0f),
            new Vector3(0.09f * s, 0.28f * s, 0.09f * s),
            Quaternion.Euler(0f, 0f, -50f), mat);
        // legs
        AddPrimMat(PrimitiveType.Capsule, "LegL", parent,
            new Vector3(-0.1f * s, 0.2f * s, 0f),
            new Vector3(0.1f * s, 0.3f * s, 0.1f * s),
            Quaternion.identity, mat);
        AddPrimMat(PrimitiveType.Capsule, "LegR", parent,
            new Vector3(0.1f * s, 0.2f * s, 0f),
            new Vector3(0.1f * s, 0.3f * s, 0.1f * s),
            Quaternion.identity, mat);
    }

    // =========================================================================
    static void CreateCameraProp(Vector3 pos)
    {
        GameObject root = new GameObject("CameraAppareil");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, -25f, 0f);

        Material bodyMat   = GetOrMakeMat("cam_body",   new Color(0.10f, 0.10f, 0.10f));
        Material silverMat = GetOrMakeMat("cam_silver", new Color(0.72f, 0.70f, 0.67f));
        Material lensMat   = GetOrMakeMat("cam_lens",   new Color(0.04f, 0.04f, 0.04f));
        Material glassMat2 = GetOrMakeMat("cam_glass",  new Color(0.18f, 0.22f, 0.55f, 0.8f));

        // Body
        AddPrimMat(PrimitiveType.Cube, "Body", root.transform,
            Vector3.zero, new Vector3(0.18f, 0.12f, 0.08f), Quaternion.identity, bodyMat);
        // Grip
        AddPrimMat(PrimitiveType.Cube, "Grip", root.transform,
            new Vector3(0.097f, -0.02f, 0f), new Vector3(0.04f, 0.09f, 0.076f), Quaternion.identity, bodyMat);
        // Lens barrel
        AddPrimMat(PrimitiveType.Cylinder, "Lens", root.transform,
            new Vector3(-0.02f, 0f, -0.076f), new Vector3(0.066f, 0.056f, 0.066f),
            Quaternion.Euler(90f, 0f, 0f), lensMat);
        // Lens front
        AddPrimMat(PrimitiveType.Cylinder, "LensFront", root.transform,
            new Vector3(-0.02f, 0f, -0.130f), new Vector3(0.054f, 0.004f, 0.054f),
            Quaternion.Euler(90f, 0f, 0f), glassMat2);
        // Viewfinder
        AddPrimMat(PrimitiveType.Cube, "Viewfinder", root.transform,
            new Vector3(0.025f, 0.072f, 0f), new Vector3(0.062f, 0.026f, 0.042f),
            Quaternion.identity, bodyMat);
        // Hot shoe
        AddPrimMat(PrimitiveType.Cube, "HotShoe", root.transform,
            new Vector3(0.025f, 0.069f, 0f), new Vector3(0.042f, 0.004f, 0.026f),
            Quaternion.identity, silverMat);
        // Shutter button
        AddPrimMat(PrimitiveType.Cylinder, "ShutterBtn", root.transform,
            new Vector3(-0.038f, 0.066f, -0.01f), new Vector3(0.013f, 0.006f, 0.013f),
            Quaternion.identity, silverMat);
        // Strap lugs
        AddPrimMat(PrimitiveType.Cube, "LugL", root.transform,
            new Vector3(-0.093f, 0.045f, 0f), new Vector3(0.008f, 0.026f, 0.013f),
            Quaternion.identity, silverMat);
        AddPrimMat(PrimitiveType.Cube, "LugR", root.transform,
            new Vector3(0.093f, 0.045f, 0f), new Vector3(0.008f, 0.026f, 0.013f),
            Quaternion.identity, silverMat);
        // Pedestal
        Material pedMat = GetOrMakeMat("pedestal", new Color(0.44f, 0.40f, 0.36f));
        AddPrimMat(PrimitiveType.Cylinder, "Pedestal", root.transform,
            new Vector3(0f, -0.105f, 0f), new Vector3(0.26f, 0.065f, 0.26f),
            Quaternion.identity, pedMat);
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    static void Panel(string name, GameObject parent, Vector3 localPos, Vector3 scale, Color col)
    {
        Material mat = GetOrMakeMat("wall_" + col.GetHashCode(), col);
        AddPrimMat(PrimitiveType.Cube, name, parent.transform, localPos, scale, Quaternion.identity, mat);
    }

    static void ApplyMat(GameObject go, Material mat)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    static GameObject MakeCube(string name, Transform parent, Vector3 pos, Vector3 scale, string matKey)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        return go;
    }

    static void AddPrimMat(PrimitiveType type, string name, Transform parent,
        Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        go.transform.localRotation = localRot;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material GetOrMakeMat(string key, Color col)
    {
        if (_mats.TryGetValue(key, out Material m)) return m;
        m = MakeMat(key, col);
        _mats[key] = m;
        return m;
    }

    static Material MakeMat(string key, Color col)
    {
        string path = "Assets/Materials/" + key + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = col;
        if (col.a < 1f)
        {
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        _mats[key] = m;
        return m;
    }

    static void SavePendingMats()
    {
        foreach (var kv in _mats)
        {
            string path = "Assets/Materials/" + kv.Key + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                AssetDatabase.CreateAsset(kv.Value, path);
            else
                EditorUtility.SetDirty(kv.Value);
        }
        AssetDatabase.SaveAssets();
    }
}
