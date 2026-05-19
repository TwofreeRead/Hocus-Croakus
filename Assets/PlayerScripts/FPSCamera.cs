using UnityEngine;
using Mirror;
using UnityEngine.Rendering; // Required for Volume Profiles

public class FPSCamera : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform headMount;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Camera cam;

    private PlayerHealth playerHealth;
    private StatusEffectManager statusEffectManager;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    private float xRotation = 0f;

    [Header("FOV Settings")]
    [SerializeField] private float baseFOV = 85f;
    [SerializeField] private float fovSmoothTime = 0.15f;
    [SerializeField] private float speedToFOVRatio = 1.2f;

    private float fovVelocity;

    [Header("Volume / Post Processing")]
    [Tooltip("Drag the object that has the Volume component here (usually the Camera).")]
    [SerializeField] private Volume statusEffectVolume;

    [Tooltip("Your normal gameplay Volume Profile. The script reverts to this when no debuffs are active.")]
    [SerializeField] private VolumeProfile defaultProfile;

    void Start()
    {
        if (!isLocalPlayer)
        {
            if (cam != null) cam.gameObject.SetActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        if (cam == null) cam = GetComponent<Camera>();

        AudioListener[] allListeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (AudioListener listener in allListeners)
        {
            if (listener.gameObject != cam.gameObject)
            {
                listener.enabled = false;
            }
        }

        if (cam.GetComponent<AudioListener>() == null)
        {
            cam.gameObject.AddComponent<AudioListener>();
        }

        playerHealth = GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        statusEffectManager = GetComponentInParent<StatusEffectManager>();
        if (statusEffectManager == null) statusEffectManager = GetComponent<StatusEffectManager>();

        // Fallback: If you forgot to assign the volume in the inspector, it tries to find or add one
        if (statusEffectVolume == null)
        {
            statusEffectVolume = cam.GetComponent<Volume>();
            if (statusEffectVolume == null)
            {
                statusEffectVolume = cam.gameObject.AddComponent<Volume>();
            }
        }

        statusEffectVolume.isGlobal = true;
        statusEffectVolume.weight = 1f;

        // Ensure we start with the normal game look
        if (defaultProfile != null)
        {
            statusEffectVolume.profile = defaultProfile;
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (playerHealth != null && playerHealth.isDead) return;

        HandleLook();
        HandleFOV();
        HandleShaderUpdates();
    }

    private void HandleLook()
    {
        if (playerMovement != null && playerMovement.isCameraLocked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (headMount != null)
        {
            headMount.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        playerBody.Rotate(Vector3.up * mouseX);
    }

    private void HandleFOV()
    {
        float targetFOV = baseFOV;

        if (playerMovement != null && playerMovement.CurrentSpeed > 5f)
        {
            float excessSpeed = playerMovement.CurrentSpeed - 5f;
            targetFOV += excessSpeed * speedToFOVRatio;
        }

        if (cam != null)
        {
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFOV, ref fovVelocity, fovSmoothTime);
        }
    }

    private void HandleShaderUpdates()
    {
        if (statusEffectManager == null || statusEffectVolume == null) return;

        int volIndex = statusEffectManager.activeVolumeProfileIndex;

        // Apply debuff effect profile if active and valid
        if (volIndex >= 0 && volIndex < statusEffectManager.effectDatabase.Length)
        {
            var effectProfile = statusEffectManager.effectDatabase[volIndex].postProcessVolume;
            if (effectProfile != null)
            {
                statusEffectVolume.profile = effectProfile;
            }
        }
        else
        {
            // No debuff active, return to the default profile
            statusEffectVolume.profile = defaultProfile;
        }
    }
}