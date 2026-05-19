using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance;

    [Header("General UI References")]
    [SerializeField] private CanvasGroup minigameCanvasGroup;
    [SerializeField] private GameObject crosshairRoot;

    [Header("Global Bars")]
    [SerializeField] private RectMask2D globalTimeMask;
    [SerializeField] private float timeFullRightPadding = 0f;
    [SerializeField] private float timeEmptyRightPadding = 800f;

    [Header("Global Horizontal Progress (Focus Only)")]
    [SerializeField] private GameObject horizProgressContainer;
    [SerializeField] private RectMask2D globalHorizontalProgressMask;
    [SerializeField] private float horizProgFullRightPadding = 0f;
    [SerializeField] private float horizProgEmptyRightPadding = 800f;

    [Header("Balance Minigame (Stardew)")]
    [SerializeField] private GameObject balanceContainer;
    [SerializeField] private GameObject balanceProgressContainer;
    [SerializeField] private RectMask2D balanceVerticalProgressMask;
    [SerializeField] private float balanceProgFullTopPadding = 0f;
    [SerializeField] private float balanceProgEmptyTopPadding = 500f;
    [Space(5)]
    [SerializeField] private RectTransform balanceBarArea;
    [SerializeField] private RectTransform balanceTargetArea;
    [SerializeField] private RectTransform balancePlayerKnob;

    [Header("Focus Minigame (Pandemonium)")]
    [SerializeField] private GameObject focusContainer;
    [SerializeField] private RectTransform focusMercyRing;
    [SerializeField] private RectTransform focusTargetRing;
    [SerializeField] private RectTransform focusPlayerCursor;
    [Tooltip("If it feels too slow, crank this up to 50 or 100.")]
    [SerializeField] private float focusCursorSensitivity = 25f;

    // State Variables
    public bool IsPlaying { get; private set; }
    private MinigameType currentType;
    private HarvestableData currentData;

    private float timeRemaining;
    private float successProgress;

    // Balance Math
    private float balanceTargetPos;
    private float balancePlayerPos;
    private float balancePlayerVelocity;
    private float noiseOffset;

    // Focus Math
    private Vector2 focusCurrentPos;
    private Vector2 focusFlingVelocity;
    private float focusFlingTimer;

    private PlayerMovement localPlayerMovement;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (minigameCanvasGroup != null)
        {
            minigameCanvasGroup.alpha = 0f;
            minigameCanvasGroup.blocksRaycasts = false;
        }
    }

    private void FindLocalPlayer()
    {
        if (localPlayerMovement == null && NetworkClient.localPlayer != null)
        {
            localPlayerMovement = NetworkClient.localPlayer.GetComponent<PlayerMovement>();
        }
    }

    public void StartMinigame(HarvestableData data)
    {
        currentData = data;
        currentType = data.minigameType;

        if (currentType == MinigameType.None) return;

        timeRemaining = data.harvestTime;
        successProgress = 0.25f;
        noiseOffset = Random.Range(0f, 100f);

        if (balanceContainer != null) balanceContainer.SetActive(currentType == MinigameType.Balance);
        if (focusContainer != null) focusContainer.SetActive(currentType == MinigameType.Focus);

        if (horizProgressContainer != null) horizProgressContainer.SetActive(currentType == MinigameType.Focus);
        if (balanceProgressContainer != null) balanceProgressContainer.SetActive(currentType == MinigameType.Balance);

        if (crosshairRoot != null) crosshairRoot.SetActive(false);

        if (currentType == MinigameType.Balance && balanceBarArea != null)
        {
            balanceTargetPos = 0f;
            balancePlayerPos = -balanceBarArea.rect.height / 2f;
            float mult = data.balanceTargetSizeMultiplier > 0 ? data.balanceTargetSizeMultiplier : 1f;
            balanceTargetArea.localScale = new Vector3(mult, mult, 1f);
        }
        else if (currentType == MinigameType.Focus)
        {
            focusCurrentPos = Vector2.zero;
            focusFlingVelocity = Vector2.zero;
            float minT = data.focusFlingMinInterval > 0 ? data.focusFlingMinInterval : 1f;
            float maxT = data.focusFlingMaxInterval > 0 ? data.focusFlingMaxInterval : 3f;
            focusFlingTimer = Random.Range(minT, maxT);
        }

        FindLocalPlayer();
        if (localPlayerMovement != null)
        {
            localPlayerMovement.isJumpLocked = true;
            localPlayerMovement.isCameraLocked = true;
            localPlayerMovement.isMovementLocked = true;
        }

        minigameCanvasGroup.alpha = 1f;
        IsPlaying = true;
    }

    public void StopMinigame(bool success)
    {
        IsPlaying = false;
        minigameCanvasGroup.alpha = 0f;
        if (crosshairRoot != null) crosshairRoot.SetActive(true);

        if (success && currentData.minigameSuccessSound != null)
            AudioSource.PlayClipAtPoint(currentData.minigameSuccessSound, Camera.main.transform.position);
        else if (!success && currentData.minigameFailureSound != null)
            AudioSource.PlayClipAtPoint(currentData.minigameFailureSound, Camera.main.transform.position);

        if (localPlayerMovement != null)
        {
            localPlayerMovement.isJumpLocked = false;
            localPlayerMovement.isCameraLocked = false;
            localPlayerMovement.isMovementLocked = false;
        }

        WandController localWand = NetworkClient.localPlayer.GetComponent<WandController>();
        if (localWand != null) localWand.OnMinigameComplete(success);
    }

    void Update()
    {
        if (!IsPlaying || currentData == null) return;

        timeRemaining -= Time.deltaTime;

        if (currentType == MinigameType.Balance) UpdateBalanceMinigame();
        else if (currentType == MinigameType.Focus) UpdateFocusMinigame();

        successProgress = Mathf.Clamp01(successProgress);
        float timeRatio = Mathf.Clamp01(timeRemaining / Mathf.Max(1f, currentData.harvestTime));

        if (globalTimeMask != null)
        {
            float activeTimePad = Mathf.Lerp(timeEmptyRightPadding, timeFullRightPadding, timeRatio);
            globalTimeMask.padding = new Vector4(0, 0, activeTimePad, 0);
        }

        if (currentType == MinigameType.Balance && balanceVerticalProgressMask != null)
        {
            float activeProgPad = Mathf.Lerp(balanceProgEmptyTopPadding, balanceProgFullTopPadding, successProgress);
            balanceVerticalProgressMask.padding = new Vector4(0, 0, 0, activeProgPad);
        }
        else if (currentType == MinigameType.Focus && globalHorizontalProgressMask != null)
        {
            float activeProgPad = Mathf.Lerp(horizProgEmptyRightPadding, horizProgFullRightPadding, successProgress);
            globalHorizontalProgressMask.padding = new Vector4(0, 0, activeProgPad, 0);
        }

        if (successProgress >= 1f) StopMinigame(true);
        else if (timeRemaining <= 0f) StopMinigame(false);
    }

    private void UpdateBalanceMinigame()
    {
        if (balanceBarArea == null || balanceTargetArea == null || balancePlayerKnob == null) return;

        float speed = currentData.balanceTargetSpeed > 0 ? currentData.balanceTargetSpeed : 50f;
        float thrust = currentData.balanceKnobThrust > 0 ? currentData.balanceKnobThrust : 1000f;
        float gravity = currentData.balanceKnobGravity > 0 ? currentData.balanceKnobGravity : 800f;

        float actualTargetHeight = balanceTargetArea.rect.height * balanceTargetArea.localScale.y;

        float maxTargetPos = (balanceBarArea.rect.height - actualTargetHeight) / 2f;
        float maxKnobPos = (balanceBarArea.rect.height - balancePlayerKnob.rect.height) / 2f;

        float noise = Mathf.PerlinNoise(Time.time * speed * 0.05f, noiseOffset);
        float desiredTargetPos = Mathf.Lerp(-maxTargetPos, maxTargetPos, noise);

        balanceTargetPos = Mathf.Lerp(balanceTargetPos, desiredTargetPos, Time.deltaTime * 5f);
        balanceTargetArea.anchoredPosition = new Vector2(0, balanceTargetPos);

        if (Input.GetKey(KeyCode.Space)) balancePlayerVelocity += thrust * Time.deltaTime;
        else balancePlayerVelocity -= gravity * Time.deltaTime;

        balancePlayerVelocity = Mathf.Lerp(balancePlayerVelocity, 0f, 4f * Time.deltaTime);
        balancePlayerVelocity = Mathf.Clamp(balancePlayerVelocity, -gravity, thrust);

        balancePlayerPos += balancePlayerVelocity * Time.deltaTime;
        balancePlayerPos = Mathf.Clamp(balancePlayerPos, -maxKnobPos, maxKnobPos);
        balancePlayerKnob.anchoredPosition = new Vector2(0, balancePlayerPos);

        float distance = Mathf.Abs(balanceTargetPos - balancePlayerPos);
        float halfTarget = actualTargetHeight / 2f;

        float fillRate = currentData.minigameProgressFillRate > 0 ? currentData.minigameProgressFillRate : 0.4f;
        float drainRate = currentData.minigameProgressDrainRate > 0 ? currentData.minigameProgressDrainRate : 0.2f;

        if (distance <= halfTarget) successProgress += fillRate * Time.deltaTime;
        else successProgress -= drainRate * Time.deltaTime;
    }

    private void UpdateFocusMinigame()
    {
        if (focusPlayerCursor == null || focusTargetRing == null || focusMercyRing == null) return;

        // 1. Mouse Input
        float mX = Input.GetAxisRaw("Mouse X");
        float mY = Input.GetAxisRaw("Mouse Y");
        float sens = focusCursorSensitivity > 0 ? focusCursorSensitivity : 25f;

        focusCurrentPos.x += mX * sens;
        focusCurrentPos.y += mY * sens;

        // 2. Resistance (Pulls away from center)
        float resistance = currentData.focusResistancePower > 0 ? currentData.focusResistancePower : 150f;
        Vector2 pushOutDirection = focusCurrentPos.normalized;
        if (pushOutDirection == Vector2.zero) pushOutDirection = Random.insideUnitCircle.normalized;

        focusCurrentPos += pushOutDirection * resistance * Time.deltaTime;

        // 3. Fling Knockback
        focusFlingTimer -= Time.deltaTime;
        if (focusFlingTimer <= 0f)
        {
            Vector2 randomBlast = Random.insideUnitCircle.normalized;
            float power = currentData.focusFlingPower > 0 ? currentData.focusFlingPower : 1500f;
            focusFlingVelocity = randomBlast * power;

            float minT = currentData.focusFlingMinInterval > 0 ? currentData.focusFlingMinInterval : 1f;
            float maxT = currentData.focusFlingMaxInterval > 0 ? currentData.focusFlingMaxInterval : 3f;
            focusFlingTimer = Random.Range(minT, maxT);
        }

        focusCurrentPos += focusFlingVelocity * Time.deltaTime;
        focusFlingVelocity = Vector2.Lerp(focusFlingVelocity, Vector2.zero, Time.deltaTime * 5f);

        // --- THE CULPRIT IS DEAD ---
        // Instead of calculating rects, we clamp the cursor to a physical radius set in HarvestableData.
        float maxRadius = currentData.focusPlayAreaRadius > 0 ? currentData.focusPlayAreaRadius : 400f;
        focusCurrentPos = Vector2.ClampMagnitude(focusCurrentPos, maxRadius);

        // 4. Visual Shake
        float shakeLimit = currentData.focusShakeLimit > 0 ? currentData.focusShakeLimit : 15f;
        float shakeSpeed = currentData.focusShakeSpeed > 0 ? currentData.focusShakeSpeed : 25f;

        float shakeX = (Mathf.PerlinNoise(Time.time * shakeSpeed, noiseOffset) - 0.5f) * 2f;
        float shakeY = (Mathf.PerlinNoise(noiseOffset, Time.time * shakeSpeed) - 0.5f) * 2f;
        Vector2 visualOffset = new Vector2(shakeX, shakeY) * shakeLimit;

        focusPlayerCursor.anchoredPosition = focusCurrentPos + visualOffset;

        // 5. Validation
        float distanceToTarget = Vector2.Distance(focusPlayerCursor.anchoredPosition, focusTargetRing.anchoredPosition);
        float distanceToMercy = Vector2.Distance(focusPlayerCursor.anchoredPosition, focusMercyRing.anchoredPosition);

        float tRingW = focusTargetRing.rect.width > 10f ? focusTargetRing.rect.width : 100f;
        float mRingW = focusMercyRing.rect.width > 10f ? focusMercyRing.rect.width : 300f;

        float targetRadius = tRingW / 2f;
        float mercyRadius = mRingW / 2f;

        float fillRate = currentData.minigameProgressFillRate > 0 ? currentData.minigameProgressFillRate : 0.4f;
        float mercyRate = currentData.focusMercyFillRate > 0 ? currentData.focusMercyFillRate : 0.15f;
        float drainRate = currentData.minigameProgressDrainRate > 0 ? currentData.minigameProgressDrainRate : 0.2f;

        if (distanceToTarget <= targetRadius)
            successProgress += fillRate * Time.deltaTime;
        else if (distanceToMercy <= mercyRadius)
            successProgress += mercyRate * Time.deltaTime;
        else
            successProgress -= drainRate * Time.deltaTime;
    }
}