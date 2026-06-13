using UnityEngine;

public class VFXShooter : MonoBehaviour
{
    public enum Mode { Projectile, Beam }

    [SerializeField] private Mode mode = Mode.Projectile;

    [Tooltip("Projectile: units per second toward the target.")]
    [SerializeField] private float speed = 10f;

    [Tooltip("Beam: how long the beam stays visible before destroying itself.")]
    [SerializeField] private float beamDuration = 0.4f;

    [Tooltip("Projectile: rotate the object to face its direction of travel.")]
    [SerializeField] private bool faceDirection = true;

    private Vector3 _targetPos;
    private bool _ready;

    private void Start()
    {
        Monster target = AttackCommandManager.Instance?.Target;
        if (target == null)
        {
            Debug.LogWarning("[VFXShooter] No target found in AttackCommandManager — destroying VFX.");
            Destroy(gameObject);
            return;
        }

        _targetPos = target.transform.position;
        _ready = true;

        if (mode == Mode.Beam)
        {
            ApplyBeam();
            Destroy(gameObject, beamDuration);
        }
    }

    private void Update()
    {
        if (!_ready || mode != Mode.Projectile) return;

        Vector3 dir = _targetPos - transform.position;

        if (faceDirection && dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        transform.position = Vector3.MoveTowards(transform.position, _targetPos, speed * Time.deltaTime);

        if (dir.magnitude < 0.05f)
            Destroy(gameObject);
    }

    // Stretches and orients the object along its local Z axis to span attacker → target.
    // The VFX prefab should be modelled/oriented along its local Z (forward) axis.
    private void ApplyBeam()
    {
        Vector3 dir = _targetPos - transform.position;
        float dist = dir.magnitude;

        transform.position += dir * 0.5f;

        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(s.x, s.y, dist);
    }
}
