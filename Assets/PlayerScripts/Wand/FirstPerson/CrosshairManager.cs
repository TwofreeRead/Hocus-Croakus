using UnityEngine;
using UnityEngine.UI;

public class CrosshairManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform topBracket;
    [SerializeField] private RectTransform bottomBracket;
    [SerializeField] private RectTransform leftBracket;
    [SerializeField] private RectTransform rightBracket;
    [SerializeField] private Image centerDot;
    [SerializeField] private Image[] dynamicParts;

    [Header("Animation Speeds")]
    [SerializeField] private float returnSpeed = 15f;
    [SerializeField] private float colorLerpSpeed = 10f;
    [SerializeField] private float baseGravitySpinSpeed = 150f;
    [SerializeField] private float maxChargeSpinSpeed = 700f;

    private float currentSpread = 0f;
    private float targetSpread = 0f;
    private float recoilSpread = 0f;

    private Color currentColor = Color.white;
    private Color targetColor = Color.white;

    private float currentRotation = 0f;
    private bool isSpinning = false;
    private float targetSpinSpeed = 0f;
    private float currentSpinSpeed = 0f;

    private float currentDotAlpha = 1f;
    private float currentBracketAlpha = 1f;

    void Update()
    {
        // 1. Calculate Spread
        recoilSpread = Mathf.Lerp(recoilSpread, 0f, returnSpeed * Time.deltaTime);
        currentSpread = Mathf.Lerp(currentSpread, targetSpread, returnSpeed * Time.deltaTime);
        float finalSpread = currentSpread + recoilSpread;

        // 2. Calculate Rotation
        currentSpinSpeed = Mathf.Lerp(currentSpinSpeed, targetSpinSpeed, returnSpeed * Time.deltaTime);

        if (isSpinning) currentRotation -= currentSpinSpeed * Time.deltaTime;
        else currentRotation = Mathf.LerpAngle(currentRotation, 0f, returnSpeed * Time.deltaTime);

        Quaternion rotationQuat = Quaternion.Euler(0, 0, currentRotation);

        // 3. Apply Orbital Positioning & Rotation
        if (topBracket != null) { topBracket.anchoredPosition = rotationQuat * Vector2.up * finalSpread; topBracket.localRotation = rotationQuat; }
        if (bottomBracket != null) { bottomBracket.anchoredPosition = rotationQuat * Vector2.down * finalSpread; bottomBracket.localRotation = rotationQuat; }
        if (leftBracket != null) { leftBracket.anchoredPosition = rotationQuat * Vector2.left * finalSpread; leftBracket.localRotation = rotationQuat; }
        if (rightBracket != null) { rightBracket.anchoredPosition = rotationQuat * Vector2.right * finalSpread; rightBracket.localRotation = rotationQuat; }

        // 4. Calculate Dynamic Fading & Colors
        currentColor = Color.Lerp(currentColor, targetColor, colorLerpSpeed * Time.deltaTime);

        float targetDotAlpha = isSpinning ? 0f : 1f;
        float targetBracketAlpha = (finalSpread < 5f && !isSpinning) ? 0f : 1f;

        currentDotAlpha = Mathf.Lerp(currentDotAlpha, targetDotAlpha, colorLerpSpeed * Time.deltaTime);
        currentBracketAlpha = Mathf.Lerp(currentBracketAlpha, targetBracketAlpha, colorLerpSpeed * Time.deltaTime);

        // THE FIX: Explicitly color the center dot so it never gets missed by non-hosts
        if (centerDot != null)
        {
            Color dotC = currentColor;
            dotC.a = currentDotAlpha;
            centerDot.color = dotC;
        }

        foreach (Image img in dynamicParts)
        {
            if (img != null && img != centerDot)
            {
                Color c = currentColor;
                c.a = currentBracketAlpha;
                img.color = c;
            }
        }
    }

    public void UpdateCrosshairState(float baseSpread, float movementSpread, Color color, bool isGrabbing, float chargeRatio)
    {
        targetSpread = baseSpread + movementSpread;
        targetColor = color;
        isSpinning = isGrabbing;
        targetSpinSpeed = Mathf.Lerp(baseGravitySpinSpeed, maxChargeSpinSpeed, chargeRatio);
    }

    public void ApplyRecoilPunch(float punchAmount)
    {
        recoilSpread += punchAmount;
    }
}