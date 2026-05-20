using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class CameraViewfinder : MonoBehaviour
{
    [Header("Références")]
    public CameraStation cameraStation;

    // ── état ──────────────────────────────────────────────────────────────
    private bool _viewfinderOpen;
    private float _hintAt = -1f;          // moment où afficher "[ B ] Activer"

    // ── input ─────────────────────────────────────────────────────────────
    private InputAction _bAction;

    // ── head transform ────────────────────────────────────────────────────
    private Transform _headTf;

    // ── canvas viewfinder ─────────────────────────────────────────────────
    private GameObject _vfRoot;
    private TextMeshProUGUI _vfLabel;

    // ── canvas hint ───────────────────────────────────────────────────────
    private TextMeshProUGUI _hintLabel;
    private float _hintClearAt = -1f;

    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (cameraStation == null)
            cameraStation = GetComponent<CameraStation>();

        // Récupère la tête VR via la même logique que CameraStation
        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
            _headTf = origin.Camera != null ? origin.Camera.transform : Camera.main?.transform;
        else
            _headTf = Camera.main?.transform;

        BuildViewfinderUI();
        BuildHintUI();
        SetViewfinder(false);

        _bAction = new InputAction("Viewfinder", InputActionType.Button);
        _bAction.AddBinding("<Keyboard>/v");
        _bAction.AddBinding("<XRController>{RightHand}/secondaryButton");  // B
        _bAction.Enable();
        _bAction.performed += OnToggle;
    }

    void OnDestroy()
    {
        if (_bAction != null) { _bAction.performed -= OnToggle; _bAction.Dispose(); }
    }

    void Update()
    {
        // Efface le hint après sa durée
        if (_hintClearAt > 0f && Time.time >= _hintClearAt)
        {
            _hintClearAt = -1f;
            SetHint("");
        }

        // Affiche le hint "[ B ] Activer le viseur" une fois le message pickup passé
        if (_hintAt > 0f && Time.time >= _hintAt)
        {
            _hintAt = -1f;
            if (cameraStation != null && cameraStation.IsHolding && !_viewfinderOpen)
                ShowHintTimed("[ B ]   Activer le viseur", 5f);
        }

        // Si on n'a plus l'appareil, ferme le viseur
        if (_viewfinderOpen && (cameraStation == null || !cameraStation.IsHolding))
            SetViewfinder(false);
    }

    // ── callback CameraStation ────────────────────────────────────────────
    public void OnPickup()
    {
        // Déclenche le hint 7 s après le pickup (pickup-message dure 6 s)
        _hintAt = Time.time + 7f;
    }

    // ── toggle ────────────────────────────────────────────────────────────
    private void OnToggle(InputAction.CallbackContext _)
    {
        if (cameraStation == null || !cameraStation.IsHolding) return;
        SetViewfinder(!_viewfinderOpen);
    }

    private void SetViewfinder(bool on)
    {
        _viewfinderOpen = on;
        if (_vfRoot != null) _vfRoot.SetActive(on);

        if (on)
        {
            SetHint("");
            _hintAt = -1f;
        }
        else if (cameraStation != null && cameraStation.IsHolding)
        {
            ShowHintTimed("[ B ]   Activer le viseur", 4f);
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────
    private void SetHint(string t)      { if (_hintLabel) _hintLabel.text = t; }
    private void ShowHintTimed(string t, float d) { SetHint(t); _hintClearAt = Time.time + d; }

    // ── build viewfinder canvas ───────────────────────────────────────────
    private void BuildViewfinderUI()
    {
        if (_headTf == null) return;

        _vfRoot = new GameObject("_ViewfinderCanvas");
        _vfRoot.transform.SetParent(_headTf, false);
        _vfRoot.transform.localPosition = new Vector3(0f, 0f, 0.75f);
        _vfRoot.transform.localRotation = Quaternion.identity;
        _vfRoot.transform.localScale    = Vector3.one * 0.001f;

        var canvas = _vfRoot.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        _vfRoot.AddComponent<CanvasScaler>();

        var rt = _vfRoot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000f, 700f);

        // ── Letterbox barres (haut / bas) ─────────────────────────────────
        AddRect(_vfRoot.transform, "BarTop",
            new Vector2(0f, 0.85f), Vector2.one,
            Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.75f));

        AddRect(_vfRoot.transform, "BarBottom",
            Vector2.zero, new Vector2(1f, 0.15f),
            Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.75f));

        // ── Vignette bords gauche / droite ────────────────────────────────
        AddRect(_vfRoot.transform, "BorderL",
            new Vector2(0f, 0.15f), new Vector2(0.04f, 0.85f),
            Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.5f));

        AddRect(_vfRoot.transform, "BorderR",
            new Vector2(0.96f, 0.15f), new Vector2(1f, 0.85f),
            Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.5f));

        // ── Coins (brackets) ──────────────────────────────────────────────
        AddCornerBrackets(_vfRoot.transform);

        // ── Réticule central ──────────────────────────────────────────────
        AddCrosshair(_vfRoot.transform);

        // ── Label "[ B ] Désactiver le viseur" ────────────────────────────
        var labelGo = new GameObject("VFLabel");
        labelGo.transform.SetParent(_vfRoot.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin  = new Vector2(0f, 0.14f);
        labelRt.anchorMax  = new Vector2(1f, 0.18f);
        labelRt.offsetMin  = Vector2.zero;
        labelRt.offsetMax  = Vector2.zero;

        _vfLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _vfLabel.text         = "[ B ]   Désactiver le viseur";
        _vfLabel.fontSize     = 36f;
        _vfLabel.alignment    = TextAlignmentOptions.Center;
        _vfLabel.color        = Color.white;
        _vfLabel.fontStyle    = FontStyles.Bold;
        _vfLabel.outlineColor = Color.black;
        _vfLabel.outlineWidth = 0.25f;
        _vfLabel.enableWordWrapping = false;
    }

    // ── build hint canvas (même que subtitle dans CameraStation) ─────────
    private void BuildHintUI()
    {
        if (_headTf == null) return;

        var root = new GameObject("_VFHintCanvas");
        root.transform.SetParent(_headTf, false);
        root.transform.localPosition = new Vector3(0f, -0.13f, 1.2f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one * 0.001f;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 51;
        root.AddComponent<CanvasScaler>();

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(900f, 90f);

        var bg = new GameObject("BG");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 4f);
        textRt.offsetMax = new Vector2(-16f, -4f);

        _hintLabel = textGo.AddComponent<TextMeshProUGUI>();
        _hintLabel.text         = "";
        _hintLabel.fontSize     = 46f;
        _hintLabel.alignment    = TextAlignmentOptions.Center;
        _hintLabel.color        = Color.white;
        _hintLabel.fontStyle    = FontStyles.Bold;
        _hintLabel.outlineColor = Color.black;
        _hintLabel.outlineWidth = 0.28f;
        _hintLabel.enableAutoSizing   = false;
        _hintLabel.overflowMode       = TextOverflowModes.Overflow;
        _hintLabel.enableWordWrapping = false;
    }

    // ── helpers de construction UI ────────────────────────────────────────
    private static GameObject AddRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void AddCornerBrackets(Transform parent)
    {
        float bw = 0.008f;  // épaisseur relative
        float bl = 0.07f;   // longueur relative
        float m  = 0.05f;   // marge depuis le bord

        // Coin haut-gauche
        AddRect(parent, "BrTL_H", new Vector2(m, 1f - m - bw), new Vector2(m + bl, 1f - m), Vector2.zero, Vector2.zero, Color.white);
        AddRect(parent, "BrTL_V", new Vector2(m, 1f - m - bl), new Vector2(m + bw, 1f - m), Vector2.zero, Vector2.zero, Color.white);
        // Coin haut-droit
        AddRect(parent, "BrTR_H", new Vector2(1f - m - bl, 1f - m - bw), new Vector2(1f - m, 1f - m), Vector2.zero, Vector2.zero, Color.white);
        AddRect(parent, "BrTR_V", new Vector2(1f - m - bw, 1f - m - bl), new Vector2(1f - m, 1f - m), Vector2.zero, Vector2.zero, Color.white);
        // Coin bas-gauche
        AddRect(parent, "BrBL_H", new Vector2(m, m), new Vector2(m + bl, m + bw), Vector2.zero, Vector2.zero, Color.white);
        AddRect(parent, "BrBL_V", new Vector2(m, m), new Vector2(m + bw, m + bl), Vector2.zero, Vector2.zero, Color.white);
        // Coin bas-droit
        AddRect(parent, "BrBR_H", new Vector2(1f - m - bl, m), new Vector2(1f - m, m + bw), Vector2.zero, Vector2.zero, Color.white);
        AddRect(parent, "BrBR_V", new Vector2(1f - m - bw, m), new Vector2(1f - m, m + bl), Vector2.zero, Vector2.zero, Color.white);
    }

    private static void AddCrosshair(Transform parent)
    {
        float cw = 0.003f;
        float cl = 0.025f;

        // Barre horizontale
        AddRect(parent, "CH_H",
            new Vector2(0.5f - cl, 0.5f - cw),
            new Vector2(0.5f + cl, 0.5f + cw),
            Vector2.zero, Vector2.zero,
            new Color(1f, 1f, 1f, 0.9f));
        // Barre verticale
        AddRect(parent, "CH_V",
            new Vector2(0.5f - cw, 0.5f - cl),
            new Vector2(0.5f + cw, 0.5f + cl),
            Vector2.zero, Vector2.zero,
            new Color(1f, 1f, 1f, 0.9f));
    }
}
