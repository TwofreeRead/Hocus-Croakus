using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class WandController : NetworkBehaviour
{
    [Header("Wand Configuration")]
    [SerializeField] private WandData[] wandModes = new WandData[3];

    [Header("References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform headMount;
    [SerializeField] private FPWandVisuals fpVisuals;
    [SerializeField] private TPWandVisuals tpVisuals;
    [SerializeField] private FrogAnimator tpAnimator;
    [SerializeField] private CrosshairManager crosshairManager;
    [SerializeField] private Transform fpFirePoint;
    [SerializeField] private Transform tpFirePoint;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;

    public Transform FPFirePoint => fpFirePoint;
    public Transform TPFirePoint => tpFirePoint;

    [Header("Unified Energy System")]
    public int maxReserveEnergy = 1000;

    [SyncVar] public int loadedEnergyMode0;
    [SyncVar] public int loadedEnergyMode1;
    [SyncVar] public int loadedEnergyMode2;
    [SyncVar] public int currentReserveEnergy = 1000;

    public int[] localLoadedEnergy = new int[3];
    public int localReserveEnergy = 1000;

    [Header("Debug UI")]
    public bool showDebug = true;

    [Header("Network Sync")]
    [SyncVar(hook = nameof(OnWandModeChanged))] public int syncedWandMode = 0;
    [SyncVar] public float syncedPitch = 0f;
    [SyncVar] public bool syncedIsFiringMode2 = false;
    [SyncVar] public float syncedChargeRatio = 0f;
    [SyncVar] public bool syncedIsHarvesting = false;

    [SyncVar] public NetworkIdentity serverHeldObject;
    [SyncVar] private float serverHoldDistance;
    [SyncVar] private Quaternion serverGrabRotOffset;
    [SyncVar] private bool serverIsChargingThrow;

    public bool IsReloading => isReloading;
    public bool IsHoldingObject => isLocalPlayer ? localIntendedGrabState : (serverHeldObject != null);
    public bool syncedIsGrabbing => serverHeldObject != null;
    public int CurrentModeIndex => isLocalPlayer ? currentModeIndex : syncedWandMode;
    public float CurrentChargeRatio => wandModes[CurrentModeIndex].gravitySpell.maxChargeTime > 0 ? (currentChargeTime / wandModes[CurrentModeIndex].gravitySpell.maxChargeTime) : 0f;
    public GravitySpellProperties CurrentGravitySpell => wandModes[CurrentModeIndex].gravitySpell;
    public float NetworkPitch => isLocalPlayer ? GetLocalPitch() : syncedPitch;
    public bool NetworkIsFiringMode2 => isLocalPlayer ? (currentModeIndex == 1 && Input.GetMouseButton(0)) : syncedIsFiringMode2;
    public float NetworkChargeRatio => isLocalPlayer ? CurrentChargeRatio : syncedChargeRatio;

    private int currentModeIndex = 0;
    private float nextFireTime = 0f;
    private bool isReloading = false;
    private float reloadTimer = 0f;
    private bool isLookingAtGrabbable = false;

    private bool localIntendedGrabState = false;
    private float currentHoldDistance;
    private float currentChargeTime = 0f;
    private float serverShakePhase = 0f;
    private GrabbedObjectAura currentAura;

    private bool isHarvesting = false;
    private GrowthPoint targetGrowthPoint = null;
    private float harvestTimer = 0f;
    private bool wasHarvestingFlag = false;

    private Vector3 serverAimPos;
    private Vector3 serverAimForward;
    private Vector3 serverAimRight;
    private Vector3 serverAimUp;

    private NetworkIdentity lastSeenServerObject;
    private AudioSource sfxSource;
    private AudioSource loopSource;

    void Start()
    {
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        localReserveEnergy = currentReserveEnergy;

        for (int i = 0; i < wandModes.Length; i++)
        {
            if (wandModes[i] != null) localLoadedEnergy[i] = wandModes[i].maxLoadedEnergy;
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.spatialBlend = 1f;

        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.spatialBlend = 1f;
        loopSource.loop = true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerResetEnergy();
    }

    [Server]
    public void ServerResetEnergy()
    {
        currentReserveEnergy = maxReserveEnergy;

        if (wandModes.Length > 0 && wandModes[0] != null) loadedEnergyMode0 = wandModes[0].maxLoadedEnergy;
        if (wandModes.Length > 1 && wandModes[1] != null) loadedEnergyMode1 = wandModes[1].maxLoadedEnergy;
        if (wandModes.Length > 2 && wandModes[2] != null) loadedEnergyMode2 = wandModes[2].maxLoadedEnergy;

        TargetResetLocalEnergy();
        TargetUpdateReserveEnergy(currentReserveEnergy);
    }

    private int GetLoadedEnergy(int mode)
    {
        if (mode == 0) return loadedEnergyMode0;
        if (mode == 1) return loadedEnergyMode1;
        if (mode == 2) return loadedEnergyMode2;
        return 0;
    }

    private void SetLoadedEnergy(int mode, int val)
    {
        if (mode == 0) loadedEnergyMode0 = val;
        else if (mode == 1) loadedEnergyMode1 = val;
        else if (mode == 2) loadedEnergyMode2 = val;
    }

    private float GetLocalPitch()
    {
        if (headMount == null) return 0f;
        float pitch = headMount.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
        return Mathf.Clamp(pitch, -60f, 50f);
    }

    void Update()
    {
        if (wandModes.Length == 0 || wandModes[CurrentModeIndex] == null) return;
        HandleReloadTimer();

        if (isLocalPlayer)
        {
            if (!NetworkClient.ready) return;

            if (playerHealth != null && playerHealth.isDead)
            {
                if (localIntendedGrabState) ExecuteDropLocal(0.1f);
                if (isHarvesting)
                {
                    if (MinigameManager.Instance != null && MinigameManager.Instance.IsPlaying)
                        MinigameManager.Instance.StopMinigame(false);
                    else
                        CancelHarvest();
                }
            }
            else
            {
                HandleInput();
                UpdateCrosshairState();

                float p = GetLocalPitch();
                bool f = (currentModeIndex == 1 && Input.GetMouseButton(0));
                float c = CurrentChargeRatio;

                if (Mathf.Abs(p - syncedPitch) > 1f || f != syncedIsFiringMode2 || Mathf.Abs(c - syncedChargeRatio) > 0.05f)
                {
                    CmdSyncUpperBody(p, f, c);
                }

                if (localIntendedGrabState)
                {
                    CmdSyncAim(playerCamera.position, playerCamera.forward, playerCamera.right, playerCamera.up);
                }
            }
        }

        HandleAudioLoops();
        UpdateVisuals();
    }

    void FixedUpdate() { if (isServer) ServerCalculateGravityPhysics(); }

    public override void OnStopServer()
    {
        if (serverHeldObject != null)
        {
            Rigidbody rb = serverHeldObject.GetComponent<Rigidbody>();
            if (rb != null) { rb.useGravity = true; rb.linearDamping = 0f; rb.angularDamping = 0.05f; }
            serverHeldObject = null;
        }
        base.OnStopServer();
    }

    public void SyncPitch(float pitch) { CmdSyncPitch(pitch); }
    [Command] private void CmdSyncPitch(float p) { syncedPitch = p; }
    [Command] public void CmdSetChargeRatio(float r) { syncedChargeRatio = r; }
    [Command] private void CmdSyncUpperBody(float p, bool f, float c) { syncedPitch = p; syncedIsFiringMode2 = f; syncedChargeRatio = c; }
    [Command] private void CmdSyncAim(Vector3 pos, Vector3 fwd, Vector3 right, Vector3 up) { serverAimPos = pos; serverAimForward = fwd; serverAimRight = right; serverAimUp = up; }
    [Command] private void CmdSetWandMode(int modeIndex) { syncedWandMode = modeIndex; }
    [Command] private void CmdSetHarvestingState(bool state) { syncedIsHarvesting = state; }

    [Command] private void CmdFire(Vector3 targetPoint, int seed) { RpcFire(targetPoint, seed); }
    [ClientRpc(includeOwner = false)] private void RpcFire(Vector3 targetPoint, int seed) { ExecuteRemoteFire(targetPoint, seed); }
    [Command] private void CmdEmptyShoot() { RpcEmptyShoot(); }
    [ClientRpc(includeOwner = false)] private void RpcEmptyShoot() { ExecuteRemoteEmptyShoot(); }

    [Command]
    private void CmdReloadAndTransferEnergy(int modeIndex)
    {
        if (modeIndex < 0 || modeIndex >= wandModes.Length || wandModes[modeIndex] == null) return;
        WandData data = wandModes[modeIndex];
        int current = GetLoadedEnergy(modeIndex);
        int needed = data.maxLoadedEnergy - current;

        if (needed <= 0 || currentReserveEnergy <= 0) return;

        int transferAmount = Mathf.Min(needed, currentReserveEnergy);
        currentReserveEnergy -= transferAmount;
        SetLoadedEnergy(modeIndex, current + transferAmount);

        TargetSetLocalEnergy(modeIndex, current + transferAmount);
        TargetUpdateReserveEnergy(currentReserveEnergy);
        RpcReload();
    }

    [ClientRpc(includeOwner = false)] private void RpcReload() { TriggerRemoteReload(); }

    [Command]
    private void CmdDrainEnergy(int modeIndex, int amount)
    {
        if (modeIndex < 0 || modeIndex >= 3) return;
        int current = GetLoadedEnergy(modeIndex);
        SetLoadedEnergy(modeIndex, Mathf.Max(0, current - amount));
    }

    [Command]
    public void CmdDestroyGrowthPoint(GameObject gpObj)
    {
        if (gpObj != null)
        {
            GrowthPoint gp = gpObj.GetComponent<GrowthPoint>();
            if (gp != null && gp.isOccupied) gp.ClearNode();
        }
    }

    [TargetRpc] private void TargetSetLocalEnergy(int modeIndex, int amount) { if (modeIndex >= 0 && modeIndex < localLoadedEnergy.Length) localLoadedEnergy[modeIndex] = amount; }
    [TargetRpc] public void TargetUpdateReserveEnergy(int amount) { localReserveEnergy = amount; }

    [TargetRpc]
    private void TargetResetLocalEnergy()
    {
        if (wandModes.Length > 0 && wandModes[0] != null) localLoadedEnergy[0] = wandModes[0].maxLoadedEnergy;
        if (wandModes.Length > 1 && wandModes[1] != null) localLoadedEnergy[1] = wandModes[1].maxLoadedEnergy;
        if (wandModes.Length > 2 && wandModes[2] != null) localLoadedEnergy[2] = wandModes[2].maxLoadedEnergy;
    }

    [Command] private void CmdTriggerWag() { RpcTriggerWag(); }
    [ClientRpc(includeOwner = false)] private void RpcTriggerWag() { if (tpAnimator != null) tpAnimator.TriggerModeSwitchWag(); if (wandModes[syncedWandMode].switchModeSound != null) sfxSource.PlayOneShot(wandModes[syncedWandMode].switchModeSound); }
    [ClientRpc(includeOwner = false)] private void RpcPlayThrowSound() { AudioClip clip = wandModes[syncedWandMode].gravitySpell.throwSound; if (clip != null) sfxSource.PlayOneShot(clip); }
    [ClientRpc(includeOwner = false)] private void RpcPlayDropSound() { AudioClip clip = wandModes[syncedWandMode].gravitySpell.dropSound; if (clip != null) sfxSource.PlayOneShot(clip); }

    [Command]
    public void CmdApplyProjectileForce(NetworkIdentity target, Vector3 force)
    {
        if (target != null) { Rigidbody rb = target.GetComponent<Rigidbody>(); if (rb != null && !rb.isKinematic) rb.AddForce(force, ForceMode.Impulse); }
    }

    [Command]
    public void CmdApplyProjectileDamage(NetworkIdentity target, int damage, Vector3 hitDir)
    {
        if (target != null) { PlayerHealth health = target.GetComponent<PlayerHealth>(); if (health != null) { health.ServerTakeDamage(damage, hitDir); health.RpcApplyImpact(hitDir * wandModes[syncedWandMode].projectileForce); } }
    }

    [Command]
    private void CmdTryGrab(NetworkIdentity target, float dist, Quaternion rotOffset)
    {
        if (target != null && serverHeldObject == null)
        {
            serverHeldObject = target;
            serverGrabRotOffset = rotOffset;
            WandData data = wandModes[syncedWandMode];
            serverHoldDistance = Mathf.Clamp(dist, data.gravitySpell.minDistance, data.gravitySpell.maxDistance);
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null) { rb.useGravity = false; rb.linearDamping = 5f; rb.angularDamping = 5f; }
        }
    }

    [Command] private void CmdUpdateHoldDistance(float dist) { WandData data = wandModes[syncedWandMode]; serverHoldDistance = Mathf.Clamp(dist, data.gravitySpell.minDistance, data.gravitySpell.maxDistance); }
    [Command] private void CmdSetChargingState(bool charging) { serverIsChargingThrow = charging; }

    [Command]
    private void CmdThrow(float forceRatio)
    {
        if (serverHeldObject != null)
        {
            Rigidbody rb = serverHeldObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true; rb.linearDamping = 0f; rb.angularDamping = 0.05f;
                WandData data = wandModes[syncedWandMode];
                float force = data.gravitySpell.maxThrowForce * forceRatio;
                Vector3 simCamForward = serverAimForward != Vector3.zero ? serverAimForward : (Quaternion.Euler(syncedPitch, transform.eulerAngles.y, 0f) * Vector3.forward);
                rb.AddForce(simCamForward * force * rb.mass, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * (force * 0.5f), ForceMode.Impulse);
            }
            serverHeldObject = null; syncedChargeRatio = 0f; serverIsChargingThrow = false;
        }
        RpcPlayThrowSound();
    }

    [Command]
    private void CmdDrop()
    {
        if (serverHeldObject != null)
        {
            Rigidbody rb = serverHeldObject.GetComponent<Rigidbody>();
            if (rb != null) { rb.useGravity = true; rb.linearDamping = 0f; rb.angularDamping = 0.05f; }
            serverHeldObject = null; syncedChargeRatio = 0f; serverIsChargingThrow = false;
        }
        RpcPlayDropSound();
    }

    private void OnWandModeChanged(int oldMode, int newMode) { if (!isLocalPlayer && tpVisuals != null) { if (serverHeldObject == null) tpVisuals.DeactivateGrabVisuals(wandModes[newMode].gravitySpell.fadeTime); } }

    private void HandleAudioLoops()
    {
        if (wandModes.Length == 0) return;
        if (IsHoldingObject)
        {
            AudioClip holdClip = wandModes[CurrentModeIndex].gravitySpell.holdLoopSound;
            if (holdClip != null)
            {
                if (loopSource.clip != holdClip || !loopSource.isPlaying) { loopSource.clip = holdClip; loopSource.Play(); }
                loopSource.pitch = 1f + (NetworkChargeRatio * 1.5f);
            }
        }
        else if (loopSource.isPlaying) loopSource.Stop();
    }

    private void SetLayerRecursively(GameObject obj, int newLayer) { if (obj == null) return; obj.layer = newLayer; foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, newLayer); }
    private void HandleReloadTimer() { if (isReloading) { reloadTimer -= Time.deltaTime; if (reloadTimer <= 0f) isReloading = false; } }

    private void CancelHarvest()
    {
        isHarvesting = false;
        if (targetGrowthPoint != null) targetGrowthPoint.CmdCancelHarvest();
        targetGrowthPoint = null;
        CmdSetHarvestingState(false);
        if (tpAnimator != null) tpAnimator.CmdSyncHarvesting(false);
    }

    private void CancelHarvestAndDestroyNode()
    {
        isHarvesting = false;
        if (targetGrowthPoint != null)
        {
            targetGrowthPoint.CmdCancelHarvest();
            CmdDestroyGrowthPoint(targetGrowthPoint.gameObject);
        }
        targetGrowthPoint = null;
        CmdSetHarvestingState(false);
        if (tpAnimator != null) tpAnimator.CmdSyncHarvesting(false);
    }

    // THE FIX: High Visibility Colored Logging!
    public void OnMinigameComplete(bool success)
    {
        Debug.Log($"<color=cyan><b>[LOCAL UI]</b></color> Minigame Manager sent result: Success = {success}. Currently harvesting? {isHarvesting}");

        if (!isHarvesting) return;

        isHarvesting = false;

        if (targetGrowthPoint != null)
        {
            Debug.Log($"<color=cyan><b>[LOCAL UI]</b></color> Forwarding CmdResolveMinigame({success}) to Server Node: {targetGrowthPoint.gameObject.name}");
            targetGrowthPoint.CmdResolveMinigame(success);
        }
        else
        {
            Debug.LogError($"<color=red><b>[CRITICAL ERROR]</b></color> Target Growth Point was NULL when attempting to resolve!");
        }

        targetGrowthPoint = null;
        CmdSetHarvestingState(false);
        if (tpAnimator != null) tpAnimator.CmdSyncHarvesting(false);
    }

    private void HandleInput()
    {
        if (isReloading) return;
        WandData data = wandModes[currentModeIndex];

        if (isHarvesting)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            {
                if (MinigameManager.Instance != null && MinigameManager.Instance.IsPlaying)
                    MinigameManager.Instance.StopMinigame(false);
                else
                    CancelHarvestAndDestroyNode();
                return;
            }

            if (MinigameManager.Instance != null && MinigameManager.Instance.IsPlaying) return;

            RaycastHit hit;
            bool hitValid = Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, data.gravitySpell.grabRange, LayerMask.GetMask("Harvestable"), QueryTriggerInteraction.Collide);
            GrowthPoint gp = hitValid ? hit.collider.GetComponent<GrowthPoint>() : null;

            if (!Input.GetMouseButton(1) || gp != targetGrowthPoint)
            {
                CancelHarvest();
            }
            else if (targetGrowthPoint != null && targetGrowthPoint.currentData != null)
            {
                harvestTimer += Time.deltaTime;
                if (harvestTimer >= targetGrowthPoint.currentData.harvestTime)
                {
                    isHarvesting = false;

                    Debug.Log($"<color=cyan><b>[LOCAL UI]</b></color> Normal 'Hold' harvest completed! Forwarding success to Server.");
                    targetGrowthPoint.CmdResolveMinigame(true);

                    targetGrowthPoint = null;
                    CmdSetHarvestingState(false);
                    if (tpAnimator != null) tpAnimator.CmdSyncHarvesting(false);
                }
            }
            return;
        }

        if (!localIntendedGrabState)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(2);
        }

        isLookingAtGrabbable = false;

        if (!localIntendedGrabState)
        {
            RaycastHit triggerHit;
            if (Physics.Raycast(playerCamera.position, playerCamera.forward, out triggerHit, data.gravitySpell.grabRange, LayerMask.GetMask("Harvestable"), QueryTriggerInteraction.Collide))
            {
                GrowthPoint gp = triggerHit.collider.GetComponent<GrowthPoint>();
                if (gp != null && gp.isFullyGrown)
                {
                    isLookingAtGrabbable = true;
                    if (Input.GetMouseButtonDown(1))
                    {
                        isHarvesting = true;
                        targetGrowthPoint = gp;
                        harvestTimer = 0f;
                        CmdSetHarvestingState(true);
                        if (tpAnimator != null) tpAnimator.CmdSyncHarvesting(true);
                        gp.CmdStartHarvesting();

                        if (MinigameManager.Instance != null)
                            MinigameManager.Instance.StartMinigame(gp.currentData);

                        return;
                    }
                }
            }

            if (Physics.Raycast(playerCamera.position, playerCamera.forward, out RaycastHit hit, data.gravitySpell.grabRange, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag(data.gravitySpell.grabTag))
                {
                    isLookingAtGrabbable = true;
                    if (Input.GetMouseButtonDown(1))
                    {
                        NetworkIdentity netId = hit.collider.GetComponent<NetworkIdentity>();
                        if (netId != null) InitiateGrabLocal(netId, data.gravitySpell);
                    }
                }
            }
        }
        else
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0 && CurrentChargeRatio < 1f)
            {
                currentHoldDistance = Mathf.Clamp(currentHoldDistance + (scroll * data.gravitySpell.scrollSpeed), data.gravitySpell.minDistance, data.gravitySpell.maxDistance);
                CmdUpdateHoldDistance(currentHoldDistance);
            }

            if (Input.GetKeyDown(KeyCode.E)) { currentChargeTime = 0f; CmdSetChargingState(true); }
            if (Input.GetKey(KeyCode.E)) { currentChargeTime = Mathf.Clamp(currentChargeTime + Time.deltaTime, 0f, data.gravitySpell.maxChargeTime); }
            if (Input.GetKeyUp(KeyCode.E)) { ExecuteThrowLocal(data.gravitySpell.fadeTime); }
            if (Input.GetMouseButtonDown(1)) { ExecuteDropLocal(data.gravitySpell.fadeTime); }
        }

        if (!localIntendedGrabState)
        {
            if (Input.GetKeyDown(KeyCode.R) && localLoadedEnergy[currentModeIndex] < data.maxLoadedEnergy)
            {
                if (localReserveEnergy > 0) StartReloadLocal();
                else
                {
                    ManaHUD hud = Object.FindFirstObjectByType<ManaHUD>();
                    if (hud != null) hud.TriggerEmptyReserveWobble();
                }
            }

            bool fireInput = data.isAutomatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (fireInput && Time.time >= nextFireTime)
            {
                if (localLoadedEnergy[currentModeIndex] >= data.energyCostPerShot) AttemptShootLocal();
                else if (Input.GetMouseButtonDown(0)) AttemptEmptyShootLocal();
            }
        }
    }

    private void InitiateGrabLocal(NetworkIdentity netId, GravitySpellProperties props)
    {
        localIntendedGrabState = true;
        currentHoldDistance = Mathf.Clamp(Vector3.Distance(playerCamera.position, netId.transform.position), props.minDistance, props.maxDistance);
        Quaternion rotOffset = Quaternion.Inverse(playerCamera.rotation) * netId.transform.rotation;
        CmdTryGrab(netId, currentHoldDistance, rotOffset);
        CmdSyncAim(playerCamera.position, playerCamera.forward, playerCamera.right, playerCamera.up);

        if (fpVisuals != null) fpVisuals.ActivateGrabVisuals(props, fpFirePoint);

        GrabbedObjectAura aura = netId.gameObject.GetComponent<GrabbedObjectAura>();
        if (aura == null) aura = netId.gameObject.AddComponent<GrabbedObjectAura>();
        aura.Setup(props.grabbedAuraMaterial, props.grabbedObjectParticles, props.fadeTime, props.heldObjectAlpha, props.auraColorNormal, props.auraColorMaxCharge, this.gameObject);
        currentAura = aura;

        if (props.grabSound != null) sfxSource.PlayOneShot(props.grabSound);
    }

    private void ExecuteThrowLocal(float fadeTime)
    {
        localIntendedGrabState = false; CmdThrow(CurrentChargeRatio); ResetLocalGrabVisuals(fadeTime);
        AudioClip clip = wandModes[currentModeIndex].gravitySpell.throwSound; if (clip != null) sfxSource.PlayOneShot(clip);
    }

    private void ExecuteDropLocal(float fadeTime)
    {
        localIntendedGrabState = false; CmdDrop(); ResetLocalGrabVisuals(fadeTime);
        AudioClip clip = wandModes[currentModeIndex].gravitySpell.dropSound; if (clip != null) sfxSource.PlayOneShot(clip);
    }

    private void ResetLocalGrabVisuals(float fadeTime)
    {
        currentChargeTime = 0f; serverAimPos = Vector3.zero;
        if (currentAura != null) { currentAura.Release(); currentAura = null; }
        if (fpVisuals != null) fpVisuals.DeactivateGrabVisuals(fadeTime);
    }

    private void ServerCalculateGravityPhysics()
    {
        if (serverHeldObject == null) return;
        Rigidbody rb = serverHeldObject.GetComponent<Rigidbody>();
        if (rb == null) return;

        WandData data = wandModes[syncedWandMode];
        GravitySpellProperties props = data.gravitySpell;

        Vector3 simCamPos = serverAimPos != Vector3.zero ? serverAimPos : (headMount != null ? headMount.position : transform.position + (Vector3.up * 1.5f));
        Vector3 simCamForward = serverAimForward != Vector3.zero ? serverAimForward : (Quaternion.Euler(syncedPitch, transform.eulerAngles.y, 0f) * Vector3.forward);
        Vector3 simCamRight = serverAimRight != Vector3.zero ? serverAimRight : transform.right;
        Vector3 simCamUp = serverAimUp != Vector3.zero ? serverAimUp : transform.up;

        float enforcedDistance = Mathf.Clamp(serverHoldDistance, props.minDistance, props.maxDistance);
        Vector3 baseTargetPos = simCamPos + (simCamForward * enforcedDistance);
        if (serverIsChargingThrow) baseTargetPos = simCamPos + (simCamForward * props.chargeDistance);

        Vector3 shakeOffset = Vector3.zero;
        if (syncedChargeRatio > 0f)
        {
            serverShakePhase += Mathf.Lerp(props.minObjectShakeSpeed, props.maxObjectShakeSpeed, syncedChargeRatio) * Time.fixedDeltaTime;
            float shakeAmt = Mathf.Lerp(props.minObjectShakeAmount, props.maxObjectShakeAmount, syncedChargeRatio);
            shakeOffset = (simCamRight * Mathf.Sin(serverShakePhase) * shakeAmt) + (simCamUp * Mathf.Cos(serverShakePhase * 1.2f) * shakeAmt);
        }

        rb.AddForce(((baseTargetPos + shakeOffset) - rb.position) * props.pullStrength * rb.mass, ForceMode.Force);

        Quaternion simCamRot = serverAimForward != Vector3.zero ? Quaternion.LookRotation(serverAimForward, simCamUp) : Quaternion.Euler(syncedPitch, transform.eulerAngles.y, 0f);
        Quaternion rotError = (simCamRot * serverGrabRotOffset) * Quaternion.Inverse(rb.rotation);

        rotError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (angle != 0) rb.AddTorque(axis * (angle * Mathf.Deg2Rad * props.rotationStrength * rb.mass), ForceMode.Force);
    }

    private void AttemptShootLocal()
    {
        WandData data = wandModes[currentModeIndex];
        nextFireTime = Time.time + data.fireRate;

        localLoadedEnergy[currentModeIndex] = Mathf.Max(0, localLoadedEnergy[currentModeIndex] - data.energyCostPerShot);
        CmdDrainEnergy(currentModeIndex, data.energyCostPerShot);

        if (data.shootSound != null) sfxSource.PlayOneShot(data.shootSound);

        List<Collider> myColliders = new List<Collider>(transform.root.GetComponentsInChildren<Collider>());
        if (tpAnimator != null && tpAnimator.myRagdoll != null && tpAnimator.myRagdoll.Handler != null)
        {
            var dummies = tpAnimator.myRagdoll.Handler.User_GetAllDummyColliders();
            if (dummies != null) myColliders.AddRange(dummies);
        }

        Ray aimRay = new Ray(playerCamera.position, playerCamera.forward);
        Vector3 convergencePoint = aimRay.GetPoint(100f);

        RaycastHit[] hits = Physics.RaycastAll(aimRay, 200f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (!hit.collider.isTrigger && hit.collider.transform.root != this.transform.root && !(hit.collider is CharacterController))
            {
                Vector3 dirFromGun = (hit.point - fpFirePoint.position).normalized;
                if (Vector3.Dot(playerCamera.forward, dirFromGun) > 0.05f)
                {
                    convergencePoint = hit.point;
                    break;
                }
            }
        }

        int fireSeed = Random.Range(0, 100000);
        ExecuteLocalFire(data, convergencePoint, fireSeed);
        CmdFire(convergencePoint, fireSeed);

        if (tpAnimator != null) tpAnimator.TriggerShoot(currentModeIndex);
    }

    private void AttemptEmptyShootLocal()
    {
        WandData data = wandModes[currentModeIndex];
        nextFireTime = Time.time + data.fireRate;

        if (data.emptyAmmoSound != null) sfxSource.PlayOneShot(data.emptyAmmoSound);

        if (data.emptyAmmoClickPrefab != null)
        {
            GameObject flashObj = Instantiate(data.emptyAmmoClickPrefab, fpFirePoint.position, fpFirePoint.rotation, fpFirePoint);
            SetLayerRecursively(flashObj, fpFirePoint.gameObject.layer);
            Destroy(flashObj, 2f);
        }

        ManaHUD hud = Object.FindFirstObjectByType<ManaHUD>();
        if (hud != null) hud.TriggerEmptyFireWobble();

        if (tpAnimator != null) tpAnimator.TriggerShoot(0);
        CmdEmptyShoot();
    }

    private void ExecuteLocalFire(WandData data, Vector3 targetPoint, int seed)
    {
        if (fpVisuals != null) fpVisuals.ApplyShootRecoil(data.recoilPunch);
        if (crosshairManager != null) crosshairManager.ApplyRecoilPunch(data.crosshair.shootPunchAmount);

        if (data.muzzleFlashPrefab != null)
        {
            GameObject flashObj = Instantiate(data.muzzleFlashPrefab, fpFirePoint.position, fpFirePoint.rotation, fpFirePoint);
            SetLayerRecursively(flashObj, fpFirePoint.gameObject.layer);
            Destroy(flashObj, 2f);
        }

        Collider[] spawnedProjectiles = new Collider[data.shellCount];
        GameObject[] projectileObjects = new GameObject[data.shellCount];

        Random.InitState(seed);

        List<Collider> myColliders = new List<Collider>(transform.root.GetComponentsInChildren<Collider>());
        if (tpAnimator != null && tpAnimator.myRagdoll != null && tpAnimator.myRagdoll.Handler != null)
        {
            var dummies = tpAnimator.myRagdoll.Handler.User_GetAllDummyColliders();
            if (dummies != null) myColliders.AddRange(dummies);
        }

        Vector3 spawnPos = fpFirePoint.position;
        float distToTarget = Vector3.Distance(playerCamera.position, targetPoint);
        float distToBarrel = Vector3.Distance(playerCamera.position, fpFirePoint.position);

        if (distToTarget <= distToBarrel + 0.3f) spawnPos = playerCamera.position + (playerCamera.forward * 0.1f);
        Vector3 shootDir = (targetPoint - spawnPos).normalized;
        if (shootDir == Vector3.zero) shootDir = playerCamera.forward;

        for (int i = 0; i < data.shellCount; i++)
        {
            Quaternion rot = Quaternion.LookRotation(shootDir);
            float spread = data.spreadFactor + (playerMovement != null ? playerMovement.CurrentSpeed * 0.002f : 0f);
            if (spread > 0f) rot *= Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f);

            GameObject proj = Instantiate(data.projectilePrefab, spawnPos, rot);
            SetLayerRecursively(proj, fpFirePoint.gameObject.layer);

            projectileObjects[i] = proj;
            spawnedProjectiles[i] = proj.GetComponent<Collider>();
        }

        for (int i = 0; i < data.shellCount; i++)
        {
            for (int j = i + 1; j < data.shellCount; j++)
            {
                if (spawnedProjectiles[i] != null && spawnedProjectiles[j] != null) Physics.IgnoreCollision(spawnedProjectiles[i], spawnedProjectiles[j]);
            }
        }

        for (int i = 0; i < data.shellCount; i++)
        {
            if (projectileObjects[i] != null) projectileObjects[i].GetComponent<WandProjectile>().Setup(data.projectileSpeed, data.projectileForce, data.projectileDamage, data.impactPrefab, data.projectileHitLayers, myColliders.ToArray(), this);
        }
    }

    public void ExecuteRemoteFire(Vector3 targetPoint, int seed)
    {
        WandData data = wandModes[CurrentModeIndex];
        if (data.shootSound != null) sfxSource.PlayOneShot(data.shootSound);
        if (tpAnimator != null) tpAnimator.TriggerShoot(CurrentModeIndex);
        if (tpVisuals != null && data.muzzleFlashPrefab != null) tpVisuals.PlayMuzzleFlash(data.muzzleFlashPrefab);

        Random.InitState(seed);

        List<Collider> myColliders = new List<Collider>(transform.root.GetComponentsInChildren<Collider>());
        if (tpAnimator != null && tpAnimator.myRagdoll != null && tpAnimator.myRagdoll.Handler != null)
        {
            var dummies = tpAnimator.myRagdoll.Handler.User_GetAllDummyColliders();
            if (dummies != null) myColliders.AddRange(dummies);
        }

        Collider[] spawnedDummies = new Collider[data.shellCount];
        GameObject[] dummyObjects = new GameObject[data.shellCount];

        Vector3 spawnPos = tpFirePoint.position;
        if (headMount != null)
        {
            float distToTarget = Vector3.Distance(headMount.position, targetPoint);
            float distToBarrel = Vector3.Distance(headMount.position, tpFirePoint.position);
            if (distToTarget <= distToBarrel + 0.3f) spawnPos = headMount.position + (headMount.forward * 0.1f);
        }

        Vector3 shootDir = (targetPoint - spawnPos).normalized;
        if (shootDir == Vector3.zero && headMount != null) shootDir = headMount.forward;

        for (int i = 0; i < data.shellCount; i++)
        {
            Quaternion rot = Quaternion.LookRotation(shootDir);
            if (data.spreadFactor > 0f) rot *= Quaternion.Euler(Random.Range(-data.spreadFactor, data.spreadFactor), Random.Range(-data.spreadFactor, data.spreadFactor), 0f);

            GameObject dummyProj = Instantiate(data.projectilePrefab, spawnPos, rot);
            SetLayerRecursively(dummyProj, tpFirePoint.gameObject.layer);

            dummyObjects[i] = dummyProj;
            spawnedDummies[i] = dummyProj.GetComponent<Collider>();
        }

        for (int i = 0; i < data.shellCount; i++)
        {
            for (int j = i + 1; j < data.shellCount; j++)
            {
                if (spawnedDummies[i] != null && spawnedDummies[j] != null) Physics.IgnoreCollision(spawnedDummies[i], spawnedDummies[j]);
            }
        }

        for (int i = 0; i < data.shellCount; i++)
        {
            if (dummyObjects[i] != null) dummyObjects[i].GetComponent<WandProjectile>().SetupDummy(data.projectileSpeed, data.impactPrefab, myColliders.ToArray());
        }
    }

    public void ExecuteRemoteEmptyShoot()
    {
        WandData data = wandModes[CurrentModeIndex];
        if (data.emptyAmmoSound != null) sfxSource.PlayOneShot(data.emptyAmmoSound);
        if (tpAnimator != null) tpAnimator.TriggerShoot(0);
        if (tpVisuals != null && data.emptyAmmoClickPrefab != null) tpVisuals.PlayMuzzleFlash(data.emptyAmmoClickPrefab);
    }

    private void StartReloadLocal()
    {
        isReloading = true;
        reloadTimer = wandModes[currentModeIndex].reloadTime;
        WandData data = wandModes[currentModeIndex];

        if (data.reloadSound != null) sfxSource.PlayOneShot(data.reloadSound);
        if (data.reloadEffectPrefab != null)
        {
            GameObject reloadObj = Instantiate(data.reloadEffectPrefab, fpFirePoint.position, fpFirePoint.rotation, fpFirePoint);
            SetLayerRecursively(reloadObj, fpFirePoint.gameObject.layer);
            Destroy(reloadObj, 2f);
        }

        int current = localLoadedEnergy[currentModeIndex];
        int needed = data.maxLoadedEnergy - current;
        if (needed > 0 && localReserveEnergy > 0)
        {
            int transferAmount = Mathf.Min(needed, localReserveEnergy);

            int finalReserve = localReserveEnergy - transferAmount;
            int finalLoaded = current + transferAmount;

            localReserveEnergy = finalReserve;
            localLoadedEnergy[currentModeIndex] = finalLoaded;

            ManaHUD hud = Object.FindFirstObjectByType<ManaHUD>();
            if (hud != null)
            {
                float targetLoadedRatio = (float)finalLoaded / data.maxLoadedEnergy;
                float targetReserveRatio = (float)finalReserve / maxReserveEnergy;
                hud.TriggerReloadVisuals(data.reloadTime, targetLoadedRatio, targetReserveRatio, finalLoaded, finalReserve);
            }
        }

        CmdReloadAndTransferEnergy(currentModeIndex);
        if (tpAnimator != null) tpAnimator.TriggerReload();
    }

    public void TriggerRemoteReload()
    {
        if (wandModes[CurrentModeIndex].reloadSound != null) sfxSource.PlayOneShot(wandModes[CurrentModeIndex].reloadSound);
        if (tpAnimator != null) tpAnimator.TriggerReload();
        if (tpVisuals != null && wandModes[CurrentModeIndex].reloadEffectPrefab != null) tpVisuals.PlayReloadEffect(wandModes[CurrentModeIndex].reloadEffectPrefab);
    }

    private void SwitchMode(int index)
    {
        if (currentModeIndex == index) return;
        currentModeIndex = index;

        if (wandModes[currentModeIndex].switchModeSound != null) sfxSource.PlayOneShot(wandModes[currentModeIndex].switchModeSound);
        if (fpVisuals != null) fpVisuals.TriggerModeSwitchWiggle();
        if (tpAnimator != null) tpAnimator.TriggerModeSwitchWag();

        CmdSetWandMode(index);
        CmdTriggerWag();
    }

    private void UpdateCrosshairState()
    {
        if (crosshairManager == null || !isLocalPlayer) return;
        WandData data = wandModes[currentModeIndex];
        float movePenalty = playerMovement != null ? playerMovement.CurrentSpeed * data.crosshair.movementSpreadMultiplier : 0;
        if (playerMovement != null && playerMovement.CurrentState == MovementState.Airborne) movePenalty += 30f;

        bool spinning = localIntendedGrabState || isHarvesting;
        float charge = isHarvesting ? 1f : CurrentChargeRatio;

        Color color = data.crosshair.normalColor;
        float spread = data.crosshair.baseSpread;

        if (spinning)
        {
            color = isHarvesting ? data.harvestSettings.crosshairColor : Color.Lerp(data.gravitySpell.grabbedColor, data.gravitySpell.chargeColor, charge);
            spread = Mathf.Lerp(data.gravitySpell.grabbedCrosshairSpread, data.gravitySpell.chargeCrosshairSpread, charge);
            movePenalty = 0f;
        }
        else if (isLookingAtGrabbable) color = data.gravitySpell.hoverColor;

        crosshairManager.UpdateCrosshairState(spread, movePenalty, color, spinning, charge);
    }

    private void UpdateVisuals()
    {
        WandData data = wandModes[CurrentModeIndex];
        float charge = NetworkChargeRatio;

        if (playerHealth != null && playerHealth.isDead)
        {
            if (tpVisuals != null) tpVisuals.UpdateVisualState(0, 1, Color.black, 0f, false, 0f, false, false, 0f, Color.black, Color.black, data.harvestSettings);
            return;
        }

        bool currentlyHarvesting = isLocalPlayer ? isHarvesting : syncedIsHarvesting;

        if (currentlyHarvesting)
        {
            if (!wasHarvestingFlag)
            {
                if (isLocalPlayer && fpVisuals != null) fpVisuals.ActivateHarvestVisuals(data.harvestSettings.pullingParticlePrefab, fpFirePoint);
                if (!isLocalPlayer && tpVisuals != null) tpVisuals.ActivateHarvestVisuals(data.harvestSettings.pullingParticlePrefab, tpFirePoint);
                wasHarvestingFlag = true;
            }
        }
        else
        {
            if (wasHarvestingFlag)
            {
                if (isLocalPlayer && fpVisuals != null) fpVisuals.DeactivateHarvestVisuals();
                if (!isLocalPlayer && tpVisuals != null) tpVisuals.DeactivateHarvestVisuals();
                wasHarvestingFlag = false;
            }
        }

        if (isLocalPlayer)
        {
            if (currentAura != null) currentAura.UpdateAura(charge);
            float prog = isReloading ? 1f - (reloadTimer / data.reloadTime) : 0f;
            float mass = 0f;
            if (serverHeldObject != null) { Rigidbody rb = serverHeldObject.GetComponent<Rigidbody>(); if (rb != null) mass = rb.mass; }

            int loadedLocal = localLoadedEnergy[currentModeIndex];

            if (fpVisuals != null)
            {
                fpVisuals.UpdateVisualState(
                    loadedLocal,
                    data.maxLoadedEnergy,
                    data.modeEmissionColor,
                    data.maxEmissionIntensity,
                    isReloading,
                    prog,
                    localIntendedGrabState,
                    isHarvesting,
                    mass,
                    isHarvesting ? 1f : charge,
                    data.gravitySpell,
                    data.harvestSettings
                );
            }
        }
        else
        {
            if (serverHeldObject != null)
            {
                if (lastSeenServerObject != serverHeldObject)
                {
                    if (lastSeenServerObject != null) { GrabbedObjectAura oldAura = lastSeenServerObject.GetComponent<GrabbedObjectAura>(); if (oldAura != null) oldAura.Release(); }
                    GrabbedObjectAura aura = serverHeldObject.GetComponent<GrabbedObjectAura>();
                    if (aura == null) aura = serverHeldObject.gameObject.AddComponent<GrabbedObjectAura>();
                    aura.Setup(data.gravitySpell.grabbedAuraMaterial, data.gravitySpell.grabbedObjectParticles, data.gravitySpell.fadeTime, data.gravitySpell.heldObjectAlpha, data.gravitySpell.auraColorNormal, data.gravitySpell.auraColorMaxCharge, this.gameObject);

                    if (tpVisuals != null) tpVisuals.ActivateGrabVisuals(data.gravitySpell, tpFirePoint);
                    if (data.gravitySpell.grabSound != null) sfxSource.PlayOneShot(data.gravitySpell.grabSound);
                    lastSeenServerObject = serverHeldObject;
                }
                GrabbedObjectAura activeAura = serverHeldObject.GetComponent<GrabbedObjectAura>();
                if (activeAura != null) activeAura.UpdateAura(charge);
            }
            else if (lastSeenServerObject != null)
            {
                GrabbedObjectAura aura = lastSeenServerObject.GetComponent<GrabbedObjectAura>();
                if (aura != null) aura.Release();
                if (tpVisuals != null) tpVisuals.DeactivateGrabVisuals(data.gravitySpell.fadeTime);
                lastSeenServerObject = null;
            }
        }

        if (tpVisuals != null)
        {
            float prog = isLocalPlayer ? (isReloading ? 1f - (reloadTimer / data.reloadTime) : 0f) : 0f;
            int loadedRemote = GetLoadedEnergy(CurrentModeIndex);
            int currentVisEnergy = isLocalPlayer ? localLoadedEnergy[CurrentModeIndex] : loadedRemote;

            tpVisuals.UpdateVisualState(
                currentVisEnergy,
                data.maxLoadedEnergy,
                data.modeEmissionColor,
                data.maxEmissionIntensity,
                false,
                prog,
                syncedIsGrabbing,
                syncedIsHarvesting,
                syncedIsHarvesting ? 1f : charge,
                data.gravitySpell.gravityEmissionColor,
                data.gravitySpell.maxChargeEmissionColor,
                data.harvestSettings
            );
        }
    }

    void OnGUI()
    {
        if (!isLocalPlayer || !showDebug) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;

        style.normal.textColor = Color.cyan;
        int loaded = localLoadedEnergy[currentModeIndex];
        int max = wandModes[currentModeIndex] != null ? wandModes[currentModeIndex].maxLoadedEnergy : 0;
        GUI.Label(new Rect(20, Screen.height - 80, 400, 30), $"Wand Energy: {loaded} / {max}", style);

        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(20, Screen.height - 50, 400, 30), $"Reserve Mana: {localReserveEnergy} / {maxReserveEnergy}", style);
    }
}