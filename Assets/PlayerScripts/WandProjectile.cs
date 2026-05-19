using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class WandProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;
    private float speed;
    private float force;
    private int damage;
    private GameObject impactPrefab;
    private WandController shooterController;

    [Header("Audio Settings")]
    public AudioClip travelSound;
    public AudioClip impactSound;
    private AudioSource audioSource;

    public bool isVisualOnlyDummy = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.loop = true;
    }

    public void Setup(float projSpeed, float projForce, int projDamage, GameObject impact, LayerMask hitLayers, Collider[] ignoredColliders, WandController shooter)
    {
        speed = projSpeed;
        force = projForce;
        damage = projDamage;
        impactPrefab = impact;
        shooterController = shooter;
        isVisualOnlyDummy = false;

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        col.enabled = true;

        foreach (var c in ignoredColliders)
        {
            if (c != null && col != null) Physics.IgnoreCollision(col, c);
        }

        if (travelSound != null)
        {
            audioSource.clip = travelSound;
            audioSource.Play();
        }

        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, 5f);
    }

    public void SetupDummy(float projSpeed, GameObject impact, Collider[] ignoredColliders)
    {
        speed = projSpeed;
        impactPrefab = impact;
        isVisualOnlyDummy = true;

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        col.enabled = true;

        foreach (var c in ignoredColliders)
        {
            if (c != null && col != null) Physics.IgnoreCollision(col, c);
        }

        if (travelSound != null)
        {
            audioSource.clip = travelSound;
            audioSource.Play();
        }

        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];
        SpawnImpact(contact.point, contact.normal);

        if (isVisualOnlyDummy)
        {
            Destroy(gameObject);
            return;
        }

        GrowthPoint gp = collision.collider.GetComponentInParent<GrowthPoint>();
        if (gp != null)
        {
            gp.CmdTakeProjectileDamage();
            Destroy(gameObject);
            return;
        }

        PlayerHealth health = collision.collider.GetComponentInParent<PlayerHealth>();
        if (health != null && shooterController != null)
        {
            NetworkIdentity netId = health.GetComponent<NetworkIdentity>();
            if (netId != null)
            {
                shooterController.CmdApplyProjectileDamage(netId, damage, transform.forward);
            }
        }
        else
        {
            Rigidbody hitRb = collision.collider.attachedRigidbody;
            if (hitRb != null && shooterController != null)
            {
                NetworkIdentity netId = hitRb.GetComponentInParent<NetworkIdentity>();
                if (netId != null) shooterController.CmdApplyProjectileForce(netId, transform.forward * force);
                else if (!hitRb.isKinematic) hitRb.AddForce(transform.forward * force, ForceMode.Impulse);
            }
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isVisualOnlyDummy) return;

        GrowthPoint gp = other.GetComponent<GrowthPoint>();
        if (gp != null)
        {
            gp.CmdTakeProjectileDamage();
            SpawnImpact(transform.position, -transform.forward);
            Destroy(gameObject);
        }
    }

    private void SpawnImpact(Vector3 position, Vector3 normal)
    {
        if (impactSound != null) AudioSource.PlayClipAtPoint(impactSound, position, 1f);

        if (impactPrefab != null)
        {
            GameObject impact = Instantiate(impactPrefab, position, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
    }
}