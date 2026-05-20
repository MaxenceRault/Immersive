using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;
using TMPro;

public class CameraStation : MonoBehaviour
{
    [Header("Références (auto si vides)")]
    public GameObject cameraWorldObject;

    [Header("Réglages")]
    public float proximityRadius = 3f;

    // ── état ─────────────────────────────────────────────────────────────
    private bool      _holding;
    private bool      _hovered;
    private Transform _playerTf;
    private Transform _headTf;

    // ── XRI ───────────────────────────────────────────────────────────────
    private XRSimpleInteractable _xri;

    // ── input ─────────────────────────────────────────────────────────────
    private InputAction _grabAction;

    // ── sous-titre ────────────────────────────────────────────────────────
    private TextMeshProUGUI _label;
    private float           _clearAt = -1f;

    // ──────────────────────────────────────────────────────────────────────

    void Start()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            _playerTf = origin.transform;
            _headTf   = origin.Camera != null ? origin.Camera.transform : Camera.main?.transform;
        }
        else
        {
            _headTf = _playerTf = Camera.main?.transform;
        }

        if (cameraWorldObject == null)
            cameraWorldObject = GameObject.Find("CameraAppareil");

        if (cameraWorldObject == null)
        {
            Debug.LogError("[CameraStation] CameraAppareil introuvable !");
            return;
        }

        SetupInteractable();

        _grabAction = new InputAction("Grab", InputActionType.Button);
        _grabAction.AddBinding("<Keyboard>/e");
        _grabAction.AddBinding("<XRController>{LeftHand}/secondaryButton");   // Y
        _grabAction.Enable();
        _grabAction.performed += OnButtonGrab;

        BuildSubtitleUI();
    }

    void OnDestroy()
    {
        if (_xri != null)
        {
            _xri.hoverEntered.RemoveListener(OnHoverEnter);
            _xri.hoverExited.RemoveListener(OnHoverExit);
            _xri.selectEntered.RemoveListener(OnXRISelect);
        }
        if (_grabAction != null) { _grabAction.performed -= OnButtonGrab; _grabAction.Dispose(); }
    }

    // ── update ────────────────────────────────────────────────────────────
    void Update()
    {
        if (_clearAt > 0f && Time.time >= _clearAt)
        {
            _clearAt = -1f;
            SetLabel("");
        }

        if (_clearAt > 0f || _holding) return;

        if (cameraWorldObject == null || !cameraWorldObject.activeSelf)
        {
            SetLabel("");
            return;
        }

        bool near = _playerTf != null
            && Vector3.Distance(_playerTf.position, cameraWorldObject.transform.position) <= proximityRadius;

        if (_hovered)
            SetLabel("Appuyer sur la gâchette pour prendre l'appareil");
        else if (near)
            SetLabel("[ Y ]   Prendre l'appareil photo");
        else
            SetLabel("");
    }

    // ── XRI events ────────────────────────────────────────────────────────
    private void OnHoverEnter(HoverEnterEventArgs _)  => _hovered = true;
    private void OnHoverExit(HoverExitEventArgs _)    => _hovered = false;
    private void OnXRISelect(SelectEnterEventArgs _)  => Pickup();

    // ── bouton Y / E ──────────────────────────────────────────────────────
    private void OnButtonGrab(InputAction.CallbackContext _)
    {
        if (_playerTf == null) return;
        if (Vector3.Distance(_playerTf.position, cameraWorldObject.transform.position) > proximityRadius) return;
        Pickup();
    }

    // ── pickup ────────────────────────────────────────────────────────────
    private void Pickup()
    {
        if (_holding || cameraWorldObject == null || !cameraWorldObject.activeSelf) return;
        _holding = true;
        _hovered = false;
        cameraWorldObject.SetActive(false);
        ShowTimed("Appareil photo en main", 6f);
        GetComponent<CameraViewfinder>()?.OnPickup();
        Debug.Log("[CameraStation] Caméra ramassée.");
    }

    public bool IsHolding => _holding;

    // ── XRSimpleInteractable ──────────────────────────────────────────────
    private void SetupInteractable()
    {
        var sc = cameraWorldObject.GetComponent<SphereCollider>();
        if (sc == null) sc = cameraWorldObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius    = 0.5f;
        sc.center    = Vector3.zero;

        _xri = cameraWorldObject.GetComponent<XRSimpleInteractable>();
        if (_xri == null) _xri = cameraWorldObject.AddComponent<XRSimpleInteractable>();

        _xri.hoverEntered.AddListener(OnHoverEnter);
        _xri.hoverExited.AddListener(OnHoverExit);
        _xri.selectEntered.AddListener(OnXRISelect);
    }

    // ── UI ────────────────────────────────────────────────────────────────
    private void SetLabel(string t)             { if (_label) _label.text = t; }
    private void ShowTimed(string t, float d)   { SetLabel(t); _clearAt = Time.time + d; }

    private void BuildSubtitleUI()
    {
        if (_headTf == null) return;

        // Canvas World Space attaché à la tête VR
        var root = new GameObject("_SubtitleCanvas");
        root.transform.SetParent(_headTf, false);

        // Position en bas de la vue, 1.2 m devant
        root.transform.localPosition = new Vector3(0f, -0.13f, 1.2f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale    = Vector3.one * 0.001f;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        root.AddComponent<UnityEngine.UI.CanvasScaler>();

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(900f, 90f);

        // Fond noir arrondi
        var bg = new GameObject("BG");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var img = bg.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

        // Texte
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(16f, 4f);
        textRt.offsetMax = new Vector2(-16f, -4f);

        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.text         = "";
        _label.fontSize     = 46f;
        _label.alignment    = TextAlignmentOptions.Center;
        _label.color        = new Color(1f, 1f, 1f, 1f);
        _label.fontStyle    = FontStyles.Bold;
        _label.outlineColor = new Color(0f, 0f, 0f, 1f);
        _label.outlineWidth = 0.28f;
        _label.enableAutoSizing    = false;
        _label.overflowMode        = TextOverflowModes.Overflow;
        _label.enableWordWrapping  = false;
    }
}
