using UnityEngine;
using Mirror;
using UnityEngine.Rendering;

public class PlayerSetup : NetworkBehaviour
{
    [Header("Local Senses")]
    [SerializeField] private Camera fpCamera;
    [SerializeField] private AudioListener audioListener;

    [Header("Visual Isolation")]
    [SerializeField] private GameObject fpWandRoot;
    [SerializeField] private SkinnedMeshRenderer tpFrogRenderer;

    void Start()
    {
        if (!isLocalPlayer)
        {
            if (fpCamera != null) Destroy(fpCamera.gameObject);
            if (audioListener != null) Destroy(audioListener);

            if (fpWandRoot != null)
            {
                MeshRenderer[] fpRenderers = fpWandRoot.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer r in fpRenderers)
                {
                    r.enabled = false;
                }
            }

            if (tpFrogRenderer != null)
            {
                tpFrogRenderer.enabled = true;
                tpFrogRenderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (fpCamera != null) fpCamera.gameObject.SetActive(true);
        if (audioListener != null) audioListener.enabled = true;

        if (fpWandRoot != null)
        {
            MeshRenderer[] fpRenderers = fpWandRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in fpRenderers)
            {
                r.enabled = true;
            }
        }

        if (tpFrogRenderer != null)
        {
            tpFrogRenderer.enabled = true;
            tpFrogRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    // Death State Control
    public void DisableFPWand()
    {
        if (fpWandRoot != null) fpWandRoot.SetActive(false);
    }

    // Respawn State Control
    public void EnableFPWand()
    {
        if (fpWandRoot != null) fpWandRoot.SetActive(true);
    }
}