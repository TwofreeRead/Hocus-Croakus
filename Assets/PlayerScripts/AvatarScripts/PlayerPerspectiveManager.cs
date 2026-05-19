using UnityEngine;

public class PlayerPerspectiveManager : MonoBehaviour
{
    [Header("Network State")]
    [Tooltip("Dummy toggle. True = This is your local client. False = A remote player over the network.")]
    public bool isLocalPlayer = true;

    [Header("Hierarchy References")]
    [Tooltip("The root object containing the FP camera, FP wand, and FP arms.")]
    [SerializeField] private GameObject fpRoot;

    [Tooltip("The root object containing the actual Frog 3D model and TP armature.")]
    [SerializeField] private GameObject tpFrogRoot;

    void Start()
    {
        ApplyPerspectiveSetup();
    }

    private void ApplyPerspectiveSetup()
    {
        if (isLocalPlayer)
        {
            // 1. Local Player: Activate FP view
            if (fpRoot != null) fpRoot.SetActive(true);

            // 2. Local Player: Hide TP model from local camera
            if (tpFrogRoot != null)
            {
                int tpLayer = LayerMask.NameToLayer("LocalPlayerTP");
                if (tpLayer == -1)
                {
                    Debug.LogError("CRITICAL: Layer 'LocalPlayerTP' does not exist! Go to Edit -> Settings -> Tags and Layers and add it.");
                    return;
                }

                SetLayerRecursively(tpFrogRoot, tpLayer);
            }
        }
        else
        {
            // 1. Remote Player: Destroy/Disable their FP view so you don't see floating guns inside their head
            if (fpRoot != null) fpRoot.SetActive(false);

            // 2. Remote Player: Ensure their TP model is on the standard Default/ThirdPerson layer so you CAN see them
            if (tpFrogRoot != null)
            {
                int defaultLayer = LayerMask.NameToLayer("Default");
                SetLayerRecursively(tpFrogRoot, defaultLayer);
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}