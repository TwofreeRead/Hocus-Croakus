using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class StatusEffectUIManager : MonoBehaviour
{
    [Header("Debuff Icons")]
    public GameObject effectIconPrefab;
    public Transform iconContainer;

    [Header("Visual Overlays")]
    [Tooltip("Used for Custom Shader Materials")]
    public Image materialOverlayImage;
    [Tooltip("Used for standard 2D UI Sprites (e.g., ice borders)")]
    public Image spriteOverlayImage;
    [Tooltip("Used to flash the screen when stunned")]
    public Image stunFlashImage;

    private Dictionary<uint, StatusEffectUIItem> activeIcons = new Dictionary<uint, StatusEffectUIItem>();
    private AudioSource uiAudioSource;
    private Coroutine stunFlashCoroutine;

    void Awake()
    {
        uiAudioSource = gameObject.AddComponent<AudioSource>();
        uiAudioSource.spatialBlend = 0f; // 2D UI sound

        // Destroys any dummy template icons left in the Canvas by accident
        foreach (Transform child in iconContainer)
        {
            Destroy(child.gameObject);
        }

        if (materialOverlayImage != null) materialOverlayImage.enabled = false;
        if (spriteOverlayImage != null) spriteOverlayImage.enabled = false;
        if (stunFlashImage != null)
        {
            stunFlashImage.enabled = false;
            stunFlashImage.color = new Color(0, 0, 0, 0);
        }
    }

    public void AddEffect(StatusEffectData data, float duration, uint instanceId)
    {
        if (activeIcons.ContainsKey(instanceId)) return;

        if (data.onAppliedSound != null) uiAudioSource.PlayOneShot(data.onAppliedSound);

        // THE FIX: Adding 'false' forces worldPositionStays to false. 
        // This stops Unity from throwing the UI 5000 pixels off-screen with a broken scale.
        GameObject newIcon = Instantiate(effectIconPrefab, iconContainer, false);

        // Safety lock
        RectTransform rt = newIcon.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;
        newIcon.SetActive(true);

        StatusEffectUIItem itemScript = newIcon.GetComponent<StatusEffectUIItem>();
        itemScript.Setup(data, duration);
        activeIcons.Add(instanceId, itemScript);
    }

    public void RemoveEffect(StatusEffectData data, uint instanceId)
    {
        if (activeIcons.TryGetValue(instanceId, out StatusEffectUIItem item))
        {
            if (data.onRemovedSound != null) uiAudioSource.PlayOneShot(data.onRemovedSound);

            if (item != null && item.gameObject != null) Destroy(item.gameObject);
            activeIcons.Remove(instanceId);
        }
    }

    public void TriggerStunFeedback(AudioClip clip, Color flashColor)
    {
        if (clip != null) uiAudioSource.PlayOneShot(clip);

        if (stunFlashImage != null)
        {
            if (stunFlashCoroutine != null) StopCoroutine(stunFlashCoroutine);
            stunFlashCoroutine = StartCoroutine(StunFlashRoutine(flashColor));
        }
    }

    private IEnumerator StunFlashRoutine(Color flashColor)
    {
        stunFlashImage.color = flashColor;
        stunFlashImage.enabled = true;

        float timer = 0f;
        float fadeTime = 0.4f;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, timer / fadeTime);
            stunFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }

        stunFlashImage.enabled = false;
    }

    public void ApplyOverlays(Material customMat, Sprite customSprite)
    {
        if (materialOverlayImage != null)
        {
            materialOverlayImage.material = customMat;
            materialOverlayImage.enabled = (customMat != null);
        }

        if (spriteOverlayImage != null)
        {
            spriteOverlayImage.sprite = customSprite;
            spriteOverlayImage.enabled = (customSprite != null);
        }
    }
}