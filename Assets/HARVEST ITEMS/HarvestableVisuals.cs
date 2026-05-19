using UnityEngine;
using System.Collections;

public class HarvestableVisuals : MonoBehaviour
{
    private HarvestableData data;
    private GrowthPoint parentNode;
    private bool isOvergrown;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;
    private Color baseEmissionColor = Color.white;

    private Vector3 originalPrefabScale;

    // THE FIX: Initialize now accepts 'timePassed' from the server calculation
    public void Initialize(HarvestableData harvestableData, bool overgrown, GrowthPoint node, float timePassed)
    {
        data = harvestableData;
        isOvergrown = overgrown;
        parentNode = node;

        originalPrefabScale = transform.localScale;

        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null && data.isEmissive)
        {
            propBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propBlock);

            if (meshRenderer.sharedMaterial.HasProperty("_EmissionColor"))
            {
                baseEmissionColor = meshRenderer.sharedMaterial.GetColor("_EmissionColor");
            }
            else if (meshRenderer.sharedMaterial.HasProperty("_Color"))
            {
                baseEmissionColor = meshRenderer.sharedMaterial.GetColor("_Color");
            }

            if (data.emissionMode == EmissionMode.Constant)
            {
                propBlock.SetColor("_EmissionColor", baseEmissionColor * data.maxEmission);
                meshRenderer.SetPropertyBlock(propBlock);
            }
        }

        transform.localScale = Vector3.zero;
        StartCoroutine(GrowthRoutine(timePassed));
    }

    private IEnumerator GrowthRoutine(float timePassed)
    {
        float targetScaleMultiplier = isOvergrown ? 1.25f : 1.0f;
        Vector3 finalScale = originalPrefabScale * targetScaleMultiplier;

        float timer = timePassed;

        // If the player joins when it's already completely done growing:
        if (timer >= data.growthTime)
        {
            transform.localScale = finalScale;

            // We only notify the server to unlock the node if it's not fully grown yet.
            // This prevents late joiners from accidentally bypassing the server lock.
            if (parentNode != null && !parentNode.isFullyGrown)
            {
                parentNode.OnVisualGrowthComplete();
            }
        }
        else
        {
            // Instantly snap to the correct partial scale based on when they joined!
            transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, timer / data.growthTime);

            while (timer < data.growthTime)
            {
                timer += Time.deltaTime;
                float progress = timer / data.growthTime;

                transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, progress);
                yield return null;
            }

            transform.localScale = finalScale;

            if (data.grownParticle != null) Instantiate(data.grownParticle, transform.position, Quaternion.identity);
            if (data.grownSound != null) AudioSource.PlayClipAtPoint(data.grownSound, transform.position, 1f);

            if (parentNode != null) parentNode.OnVisualGrowthComplete();
        }
    }

    void Update()
    {
        if (data == null || !data.isEmissive || meshRenderer == null || propBlock == null) return;

        if (data.emissionMode == EmissionMode.Wave)
        {
            float wavePhase = (Mathf.Sin(Time.time * data.waveSpeed) + 1f) / 2f;
            float currentIntensity = Mathf.Lerp(data.minEmission, data.maxEmission, wavePhase);

            propBlock.SetColor("_EmissionColor", baseEmissionColor * currentIntensity);
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }
}