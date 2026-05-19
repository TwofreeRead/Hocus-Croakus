using UnityEngine;
using System.Collections;

public class FPWandVisuals : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Transform wandModel;
    [SerializeField] private Transform recoilPivot;
    [SerializeField] private MeshRenderer wandRenderer;
    [SerializeField] private Camera fpCamera;

    [Header("Emission & Motion")]
    [SerializeField] private float colorLerpSpeed = 8f;
    [SerializeField] private float rotationalReturnSpeed = 15f;
    [SerializeField] private float switchWiggleRollAmount = 15f;
    [SerializeField] private float reloadShakeIntensity = 2f;
    [SerializeField] private float reloadShakeSpeed = 30f;
    [SerializeField] private float jitterSpeed = 25f;
    [Tooltip("Converts your data wobble amounts into physical rotation angles.")]
    [SerializeField] private float wobbleRotationMultiplier = 35f;

    private ParticleSystem activeMuzzleGrabParticles;
    // THE FIX: Tracker for the pulling particle
    private ParticleSystem activeHarvestParticles;

    private Material wandMat;
    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    private int currentAmmo = 1;
    private int maxAmmo = 1;
    private Color targetColor = Color.black;
    private float targetIntensity = 0f;

    private bool isReloading = false;
    private bool isHolding = false;
    private float currentJitterAmount = 0f;
    private float currentChargeShakeAmount = 0f;

    private Quaternion targetRotOffset = Quaternion.identity;
    private Quaternion currentRotOffset = Quaternion.identity;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (wandRenderer != null) wandRenderer.sharedMaterial.EnableKeyword("_EMISSION");
        if (wandModel != null)
        {
            initialLocalPosition = wandModel.localPosition;
            initialLocalRotation = wandModel.localRotation;
        }
    }

    // THE FIX: Signature updated to accept HarvestSettings
    public void UpdateVisualState(int ammo, int max, Color modeColor, float maxInt, bool reloading, float progress, bool holding, bool harvesting, float mass, float charge, GravitySpellProperties gravProps, HarvestSettings harvestProps)
    {
        currentAmmo = ammo;
        maxAmmo = max;
        isReloading = reloading;
        isHolding = holding || harvesting;

        targetColor = modeColor;
        targetIntensity = ammo > 0 ? maxInt * ((float)ammo / max) : 0f;

        if (harvesting)
        {
            // Fakes the max charge wobble, but specifically applies your Custom Harvesting Color and intensity
            currentChargeShakeAmount = Mathf.Lerp(gravProps.minChargeShake, gravProps.maxChargeShake, 1f);
            targetColor = harvestProps.wandEmissionColor;
            targetIntensity = maxInt + 3f;
        }
        else if (holding)
        {
            float wMass = Mathf.Clamp(mass, gravProps.minMassLevel, gravProps.maxMassLevel);
            float p = Mathf.InverseLerp(gravProps.minMassLevel, gravProps.maxMassLevel, wMass);
            currentJitterAmount = Mathf.Lerp(gravProps.minMassWiggle, gravProps.maxMassWiggle, p);
            currentChargeShakeAmount = Mathf.Lerp(gravProps.minChargeShake, gravProps.maxChargeShake, charge);

            targetColor = Color.Lerp(gravProps.gravityEmissionColor, gravProps.maxChargeEmissionColor, charge);
            targetIntensity = maxInt + (charge * 3f);
        }
        else
        {
            currentJitterAmount = 0f;
            currentChargeShakeAmount = 0f;

            if (reloading)
            {
                float baseEmptyIntensity = maxInt * 0.1f;
                targetIntensity = Mathf.Lerp(baseEmptyIntensity, maxInt, progress);
            }
        }

        UpdateMaterial();
    }

    private void UpdateMaterial()
    {
        if (wandRenderer == null) return;
        wandRenderer.GetPropertyBlock(propBlock);

        Color currentColor = propBlock.HasProperty(EmissionColorProp) ? propBlock.GetColor(EmissionColorProp) : Color.black;
        Color lerpedColor = Color.Lerp(currentColor, targetColor * targetIntensity, colorLerpSpeed * Time.deltaTime);

        propBlock.SetColor(EmissionColorProp, lerpedColor);
        wandRenderer.SetPropertyBlock(propBlock);
    }

    public void TriggerModeSwitchWiggle()
    {
        targetRotOffset *= Quaternion.Euler(0, 0, switchWiggleRollAmount);
    }

    public void ApplyShootRecoil(float punchAmount)
    {
        if (recoilPivot != null) recoilPivot.localRotation *= Quaternion.Euler(-punchAmount, 0, 0);
    }

    public void ActivateGrabVisuals(GravitySpellProperties props, Transform firePoint)
    {
        if (props.muzzleHoldParticles != null && activeMuzzleGrabParticles == null)
        {
            GameObject obj = Instantiate(props.muzzleHoldParticles, firePoint.position, firePoint.rotation, firePoint);
            activeMuzzleGrabParticles = obj.GetComponent<ParticleSystem>();
        }
    }

    public void DeactivateGrabVisuals(float fadeTime)
    {
        if (activeMuzzleGrabParticles != null)
        {
            activeMuzzleGrabParticles.Stop();
            Destroy(activeMuzzleGrabParticles.gameObject, 2f);
            activeMuzzleGrabParticles = null;
        }
    }

    // THE FIX: Start playing the pulling particles locally
    public void ActivateHarvestVisuals(GameObject prefab, Transform firePoint)
    {
        if (prefab != null && activeHarvestParticles == null)
        {
            GameObject obj = Instantiate(prefab, firePoint.position, firePoint.rotation, firePoint);
            activeHarvestParticles = obj.GetComponent<ParticleSystem>();
        }
    }

    // THE FIX: Stop playing the pulling particles
    public void DeactivateHarvestVisuals()
    {
        if (activeHarvestParticles != null)
        {
            activeHarvestParticles.Stop();
            Destroy(activeHarvestParticles.gameObject, 2f);
            activeHarvestParticles = null;
        }
    }

    void Update()
    {
        if (wandModel == null) return;

        targetRotOffset = Quaternion.Slerp(targetRotOffset, Quaternion.identity, rotationalReturnSpeed * Time.deltaTime);
        currentRotOffset = Quaternion.Slerp(currentRotOffset, targetRotOffset, rotationalReturnSpeed * Time.deltaTime);

        Quaternion shakeRot = Quaternion.identity;
        Quaternion massWobbleRot = Quaternion.identity;

        if (isReloading)
        {
            float reloadNoiseP = (Mathf.PerlinNoise(Time.time * reloadShakeSpeed, 100) - 0.5f) * 2f;
            float reloadNoiseY = (Mathf.PerlinNoise(100, Time.time * reloadShakeSpeed) - 0.5f) * 2f;
            shakeRot = Quaternion.Euler(reloadNoiseP * reloadShakeIntensity, reloadNoiseY * reloadShakeIntensity, 0);
        }

        if (isHolding)
        {
            float combinedShake = currentJitterAmount + currentChargeShakeAmount;
            float noiseX = (Mathf.PerlinNoise(Time.time * jitterSpeed, 0) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0, Time.time * jitterSpeed) - 0.5f) * 2f;
            massWobbleRot = Quaternion.Euler(noiseX * combinedShake * wobbleRotationMultiplier, noiseY * combinedShake * wobbleRotationMultiplier, 0);
        }

        Quaternion finalRotation = initialLocalRotation * currentRotOffset * shakeRot * massWobbleRot;
        wandModel.localRotation = finalRotation;

        if (recoilPivot != null)
        {
            recoilPivot.localRotation = Quaternion.Slerp(recoilPivot.localRotation, Quaternion.identity, rotationalReturnSpeed * Time.deltaTime);
        }
    }
}