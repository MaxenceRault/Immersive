using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class CameraViewfinder : MonoBehaviour
{
    [Header("Références")]
    public CameraStation cameraStation;

    // ── zoom camera ───────────────────────────────────────────────────────
    private const float FOV_DEFAULT = 60f;
    private const float ZOOM_SPEED  = 2.0f;
    private float         _zoomFactor = 1f;
    private Camera        _zoomCam;
    private RenderTexture _rt;
    private RawImage      _viewportImage;

    private InputAction _rightTrigger;
    private InputAction _leftTrigger;

    // ── cadre couleur (vert/rouge) ────────────────────────────────────────
    private Image[] _frameBorder  = new Image[4];
    private bool    _frameIsGreen = false;

    // ── highlight objet bloquant ──────────────────────────────────────────
    private GameObject         _highlightShell;
    private GameObject         _currentBlocker;
    private Material           _hlMatRed;
    private Material           _hlMatGreen;
    private int                _rayFrame;

    // ── flash (bouton A) ──────────────────────────────────────────────────
    private const float FLASH_DURATION = 0.45f;
    private InputAction _flashAction;
    private Image       _flashImage;
    private float       _flashEnd = -1f;

    // ── viewfinder toggle (bouton B) ──────────────────────────────────────
    private bool        _viewfinderOpen;
    private InputAction _bAction;

    // ── hint (sous-titre secondaire) ──────────────────────────────────────
    private float           _hintAt      = -1f;
    private float           _hintClearAt = -1f;
    private TextMeshProUGUI _hintLabel;

    // ── canvases ──────────────────────────────────────────────────────────
    private GameObject      _vfRoot;
    private TextMeshProUGUI _zoomLabel;

    // ── head ──────────────────────────────────────────────────────────────
    private Transform _headTf;
    private Camera    _headCam;

    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (cameraStation == null)
            cameraStation = GetComponent<CameraStation>();

        var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
        {
            _headCam = origin.Camera;
            _headTf  = _headCam != null ? _headCam.transform : Camera.main?.transform;
        }
        else
        {
            _headCam = Camera.main;
            _headTf  = _headCam?.transform;
        }

        // Matériaux highlight (transparent coloré via Sprites/Default — marche dans tout pipeline)
        _hlMatRed   = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.05f, 0.05f, 0.45f), renderQueue = 3000 };
        _hlMatGreen = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 1f, 0.15f, 0.45f), renderQueue = 3000 };

        BuildViewfinderUI();
        SetupZoomCamera();
        BuildHintUI();
        BuildFlashUI();
        SetViewfinder(false);

        // Bouton B – viseur
        _bAction = new InputAction("Viewfinder", InputActionType.Button);
        _bAction.AddBinding("<Keyboard>/v");
        _bAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        _bAction.Enable();
        _bAction.performed += OnToggleViewfinder;

        // Bouton A – flash photo
        _flashAction = new InputAction("Flash", InputActionType.Button);
        _flashAction.AddBinding("<Keyboard>/f");
        _flashAction.AddBinding("<XRController>{RightHand}/primaryButton");
        _flashAction.Enable();
        _flashAction.performed += OnFlash;

        // Gâchettes – zoom
        _rightTrigger = new InputAction("ZoomIn",  InputActionType.Value, expectedControlType: "Axis");
        _rightTrigger.AddBinding("<XRController>{RightHand}/trigger");
        _rightTrigger.AddBinding("<Keyboard>/equals");
        _rightTrigger.Enable();

        _leftTrigger = new InputAction("ZoomOut", InputActionType.Value, expectedControlType: "Axis");
        _leftTrigger.AddBinding("<XRController>{LeftHand}/trigger");
        _leftTrigger.AddBinding("<Keyboard>/minus");
        _leftTrigger.Enable();
    }

    void OnDestroy()
    {
        if (_bAction      != null) { _bAction.performed      -= OnToggleViewfinder; _bAction.Dispose(); }
        if (_flashAction  != null) { _flashAction.performed  -= OnFlash;            _flashAction.Dispose(); }
        if (_rightTrigger != null) _rightTrigger.Dispose();
        if (_leftTrigger  != null) _leftTrigger.Dispose();
        if (_rt           != null) { _rt.Release(); Destroy(_rt); }
        if (_hlMatRed     != null) Destroy(_hlMatRed);
        if (_hlMatGreen   != null) Destroy(_hlMatGreen);
        ClearHighlight();
    }

    // ─────────────────────────────────────────────────────────────────────

    void Update()
    {
        bool holding = cameraStation != null && cameraStation.IsHolding;

        if (_viewfinderOpen && !holding) SetViewfinder(false);

        // Hint "[ B ] Activer"
        if (_hintAt > 0f && Time.time >= _hintAt)
        {
            _hintAt = -1f;
            if (holding && !_viewfinderOpen) ShowHintTimed("[ B ]   Activer le viseur", 5f);
        }
        if (_hintClearAt > 0f && Time.time >= _hintClearAt) { _hintClearAt = -1f; SetHint(""); }

        // Zoom + détection blocage (uniquement viseur ouvert)
        if (_viewfinderOpen)
        {
            float zoomIn  = _rightTrigger?.ReadValue<float>() ?? 0f;
            float zoomOut = _leftTrigger?.ReadValue<float>()  ?? 0f;
            _zoomFactor = Mathf.Clamp(_zoomFactor + (zoomIn - zoomOut) * ZOOM_SPEED * 3f * Time.deltaTime, 1f, 4f);
            if (_zoomCam != null) _zoomCam.fieldOfView = FOV_DEFAULT / _zoomFactor;
            UpdateZoomLabel();

            // Raycast tous les 3 frames (perf)
            if (++_rayFrame % 3 == 0) UpdateBlockingDetection();
        }
        else
        {
            _zoomFactor = 1f;
            if (_zoomCam != null) _zoomCam.fieldOfView = FOV_DEFAULT;
            ClearHighlight();
            SetFrameColor(Color.clear);
        }

        // Flash fade-out
        if (_flashEnd > 0f && _flashImage != null)
        {
            float alpha = 1f - Mathf.Clamp01((Time.time - (_flashEnd - FLASH_DURATION)) / FLASH_DURATION);
            _flashImage.color = new Color(1f, 1f, 1f, alpha);
            if (Time.time >= _flashEnd) { _flashEnd = -1f; _flashImage.color = new Color(1f,1f,1f,0f); }
        }
    }

    // ── détection objet bloquant ──────────────────────────────────────────

    private void UpdateBlockingDetection()
    {
        if (_zoomCam == null) return;

        Ray ray = new Ray(_zoomCam.transform.position, _zoomCam.transform.forward);
        int mask = ~(1 << 5); // ignore layer UI

        // Récupère tous les hits sur le rayon (jusqu'à 50m)
        var hits = Physics.RaycastAll(ray, 50f, mask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        PhotoSubject firstSubject = null;
        GameObject   firstBlocker = null;

        foreach (var hit in hits)
        {
            var subject = hit.collider.GetComponentInParent<PhotoSubject>();
            if (subject != null)
            {
                firstSubject = subject;
                break; // on a trouvé le subject — ce qui est avant lui est bloquant
            }
            else if (firstBlocker == null)
            {
                var rParent = FindRendererParent(hit.collider.transform);
                if (rParent != null) firstBlocker = rParent;
            }
        }

        if (firstSubject != null)
        {
            // Mode "subject tagué" : rouge si bloqué, vert si dégagé
            if (firstBlocker != null)
            {
                if (firstBlocker != _currentBlocker)
                {
                    ClearHighlight();
                    _currentBlocker = firstBlocker;
                    _highlightShell = BuildHighlightShell(firstBlocker, _hlMatRed);
                }
                SetFrameColor(new Color(1f, 0.08f, 0.08f, 0.85f));
            }
            else
            {
                ClearHighlight();
                SetFrameColor(new Color(0.1f, 1f, 0.2f, 0.85f));
            }
            return;
        }

        // Fallback : pas de PhotoSubject taggué → détection par distance (2 m)
        if (hits.Length > 0 && hits[0].distance < 2f)
        {
            var blocker = FindRendererParent(hits[0].collider.transform);
            if (blocker != null && blocker != _currentBlocker)
            {
                ClearHighlight();
                _currentBlocker = blocker;
                _highlightShell = BuildHighlightShell(blocker, _hlMatRed);
            }
            SetFrameColor(new Color(1f, 0.08f, 0.08f, 0.85f));
        }
        else
        {
            ClearHighlight();
            SetFrameColor(new Color(0.1f, 1f, 0.2f, 0.85f));
        }
    }

    private static GameObject FindRendererParent(Transform t)
    {
        for (int i = 0; i < 4 && t != null; i++)
        {
            if (t.GetComponentInChildren<Renderer>() != null) return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    // ── highlight shell : copie des meshes légèrement agrandie ────────────

    private static GameObject BuildHighlightShell(GameObject target, Material mat)
    {
        var shell = new GameObject("_HL");
        shell.transform.SetParent(target.transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale    = Vector3.one * 1.06f;

        foreach (var mr in target.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var go = new GameObject("_HL_" + mr.name);
            go.transform.SetParent(shell.transform, false);
            go.transform.localPosition = mr.transform.localPosition;
            go.transform.localRotation = mr.transform.localRotation;
            go.transform.localScale    = mr.transform.localScale;

            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        return shell;
    }

    private void ClearHighlight()
    {
        if (_highlightShell != null) { Destroy(_highlightShell); _highlightShell = null; }
        _currentBlocker = null;
    }

    // ── cadre couleur ─────────────────────────────────────────────────────

    private void SetFrameColor(Color c)
    {
        _frameIsGreen = (c.g > 0.5f && c.r < 0.5f);
        foreach (var img in _frameBorder)
            if (img != null) img.color = c;
    }

    // ── callbacks ─────────────────────────────────────────────────────────

    private void OnToggleViewfinder(InputAction.CallbackContext _)
    {
        if (cameraStation == null || !cameraStation.IsHolding) return;
        SetViewfinder(!_viewfinderOpen);
    }

    private void OnFlash(InputAction.CallbackContext _)
    {
        if (!_viewfinderOpen) return;
        TriggerFlash();
    }

    public void OnPickup() { _hintAt = Time.time + 7f; }

    // ─────────────────────────────────────────────────────────────────────

    private void SetViewfinder(bool on)
    {
        _viewfinderOpen = on;
        if (_vfRoot  != null) _vfRoot.SetActive(on);
        if (_zoomCam != null) _zoomCam.enabled = on;
        cameraStation?.SetSubtitleVisible(!on);

        if (on) { SetHint(""); _hintAt = -1f; }
        else
        {
            _zoomFactor = 1f;
            if (_zoomCam != null) _zoomCam.fieldOfView = FOV_DEFAULT;
            ClearHighlight();
            SetFrameColor(Color.clear);
            if (cameraStation != null && cameraStation.IsHolding)
                ShowHintTimed("[ B ]   Activer le viseur", 4f);
        }
    }

    private void TriggerFlash()
    {
        if (_flashImage == null) return;
        _flashImage.color = new Color(1f, 1f, 1f, 1f);
        _flashEnd = Time.time + FLASH_DURATION;

        if (_frameIsGreen)
            GetComponent<PhotoTeleport>()?.TeleportAndFreeze();
    }

    private void UpdateZoomLabel()
    {
        if (_zoomLabel == null) return;
        _zoomLabel.text = _zoomFactor < 1.05f ? "" : $"×{_zoomFactor:F1}";
    }

    private void SetHint(string t)                { if (_hintLabel) _hintLabel.text = t; }
    private void ShowHintTimed(string t, float d) { SetHint(t); _hintClearAt = Time.time + d; }

    // ── setup zoom camera ─────────────────────────────────────────────────

    private void SetupZoomCamera()
    {
        if (_headTf == null) return;

        _rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.Default);
        _rt.antiAliasing = 2;
        _rt.Create();

        var go = new GameObject("_ZoomCamera");
        go.transform.SetParent(_headTf, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        _zoomCam = go.AddComponent<Camera>();
        _zoomCam.fieldOfView   = FOV_DEFAULT;
        _zoomCam.nearClipPlane = _headCam != null ? _headCam.nearClipPlane : 0.01f;
        _zoomCam.farClipPlane  = _headCam != null ? _headCam.farClipPlane  : 500f;
        int baseMask = _headCam != null ? _headCam.cullingMask : -1;
        _zoomCam.cullingMask   = baseMask & ~(1 << 5);
        _zoomCam.depth         = _headCam != null ? _headCam.depth - 1 : -2;
        _zoomCam.targetTexture = _rt;
        _zoomCam.enabled       = false;

        if (_viewportImage != null) _viewportImage.texture = _rt;
    }

    // ── construction canvases ─────────────────────────────────────────────

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
        _vfRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(1000f, 700f);

        // Viewport RT (fond)
        var vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(_vfRoot.transform, false);
        var vpRt = vpGo.AddComponent<RectTransform>();
        vpRt.anchorMin = new Vector2(0.04f, 0.14f); vpRt.anchorMax = new Vector2(0.96f, 0.86f);
        vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
        _viewportImage = vpGo.AddComponent<RawImage>();
        _viewportImage.color = Color.white;

        // Cadre rouge/vert (4 bandes sur les bords du viewport)
        float vx0=0.04f, vx1=0.96f, vy0=0.14f, vy1=0.86f, bt=0.018f;
        _frameBorder[0] = MakeRect(_vfRoot.transform, "FrT", new Vector2(vx0,vy1-bt),    new Vector2(vx1, vy1),    Color.clear);
        _frameBorder[1] = MakeRect(_vfRoot.transform, "FrB", new Vector2(vx0, vy0),      new Vector2(vx1, vy0+bt), Color.clear);
        _frameBorder[2] = MakeRect(_vfRoot.transform, "FrL", new Vector2(vx0, vy0+bt),   new Vector2(vx0+bt, vy1-bt), Color.clear);
        _frameBorder[3] = MakeRect(_vfRoot.transform, "FrR", new Vector2(vx1-bt, vy0+bt),new Vector2(vx1, vy1-bt), Color.clear);

        // Letterbox + bords noirs
        MakeRect(_vfRoot.transform, "BarTop",    new Vector2(0f, 0.86f),    Vector2.one,               new Color(0f,0f,0f,0.78f));
        MakeRect(_vfRoot.transform, "BarBottom", Vector2.zero,               new Vector2(1f, 0.14f),    new Color(0f,0f,0f,0.78f));
        MakeRect(_vfRoot.transform, "BorderL",   new Vector2(0f, 0.14f),    new Vector2(0.04f, 0.86f), new Color(0f,0f,0f,0.5f));
        MakeRect(_vfRoot.transform, "BorderR",   new Vector2(0.96f, 0.14f), new Vector2(1f, 0.86f),    new Color(0f,0f,0f,0.5f));

        AddCornerBrackets(_vfRoot.transform);
        AddCrosshair(_vfRoot.transform);

        var la = MakeTextGo(_vfRoot.transform, "VFLabel",   new Vector2(0f, 0.14f),    new Vector2(1f, 0.185f));
        MakeLabel(la, "[ B ]   Désactiver le viseur", 34f);

        var zg = MakeTextGo(_vfRoot.transform, "ZoomLabel", new Vector2(0.05f, 0.82f), new Vector2(0.28f, 0.89f));
        _zoomLabel = MakeLabel(zg, "", 38f);
        _zoomLabel.alignment = TextAlignmentOptions.Left;

        var ag = MakeTextGo(_vfRoot.transform, "FlashHint", new Vector2(0.70f, 0.82f), new Vector2(0.97f, 0.89f));
        var al = MakeLabel(ag, "[ A ]   Photo", 30f);
        al.alignment = TextAlignmentOptions.Right;

        SetLayerRecursive(_vfRoot, 5);
    }

    private void BuildHintUI()
    {
        if (_headTf == null) return;
        var root = new GameObject("_VFHintCanvas");
        root.transform.SetParent(_headTf, false);
        root.transform.localPosition = new Vector3(0f, -0.13f, 1.2f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one * 0.001f;

        root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<Canvas>().sortingOrder = 51;
        root.AddComponent<CanvasScaler>();
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 90f);

        var bg = new GameObject("BG"); bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

        var textGo = new GameObject("Text"); textGo.transform.SetParent(root.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 4f); textRt.offsetMax = new Vector2(-16f, -4f);

        _hintLabel = textGo.AddComponent<TextMeshProUGUI>();
        _hintLabel.text = ""; _hintLabel.fontSize = 46f;
        _hintLabel.alignment = TextAlignmentOptions.Center;
        _hintLabel.color = Color.white; _hintLabel.fontStyle = FontStyles.Bold;
        _hintLabel.outlineColor = Color.black; _hintLabel.outlineWidth = 0.28f;
        _hintLabel.enableAutoSizing = false; _hintLabel.overflowMode = TextOverflowModes.Overflow;
        _hintLabel.enableWordWrapping = false;
        SetLayerRecursive(root, 5);
    }

    private void BuildFlashUI()
    {
        if (_headTf == null) return;
        var root = new GameObject("_FlashCanvas");
        root.transform.SetParent(_headTf, false);
        root.transform.localPosition = new Vector3(0f, 0f, 0.35f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one * 0.001f;

        root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.GetComponent<Canvas>().sortingOrder = 100;
        root.AddComponent<CanvasScaler>();
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(2200f, 2200f);

        var panel = new GameObject("FlashPanel"); panel.transform.SetParent(root.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        _flashImage = panel.AddComponent<Image>();
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        SetLayerRecursive(root, 5);
    }

    // ── helpers UI ────────────────────────────────────────────────────────

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    private static Image MakeRect(Transform parent, string name, Vector2 amin, Vector2 amax, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>(); img.color = color;
        return img;
    }

    private static GameObject MakeTextGo(Transform parent, string name, Vector2 amin, Vector2 amax)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    private static TextMeshProUGUI MakeLabel(GameObject go, string text, float size)
    {
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text; lbl.fontSize = size;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white; lbl.fontStyle = FontStyles.Bold;
        lbl.outlineColor = Color.black; lbl.outlineWidth = 0.25f;
        lbl.enableWordWrapping = false;
        return lbl;
    }

    private static void AddCornerBrackets(Transform parent)
    {
        const float bw=0.008f, bl=0.07f, m=0.05f;
        MakeRect(parent,"BrTL_H",new Vector2(m,1f-m-bw),new Vector2(m+bl,1f-m),Color.white);
        MakeRect(parent,"BrTL_V",new Vector2(m,1f-m-bl),new Vector2(m+bw,1f-m),Color.white);
        MakeRect(parent,"BrTR_H",new Vector2(1f-m-bl,1f-m-bw),new Vector2(1f-m,1f-m),Color.white);
        MakeRect(parent,"BrTR_V",new Vector2(1f-m-bw,1f-m-bl),new Vector2(1f-m,1f-m),Color.white);
        MakeRect(parent,"BrBL_H",new Vector2(m,m),new Vector2(m+bl,m+bw),Color.white);
        MakeRect(parent,"BrBL_V",new Vector2(m,m),new Vector2(m+bw,m+bl),Color.white);
        MakeRect(parent,"BrBR_H",new Vector2(1f-m-bl,m),new Vector2(1f-m,m+bw),Color.white);
        MakeRect(parent,"BrBR_V",new Vector2(1f-m-bw,m),new Vector2(1f-m,m+bl),Color.white);
    }

    private static void AddCrosshair(Transform parent)
    {
        const float cw=0.003f, cl=0.025f;
        var c = new Color(1f,1f,1f,0.9f);
        MakeRect(parent,"CH_H",new Vector2(0.5f-cl,0.5f-cw),new Vector2(0.5f+cl,0.5f+cw),c);
        MakeRect(parent,"CH_V",new Vector2(0.5f-cw,0.5f-cl),new Vector2(0.5f+cw,0.5f+cl),c);
    }
}
