using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class StatusEffectManager : NetworkBehaviour
{
    [Header("Component Links")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WandController wandController;
    [SerializeField] public StatusEffectUIManager localUIManager;

    [Header("Database")]
    public StatusEffectData[] effectDatabase;

    [SyncVar] public bool isStunned = false;
    [SyncVar] public float currentSpeedMultiplier = 1f;
    [SyncVar] public bool isHealingDisabled = false;

    [SyncVar] public bool hasHealthColorOverride = false;
    [SyncVar] public Color healthColorOverride = Color.white;

    [SyncVar] public bool hasManaColorOverride = false;
    [SyncVar] public Color manaColorOverride = Color.white;

    [SyncVar(hook = nameof(OnVisualsChanged))]
    public int activeVisualDataIndex = -1;

    [SyncVar] public int activeVolumeProfileIndex = -1;

    private class ActiveEffect
    {
        public StatusEffectData data;
        public float timeRemaining;
        public float tickTimer;
        public uint instanceId;
    }

    private List<ActiveEffect> activeEffects = new List<ActiveEffect>();
    private Dictionary<string, float> immunityTimers = new Dictionary<string, float>();
    private uint nextEffectId = 0;
    private float lastStunFeedbackTime;

    void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (wandController == null) wandController = GetComponent<WandController>();
    }

    void Start()
    {
        if (isLocalPlayer && localUIManager == null)
        {
            localUIManager = Object.FindAnyObjectByType<StatusEffectUIManager>();
            if (localUIManager == null) Debug.LogError("<color=red><b>[UI ERROR]</b></color> StatusEffectUIManager not found in scene, UI will not work!");
        }
    }

    public void TriggerStunFeedback()
    {
        if (!isLocalPlayer || localUIManager == null) return;
        if (Time.time - lastStunFeedbackTime < 0.6f) return;

        lastStunFeedbackTime = Time.time;

        foreach (var e in activeEffects)
        {
            if (e.data.isStun)
            {
                localUIManager.TriggerStunFeedback(e.data.actionBlockedSound, e.data.stunFlashColor);
                break;
            }
        }
    }

    [Server]
    public void ApplyEffect(StatusEffectData effect)
    {
        if (playerHealth != null && playerHealth.isDead) return;

        if (immunityTimers.ContainsKey(effect.effectName) && immunityTimers[effect.effectName] > 0f) return;
        if (effect.immunityDuration > 0f) immunityTimers[effect.effectName] = effect.immunityDuration;

        if (effect.instantDamage > 0 && playerHealth != null) playerHealth.ServerTakeDamage(effect.instantDamage, Vector3.zero);
        if (effect.instantHeal > 0 && playerHealth != null && !isHealingDisabled) playerHealth.currentHealth = Mathf.Min(playerHealth.currentHealth + effect.instantHeal, playerHealth.maxHealth);

        if (effect.baseDuration <= 0f) return;

        ActiveEffect newEffect = new ActiveEffect
        {
            data = effect,
            timeRemaining = effect.baseDuration,
            tickTimer = 0f,
            instanceId = ++nextEffectId
        };

        activeEffects.Add(newEffect);
        CalculateModifiers();

        int index = System.Array.IndexOf(effectDatabase, effect);
        if (index != -1) TargetAddUIEffect(connectionToClient, index, effect.baseDuration, newEffect.instanceId);
    }

    [Server]
    private void CalculateModifiers()
    {
        bool stun = false; bool noHeal = false; float speed = 1f;
        bool overrideHealth = false; Color hColor = Color.white;
        bool overrideMana = false; Color mColor = Color.white;

        int visualIndex = -1;
        int volumeIndex = -1;

        foreach (var e in activeEffects)
        {
            if (e.data.isStun) stun = true;
            if (e.data.disableHealing) noHeal = true;
            if (e.data.speedMultiplier != 1f) speed *= e.data.speedMultiplier;

            if (e.data.overrideHealthBarColor) { overrideHealth = true; hColor = e.data.newHealthBarColor; }
            if (e.data.overrideManaBarColor) { overrideMana = true; mColor = e.data.newManaBarColor; }

            if (e.data.customScreenMaterial != null || e.data.customUIOverlaySprite != null)
                visualIndex = System.Array.IndexOf(effectDatabase, e.data);

            if (e.data.postProcessVolume != null)
                volumeIndex = System.Array.IndexOf(effectDatabase, e.data);
        }

        isStunned = stun; isHealingDisabled = noHeal; currentSpeedMultiplier = speed;

        hasHealthColorOverride = overrideHealth; healthColorOverride = hColor;
        hasManaColorOverride = overrideMana; manaColorOverride = mColor;

        activeVisualDataIndex = visualIndex;
        activeVolumeProfileIndex = volumeIndex;
    }

    void FixedUpdate() { if (isServer) ServerTick(); }

    [Server]
    private void ServerTick()
    {
        List<string> keys = new List<string>(immunityTimers.Keys);
        foreach (string key in keys)
        {
            immunityTimers[key] -= Time.fixedDeltaTime;
            if (immunityTimers[key] <= 0f) immunityTimers.Remove(key);
        }

        if (activeEffects.Count == 0 || (playerHealth != null && playerHealth.isDead))
        {
            if (activeEffects.Count > 0) ClearAllEffects();
            return;
        }

        bool modifiersChanged = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var e = activeEffects[i];
            e.timeRemaining -= Time.fixedDeltaTime;
            e.tickTimer += Time.fixedDeltaTime;

            if (e.tickTimer >= 1f)
            {
                e.tickTimer -= 1f;
                if (e.data.poisonDamagePerSecond > 0 && playerHealth != null) playerHealth.ServerTakeDamage(e.data.poisonDamagePerSecond, Vector3.zero);
                if (e.data.healPerSecond > 0 && playerHealth != null && !isHealingDisabled) playerHealth.currentHealth = Mathf.Min(playerHealth.currentHealth + e.data.healPerSecond, playerHealth.maxHealth);
                if (e.data.manaDrainPerSecond > 0 && wandController != null && wandController.currentReserveEnergy > 0)
                {
                    int newMana = Mathf.Max(0, wandController.currentReserveEnergy - e.data.manaDrainPerSecond);
                    wandController.currentReserveEnergy = newMana; wandController.TargetUpdateReserveEnergy(newMana);
                }
                if (e.data.manaRegenPerSecond > 0 && wandController != null)
                {
                    int newMana = Mathf.Min(wandController.maxReserveEnergy, wandController.currentReserveEnergy + e.data.manaRegenPerSecond);
                    wandController.currentReserveEnergy = newMana; wandController.TargetUpdateReserveEnergy(newMana);
                }
            }

            if (e.timeRemaining <= 0f)
            {
                int index = System.Array.IndexOf(effectDatabase, e.data);
                TargetRemoveUIEffect(connectionToClient, index, e.instanceId);
                activeEffects.RemoveAt(i);
                modifiersChanged = true;
            }
        }
        if (modifiersChanged) CalculateModifiers();
    }

    [Server]
    public void ClearAllEffects()
    {
        foreach (var e in activeEffects)
        {
            int index = System.Array.IndexOf(effectDatabase, e.data);
            TargetRemoveUIEffect(connectionToClient, index, e.instanceId);
        }
        activeEffects.Clear();
        CalculateModifiers();
    }

    [TargetRpc]
    private void TargetAddUIEffect(NetworkConnectionToClient target, int effectIndex, float duration, uint id)
    {
        if (effectIndex < 0 || effectIndex >= effectDatabase.Length || localUIManager == null) return;
        localUIManager.AddEffect(effectDatabase[effectIndex], duration, id);
    }

    [TargetRpc]
    private void TargetRemoveUIEffect(NetworkConnectionToClient target, int effectIndex, uint id)
    {
        if (effectIndex < 0 || effectIndex >= effectDatabase.Length || localUIManager == null) return;
        localUIManager.RemoveEffect(effectDatabase[effectIndex], id);
    }

    private void OnVisualsChanged(int oldIndex, int newIndex)
    {
        if (!isLocalPlayer || localUIManager == null) return;

        if (newIndex >= 0 && newIndex < effectDatabase.Length)
        {
            StatusEffectData data = effectDatabase[newIndex];
            localUIManager.ApplyOverlays(data.customScreenMaterial, data.customUIOverlaySprite);
        }
        else
        {
            localUIManager.ApplyOverlays(null, null);
        }
    }
}