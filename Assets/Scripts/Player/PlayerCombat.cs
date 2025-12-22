using UnityEngine;

/// <summary>
/// Handles simple melee combat: raycast from the camera on left click and apply damage to entities.
/// Attach to the camera (auto-added by PlayerController if missing).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public Transform cameraTransform;
    public int meleeDamage = 5;
    public float attackRange = 3f;
    public float attackRadius = 0.4f; // small sphere for melee sweep
    public LayerMask hitMask = Physics.DefaultRaycastLayers; // restrict if desired

    // Optional reference to player for applying effects
    public PlayerController player;
    public PlayerArm playerArm;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip attackSound;
    public AudioClip hitSound;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (playerArm == null && cameraTransform != null)
            playerArm = cameraTransform.GetComponent<PlayerArm>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (PauseMenu.IsPaused) return;

        if (Input.GetMouseButtonDown(0))
        {
            // Trigger arm swing if available
            playerArm?.Swing();
            if (attackSound != null) audioSource.PlayOneShot(attackSound);

            PerformMeleeAttack();
        }
    }

    void PerformMeleeAttack()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hit;

        // First try a sphere cast for a wider chance to hit
        if (Physics.SphereCast(ray, attackRadius, out hit, attackRange, hitMask))
        {
            ApplyDamageToHit(hit.collider);
            return;
        }

        // Fallback to raycast
        if (Physics.Raycast(ray, out hit, attackRange, hitMask))
        {
            ApplyDamageToHit(hit.collider);
        }
    }

    void ApplyDamageToHit(Collider col)
    {
        if (col == null) return;

        // Try to find damageable components in collider or parent
        // Zombies
        var zcComp = col.GetComponentInParent<ZombieController>();
        if (zcComp != null)
        {
            zcComp.TakeDamage(meleeDamage);
            if (hitSound != null) audioSource.PlayOneShot(hitSound);
            Debug.Log($"Hit zombie for {meleeDamage}");
            return;
        }

        // Try generic "enemy" component that implements TakeDamage
        var possibleEnemy = col.GetComponentInParent<Component>();
        if (possibleEnemy != null)
        {
            var method = possibleEnemy.GetType().GetMethod("TakeDamage");
            if (method != null)
            {
                method.Invoke(possibleEnemy, new object[] { meleeDamage });
                if (hitSound != null) audioSource.PlayOneShot(hitSound);
                Debug.Log($"Hit enemy for {meleeDamage} (reflection)");
                return;
            }
        }

        // Pigs
        var pig = col.GetComponentInParent<PigController>();
        if (pig != null)
        {
            pig.TakeDamage(meleeDamage);
            if (hitSound != null) audioSource.PlayOneShot(hitSound);
            Debug.Log($"Hit pig for {meleeDamage}");
            return;
        }

        // Generic: try component with TakeDamage method
        var comp = col.GetComponentInParent<Component>();
        if (comp != null)
        {
            var methodInfo = comp.GetType().GetMethod("TakeDamage");
            if (methodInfo != null)
            {
                methodInfo.Invoke(comp, new object[] { meleeDamage });
                Debug.Log($"Hit {comp.GetType().Name} for {meleeDamage} (generic)");
                return;
            }
        }
    }
}
