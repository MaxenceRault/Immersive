using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Affiche des bulles de hint près des boutons de manette Quest.
/// API : ControllerHint.Instance.Show(XRButton.Y, "Prendre l'appareil");
///       ControllerHint.Instance.HideAll();
/// </summary>
public class ControllerHint : MonoBehaviour
{
    public static ControllerHint Instance { get; private set; }

    // ── contrôleurs ───────────────────────────────────────────────────────
    private Transform _leftCtrl;
    private Transform _rightCtrl;

    // ── hints actifs ──────────────────────────────────────────────────────
    private readonly Dictionary<XRButton, GameObject> _hints = new();

    // ── positions locales approx. des boutons (Quest 2/3) ─────────────────
    // (x droite, y haut, z avant) en espace local du contrôleur
    // Offsets en espace local du contrôleur, + grande hauteur pour visibilité
    private static readonly Dictionary<XRButton, (bool right, Vector3 offset)> ButtonPos = new()
    {
        { XRButton.A,            (true,  new Vector3( 0.06f,  0.12f,  0.00f)) },
        { XRButton.B,            (true,  new Vector3(-0.01f,  0.14f,  0.00f)) },
        { XRButton.RightTrigger, (true,  new Vector3( 0.06f,  0.10f,  0.06f)) },
        { XRButton.RightGrip,    (true,  new Vector3( 0.10f,  0.06f,  0.02f)) },
        { XRButton.RightStick,   (true,  new Vector3(-0.02f,  0.12f, -0.02f)) },
        { XRButton.X,            (false, new Vector3(-0.06f,  0.12f,  0.00f)) },
        { XRButton.Y,            (false, new Vector3( 0.01f,  0.14f,  0.00f)) },
        { XRButton.LeftTrigger,  (false, new Vector3(-0.06f,  0.10f,  0.06f)) },
        { XRButton.LeftGrip,     (false, new Vector3(-0.10f,  0.06f,  0.02f)) },
        { XRButton.LeftStick,    (false, new Vector3( 0.02f,  0.12f, -0.02f)) },
    };

    private static readonly Dictionary<XRButton, string> ButtonLabels = new()
    {
        { XRButton.A, "A" }, { XRButton.B, "B" },
        { XRButton.X, "X" }, { XRButton.Y, "Y" },
        { XRButton.RightTrigger, "RT" }, { XRButton.LeftTrigger, "LT" },
        { XRButton.RightGrip,    "RG" }, { XRButton.LeftGrip,    "LG" },
        { XRButton.RightStick,   "RS" }, { XRButton.LeftStick,   "LS" },
    };

    // ─────────────────────────────────────────────────────────────────────

    void Awake() { Instance = this; }

    void Start() { FindControllers(); }

    void Update()
    {
        // Si contrôleurs pas encore trouvés, réessayer
        if (_leftCtrl == null || _rightCtrl == null) FindControllers();

        // Animation : légère oscillation verticale
        float bob = Mathf.Sin(Time.time * 2.5f) * 0.008f;
        foreach (var kvp in _hints)
        {
            if (kvp.Value == null) continue;
            var data = kvp.Value.GetComponent<HintData>();
            if (data == null) continue;
            kvp.Value.transform.position = data.BaseWorldPos + Vector3.up * bob;
            kvp.Value.transform.rotation = Quaternion.LookRotation(
                kvp.Value.transform.position - (Camera.main != null ? Camera.main.transform.position : Vector3.zero)
            );
        }
    }

    // ── API publique ──────────────────────────────────────────────────────

    public void Show(XRButton button, string description, Color? color = null)
    {
        if (_hints.TryGetValue(button, out var existing) && existing != null)
            Destroy(existing);

        if (!ButtonPos.TryGetValue(button, out var bdata)) return;

        Transform ctrlTf = bdata.right ? _rightCtrl : _leftCtrl;
        if (ctrlTf == null) return;

        Vector3 worldPos = ctrlTf.TransformPoint(bdata.offset + new Vector3(0f, 0.05f, 0f));

        var hint = BuildHintCanvas(button, description, color ?? Color.white, worldPos);
        _hints[button] = hint;
    }

