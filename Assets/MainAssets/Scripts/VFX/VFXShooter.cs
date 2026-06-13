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

    private Vector3 _targetPos;
    private bool    _ready;
    private GameObject _sourcePrefab;

    // Fired when the shooter reaches its target or times out.
    // Monster subscribes to this so it can remove the instance from its tracking list.
    public System.Action OnComplete;

    public void SetPoolSource(GameObject prefab) => _sourcePrefab = prefab;

    // OnEnable fires synchronously when VFXPool.Get calls SetActive(true),
    // so AttackCommandManager.Target is guaranteed still set on the same frame.
    private void OnEnable()
    {
        Monster target = AttackCommandManager.Instance?.Target;
        if (target == null)
        {
            Debug.LogWarning("[VFXShooter] No target found in AttackCommandManager.");
            ReturnToPool();
            return;
        }

        _targetPos = target.transform.position;
        _ready     = true;

        if (mode == Mode.Beam)
        {
            ApplyBeam();
            StartCoroutine(BeamCoroutine());
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _ready = false;
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
