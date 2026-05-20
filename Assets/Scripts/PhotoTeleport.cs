using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Unity.XR.CoreUtils;

/// <summary>
/// Téléporte le joueur sur la route après une bonne photo (cadre vert),
/// puis bloque les déplacements pendant freezeDuration secondes.
/// Assigner routeSpawn dans l'Inspector (ou créer un GameObject vide "RouteSpawn" dans la scène).
/// </summary>
public class PhotoTeleport : MonoBehaviour
{
    [Header("Destination")]
    public Transform routeSpawn;          // GameObject vide positionné sur la route
    public float     freezeDuration = 5f;

    private XROrigin      _origin;
    private Coroutine     _freezeRoutine;

    void Start()
    {
        _origin = FindFirstObjectByType<XROrigin>();

        // Fallback : cherche un GameObject nommé "RouteSpawn"
        if (routeSpawn == null)
        {
            var go = GameObject.Find("RouteSpawn");
            if (go != null) routeSpawn = go.transform;
        }
    }

    // ── appelé par CameraViewfinder quand photo prise en vert ────────────
    public void TeleportAndFreeze()
    {
        if (_origin == null || routeSpawn == null)
        {
            Debug.LogWarning("[PhotoTeleport] routeSpawn non assigné !");
            return;
        }

        // Téléportation : déplace le root XROrigin
        // Compense le décalage horizontal de la tête pour que la caméra arrive exactement sur routeSpawn
        var cam = _origin.Camera;
        if (cam != null)
        {
            Vector3 headLocalFloor = new Vector3(
                cam.transform.localPosition.x,
                0f,
                cam.transform.localPosition.z);
            _origin.transform.position = routeSpawn.position - _origin.transform.rotation * headLocalFloor;
        }
        else
        {
            _origin.transform.position = routeSpawn.position;
        }

        _origin.transform.rotation = routeSpawn.rotation;

        // Gel des déplacements
        if (_freezeRoutine != null) StopCoroutine(_freezeRoutine);
        _freezeRoutine = StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        var providers = FindLocomotionProviders();
        foreach (var p in providers) p.enabled = false;

        yield return new WaitForSeconds(freezeDuration);

        foreach (var p in providers) p.enabled = true;
        _freezeRoutine = null;
    }

    private static List<MonoBehaviour> FindLocomotionProviders()
    {
        var result = new List<MonoBehaviour>();
        // XRI 3.x — LocomotionProvider est la base de tous les providers
        foreach (var p in FindObjectsByType<LocomotionProvider>(FindObjectsSortMode.None))
            result.Add(p);
        return result;
    }
}
