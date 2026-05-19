using UnityEngine;

[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Hazards/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [Header("UI Display")]
    public string effectName = "New Debuff";
    public Sprite effectIcon;
    public float baseDuration = 5f;
    [Tooltip("How long after the effect is applied before it can be applied again. (e.g., 5s effect + 5s immunity = 10s total cooldown)")]
    public float immunityDuration = 0f;

    [Header("Instant Effects (Triggered Once)")]
    public int instantDamage = 0;
    public int instantHeal = 0;

    [Header("Movement & Control")]
    public bool isStun = false;
    [Range(0.1f, 3f)] public float speedMultiplier = 1f;

    [Header("Over-Time Effects (Ticks 1/sec)")]
    public int poisonDamagePerSecond = 0;
    public int healPerSecond = 0;
    public int manaDrainPerSecond = 0;
    public int manaRegenPerSecond = 0;

    [Header("Mechanic Locks")]
    public bool disableHealing = false;

    [Header("UI Color Overrides")]
    public bool overrideHealthBarColor = false;
    public Color newHealthBarColor = Color.green;
    [Space(5)]
    public bool overrideManaBarColor = false;
    public Color newManaBarColor = Color.cyan;

    [Header("Screen Effects & Custom Shaders")]
    [Tooltip("A custom material (e.g., hallucination, toxic screen) applied to the player's camera overlay.")]
    public Material customScreenMaterial;
    [Range(0f, 1f)] public float blurIntensity = 0f;
    [Range(0f, 1f)] public float hallucinationIntensity = 0f;
}