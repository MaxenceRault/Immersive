using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class PlaceCameraModel
{
    const string DONE_KEY  = "PlaceCameraModel_Done";
    const string CAM_PATH  = "Assets/3Dmodels/Camera.obj";
    const string MAT_PATH  = "Assets/3Dmodels/CameraURP.mat";

    static PlaceCameraModel() => EditorApplication.delayCall += TryPlace;

    [MenuItem("Tools/Place Camera Model")]
    static void TryPlace()
    {
        if (SessionState.GetBool(DONE_KEY, false)) return;

        GameObject camAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CAM_PATH);
        if (camAsset == null)
        {
            Debug.LogWarning("[PlaceCameraModel] Camera.obj not found – import it first.");
            return;
        }

        if (GameObject.Find("CameraAppareil") != null)
        {
            SessionState.SetBool(DONE_KEY, true);
            return;
        }

        // ── Build URP material with all texture maps ──────────────────────────
        Material mat = BuildMaterial();

        // ── Instantiate model ─────────────────────────────────────────────────
        GameObject cam = (GameObject)PrefabUtility.InstantiatePrefab(camAsset);
        if (cam == null) cam = Object.Instantiate(camAsset);
        cam.name = "CameraAppareil";

        // ── Scale to real size (~18 cm height) ────────────────────────────────
        Bounds b = GetBounds(cam);
        float s = b.size.magnitude > 0.001f ? 0.18f / b.size.y : 0.003f;
        cam.transform.localScale = Vector3.one * s;

        // ── Position on pedestal (CameraStand cylinder, top at y=1.0) ────────
        cam.transform.position = new Vector3(1.8f, 1.09f, -0.5f);
        cam.transform.rotation = Quaternion.Euler(0f, -20f, 0f);

        // ── Apply material to every renderer ──────────────────────────────────
        foreach (Renderer r in cam.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        Undo.RegisterCreatedObjectUndo(cam, "Place Camera Model");
        EditorUtility.SetDirty(cam);
        SessionState.SetBool(DONE_KEY, true);
        Debug.Log("[PlaceCameraModel] Done.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    static Material BuildMaterial()
    {
        // Reuse if already created
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        if (existing != null) return existing;

        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) urp = Shader.Find("Standard"); // fallback

        Material mat = new Material(urp);
        mat.name = "CameraURP";

        // Albedo (base colour)
        Texture2D albedo = Load<Texture2D>("Assets/3Dmodels/Camera_Albedo.jpg");
        if (albedo) mat.SetTexture("_BaseMap", albedo);

        // Normal map (body) – mark as normal in importer
        Texture2D normal = Load<Texture2D>("Assets/3Dmodels/Camera_Normal.jpg");
        if (normal)
        {
            EnsureNormalMap("Assets/3Dmodels/Camera_Normal.jpg");
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // Metallic (metalness map → R channel, smoothness from inverted specular)
        Texture2D metalTex = Load<Texture2D>("Assets/3Dmodels/Camera_Metalness.jpg");
        if (metalTex)
        {
            mat.SetTexture("_MetallicGlossMap", metalTex);
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_GlossMapScale", 0.6f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Smoothness", 0.5f);
        }

        // AO (occlusion)
        Texture2D ao = Load<Texture2D>("Assets/3Dmodels/Camera_AO.jpg");
        if (ao)
        {
            mat.SetTexture("_OcclusionMap", ao);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }

        // Workflow: metallic
        mat.SetFloat("_WorkflowMode", 1f);
        mat.SetFloat("_Smoothness", 0.55f);

        AssetDatabase.CreateAsset(mat, MAT_PATH);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static T Load<T>(string path) where T : Object
        => AssetDatabase.LoadAssetAtPath<T>(path);

    static void EnsureNormalMap(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
