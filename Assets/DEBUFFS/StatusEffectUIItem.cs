using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectUIItem : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag the Image component for the Debuff Icon here.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Drag the TextMeshPro component for the timer here.")]
    [SerializeField] private TextMeshProUGUI timerText;

    private float timeRemaining;
    private bool isSetup = false;

    public void Setup(StatusEffectData data, float duration)
    {
        // 1. Failsafe for missing Data
        if (data == null)
        {
            Debug.LogError($"<color=red><b>[UI ERROR]</b></color> StatusEffectUIItem was told to Setup, but the StatusEffectData was NULL!");
            return;
        }

        // 2. Failsafe for missing Image Reference
        if (iconImage == null)
        {
            Debug.LogError($"<color=red><b>[UI ERROR]</b></color> The 'iconImage' slot is EMPTY on the {gameObject.name} Prefab! Open the prefab and drag the Image component into the slot.");
        }
        else if (data.effectIcon != null)
        {
            iconImage.sprite = data.effectIcon;
        }

        // 3. Failsafe for missing Text Reference
        if (timerText == null)
        {
            Debug.LogError($"<color=red><b>[UI ERROR]</b></color> The 'timerText' slot is EMPTY on the {gameObject.name} Prefab! Open the prefab and drag the TextMeshPro component into the slot.");
        }

        timeRemaining = duration;
        isSetup = true;
        UpdateText();
    }

    void Update()
    {
        if (isSetup && timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            UpdateText();
        }
    }

    private void UpdateText()
    {
        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(Mathf.Max(0, timeRemaining)).ToString() + "s";
        }
    }
}