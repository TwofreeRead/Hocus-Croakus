using UnityEngine;
using Mirror;
using System.Collections.Generic;

[RequireComponent(typeof(SphereCollider))]
public class HazardArea : NetworkBehaviour
{
    [Header("Debug Visuals")]
    public bool showDebugRadius = true;

    [SyncVar(hook = nameof(OnRadiusChanged))] public float radius;
    [SyncVar] public float duration;

    private int damage;
    private StatusEffectData appliedEffect;

    private SphereCollider col;
    private HashSet<PlayerHealth> playersInCloud = new HashSet<PlayerHealth>();
    private float tickTimer = 0f;

    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    public void Setup(float r, float dur, float scaleMultiplier, int dmg, StatusEffectData effect)
    {
        radius = r * scaleMultiplier;
        duration = dur;
        damage = Mathf.CeilToInt(dmg * scaleMultiplier);
        appliedEffect = effect;

        if (col == null) col = GetComponent<SphereCollider>();
        if (col != null) col.radius = radius;
        transform.localScale = Vector3.one;

        Debug.Log($"<color=green><b>[HAZARD SPAWNED]</b></color> Radius: {radius}, Explosion Damage: {damage}");

        // THE FIX 1: INSTANT DETONATION
        // Do not wait for the next physics frame. Sweep the area instantly.
        Collider[] initialHits = Physics.OverlapSphere(transform.position, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (var hit in initialHits)
        {
            PlayerHealth ph = hit.transform.root.GetComponentInChildren<PlayerHealth>();
            if (ph != null && !playersInCloud.Contains(ph))
            {
                playersInCloud.Add(ph);
                ApplyInitialHit(ph, "Caught in initial blast");
            }
        }
    }

    private void OnRadiusChanged(float oldRadius, float newRadius)
    {
        if (col == null) col = GetComponent<SphereCollider>();
        if (col != null) col.radius = newRadius;
    }

    public override void OnStartServer()
    {
        Invoke(nameof(DestroySelf), duration);
    }

    [ServerCallback]
    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth ph = other.transform.root.GetComponentInChildren<PlayerHealth>();

        // If a player walks into the lingering cloud after the explosion...
        if (ph != null && !playersInCloud.Contains(ph))
        {
            playersInCloud.Add(ph);
            ApplyInitialHit(ph, "Walked into lingering hazard");
        }
    }

    [ServerCallback]
    private void OnTriggerExit(Collider other)
    {
        PlayerHealth ph = other.transform.root.GetComponentInChildren<PlayerHealth>();
        if (ph != null && playersInCloud.Contains(ph))
        {
            Debug.Log($"<color=cyan><b>[HAZARD]</b></color> Player {ph.gameObject.name} left the hazard zone.");
            playersInCloud.Remove(ph);
        }
    }

    private void ApplyInitialHit(PlayerHealth ph, string context)
    {
        Debug.Log($"<color=orange><b>[HAZARD STRIKE]</b></color> {ph.gameObject.name} hit! Context: {context}");

        // THE FIX 2: Apply raw explosion damage ONLY ONCE upon entry/detonation
        if (damage > 0)
        {
            ph.ServerTakeDamage(damage, Vector3.zero);
        }

        StatusEffectManager sem = ph.GetComponent<StatusEffectManager>();
        if (sem != null && appliedEffect != null)
        {
            sem.ApplyEffect(appliedEffect);
        }
    }

    [ServerCallback]
    private void Update()
    {
        tickTimer += Time.deltaTime;

        // THE FIX 3: Lingering Cloud Logic
        // While standing in the gas, do NOT spam raw explosion damage. 
        // Just refresh the Status Effect and let the poison DoT do the work!
        if (tickTimer >= 0.5f)
        {
            tickTimer = 0f;
            playersInCloud.RemoveWhere(p => p == null);

            foreach (PlayerHealth ph in playersInCloud)
            {
                StatusEffectManager sem = ph.GetComponent<StatusEffectManager>();

                if (sem != null && appliedEffect != null)
                {
                    sem.ApplyEffect(appliedEffect);
                }
            }
        }
    }

    [Server]
    void DestroySelf()
    {
        Debug.Log($"<color=red><b>[HAZARD CLEANUP]</b></color> Hazard Area expired. Destroying.");
        NetworkServer.Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (showDebugRadius)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawSphere(transform.position, col != null ? col.radius : radius);
        }
    }
}