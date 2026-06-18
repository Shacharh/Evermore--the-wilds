using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

public class VFXShooter : MonoBehaviour
{
    public enum Mode { Projectile, Beam }

    [SerializeField] private Mode mode = Mode.Projectile;

    [ShowIf("mode", "Projectile")]
    [Tooltip("Projectile: units per second toward the target.")]
    [SerializeField] private float speed = 10f;

    [ShowIf("mode", "Projectile")]
    [Tooltip("Projectile: rotate the object to face its direction of travel.")]
    [SerializeField] private bool faceDirection = true;

    [ShowIf("mode", "Beam")]
    [Tooltip("Beam: how long the beam stays visible before returning to pool.")]
    [SerializeField] private float beamDuration = 0.4f;

    [ShowIf("mode", "Beam")]
    [Tooltip("Beam: stretch and center the object between spawn and target (for mesh-based lasers). " +
             "Disable for VFX Graph particle emitters — they should stay at spawn and just face the target.")]
    [SerializeField] private bool stretchBeam = false;

    [ShowIf("mode", "Beam")]
    [Tooltip("When true, this VFX tracks its spawn-point transform every frame. " +
             "Use for effects that must stay attached to the caster (e.g. fire breath at the mouth).")]
    [SerializeField] private bool follow = false;

    private Vector3    _targetPos;
    private bool       _ready;
    private bool       _targetSet;
    private GameObject _sourcePrefab;
    private Transform  _followTransform;

    // Fired when the shooter reaches its target or times out.
    public System.Action OnComplete;

    // Called by Monster before SetActive(true) so OnEnable has a target ready.
    public void SetTarget(Vector3 position)
    {
        Debug.Log($"[VFXShooter] SetTarget called on '{gameObject.name}' (root: '{transform.root.gameObject.name}') → {position}");
        _targetPos = position;
        _targetSet = true;
    }

    public void SetPoolSource(GameObject prefab) => _sourcePrefab = prefab;

    public void SetFollowTransform(Transform t) => _followTransform = t;

    private void OnEnable()
    {
        Debug.Log($"[VFXShooter] OnEnable on '{gameObject.name}' (root: '{transform.root.gameObject.name}'), _targetSet={_targetSet}");
        if (!_targetSet)
        {
            // Fallback for any path that doesn't call SetTarget first.
            Monster fallback = AttackCommandManager.Instance?.Target;
            if (fallback == null)
            {
                Debug.LogWarning($"[VFXShooter] No target — SetTarget() was not called before SetActive(true). Object='{gameObject.name}', root='{transform.root.gameObject.name}'");
                ReturnToPool();
                return;
            }
            Debug.Log($"[VFXShooter] Using fallback target: {fallback.name}");
            _targetPos = fallback.transform.position;
        }
        _targetSet = false; // reset for next pool reuse

        _ready = true;

        // VFX Graphs don't auto-restart when re-enabled from pool — call Play() explicitly.
        var vfxGraphs = GetComponentsInChildren<VisualEffect>(true);
        Debug.Log($"[VFXShooter] OnEnable '{gameObject.name}': restarting {vfxGraphs.Length} VFX Graph(s)");
        foreach (var vfx in vfxGraphs)
            vfx.Play();

        if (mode == Mode.Beam)
        {
            ApplyBeam();
            StartCoroutine(BeamCoroutine());
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _ready           = false;
        _targetSet       = false;
        _followTransform = null;
        foreach (var vfx in GetComponentsInChildren<VisualEffect>(true))
            vfx.Stop();
    }

    private void Update()
    {
        if (follow && _followTransform != null)
        {
            transform.position = _followTransform.position;
            if (mode == Mode.Beam)
            {
                Vector3 toTarget = _targetPos - transform.position;
                if (toTarget != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }
        }

        if (!_ready || mode != Mode.Projectile) return;

        Vector3 dir = _targetPos - transform.position;

        if (faceDirection && dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        transform.position = Vector3.MoveTowards(transform.position, _targetPos, speed * Time.deltaTime);

        if (dir.magnitude < 0.05f)
            ReturnToPool();
    }

    private void ApplyBeam()
    {
        Vector3 dir = _targetPos - transform.position;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (stretchBeam)
        {
            float dist = dir.magnitude;
            transform.position += dir * 0.5f;
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(s.x, s.y, dist);
        }
    }

    private IEnumerator BeamCoroutine()
    {
        yield return new WaitForSeconds(beamDuration);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        OnComplete?.Invoke();
        OnComplete = null;

        GameObject root = transform.root.gameObject;
        if (_sourcePrefab != null && VFXPool.Instance != null)
            VFXPool.Instance.Return(_sourcePrefab, root);
        else
            root.SetActive(false);
    }
}
