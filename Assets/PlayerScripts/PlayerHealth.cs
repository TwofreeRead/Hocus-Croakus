using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using FIMSpace.FProceduralAnimation;
using FIMSpace.FLook;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;

    [SyncVar(hook = nameof(OnHealthChanged))]
    public int currentHealth;

    [SyncVar]
    public bool isDead = false;

    [Header("Component References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private WandController wandController;
    [SerializeField] private PlayerSetup playerSetup;
    [SerializeField] private CrosshairManager crosshairManager;

    [Header("FImpossible Animation Integrations")]
    [SerializeField] private Animator animator;
    [SerializeField] private LegsAnimator legsAnimator;
    [SerializeField] private FLookAnimator lookAnimator;
    [SerializeField] private RagdollAnimator2 ragdollAnimator;
    [SerializeField] private SkinnedMeshRenderer tpFrogRenderer;
    [SerializeField] private FrogAnimator frogAnimator;

    [Header("Death Physics")]
    [SerializeField] private float deathImpactForce = 40f;

    [Header("Debug UI")]
    public bool showDebug = true;

    private Material[] originalMaterials;
    private List<Transform> defaultKinematicBones = new List<Transform>();

    // THE FIX: Centralized timer variables to handle rapid overlapping hits
    private float flinchTimer = 0f;
    private bool isFlinching = false;
    private List<Rigidbody> flinchingBones = new List<Rigidbody>();

    void Start()
    {
        if (tpFrogRenderer != null)
        {
            tpFrogRenderer.updateWhenOffscreen = true;
        }

        StartCoroutine(InitRagdollSelfCollision());
    }

    private IEnumerator InitRagdollSelfCollision()
    {
        yield return new WaitForEndOfFrame();

        if (ragdollAnimator != null && ragdollAnimator.Handler != null)
        {
            var dummyColliders = ragdollAnimator.Handler.User_GetAllDummyColliders();
            var dummyRbs = ragdollAnimator.Handler.User_GetDummyRigidbodies();
            Collider myCc = GetComponent<Collider>();

            if (dummyColliders != null)
            {
                foreach (Collider dummy in dummyColliders)
                {
                    if (dummy != null && myCc != null) Physics.IgnoreCollision(myCc, dummy);
                }
            }

            if (dummyRbs != null)
            {
                foreach (Rigidbody rb in dummyRbs)
                {
                    if (rb != null && rb.isKinematic)
                    {
                        defaultKinematicBones.Add(rb.transform);
                    }
                }
            }
        }
    }

    void Update()
    {
        // THE FIX: The timer smoothly decrements. If you get hit again, it just jumps back to 0.7s without breaking.
        if (isFlinching && !isDead)
        {
            flinchTimer -= Time.deltaTime;
            if (flinchTimer <= 0f)
            {
                isFlinching = false;
                foreach (Rigidbody rb in flinchingBones)
                {
                    if (rb != null) rb.isKinematic = true;
                }
                flinchingBones.Clear();
            }
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
        isDead = false;
    }

    [Server]
    public void ServerTakeDamage(int damageAmount, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth -= damageAmount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            RpcTriggerDeath(hitDirection);
            StartCoroutine(ServerRespawnRoutine());
        }
    }

    [ClientRpc]
    public void RpcApplyImpact(Vector3 forceVector)
    {
        if (isDead || ragdollAnimator == null || ragdollAnimator.Handler == null) return;

        // Refresh the hit timer seamlessly
        flinchTimer = 0.9f;

        var dummyRbs = ragdollAnimator.Handler.User_GetDummyRigidbodies();
        if (dummyRbs != null)
        {
            foreach (Rigidbody rb in dummyRbs)
            {
                if (rb != null)
                {
                    if (!isFlinching && rb.isKinematic)
                    {
                        string boneName = rb.gameObject.name.ToLower();
                        bool isLeg = boneName.Contains("leg") || boneName.Contains("thigh") || boneName.Contains("calf") || boneName.Contains("knee") || boneName.Contains("foot");

                        if (!isLeg)
                        {
                            rb.isKinematic = false;
                            flinchingBones.Add(rb);
                        }
                    }
                    rb.AddForce(forceVector, ForceMode.Impulse);
                }
            }
        }

        isFlinching = true;
    }

    private void OnHealthChanged(int oldHealth, int newHealth) { }

    [ClientRpc]
    private void RpcTriggerDeath(Vector3 hitDirection)
    {
        if (showDebug) Debug.Log("Player Died. Triggering Ragdoll.");

        if (playerMovement != null) playerMovement.enabled = false;
        if (wandController != null) wandController.enabled = false;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (isLocalPlayer)
        {
            if (playerSetup != null) playerSetup.DisableFPWand();
            if (crosshairManager != null) crosshairManager.gameObject.SetActive(false);
        }

        if (animator != null) animator.enabled = false;
        if (legsAnimator != null) legsAnimator.enabled = false;
        if (lookAnimator != null) lookAnimator.enabled = false;
        if (frogAnimator != null) frogAnimator.enabled = false;

        StartCoroutine(ApplyDeathImpactAndFade(hitDirection));
    }

    private IEnumerator ApplyDeathImpactAndFade(Vector3 hitDir)
    {
        if (ragdollAnimator != null) ragdollAnimator.enabled = true;

        yield return null;

        if (ragdollAnimator != null && ragdollAnimator.Handler != null)
        {
            ragdollAnimator.User_SwitchFallState();
            ragdollAnimator.Handler.User_OverrideMusclesPower = 0.3f;

            ERagdollChainType[] limbChains = {
                ERagdollChainType.RightArm, ERagdollChainType.LeftArm,
                ERagdollChainType.RightLeg, ERagdollChainType.LeftLeg,
                ERagdollChainType.Core
            };

            foreach (var chainType in limbChains)
            {
                var chain = ragdollAnimator.Handler.GetChain(chainType);
                if (chain != null) chain.ChainBlend = 1f;
            }

            var dummyRbs = ragdollAnimator.Handler.User_GetDummyRigidbodies();

            if (dummyRbs != null)
            {
                foreach (Rigidbody rb in dummyRbs)
                {
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.AddForce((hitDir + (Vector3.up * 0.75f)).normalized * deathImpactForce, ForceMode.Impulse);
                    }
                }
            }
        }

        yield return new WaitForSeconds(7f);

        if (tpFrogRenderer != null)
        {
            originalMaterials = tpFrogRenderer.sharedMaterials;
            Material[] fadeMaterials = new Material[originalMaterials.Length];

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                fadeMaterials[i] = new Material(originalMaterials[i]);
                Material mat = fadeMaterials[i];

                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            tpFrogRenderer.materials = fadeMaterials;

            float timer = 0f;
            float fadeDuration = 3f;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

                foreach (Material mat in tpFrogRenderer.materials)
                {
                    Color c = Color.white;
                    if (mat.HasProperty("_BaseColor")) c = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color")) c = mat.GetColor("_Color");

                    c.a = currentAlpha;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                }
                yield return null;
            }
        }
    }

    [Server]
    private IEnumerator ServerRespawnRoutine()
    {
        yield return new WaitForSeconds(10f);

        currentHealth = maxHealth;
        if (wandController != null) wandController.ServerResetEnergy();

        Vector3 spawnPos = new Vector3(0, 5, 0);
        if (playerMovement != null)
        {
            if (NetworkManager.startPositions != null && NetworkManager.startPositions.Count > 0)
            {
                int randomIndex = Random.Range(0, NetworkManager.startPositions.Count);
                Transform randomSpawn = NetworkManager.startPositions[randomIndex];
                if (randomSpawn != null) spawnPos = randomSpawn.position;
            }
            playerMovement.ServerTeleport(spawnPos);
        }

        isDead = false;
        RpcRespawn(spawnPos);
    }

    [ClientRpc]
    private void RpcRespawn(Vector3 spawnPos)
    {
        StartCoroutine(LocalRespawnSequence(spawnPos));
    }

    private IEnumerator LocalRespawnSequence(Vector3 spawnPos)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = spawnPos;
        if (cc != null) cc.enabled = true;

        // Reset the flinch states fully upon respawn
        isFlinching = false;
        flinchingBones.Clear();

        if (ragdollAnimator != null) ragdollAnimator.enabled = false;

        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        yield return new WaitForEndOfFrame();

        if (ragdollAnimator != null && ragdollAnimator.Handler != null)
        {
            var dummyRbs = ragdollAnimator.Handler.User_GetDummyRigidbodies();
            if (dummyRbs != null)
            {
                foreach (Rigidbody rb in dummyRbs)
                {
                    if (rb != null)
                    {
                        rb.position = spawnPos;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = defaultKinematicBones.Contains(rb.transform);
                    }
                }
            }

            ragdollAnimator.enabled = true;
            ragdollAnimator.User_TransitionToStandingMode();
            ragdollAnimator.Handler.User_OverrideMusclesPower = 0.7f;

            var leftLeg = ragdollAnimator.Handler.GetChain(ERagdollChainType.LeftLeg);
            var rightLeg = ragdollAnimator.Handler.GetChain(ERagdollChainType.RightLeg);

            if (leftLeg != null) leftLeg.ChainBlend = 0f;
            if (rightLeg != null) rightLeg.ChainBlend = 0f;
        }

        if (legsAnimator != null) legsAnimator.enabled = true;
        if (lookAnimator != null) lookAnimator.enabled = true;
        if (frogAnimator != null) frogAnimator.enabled = true;

        if (tpFrogRenderer != null && originalMaterials != null)
        {
            tpFrogRenderer.materials = originalMaterials;
        }

        if (playerMovement != null) playerMovement.enabled = true;
        if (wandController != null) wandController.enabled = true;

        if (isLocalPlayer)
        {
            if (playerSetup != null) playerSetup.EnableFPWand();
            if (crosshairManager != null) crosshairManager.gameObject.SetActive(true);
        }
    }

    void OnGUI()
    {
        if (!isLocalPlayer || !showDebug) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;

        if (isDead)
        {
            style.normal.textColor = Color.red;
            style.fontSize = 40;
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2, 400, 50), "YOU DIED", style);
        }
        else
        {
            style.normal.textColor = Color.red;
            GUI.Label(new Rect(20, Screen.height - 110, 400, 30), $"Health: {currentHealth} / {maxHealth}", style);
        }
    }
}