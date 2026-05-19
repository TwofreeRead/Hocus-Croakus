using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class GrabbedObjectAura : MonoBehaviour
{
    private GameObject auraObject;
    private Material auraMaterialInstance;
    private float alphaMax;
    private float fadeTime;

    private Color colorNormal;
    private Color colorCharge;

    private MeshRenderer targetRenderer;
    private Material[] originalMaterials;
    private Material[] transparentMaterials;

    // FIX: Changed to GameObject to avoid Mirror NetworkIdentity null-check quirks
    private GameObject grabberOwner;
    private bool isReleasing = false;
    private Coroutine fadeCoroutine;

    // Shader property hashes for performance
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    public void Setup(Material baseAuraMat, GameObject particles, float fade, float heldAlpha, Color normal, Color charge, GameObject grabber)
    {
        isReleasing = false;
        fadeTime = fade;
        alphaMax = heldAlpha;
        colorNormal = normal;
        colorCharge = charge;
        grabberOwner = grabber;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // --- 1. Force the Original Object Transparent ---
        targetRenderer = GetComponent<MeshRenderer>();
        if (targetRenderer != null)
        {
            if (originalMaterials == null || originalMaterials.Length == 0)
            {
                originalMaterials = targetRenderer.sharedMaterials;
            }

            if (transparentMaterials != null)
            {
                foreach (Material m in transparentMaterials) if (m != null) Destroy(m);
            }

            transparentMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                transparentMaterials[i] = new Material(originalMaterials[i]);
                Material mat = transparentMaterials[i];

                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;

                Color c = Color.white;
                if (mat.HasProperty(BaseColorProp)) c = mat.GetColor(BaseColorProp);
                else if (mat.HasProperty(ColorProp)) c = mat.GetColor(ColorProp);

                c.a = heldAlpha;
                if (mat.HasProperty(BaseColorProp)) mat.SetColor(BaseColorProp, c);
                else if (mat.HasProperty(ColorProp)) mat.SetColor(ColorProp, c);
            }

            targetRenderer.materials = transparentMaterials;
        }

        // --- 2. Create the Emissive Aura Clone ---
        if (auraObject != null) Destroy(auraObject);

        auraObject = new GameObject("GravityAura_Visual");
        auraObject.transform.SetParent(transform);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localRotation = Quaternion.identity;
        auraObject.transform.localScale = Vector3.one * 1.05f;

        auraObject.tag = "Untagged";
        auraObject.layer = 0;

        MeshFilter originalFilter = GetComponent<MeshFilter>();
        if (originalFilter != null && baseAuraMat != null)
        {
            MeshFilter auraFilter = auraObject.AddComponent<MeshFilter>();
            auraFilter.sharedMesh = originalFilter.sharedMesh;

            MeshRenderer auraRenderer = auraObject.AddComponent<MeshRenderer>();
            auraMaterialInstance = new Material(baseAuraMat);
            auraMaterialInstance.EnableKeyword("_EMISSION");
            auraRenderer.material = auraMaterialInstance;

            UpdateAura(0f);
        }

        if (particles != null) Instantiate(particles, transform.position, Quaternion.identity, transform);
    }

    void Update()
    {
        // Unbreakable failsafe if grabber vanishes
        if (!isReleasing && grabberOwner == null)
        {
            Release();
        }
    }

    public void UpdateAura(float chargeRatio)
    {
        if (auraMaterialInstance == null) return;

        Color lerpedColor = Color.Lerp(colorNormal, colorCharge, chargeRatio);
        Color alphaColor = lerpedColor;
        alphaColor.a = alphaMax;

        if (auraMaterialInstance.HasProperty(BaseColorProp)) auraMaterialInstance.SetColor(BaseColorProp, alphaColor);
        else if (auraMaterialInstance.HasProperty(ColorProp)) auraMaterialInstance.SetColor(ColorProp, alphaColor);

        if (auraMaterialInstance.HasProperty(EmissionColorProp)) auraMaterialInstance.SetColor(EmissionColorProp, lerpedColor);
    }

    public void Release()
    {
        if (isReleasing) return;
        isReleasing = true;

        fadeCoroutine = StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float timer = 0f;
        Color startEmission = auraMaterialInstance != null && auraMaterialInstance.HasProperty(EmissionColorProp) ? auraMaterialInstance.GetColor(EmissionColorProp) : colorCharge;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float normalizedAlpha = Mathf.Lerp(alphaMax, 0f, timer / fadeTime);
            float fadeRatio = timer / fadeTime;

            if (auraMaterialInstance != null)
            {
                Color startBase = colorCharge;
                startBase.a = normalizedAlpha;
                if (auraMaterialInstance.HasProperty(BaseColorProp)) auraMaterialInstance.SetColor(BaseColorProp, startBase);
                else if (auraMaterialInstance.HasProperty(ColorProp)) auraMaterialInstance.SetColor(ColorProp, startBase);

                if (auraMaterialInstance.HasProperty(EmissionColorProp)) auraMaterialInstance.SetColor(EmissionColorProp, startEmission * (normalizedAlpha / alphaMax));
            }

            if (targetRenderer != null && transparentMaterials != null)
            {
                for (int i = 0; i < transparentMaterials.Length; i++)
                {
                    Material mat = transparentMaterials[i];
                    if (mat == null) continue;

                    Color c = Color.white;
                    if (mat.HasProperty(BaseColorProp)) c = mat.GetColor(BaseColorProp);
                    else if (mat.HasProperty(ColorProp)) c = mat.GetColor(ColorProp);

                    c.a = Mathf.Lerp(alphaMax, 1f, fadeRatio);

                    if (mat.HasProperty(BaseColorProp)) mat.SetColor(BaseColorProp, c);
                    else if (mat.HasProperty(ColorProp)) mat.SetColor(ColorProp, c);
                }
            }

            yield return null;
        }

        RestoreOriginalMaterials();
        if (auraObject != null) Destroy(auraObject);
        Destroy(this);
    }

    private void RestoreOriginalMaterials()
    {
        if (targetRenderer != null && originalMaterials != null && originalMaterials.Length > 0)
        {
            targetRenderer.materials = originalMaterials;
        }
        if (transparentMaterials != null)
        {
            foreach (Material m in transparentMaterials) { if (m != null) Destroy(m); }
            transparentMaterials = null;
        }
    }

    void OnDestroy()
    {
        RestoreOriginalMaterials();
    }
}