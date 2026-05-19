using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;
using System.Collections;

[System.Serializable]
public struct WandModeUIConfig
{
    public RectTransform iconRect;
    public CanvasGroup iconCanvasGroup;
    public Material modeBarMaterial;
    public Sprite modeSprite;
    public int maxAmmo;
}

public class ManaHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup hudCanvasGroup;
    [SerializeField] private Image loadedManaImage;

    [Header("Reserve Triple Mask (Vertical)")]
    [Tooltip("Top Layer: Main Cyan/Yellow Bar")]
    [SerializeField] private RectMask2D reserveMainMask;
    [Tooltip("Bottom Layer: White Spend Trail")]
    [SerializeField] private RectMask2D reserveSpendMask;
    [Tooltip("Middle Layer: Green Gain Trail")]
    [SerializeField] private RectMask2D reserveGainMask;
    [SerializeField] private float reserveFullTopPadding = 0f;
    [SerializeField] private float reserveEmptyTopPadding = 500f;

    [Header("Loaded Triple Mask (Vertical)")]
    [Tooltip("Top Layer: Main Cyan/Yellow Bar")]
    [SerializeField] private RectMask2D loadedMainMask;
    [Tooltip("Bottom Layer: White Spend Trail")]
    [SerializeField] private RectMask2D loadedSpendMask;
    [Tooltip("Middle Layer: Green Gain Trail")]
    [SerializeField] private RectMask2D loadedGainMask;
    [SerializeField] private float loadedFullTopPadding = 0f;
    [SerializeField] private float loadedEmptyTopPadding = 500f;

    [Header("UI Wobble Effects")]
    [SerializeField] private RectTransform loadedContainerToWobble;
    public float loadedEmptyWobbleIntensity = 15f;
    public float loadedEmptyWobbleDuration = 0.2f;
    [Space(5)]
    [SerializeField] private RectTransform reserveContainerToWobble;
    public float reserveEmptyWobbleIntensity = 15f;
    public float reserveEmptyWobbleDuration = 0.2f;

    [Header("Edge Sparkles")]
    [SerializeField] private RectTransform reserveSparkle;
    [SerializeField] private Vector2 reserveSparkleEmptyPos;
    [SerializeField] private Vector2 reserveSparkleFullPos;
    [Space(10)]
    [SerializeField] private RectTransform loadedSparkle;
    [SerializeField] private Vector2 loadedSparkleEmptyPos;
    [SerializeField] private Vector2 loadedSparkleFullPos;

    [Header("Optional Text Support")]
    [SerializeField] private TextMeshProUGUI reserveText;
    [SerializeField] private TextMeshProUGUI loadedText;

    [Header("Colors")]
    [SerializeField] private Color reserveTextColor = Color.yellow;
    [SerializeField] private Color loadedTextCyan = Color.cyan;
    [SerializeField] private Color loadedTextYellow = Color.yellow;
    [SerializeField] private Color loadedTextFire = new Color(1f, 0.5f, 0f);

    [Header("Gauge & Modes")]
    [SerializeField] private RectTransform indicatorArrow;
    [SerializeField] private float arrowScrollSpeed = 5f;
    [SerializeField] private AnimationCurve arrowScrollCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private WandModeUIConfig[] modeConfigs = new WandModeUIConfig[3];

    [Header("Animation Speeds")]
    [SerializeField] private float catchUpLerpSpeed = 15f;
    [SerializeField] private float alphaLerpSpeed = 5f;

    private WandController localWand;
    private PlayerHealth localPlayerHealth;

    private int currentUIWandMode = -1;
    private Coroutine arrowCoroutine;

    // Fills for the Triple Mask system
    private float reserveMainFill = 1f;
    private float reserveSpendFill = 1f;
    private float reserveGainFill = 1f;

    private float loadedMainFill = 1f;
    private float loadedSpendFill = 1f;
    private float loadedGainFill = 1f;

    private int cachedReserveValue = -1;
    private int cachedLoadedValue = -1;

    private float currentAlpha = 1f;
    private float targetAlpha = 1f;
    private bool wasDead = false;

    void Awake()
    {
        if (reserveText != null) reserveText.color = reserveTextColor;
    }

    void Update()
    {
        FindLocalPlayer();

        if (localWand != null && localPlayerHealth != null)
        {
            if (localPlayerHealth.isDead && !wasDead) TriggerDeathUI();
            else if (!localPlayerHealth.isDead && wasDead) TriggerRespawnUI();

            if (!localPlayerHealth.isDead)
            {
                targetAlpha = 1f;
                int actualMode = localWand.CurrentModeIndex;

                if (actualMode != currentUIWandMode && actualMode >= 0 && actualMode < modeConfigs.Length)
                {
                    // Snap the cached ammo on mode switch so it doesn't create a weird trail when changing guns
                    cachedLoadedValue = -1;
                    if (arrowCoroutine != null) StopCoroutine(arrowCoroutine);
                    arrowCoroutine = StartCoroutine(SwitchModeUI(actualMode));
                }

                int currentLoadedAmmo = localWand.localLoadedEnergy[actualMode];
                int maxLoaded = modeConfigs[actualMode].maxAmmo;
                int currentReserve = localWand.localReserveEnergy;
                int maxReserve = localWand.maxReserveEnergy;

                // 1. RESERVE MANA LOGIC
                if (currentReserve != cachedReserveValue)
                {
                    float newTarget = Mathf.Clamp01((float)currentReserve / Mathf.Max(1f, maxReserve));

                    if (cachedReserveValue == -1) // Initialization
                    {
                        reserveMainFill = newTarget; reserveSpendFill = newTarget; reserveGainFill = newTarget;
                    }
                    else if (currentReserve < cachedReserveValue) // Draining Reserve (Reloading)
                    {
                        reserveSpendFill = reserveMainFill;
                        reserveMainFill = newTarget;
                        reserveGainFill = newTarget;
                    }
                    else if (currentReserve > cachedReserveValue) // Gaining Reserve (Harvesting)
                    {
                        reserveGainFill = newTarget;
                        reserveSpendFill = reserveMainFill;
                    }

                    cachedReserveValue = currentReserve;
                }

                // 2. LOADED MANA LOGIC
                if (currentLoadedAmmo != cachedLoadedValue)
                {
                    float newTarget = Mathf.Clamp01((float)currentLoadedAmmo / Mathf.Max(1f, maxLoaded));

                    if (cachedLoadedValue == -1) // Initialization
                    {
                        loadedMainFill = newTarget; loadedSpendFill = newTarget; loadedGainFill = newTarget;
                    }
                    else if (currentLoadedAmmo < cachedLoadedValue) // Draining Ammo (Shooting)
                    {
                        loadedSpendFill = loadedMainFill;
                        loadedMainFill = newTarget;
                        loadedGainFill = newTarget;
                    }
                    else if (currentLoadedAmmo > cachedLoadedValue) // Gaining Ammo (Reloading)
                    {
                        loadedGainFill = newTarget;
                        loadedSpendFill = loadedMainFill;
                    }

                    cachedLoadedValue = currentLoadedAmmo;
                }

                if (loadedText != null) loadedText.text = currentLoadedAmmo.ToString();
                if (reserveText != null) reserveText.text = currentReserve.ToString();
            }
        }
        else
        {
            targetAlpha = 0f;
        }

        // --- ANIMATE CATCHING UP ---
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, alphaLerpSpeed * Time.deltaTime);
        if (hudCanvasGroup != null) hudCanvasGroup.alpha = currentAlpha;

        float rTarget = cachedReserveValue >= 0 && localWand != null ? Mathf.Clamp01((float)localWand.localReserveEnergy / Mathf.Max(1f, localWand.maxReserveEnergy)) : 0f;
        reserveSpendFill = Mathf.Lerp(reserveSpendFill, rTarget, catchUpLerpSpeed * Time.deltaTime);
        reserveMainFill = Mathf.Lerp(reserveMainFill, rTarget, catchUpLerpSpeed * Time.deltaTime);

        float lTarget = cachedLoadedValue >= 0 && localWand != null && currentUIWandMode >= 0 ? Mathf.Clamp01((float)localWand.localLoadedEnergy[currentUIWandMode] / Mathf.Max(1f, modeConfigs[currentUIWandMode].maxAmmo)) : 0f;
        loadedSpendFill = Mathf.Lerp(loadedSpendFill, lTarget, catchUpLerpSpeed * Time.deltaTime);
        loadedMainFill = Mathf.Lerp(loadedMainFill, lTarget, catchUpLerpSpeed * Time.deltaTime);

        // --- APPLY PADDING TO MASKS ---
        ApplyPadding(reserveMainMask, reserveMainFill, reserveEmptyTopPadding, reserveFullTopPadding);
        ApplyPadding(reserveSpendMask, reserveSpendFill, reserveEmptyTopPadding, reserveFullTopPadding);
        ApplyPadding(reserveGainMask, reserveGainFill, reserveEmptyTopPadding, reserveFullTopPadding);

        ApplyPadding(loadedMainMask, loadedMainFill, loadedEmptyTopPadding, loadedFullTopPadding);
        ApplyPadding(loadedSpendMask, loadedSpendFill, loadedEmptyTopPadding, loadedFullTopPadding);
        ApplyPadding(loadedGainMask, loadedGainFill, loadedEmptyTopPadding, loadedFullTopPadding);

        // --- APPLY SPARKLES ---
        if (reserveSparkle != null)
        {
            reserveSparkle.anchoredPosition = Vector2.Lerp(reserveSparkleEmptyPos, reserveSparkleFullPos, reserveMainFill);
            reserveSparkle.gameObject.SetActive(reserveMainFill > 0.01f);
        }

        if (loadedSparkle != null)
        {
            loadedSparkle.anchoredPosition = Vector2.Lerp(loadedSparkleEmptyPos, loadedSparkleFullPos, loadedMainFill);
            loadedSparkle.gameObject.SetActive(loadedMainFill > 0.01f);
        }
    }

    private void ApplyPadding(RectMask2D mask, float fillAmount, float emptyPad, float fullPad)
    {
        if (mask != null)
        {
            // Vertical uses Top Padding (Left, Bottom, Right, Top)
            float activePadding = Mathf.Lerp(emptyPad, fullPad, fillAmount);
            mask.padding = new Vector4(0, 0, 0, activePadding);
        }
    }

    public void TriggerEmptyFireWobble()
    {
        if (loadedContainerToWobble != null) StartCoroutine(WobbleRoutine(loadedContainerToWobble, loadedEmptyWobbleIntensity, loadedEmptyWobbleDuration));
    }

    public void TriggerEmptyReserveWobble()
    {
        if (reserveContainerToWobble != null) StartCoroutine(WobbleRoutine(reserveContainerToWobble, reserveEmptyWobbleIntensity, reserveEmptyWobbleDuration));
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

    public void TriggerReloadVisuals(float reloadDuration, float targetLoadedRatio, float targetReserveRatio, int finalLoadedStr, int finalReserveStr)
    {
        // Intentionally left blank. The snappy UI math above now automatically handles this instantly 
        // to spawn the visual gain/spend trails. Leaving this here so WandController.cs compiles cleanly!
    }

    public void TriggerDeathUI()
    {
        wasDead = true;
        targetAlpha = 0f;

        loadedMainFill = 0f; loadedSpendFill = 0f; loadedGainFill = 0f;
        reserveMainFill = 0f; reserveSpendFill = 0f; reserveGainFill = 0f;

        cachedLoadedValue = 0;
        cachedReserveValue = 0;

        if (reserveText != null) reserveText.text = "";
        if (loadedText != null) loadedText.text = "";
    }

    public void TriggerRespawnUI()
    {
        wasDead = false;
        targetAlpha = 1f;
    }

    private IEnumerator SwitchModeUI(int newModeIndex)
    {
        currentUIWandMode = newModeIndex;
        WandModeUIConfig config = modeConfigs[newModeIndex];

        for (int i = 0; i < modeConfigs.Length; i++)
        {
            if (modeConfigs[i].iconCanvasGroup != null)
            {
                modeConfigs[i].iconCanvasGroup.alpha = (i == newModeIndex) ? 1.0f : 0.7f;
            }
        }

        if (loadedText != null)
        {
            if (newModeIndex == 0) loadedText.color = loadedTextCyan;
            else if (newModeIndex == 1) loadedText.color = loadedTextYellow;
            else if (newModeIndex == 2) loadedText.color = loadedTextFire;
        }

        if (loadedManaImage != null)
        {
            if (config.modeBarMaterial != null) loadedManaImage.material = config.modeBarMaterial;
            else loadedManaImage.material = null;

            if (config.modeSprite != null) loadedManaImage.sprite = config.modeSprite;
        }

        if (indicatorArrow != null && config.iconRect != null)
        {
            float t = 0f;
            Vector2 startPos = indicatorArrow.anchoredPosition;
            Vector2 endPos = new Vector2(startPos.x, config.iconRect.anchoredPosition.y);

            while (t < 1f)
            {
                t += Time.deltaTime * arrowScrollSpeed;
                float curveT = arrowScrollCurve.Evaluate(Mathf.Clamp01(t));
                indicatorArrow.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curveT);
                yield return null;
            }
            indicatorArrow.anchoredPosition = endPos;
        }
    }

    private void FindLocalPlayer()
    {
        if (localWand == null && NetworkClient.localPlayer != null)
        {
            localWand = NetworkClient.localPlayer.GetComponent<WandController>();
            localPlayerHealth = NetworkClient.localPlayer.GetComponent<PlayerHealth>();
        }
    }
}