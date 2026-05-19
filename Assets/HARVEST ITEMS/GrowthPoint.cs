using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct GrowthHarvestableSpawnPool
{
    public HarvestableData harvestableData;
    public float spawnWeight;
}

[RequireComponent(typeof(SphereCollider))]
public class GrowthPoint : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public List<GrowthHarvestableSpawnPool> spawnPool = new List<GrowthHarvestableSpawnPool>();
    public float minSpawnDelay = 10f;
    public float maxSpawnDelay = 30f;

    [Header("Hazard Configuration")]
    public GameObject defaultHazardAreaPrefab;

    [Header("State Sync")]
    [SyncVar] public bool isOccupied = false;
    [SyncVar] public bool isFullyGrown = false;
    [SyncVar] public int currentItemIndex = -1;
    [SyncVar] public bool currentIsOvergrown = false;
    [SyncVar] public double growthStartTime = 0;

    private int currentManaYield;
    private int currentHealthYield;

    private AudioSource audioSource;
    private Transform visualMesh;
    private bool isShaking = false;
    private NetworkIdentity currentHarvester;
    private NetworkIdentity serverCurrentHarvester;
    private float localHarvestTimer = 0f;

    private SphereCollider triggerCol;
    private float baseRadius;
    private Quaternion baseLocalRotation = Quaternion.identity;

    public HarvestableData currentData
    {
        get
        {
            if (currentItemIndex >= 0 && currentItemIndex < spawnPool.Count)
                return spawnPool[currentItemIndex].harvestableData;
            return null;
        }
    }

    private void Awake()
    {
        triggerCol = GetComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        baseRadius = triggerCol.radius;
        triggerCol.radius = 0f;
        gameObject.layer = LayerMask.NameToLayer("Harvestable");

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.loop = true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(SpawnCycle());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isOccupied && currentItemIndex >= 0)
        {
            float timePassed = (float)(NetworkTime.time - growthStartTime);
            if (timePassed < 0f) timePassed = 0f;
            SpawnVisualsLocally(currentItemIndex, currentIsOvergrown, timePassed);
        }
    }

    [Server]
    private IEnumerator SpawnCycle()
    {
        while (true)
        {
            yield return new WaitUntil(() => !isOccupied);

            float waitTime = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(waitTime);

            if (isOccupied || spawnPool.Count == 0) continue;

            isOccupied = true;
            isFullyGrown = false;

            float totalWeight = 0f;
            foreach (var item in spawnPool) totalWeight += item.spawnWeight;

            float randomVal = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;
            int selectedIndex = 0;

            for (int i = 0; i < spawnPool.Count; i++)
            {
                cumulativeWeight += spawnPool[i].spawnWeight;
                if (randomVal <= cumulativeWeight)
                {
                    selectedIndex = i;
                    break;
                }
            }

            currentItemIndex = selectedIndex;
            HarvestableData selectedData = spawnPool[selectedIndex].harvestableData;
            currentIsOvergrown = Random.value <= selectedData.overgrowProbability;

            if (currentIsOvergrown)
            {
                currentManaYield = Mathf.RoundToInt(selectedData.manaYield * 1.25f);
                currentHealthYield = Mathf.RoundToInt(selectedData.healthYield * 1.25f);
            }
            else
            {
                currentManaYield = selectedData.manaYield;
                currentHealthYield = selectedData.healthYield;
            }

            growthStartTime = NetworkTime.time;
            RpcStartGrowing(selectedIndex, currentIsOvergrown, growthStartTime);
        }
    }

    void Update()
    {
        if (isOccupied && currentData != null)
        {
            float maxTime = Mathf.Max(currentData.growthTime, 0.1f);
            float progress = Mathf.Clamp01((float)(NetworkTime.time - growthStartTime) / maxTime);
            triggerCol.radius = Mathf.Lerp(0f, baseRadius, progress);
        }
        else
        {
            triggerCol.radius = 0f;
        }

        if (visualMesh == null) return;

        if (isShaking && currentHarvester != null && currentData != null)
        {
            localHarvestTimer += Time.deltaTime;
            float maxTime = Mathf.Max(currentData.harvestTime, 0.1f);
            float progress = Mathf.Clamp01(localHarvestTimer / maxTime);

            Vector3 dirToPlayer = (currentHarvester.transform.position - transform.position).normalized;
            Vector3 flatDir = new Vector3(dirToPlayer.x, 0, dirToPlayer.z).normalized;
            if (flatDir == Vector3.zero) flatDir = Vector3.forward;

            Vector3 leanAxis = Vector3.Cross(Vector3.up, flatDir);
            float leanAngle = Mathf.Lerp(0f, 45f, progress);
            Quaternion leanRot = Quaternion.AngleAxis(leanAngle, leanAxis);

            float shakeIntensity = Mathf.Lerp(0.5f, 6f, progress);
            float shakeX = Mathf.Sin(Time.time * 50f) * shakeIntensity;
            float shakeZ = Mathf.Cos(Time.time * 45f) * shakeIntensity;
            Quaternion shakeRot = Quaternion.Euler(shakeX, 0, shakeZ);

            Quaternion targetRot = baseLocalRotation * leanRot * shakeRot;
            visualMesh.localRotation = Quaternion.Slerp(visualMesh.localRotation, targetRot, Time.deltaTime * 10f);
        }
        else
        {
            localHarvestTimer = 0f;
            if (visualMesh.localRotation != baseLocalRotation)
            {
                visualMesh.localRotation = Quaternion.Slerp(visualMesh.localRotation, baseLocalRotation, Time.deltaTime * 8f);
            }
        }
    }

    [ClientRpc]
    private void RpcStartGrowing(int itemIndex, bool isOvergrown, double startTime)
    {
        float timePassed = (float)(NetworkTime.time - startTime);
        if (timePassed < 0f) timePassed = 0f;
        SpawnVisualsLocally(itemIndex, isOvergrown, timePassed);
    }

    private void SpawnVisualsLocally(int itemIndex, bool isOvergrown, float timePassed)
    {
        if (itemIndex < 0 || itemIndex >= spawnPool.Count) return;
        HarvestableData data = spawnPool[itemIndex].harvestableData;
        if (data.modelPrefab == null) return;

        foreach (Transform child in transform) Destroy(child.gameObject);

        GameObject obj = Instantiate(data.modelPrefab, transform);
        obj.transform.localPosition = Vector3.zero;

        visualMesh = obj.transform;
        baseLocalRotation = visualMesh.localRotation;

        Collider[] rogueColliders = obj.GetComponentsInChildren<Collider>();
        foreach (var col in rogueColliders)
        {
            Destroy(col);
        }

        HarvestableVisuals visuals = obj.GetComponent<HarvestableVisuals>();
        if (visuals == null) visuals = obj.AddComponent<HarvestableVisuals>();

        visuals.Initialize(data, isOvergrown, this, timePassed);
    }

    public void OnVisualGrowthComplete()
    {
        if (!isServer) return;
        isFullyGrown = true;
    }

    [Command(requiresAuthority = false)]
    public void CmdTakeProjectileDamage()
    {
        if (!isOccupied || currentData == null)
        {
            Debug.LogWarning($"<color=red><b>[SERVER]</b></color> Node {gameObject.name} shot but has empty contents.");
            return;
        }

        Debug.Log($"<color=orange><b>[SERVER HARVEST HIT]</b></color> Harvesting point triggered by damage. Evaluating explosion states...");

        float maxTime = Mathf.Max(currentData.growthTime, 0.1f);
        float progress = Mathf.Clamp01((float)(NetworkTime.time - growthStartTime) / maxTime);

        if (currentData.explodeOnShoot)
        {
            float scaleMult = isFullyGrown ? 1f : progress;
            SpawnHazardArea(scaleMult);
        }

        ClearNode();
    }

    [Server]
    private void SpawnHazardArea(float scaleMultiplier)
    {
        if (defaultHazardAreaPrefab == null)
        {
            Debug.LogError($"<color=red><b>[CRITICAL]</b></color> Spawning aborted: defaultHazardAreaPrefab is missing on node {gameObject.name}!");
            return;
        }

        if (currentData == null) return;

        GameObject hazardObj = Instantiate(defaultHazardAreaPrefab, transform.position, Quaternion.identity);
        HazardArea hazardData = hazardObj.GetComponent<HazardArea>();

        if (hazardData != null)
        {
            hazardData.Setup(currentData.explosionRadius, currentData.hazardAreaDuration, scaleMultiplier, currentData.immediateHazardDamage, currentData.hazardDebuff);
        }

        NetworkServer.Spawn(hazardObj);
        Debug.Log($"<color=green><b>[SERVER HARVEST EXPLOSION]</b></color> Deployed world cloud instance at {transform.position}.");
    }

    [Command(requiresAuthority = false)]
    public void CmdStartHarvesting(NetworkConnectionToClient sender = null)
    {
        if (!isFullyGrown || !isOccupied) return;
        if (sender != null && sender.identity != null)
        {
            serverCurrentHarvester = sender.identity;
            RpcHarvestTick(sender.identity);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdCancelHarvest(NetworkConnectionToClient sender = null)
    {
        if (sender != null && sender.identity == serverCurrentHarvester)
        {
            serverCurrentHarvester = null;
        }
        RpcCancelHarvest();
    }

    [Command(requiresAuthority = false)]
    public void CmdResolveMinigame(bool success, NetworkConnectionToClient sender = null)
    {
        if (!isFullyGrown || !isOccupied) return;
        if (sender == null || sender.identity != serverCurrentHarvester) return;

        serverCurrentHarvester = null;

        if (success)
        {
            AwardYields(sender.identity, currentManaYield, currentHealthYield);
            RpcHarvestComplete(sender.identity);
        }
        else
        {
            Debug.Log($"<color=orange><b>[SERVER]</b></color> Failed minigame check on {gameObject.name}. processing failure consequence rules...");

            if (currentData.explodeOnFail)
            {
                SpawnHazardArea(1f);
                ClearNode();
            }
            else if (currentData.failurePenalty == FailurePenalty.HalfYield)
            {
                int halfMana = Mathf.CeilToInt(currentManaYield / 2f);
                AwardYields(sender.identity, halfMana, Mathf.CeilToInt(currentHealthYield / 2f));
                RpcHarvestComplete(sender.identity);
            }
            else
            {
                ClearNode();
            }
        }

        isOccupied = false;
        isFullyGrown = false;
        currentItemIndex = -1;
    }

    [Server]
    private void AwardYields(NetworkIdentity playerNetId, int mana, int health)
    {
        PlayerHealth ph = playerNetId.GetComponent<PlayerHealth>();

        StatusEffectManager sem = playerNetId.GetComponent<StatusEffectManager>();
        bool canHeal = (sem == null || !sem.isHealingDisabled);

        if (ph != null && canHeal) ph.currentHealth = Mathf.Min(ph.currentHealth + health, ph.maxHealth);

        WandController wc = playerNetId.GetComponent<WandController>();
        if (wc != null)
        {
            wc.currentReserveEnergy = Mathf.Min(wc.currentReserveEnergy + mana, wc.maxReserveEnergy);
            wc.TargetUpdateReserveEnergy(wc.currentReserveEnergy);
        }
    }

    [Server]
    public void ClearNode()
    {
        isOccupied = false;
        isFullyGrown = false;
        currentItemIndex = -1;
        serverCurrentHarvester = null;
        triggerCol.radius = 0f;
        RpcClearVisualsWithParticles();
    }

    [ClientRpc]
    private void RpcClearVisualsWithParticles()
    {
        if (currentData != null && currentData.destroyedParticle != null)
            Instantiate(currentData.destroyedParticle, transform.position, Quaternion.identity);

        if (currentData != null && currentData.destroyedSound != null)
            AudioSource.PlayClipAtPoint(currentData.destroyedSound, transform.position);

        foreach (Transform child in transform) Destroy(child.gameObject);

        visualMesh = null;
        isShaking = false;
        currentHarvester = null;
        if (audioSource.isPlaying) audioSource.Stop();
    }

    [ClientRpc]
    private void RpcHarvestTick(NetworkIdentity playerNetId)
    {
        currentHarvester = playerNetId;
        isShaking = true;
        if (currentData != null && currentData.harvestingLoopSound != null)
        {
            audioSource.clip = currentData.harvestingLoopSound;
            if (!audioSource.isPlaying) audioSource.Play();
        }
    }

    [ClientRpc]
    private void RpcCancelHarvest()
    {
        isShaking = false;
        currentHarvester = null;
        audioSource.Stop();
    }

    [ClientRpc]
    private void RpcHarvestComplete(NetworkIdentity playerNetId)
    {
        isShaking = false;
        currentHarvester = null;
        audioSource.Stop();

        WandController wc = playerNetId.GetComponent<WandController>();
        Transform targetWand = playerNetId.isLocalPlayer ? wc.FPFirePoint : wc.TPFirePoint;

        StartCoroutine(AbsorbRoutine(targetWand, currentData));
    }

    private IEnumerator AbsorbRoutine(Transform targetWand, HarvestableData data)
    {
        if (visualMesh == null) yield break;
        Transform meshToAbsorb = visualMesh;
        visualMesh = null;

        float t = 0f;
        Vector3 startPos = meshToAbsorb.position;
        Vector3 startScale = meshToAbsorb.localScale;

        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            if (targetWand != null) meshToAbsorb.position = Vector3.Lerp(startPos, targetWand.position, t);
            meshToAbsorb.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (targetWand != null && data != null)
        {
            if (data.absorbedParticle != null) Instantiate(data.absorbedParticle, targetWand.position, Quaternion.identity);
            if (data.harvestedSound != null) AudioSource.PlayClipAtPoint(data.harvestedSound, targetWand.position);
        }

        Destroy(meshToAbsorb.gameObject);
    }
}