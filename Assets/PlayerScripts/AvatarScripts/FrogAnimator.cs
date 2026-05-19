using UnityEngine;
using FIMSpace.FProceduralAnimation;
using FIMSpace.FLook;
using Mirror;

public class FrogAnimator : NetworkBehaviour
{
    [Header("Core References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private NetworkAnimator networkAnimator;

    [Header("FImpossible Integrations")]
    [SerializeField] private LegsAnimator legsAnimator;
    [SerializeField] private FLookAnimator lookAnimator;
    [SerializeField] private Transform lookTarget;
    public RagdollAnimator2 myRagdoll;

    [Header("Legs Animator Settings")]
    [SerializeField] private float slideEnterBlendSpeed = 15f;
    [SerializeField] private float slideRecoverBlendSpeed = 40f;

    [Header("Base Layer State Names")]
    [SerializeField] private string idleAnim = "Idle";
    [SerializeField] private string crouchAnim = "Crouch";
    [SerializeField] private string slideAnim = "Slide";

    [Header("Upper Body State Names")]
    [SerializeField] private string upperIdleAnim = "Idle";
    [SerializeField] private string shoot1Anim = "Shoot1";
    [SerializeField] private string shoot2Anim = "Shoot2";
    [SerializeField] private string reloadAnim = "Reload";
    [SerializeField] private string grabAnim = "Grab";
    [SerializeField] private float combatCrossfade = 0.05f;

    [Header("Head & Arm Tracking")]
    [SerializeField] private Transform headMount;
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform rightArmShoulder;
    [SerializeField] private Transform leftArmShoulder;
    [SerializeField] private float maxLookUpAngle = 60f;
    [SerializeField] private float maxLookDownAngle = 50f;

    [Header("Mode Switch Wag Settings")]
    [SerializeField] private float wagAmount = 15f;
    [SerializeField] private float wagFrequency = 35f;
    [SerializeField] private float wagDecaySpeed = 8f;

    public bool IsCurrentlyReloading => currentUpperState == reloadAnim;

    private string currentUpperState;
    private float targetLegsBlend = 1f;
    private float combatActionTimer = 0f;
    private bool isUpperBodyIdle = true;
    private float currentRightArmWeight = 1f;
    private float currentLeftArmWeight = 0f;
    private float armWobblePhase = 0f;
    private float currentWagIntensity = 0f;
    private float wagPhase = 0f;

    [SyncVar] public float syncedPitch;
    [SyncVar] public int syncedWandMode;
    [SyncVar] public bool syncedIsGrabbing;
    [SyncVar] public float syncedChargeRatio;
    [SyncVar] public bool syncedIsHarvesting;

    private float lastSentPitch;
    private float smoothedPitch;
    private float pitchVelocity;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (networkAnimator == null) networkAnimator = GetComponent<NetworkAnimator>();
    }

    void Update()
    {
        if (playerMovement == null || animator == null || !animator.enabled) return;

        if (isLocalPlayer)
        {
            HandleBaseAnimationStates();
            ProcessNetworkPitch();
        }

        HandleUpperBodyStates();
        HandleLegsAnimator();
        HandleDynamicArmBlending();
        HandleVirtualLookTarget();
    }

    void LateUpdate()
    {
        if (animator == null || !animator.enabled) return;
        HandleDirectArmTracking();
    }

    [Command] private void CmdSyncPitch(float pitch) { syncedPitch = pitch; }
    [Command] public void CmdSyncWandMode(int mode) { syncedWandMode = mode; }
    [Command] public void CmdSyncGrabbing(bool grabbing) { syncedIsGrabbing = grabbing; }
    [Command] public void CmdSyncCharge(float charge) { syncedChargeRatio = charge; }
    [Command] public void CmdSyncHarvesting(bool harvesting) { syncedIsHarvesting = harvesting; }

    private float GetExactLocalPitch()
    {
        if (headMount == null) return 0f;
        float p = headMount.localEulerAngles.x;
        if (p > 180f) p -= 360f;
        return Mathf.Clamp(p, -maxLookUpAngle, maxLookDownAngle);
    }

    private void ProcessNetworkPitch()
    {
        float exact = GetExactLocalPitch();
        if (Mathf.Abs(exact - lastSentPitch) > 1f)
        {
            CmdSyncPitch(exact);
            lastSentPitch = exact;
        }
    }

    private void HandleBaseAnimationStates()
    {
        MovementState state = playerMovement.CurrentState;
        animator.SetBool("IsCrouching", state == MovementState.Crouching);
        animator.SetBool("IsSliding", state == MovementState.Sliding);
    }

    private void HandleLegsAnimator()
    {
        if (legsAnimator == null) return;
        MovementState state = isLocalPlayer ? playerMovement.CurrentState : playerMovement.syncedState;
        bool isGrounded = state != MovementState.Airborne;
        bool isSliding = state == MovementState.Sliding;

        legsAnimator.User_SetIsGrounded(isGrounded);

        if (isSliding || !isGrounded)
        {
            targetLegsBlend = 0f;
            legsAnimator.LegsAnimatorBlend = Mathf.MoveTowards(legsAnimator.LegsAnimatorBlend, targetLegsBlend, slideEnterBlendSpeed * Time.deltaTime);
        }
        else
        {
            targetLegsBlend = 1f;
            legsAnimator.LegsAnimatorBlend = Mathf.MoveTowards(legsAnimator.LegsAnimatorBlend, targetLegsBlend, slideRecoverBlendSpeed * Time.deltaTime);
        }
    }

    private void HandleDynamicArmBlending()
    {
        if (myRagdoll == null || myRagdoll.Handler == null) return;
        var rightArmChain = myRagdoll.Handler.GetChain(ERagdollChainType.RightArm);
        var leftArmChain = myRagdoll.Handler.GetChain(ERagdollChainType.LeftArm);
        if (rightArmChain == null || leftArmChain == null) return;

        bool isActionActive = (combatActionTimer > 0f) || syncedIsGrabbing || syncedIsHarvesting;
        float targetRightBlend = 1f;
        float targetLeftBlend = 1f;

        if (isActionActive || IsCurrentlyReloading)
        {
            if (syncedWandMode == 1 || IsCurrentlyReloading) { targetRightBlend = 0f; targetLeftBlend = 0f; }
            else { targetRightBlend = 0f; targetLeftBlend = 1f; }
        }

        rightArmChain.ChainBlend = Mathf.MoveTowards(rightArmChain.ChainBlend, targetRightBlend, 10f * Time.deltaTime);
        leftArmChain.ChainBlend = Mathf.MoveTowards(leftArmChain.ChainBlend, targetLeftBlend, 10f * Time.deltaTime);
    }

    private void SafePlayUpperAnimation(string stateName, float transitionTime)
    {
        if (currentUpperState == stateName) return;
        if (animator.HasState(1, Animator.StringToHash(stateName)))
        {
            animator.CrossFadeInFixedTime(stateName, transitionTime, 1);
            currentUpperState = stateName;
        }
    }

    private void HandleUpperBodyStates()
    {
        if (syncedIsGrabbing || syncedIsHarvesting)
        {
            SafePlayUpperAnimation(grabAnim, combatCrossfade);
            isUpperBodyIdle = false;
        }
        else if (combatActionTimer > 0f) combatActionTimer -= Time.deltaTime;
        else if (!isUpperBodyIdle)
        {
            SafePlayUpperAnimation(upperIdleAnim, 0.2f);
            isUpperBodyIdle = true;
        }
    }

    public void TriggerShoot(int modeIndex)
    {
        if (animator == null || !animator.enabled) return;
        string targetShootAnim = (modeIndex == 1) ? shoot2Anim : shoot1Anim;

        if (modeIndex != 1 && currentUpperState == targetShootAnim)
        {
            if (animator.HasState(1, Animator.StringToHash(targetShootAnim))) animator.Play(targetShootAnim, 1, 0f);
        }
        else SafePlayUpperAnimation(targetShootAnim, 0.05f);

        if (networkAnimator != null) networkAnimator.SetTrigger(targetShootAnim);
        isUpperBodyIdle = false;
        combatActionTimer = (modeIndex == 1) ? 0.2f : 0.4f;
        if (isLocalPlayer) CmdSyncWandMode(modeIndex);
    }

    public void TriggerReload()
    {
        if (animator == null || !animator.enabled) return;
        if (currentUpperState == reloadAnim) animator.Play(reloadAnim, 1, 0f);
        else SafePlayUpperAnimation(reloadAnim, 0.1f);

        if (networkAnimator != null) networkAnimator.SetTrigger(reloadAnim);
        isUpperBodyIdle = false;
        combatActionTimer = 2.0f;
    }

    public void TriggerModeSwitchWag()
    {
        if (animator == null || !animator.enabled) return;
        currentWagIntensity = 1f;
        wagPhase = 0f;
    }

    private void HandleVirtualLookTarget()
    {
        if (lookTarget == null || headBone == null) return;

        if (isLocalPlayer && Camera.main != null)
        {
            lookTarget.position = headBone.position + (Camera.main.transform.forward * 10f);
        }
        else
        {
            smoothedPitch = Mathf.SmoothDamp(smoothedPitch, syncedPitch, ref pitchVelocity, 0.05f);
            Quaternion pitchRotation = Quaternion.Euler(smoothedPitch, transform.eulerAngles.y, 0f);
            lookTarget.position = headBone.position + (pitchRotation * Vector3.forward * 10f);
        }
    }

    private void HandleDirectArmTracking()
    {
        bool isReloading = IsCurrentlyReloading;
        bool isFiringMode2 = (syncedWandMode == 1 && combatActionTimer > 0f);

        float targetRightWeight = isReloading ? 0f : 1f;
        float targetLeftWeight = (isFiringMode2 && !isReloading) ? 1f : 0f;

        currentRightArmWeight = Mathf.MoveTowards(currentRightArmWeight, targetRightWeight, 15f * Time.deltaTime);
        currentLeftArmWeight = Mathf.MoveTowards(currentLeftArmWeight, targetLeftWeight, 15f * Time.deltaTime);

        if (rightArmShoulder != null && currentRightArmWeight > 0f)
        {
            float pitchToApply = isLocalPlayer ? GetExactLocalPitch() : smoothedPitch;
            rightArmShoulder.Rotate(transform.right, pitchToApply * currentRightArmWeight, Space.World);

            if (syncedIsGrabbing || syncedIsHarvesting)
            {
                // Fake max charge wobble if harvesting
                float wobbleAmount = Mathf.Lerp(2f, 10f, syncedIsHarvesting ? 1f : syncedChargeRatio);
                float wobbleSpeed = Mathf.Lerp(5f, 15f, syncedIsHarvesting ? 1f : syncedChargeRatio);

                armWobblePhase += wobbleSpeed * Time.deltaTime;
                float waveX = Mathf.Sin(armWobblePhase) * wobbleAmount;
                float waveY = Mathf.Cos(armWobblePhase) * wobbleAmount;

                rightArmShoulder.Rotate(transform.up, waveX * currentRightArmWeight, Space.World);
                rightArmShoulder.Rotate(transform.forward, waveY * currentRightArmWeight, Space.World);
            }
            else armWobblePhase = 0f;

            if (currentWagIntensity > 0.01f)
            {
                wagPhase += Time.deltaTime * wagFrequency;
                currentWagIntensity = Mathf.Lerp(currentWagIntensity, 0f, Time.deltaTime * wagDecaySpeed);
                float activeWagAngle = Mathf.Sin(wagPhase) * currentWagIntensity * wagAmount;
                rightArmShoulder.Rotate(transform.forward, activeWagAngle * currentRightArmWeight, Space.World);
            }
        }

        if (leftArmShoulder != null && currentLeftArmWeight > 0f)
        {
            float pitchToApply = isLocalPlayer ? GetExactLocalPitch() : smoothedPitch;
            leftArmShoulder.Rotate(transform.right, pitchToApply * currentLeftArmWeight, Space.World);
        }
    }
}