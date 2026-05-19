using UnityEngine;

public class TPWandVisuals : MonoBehaviour
{
    [Header("Renderer Settings")]
    [SerializeField] private SkinnedMeshRenderer frogRenderer;
    [SerializeField] private int wandMaterialIndex = 1;

    [Header("Transform References")]
    [SerializeField] private Transform tpFirePoint;

    private MaterialPropertyBlock propBlock;
    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    private ParticleSystem activeMuzzleGrabParticles;
    private ParticleSystem activeHarvestParticles;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (frogRenderer != null) frogRenderer.sharedMaterials[wandMaterialIndex].EnableKeyword("_EMISSION");
    }

    public void UpdateVisualState(int ammo, int max, Color modeColor, float baseInt, bool reloading, float progress, bool holding, bool harvesting, float charge, Color gravColor, Color maxGravColor, HarvestSettings harvestProps)
    {
        if (frogRenderer == null) return;

        Color targetColor = modeColor;
        float targetIntensity = ammo > 0 ? baseInt * ((float)ammo / max) : 0f;

        if (harvesting)
        {
            targetColor = harvestProps.wandEmissionColor;
            targetIntensity = baseInt + 3f;
        }
        else if (holding)
        {
            targetColor = Color.Lerp(gravColor, maxGravColor, charge);
            targetIntensity = baseInt + (charge * 3f);
        }
        else if (reloading)
        {
            float baseEmptyIntensity = baseInt * 0.1f;
            targetIntensity = Mathf.Lerp(baseEmptyIntensity, baseInt, progress);
        }

        frogRenderer.GetPropertyBlock(propBlock, wandMaterialIndex);
        propBlock.SetColor(EmissionColorProp, targetColor * targetIntensity);
        frogRenderer.SetPropertyBlock(propBlock, wandMaterialIndex);
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

    public void ActivateHarvestVisuals(GameObject prefab, Transform firePoint)
    {
        if (prefab != null && activeHarvestParticles == null)
        {
            GameObject obj = Instantiate(prefab, firePoint.position, firePoint.rotation, firePoint);
            activeHarvestParticles = obj.GetComponent<ParticleSystem>();
        }
    }

    public void DeactivateHarvestVisuals()
    {
        if (activeHarvestParticles != null)
        {
            activeHarvestParticles.Stop();
            Destroy(activeHarvestParticles.gameObject, 2f);
            activeHarvestParticles = null;
        }
    }

    public void PlayMuzzleFlash(GameObject flash)
    {
        if (flash != null && tpFirePoint != null)
        {
            GameObject flashObj = Instantiate(flash, tpFirePoint.position, tpFirePoint.rotation, tpFirePoint);
            Destroy(flashObj, 2f);
        }
    }

    public void PlayReloadEffect(GameObject reload)
    {
        if (reload != null && tpFirePoint != null)
        {
            GameObject reloadObj = Instantiate(reload, tpFirePoint.position, tpFirePoint.rotation, tpFirePoint);
            Destroy(reloadObj, 2f);
        }
    }
}