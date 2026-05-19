using UnityEngine;

[System.Serializable]
public struct CrosshairSettings
{
    public Color normalColor;
    public float baseSpread;
    public float shootPunchAmount;
    public float movementSpreadMultiplier;
}

[System.Serializable]
public struct GravitySpellProperties
{
    [Header("Physics Settings")]
    public string grabTag;
    public float grabRange;
    public float maxThrowForce;
    public float maxChargeTime;
    public float chargeDistance;
    public float pullStrength;
    public float rotationStrength;
    public float minDistance;
    public float maxDistance;
    public float scrollSpeed;

    [Header("FP Wand Wobble Settings (Visual)")]
    public float minMassLevel;
    public float maxMassLevel;
    public float minMassWiggle;
    public float maxMassWiggle;
    public float minChargeShake;
    public float maxChargeShake;

    [Header("Grabbed Object Shake Settings (Physics)")]
    public float minObjectShakeAmount;
    public float maxObjectShakeAmount;
    public float minObjectShakeSpeed;
    public float maxObjectShakeSpeed;

    [Header("TP Arm Orbit Wobble Settings (Visual)")]
    public float minArmWobbleAmount;
    public float maxArmWobbleAmount;
    public float minArmWobbleSpeed;
    public float maxArmWobbleSpeed;

    [Header("Aura & Visual Settings")]
    public Material grabbedAuraMaterial;
    public GameObject grabbedObjectParticles;
    public GameObject muzzleHoldParticles;
    public float fadeTime;
    public float heldObjectAlpha;
    public Color auraColorNormal;
    public Color auraColorMaxCharge;
    public Color gravityEmissionColor;
    public Color maxChargeEmissionColor;
    public Color grabbedColor;
    public Color chargeColor;
    public Color hoverColor;
    public float grabbedCrosshairSpread;
    public float chargeCrosshairSpread;

    [Header("Audio Settings")]
    public AudioClip grabSound;
    public AudioClip throwSound;
    public AudioClip dropSound;
    public AudioClip holdLoopSound;
}

// THE FIX: Added a dedicated struct for Harvesting Aesthetics
[System.Serializable]
public struct HarvestSettings
{
    public Color wandEmissionColor;
    public Color crosshairColor;
    public GameObject pullingParticlePrefab;
}

[CreateAssetMenu(fileName = "NewWandData", menuName = "Wand/Wand Data")]
public class WandData : ScriptableObject
{
    [Header("Unified Energy & Combat Stats")]
    public bool isAutomatic = false;
    public int maxLoadedEnergy = 30;
    public int energyCostPerShot = 1;
    public int projectileDamage = 15;
    public float fireRate = 0.1f;
    public float reloadTime = 2.0f;

    [Header("Projectile Stats")]
    public float projectileSpeed = 50f;
    public float projectileForce = 15f;
    public LayerMask projectileHitLayers = ~0;
    public int shellCount = 1;
    public float spreadFactor = 0.05f;

    [Header("Standard Visual Properties")]
    public Color modeEmissionColor = Color.cyan;
    public float maxEmissionIntensity = 5f;
    public float recoilPunch = 15f;

    [Header("Crosshair Settings")]
    public CrosshairSettings crosshair;

    [Header("Particle Prefabs")]
    public GameObject projectilePrefab;
    public GameObject impactPrefab;
    public GameObject muzzleFlashPrefab;
    public GameObject emptyAmmoClickPrefab;
    public GameObject reloadEffectPrefab;

    [Header("Mode Audio Settings")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptyAmmoSound;
    public AudioClip switchModeSound;

    [Header("Gravity Spell Properties")]
    public GravitySpellProperties gravitySpell;

    // THE FIX: Adding it to the main data file
    [Header("Harvesting Settings")]
    public HarvestSettings harvestSettings;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(gravitySpell.grabTag)) gravitySpell.grabTag = "Grabbable";
    }
}