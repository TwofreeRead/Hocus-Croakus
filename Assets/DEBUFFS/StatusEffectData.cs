using UnityEngine;
using UnityEngine.Rendering; // Necesario para los Volume Profiles (URP/HDRP)

[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Hazards/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [Header("UI Display")]
    public string effectName = "New Debuff";
    public Sprite effectIcon;
    public float baseDuration = 5f;
    public float immunityDuration = 0f;

    [Header("Audio Feedback")]
    public AudioClip onAppliedSound;
    public AudioClip onRemovedSound;
    [Tooltip("Played if the player tries to move/shoot while stunned by this effect.")]
    public AudioClip actionBlockedSound;

    [Header("Instant Effects (Triggered Once)")]
    public int instantDamage = 0;
    public int instantHeal = 0;

    [Header("Movement & Control")]
    public bool isStun = false;
    [Range(0.1f, 3f)] public float speedMultiplier = 1f;
    public Color stunFlashColor = new Color(1f, 0f, 0f, 0.4f);

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

    [Header("Screen Effects (Dual Support)")]
    [Tooltip("Standard 2D UI Image overlay (e.g., frozen screen border, blood splatter).")]
    public Sprite customUIOverlaySprite;
    [Tooltip("Custom UI Material Overlay (e.g., simple transparent material).")]
    public Material customScreenMaterial;

    [Header("Post-Processing Volumes")]
    [Tooltip("Assign a URP/HDRP Volume Profile to apply real post-processing (Vignette, Chromatic Aberration, etc.)")]
    public VolumeProfile postProcessVolume;
}