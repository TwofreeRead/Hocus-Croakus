using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System.Collections;

public class HealthHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup hudCanvasGroup;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Triple Mask Setup (Assign RectMask2D)")]
    [Tooltip("The main red health bar mask")]
    [SerializeField] private RectMask2D mainHealthMask;
    [Tooltip("The white damage trail mask (Rendered BEHIND main mask)")]
    [SerializeField] private RectMask2D damageTrailMask;
    [Tooltip("The green healing trail mask (Rendered BEHIND main mask)")]
    [SerializeField] private RectMask2D healingTrailMask;

    [Header("Padding Boundaries")]
    [SerializeField] private float fullRightPadding = 0f;
    [SerializeField] private float emptyRightPadding = 800f;

    [Header("Animation Speeds")]
    [SerializeField] private float catchUpLerpSpeed = 3f;
    [SerializeField] private float alphaLerpSpeed = 5f;

    [Header("Hurt Wobble Settings")]
    [SerializeField] private RectTransform containerToWobble;
    [SerializeField] private float hurtWobbleIntensity = 15f;
    [SerializeField] private float hurtWobbleDuration = 0.3f;

    private PlayerHealth localPlayerHealth;

    private float mainFill = 1f;
    private float damageFill = 1f;
    private float healingFill = 1f;

    private int cachedHealthValue = -1;
    private float currentAlpha = 1f;
    private float targetAlpha = 1f;

    void Update()
    {
        FindLocalPlayer();

        if (localPlayerHealth != null)
        {
            if (localPlayerHealth.isDead)
            {
                targetAlpha = 0f;
                mainFill = 0f;
                damageFill = 0f;
                healingFill = 0f;
                cachedHealthValue = 0;
            }
            else
            {
                targetAlpha = 1f;

                // Did our health change this frame?
                if (localPlayerHealth.currentHealth != cachedHealthValue)
                {
                    float newTargetFill = Mathf.Clamp01((float)localPlayerHealth.currentHealth / Mathf.Max(1f, localPlayerHealth.maxHealth));

                    // 1. First Time Initialization
                    if (cachedHealthValue == -1)
                    {
                        mainFill = newTargetFill;
                        damageFill = newTargetFill;
                        healingFill = newTargetFill;
                    }
                    // 2. We took Damage!
                    else if (localPlayerHealth.currentHealth < cachedHealthValue)
                    {
                        damageFill = mainFill;       // The white bar starts exactly where we were
                        mainFill = newTargetFill;    // The red bar drops instantly to the new value
                        healingFill = newTargetFill; // Green bar hides

                        if (containerToWobble != null) StartCoroutine(WobbleRoutine(containerToWobble, hurtWobbleIntensity, hurtWobbleDuration));
                    }
                    // 3. We Healed!
                    else if (localPlayerHealth.currentHealth > cachedHealthValue)
                    {
                        healingFill = newTargetFill; // The green bar shoots forward instantly
                        damageFill = mainFill;       // White bar hides behind main
                        // Main red bar will naturally lerp forward to catch up
                    }

                    cachedHealthValue = localPlayerHealth.currentHealth;
                }

                if (healthText != null)
                {
                    healthText.text = localPlayerHealth.isDead ? "" : $"{localPlayerHealth.currentHealth} / {localPlayerHealth.maxHealth}";
                }
            }
        }
        else
        {
            targetAlpha = 0f;
        }

        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaLerpSpeed * Time.deltaTime);
        if (hudCanvasGroup != null) hudCanvasGroup.alpha = currentAlpha;

        // Animate the catching up bars
        float target = cachedHealthValue >= 0 && localPlayerHealth != null ? Mathf.Clamp01((float)localPlayerHealth.currentHealth / Mathf.Max(1f, localPlayerHealth.maxHealth)) : 0f;

        damageFill = Mathf.Lerp(damageFill, target, catchUpLerpSpeed * Time.deltaTime);
        mainFill = Mathf.Lerp(mainFill, target, catchUpLerpSpeed * Time.deltaTime);

        ApplyPadding(mainHealthMask, mainFill);
        ApplyPadding(damageTrailMask, damageFill);
        ApplyPadding(healingTrailMask, healingFill);
    }

    private void ApplyPadding(RectMask2D mask, float fillAmount)
    {
        if (mask != null)
        {
            float activePadding = Mathf.Lerp(emptyRightPadding, fullRightPadding, fillAmount);
            mask.padding = new Vector4(0, 0, activePadding, 0);
        }
    }

    private IEnumerator WobbleRoutine(RectTransform targetRect, float intensity, float duration)
    {
        Vector3 originalPos = targetRect.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float currentIntensity = intensity * (1f - (elapsed / duration));
            float offsetX = Random.Range(-1f, 1f) * currentIntensity;
            float offsetY = Random.Range(-1f, 1f) * currentIntensity;

            targetRect.localPosition = originalPos + new Vector3(offsetX, offsetY, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetRect.localPosition = originalPos;
    }

    private void FindLocalPlayer()
    {
        if (localPlayerHealth == null && NetworkClient.localPlayer != null)
        {
            localPlayerHealth = NetworkClient.localPlayer.GetComponent<PlayerHealth>();
        }
    }
}