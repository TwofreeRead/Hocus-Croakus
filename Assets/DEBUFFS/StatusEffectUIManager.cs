using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class StatusEffectUIManager : MonoBehaviour
{
    [Header("Debuff Icons")]
    [Tooltip("The Prefab representing a single debuff icon")]
    public GameObject effectIconPrefab;
    [Tooltip("The container holding the Vertical Layout Group")]
    public Transform iconContainer;

    [Header("Custom Shaders & Screen Effects")]
    [Tooltip("A full-screen, transparent UI Image used to apply Custom Shader Materials (e.g., toxic vision).")]
    public Image fullScreenOverlayImage;

    private Dictionary<uint, StatusEffectUIItem> activeIcons = new Dictionary<uint, StatusEffectUIItem>();

    public void AddEffect(StatusEffectData data, float duration, uint instanceId)
    {
        if (activeIcons.ContainsKey(instanceId)) return;

        GameObject newIcon = Instantiate(effectIconPrefab, iconContainer);
        StatusEffectUIItem itemScript = newIcon.GetComponent<StatusEffectUIItem>();

        itemScript.Setup(data, duration);
        activeIcons.Add(instanceId, itemScript);
    }

    public void RemoveEffect(uint instanceId)
    {
        if (activeIcons.TryGetValue(instanceId, out StatusEffectUIItem item))
        {
            if (item != null && item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
            activeIcons.Remove(instanceId);
        }
    }

    // --- CUSTOM SHADER INTEGRATION ---
    public void ApplyScreenMaterial(Material customMaterial)
    {
        if (fullScreenOverlayImage == null) return;
        fullScreenOverlayImage.material = customMaterial;
        fullScreenOverlayImage.enabled = true;
    }

    public void ClearScreenMaterial()
    {
        if (fullScreenOverlayImage == null) return;
        fullScreenOverlayImage.material = null;
        fullScreenOverlayImage.enabled = false;
    }
}