using System.Collections;
using UnityEngine;

public class VFXShooter : MonoBehaviour
{
    public enum Mode { Projectile, Beam }

    [SerializeField] private Mode mode = Mode.Projectile;

    [Tooltip("Projectile: units per second toward the target.")]
    [SerializeField] private float speed = 10f;

    [Tooltip("Beam: how long the beam stays visible before returning to pool.")]
    [SerializeField] private float beamDuration = 0.4f;

    [Tooltip("Projectile: rotate the object to face its direction of travel.")]
    [SerializeField] private bool faceDirection = true;

    private Vector3    _targetPos;
    private bool       _ready;
    private bool       _targetSet;
    private GameObject _sourcePrefab;

    // Fired when the shooter reaches its target or times out.
    public System.Action OnComplete;

    // Called by Monster before SetActive(true) so OnEnable has a target ready.
    public void SetTarget(Vector3 position)
    {
        _targetPos = position;
        _targetSet = true;
    }

    public void SetPoolSource(GameObject prefab) => _sourcePrefab = prefab;

    private void OnEnable()
    {
        if (!_targetSet)
        {
            // Fallback for any path that doesn't call SetTarget first.
            Monster fallback = AttackCommandManager.Instance?.Target;
            if (fallback == null)
            {
                Debug.LogWarning("[VFXShooter] No target — SetTarget() was not called before SetActive(true).");
                ReturnToPool();
                return;
            }
            _targetPos = fallback.transform.position;
        }
        _targetSet = false; // reset for next pool reuse

        _ready = true;

        if (mode == Mode.Beam)
        {
            ApplyBeam();
            StartCoroutine(BeamCoroutine());
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _ready     = false;
        _targetSet = false;
    }

    private void Update()
    {
        if (!_ready || mode != Mode.Projectile) return;

        Vector3 dir = _targetPos - transform.position;

        if (faceDirection && dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        transform.position = Vector3.MoveTowards(transform.position, _targetPos, speed * Time.deltaTime);

        if (dir.magnitude < 0.05f)
            ReturnToPool();
    }

    // Stretches and orients the object along its local Z axis to span attacker → target.
    private void ApplyBeam()
    {
        Vector3 dir  = _targetPos - transform.position;
        float   dist = dir.magnitude;
        transform.position += dir * 0.5f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(s.x, s.y, dist);
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

        if (_sourcePrefab != null && VFXPool.Instance != null)
            VFXPool.Instance.Return(_sourcePrefab, gameObject);
        else
            gameObject.SetActive(false);
    }
}
