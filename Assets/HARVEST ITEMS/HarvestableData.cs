using UnityEngine;

public enum EmissionMode { Constant, Wave }
public enum MinigameType { None, Balance, Focus }
public enum FailurePenalty { DestroyNode, HalfYield }

[CreateAssetMenu(fileName = "NewHarvestableData", menuName = "Harvestables/Harvestable Data")]
public class HarvestableData : ScriptableObject
{
    [Header("Yields")]
    public int manaYield;
    public int healthYield;

    [Header("Timers & Chances")]
    public float growthTime;
    public float harvestTime;
    [Range(0f, 1f)]
    public float overgrowProbability;

    [Header("Hazard / AOE Settings")]
    public bool explodeOnFail = false;
    public bool explodeOnShoot = false;
    public float explosionRadius = 5f;
    [Tooltip("How long the physical HazardArea gas cloud object remains active in the world.")]
    public float hazardAreaDuration = 6f;
    [Tooltip("Immediate raw damage dealt to a player the exact moment the explosion touches them.")]
    public int immediateHazardDamage = 10;
    [Tooltip("The actual ScriptableObject status effect applied to players standing inside the cloud (has its own duration).")]
    public StatusEffectData hazardDebuff;

    [Header("Minigame General")]
    public MinigameType minigameType = MinigameType.None;
    public FailurePenalty failurePenalty = FailurePenalty.DestroyNode;
    public float minigameProgressFillRate = 0.4f;
    public float minigameProgressDrainRate = 0.2f;
    public AudioClip minigameSuccessSound;
    public AudioClip minigameFailureSound;

    [Header("Balance Minigame (Stardew)")]
    public float balanceTargetSpeed = 50f;
    public float balanceKnobThrust = 1000f;
    public float balanceKnobGravity = 800f;
    public float balanceTargetSizeMultiplier = 1f;

    [Header("Focus Minigame (Pandemonium)")]
    public float focusPlayAreaRadius = 400f;
    public float focusMercyFillRate = 0.15f;
    public float focusResistancePower = 150f;
    public float focusShakeLimit = 15f;
    public float focusShakeSpeed = 25f;
    public float focusFlingPower = 1500f;
    public float focusFlingMinInterval = 1f;
    public float focusFlingMaxInterval = 3f;

    [Header("Visuals (Must be a Prefab)")]
    public GameObject modelPrefab;
    public bool isEmissive;
    public EmissionMode emissionMode = EmissionMode.Constant;
    public float minEmission;
    public float maxEmission;
    public float waveSpeed;

    [Header("Particles")]
    public GameObject grownParticle;
    public GameObject harvestingParticle;
    public GameObject destroyedParticle;
    public GameObject absorbedParticle;

    [Header("Audio")]
    public AudioClip grownSound;
    public AudioClip destroyedSound;
    public AudioClip harvestingLoopSound;
    public AudioClip harvestedSound;
}