    public void Hide(XRButton button)
    {
        if (_hints.TryGetValue(button, out var go) && go != null) Destroy(go);
        _hints.Remove(button);
    }

    public void HideAll()
    {
        foreach (var kvp in _hints)
            if (kvp.Value != null) Destroy(kvp.Value);
        _hints.Clear();
    }

    // ── recherche des contrôleurs ─────────────────────────────────────────

    private void FindControllers()
    {
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null) return;

        Transform offset = origin.CameraFloorOffsetObject != null
            ? origin.CameraFloorOffsetObject.transform
            : origin.transform;

        foreach (Transform child in offset)
        {
            string n = child.name.ToLower();
            if (n.Contains("left")  && (n.Contains("hand") || n.Contains("controller"))) _leftCtrl  = child;
            if (n.Contains("right") && (n.Contains("hand") || n.Contains("controller"))) _rightCtrl = child;
        }
    }

    // ── construction du canvas hint ───────────────────────────────────────

    private static GameObject BuildHintCanvas(XRButton btn, string description, Color accentColor, Vector3 worldPos)
    {
        var root = new GameObject($"_Hint_{btn}");
        root.transform.position = worldPos;
        root.transform.localScale = Vector3.one * 0.0008f;

        // Store position for bob animation
        var data = root.AddComponent<HintData>();
        data.BaseWorldPos = worldPos;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 70;
        root.AddComponent<CanvasScaler>();
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 100f);

        // Fond arrondi (foncé)
        var bg = MakeRectGo(root.transform, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.88f);

        // Pastille bouton (gauche)
        var badgeGo = MakeRectGo(root.transform, "Badge",
            new Vector2(0f, 0.1f), new Vector2(0.32f, 0.9f), Vector2.zero, Vector2.zero);
        badgeGo.AddComponent<Image>().color = accentColor;

        var btnLabel = badgeGo.AddComponent<TextMeshProUGUI>();
        // TextMeshProUGUI ne peut pas coexister avec Image sur le même GO – séparer
        Destroy(btnLabel);
        var badgeTxt = MakeRectGo(badgeGo.transform, "BtnTxt",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var badgeLbl = badgeTxt.AddComponent<TextMeshProUGUI>();
        badgeLbl.text = ButtonLabels.TryGetValue(btn, out var bl) ? bl : btn.ToString();
        badgeLbl.fontSize      = 52f;
        badgeLbl.alignment     = TextAlignmentOptions.Center;
        badgeLbl.color         = Color.black;
        badgeLbl.fontStyle     = FontStyles.Bold;
        badgeLbl.enableWordWrapping = false;

        // Texte description (droite)
        var descGo = MakeRectGo(root.transform, "Desc",
            new Vector2(0.34f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero);
        var descLbl = descGo.AddComponent<TextMeshProUGUI>();
        descLbl.text = description;
        descLbl.fontSize      = 36f;
        descLbl.alignment     = TextAlignmentOptions.MidlineLeft;
        descLbl.color         = Color.white;
        descLbl.fontStyle     = FontStyles.Bold;
        descLbl.outlineColor  = Color.black;
        descLbl.outlineWidth  = 0.2f;
        descLbl.enableWordWrapping = false;
        descLbl.overflowMode  = TextOverflowModes.Overflow;

        // Petite flèche vers le bas (indique le bouton)
        var arrowGo = MakeRectGo(root.transform, "Arrow",
            new Vector2(0.1f, -0.25f), new Vector2(0.25f, 0.08f), Vector2.zero, Vector2.zero);
        arrowGo.AddComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 0.7f);

        SetLayerRecursive(root, 5);
        return root;
    }

    private static GameObject MakeRectGo(Transform parent, string name,
        Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = omin; rt.offsetMax = omax;
        return go;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}

// ── enum des boutons ──────────────────────────────────────────────────────
public enum XRButton
{
    A, B, X, Y,
    RightTrigger, LeftTrigger,
    RightGrip, LeftGrip,
    RightStick, LeftStick
}

// ── composant data pour animation ────────────────────────────────────────
public class HintData : MonoBehaviour
{
    public Vector3 BaseWorldPos;
}
