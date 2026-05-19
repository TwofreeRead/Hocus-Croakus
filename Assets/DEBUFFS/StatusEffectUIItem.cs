using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusEffectUIItem : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI timerText;

    private float timeRemaining;

    public void Setup(StatusEffectData data, float duration)
    {
        if (data.effectIcon != null) iconImage.sprite = data.effectIcon;
        timeRemaining = duration;
        UpdateText();
    }

    void Update()
    {
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            UpdateText();
        }
    }

    private void UpdateText()
    {
        timerText.text = Mathf.CeilToInt(Mathf.Max(0, timeRemaining)).ToString() + "s";
    }
}