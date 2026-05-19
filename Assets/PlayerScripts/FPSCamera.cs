using UnityEngine;
using Mirror;

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

    // THE FIX: Feeds the dynamic SyncVar values straight to your custom image effect shader uniforms!
    private void HandleShaderUpdates()
    {
        if (statusEffectManager == null || cam == null) return;

        // If you are using standard materials or custom scripts, update their shader uniforms right here:
        // Shader.SetGlobalFloat("_BlurAmount", statusEffectManager.currentBlurAmount);
        // Shader.SetGlobalFloat("_HallucinationIntensity", statusEffectManager.currentHallucinationAmount);
    }
}