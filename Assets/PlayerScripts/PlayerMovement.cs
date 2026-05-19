using UnityEngine;
using Mirror;

public enum MovementState { Walking, Sprinting, Crouching, Sliding, Airborne }

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Component Links")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform headMount;
    public PlayerHealth playerHealth;

    // THE FIX: Links the new Status Effect System
    private StatusEffectManager statusManager;

    [Header("Input Locks")]
    [HideInInspector] public bool baseJumpLocked = false;
    [HideInInspector] public bool baseCameraLocked = false;
    [HideInInspector] public bool baseMovementLocked = false;

    // THE FIX: These properties gracefully combine the UI minigame locks with physical Stuns
    public bool isJumpLocked
    {
        get => baseJumpLocked || (statusManager != null && statusManager.isStunned);
        set => baseJumpLocked = value;
    }
    public bool isCameraLocked
    {
        get => baseCameraLocked || (statusManager != null && statusManager.isStunned);
        set => baseCameraLocked = value;
    }
    public bool isMovementLocked
    {
        get => baseMovementLocked || (statusManager != null && statusManager.isStunned);
        set => baseMovementLocked = value;
    }

    [Header("Speed Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 9f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float speedAcceleration = 5f;

    [Header("Movement Physics")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundAcceleration = 15f;
    [SerializeField] private float groundDeceleration = 10f;
    [SerializeField] private float airControl = 2f;
    [SerializeField] private float gravityMultiplier = 2f;
    [SerializeField] private float jumpForce = 6f;

    [Header("B-Hop Settings")]
    [SerializeField] private float bHopWindow = 0.15f;
    [SerializeField] private float minSpeedForBhop = 6f;
    [SerializeField] private float bHopSpeedMultiplier = 0.05f;

    [Header("Advanced Slide Settings")]
    [SerializeField] private float slideStartBoost = 1.3f;
    [SerializeField] private float baseSlideFriction = 15f;
    [SerializeField] private AnimationCurve slideFrictionCurve = AnimationCurve.EaseInOut(0, 0.2f, 1, 2f);
    [SerializeField] private float slideMinSpeed = 2f;
    [SerializeField] private float maxSlideTime = 1.5f;
    [SerializeField] private float slideCooldown = 0.5f;
    [SerializeField] private float minSprintTimeToSlide = 0.2f;
    [Range(0f, 10f)][SerializeField] private float slideSteeringControl = 4f;

    [Header("Dynamic Slope Settings")]
    [SerializeField] private float minSlopeAngle = 4f;
    [SerializeField] private float downhillAcceleration = 25f;
    [SerializeField] private float uphillPenalty = 20f;
    [SerializeField] private float maxDownhillSpeed = 35f;
    [SerializeField] private float maxSlopeAngleAllowed = 50f;
    [SerializeField] private float groundStickForce = 2f;

    [Header("Hitbox Settings")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    public MovementState CurrentState { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float SlideTimeElapsed { get; private set; }
    public float CurrentSlopeAngle { get; private set; }
    public float SlopeMultiplier { get; private set; }
    public float NormalizedSlideTime => Mathf.Clamp01(SlideTimeElapsed / maxSlideTime);

    [Header("Network Sync")]
    [SyncVar] public MovementState syncedState;
    public float NetworkSpeed { get; private set; }
    public MovementState ActiveState => isLocalPlayer ? CurrentState : syncedState;

    private Vector3 lastPosition;
    private float sprintTimer;
    private float lastSlideTime;
    private float lastLandedTime;
    private float currentTargetSpeed;
    private float momentumCap;
    private Vector3 moveVelocity;
    private float verticalVelocity;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private bool isSlidingDownhill;
    private float lastPushTime = 0f;

    void Awake()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        statusManager = GetComponent<StatusEffectManager>();

        currentTargetSpeed = walkSpeed;
    }

    void Start() { lastPosition = transform.position; }

    [Server]
    public void ServerTeleport(Vector3 position)
    {
        RpcTeleport(position);
    }

    [ClientRpc]
    private void RpcTeleport(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
    }

    void Update()
    {
        if (playerHealth != null && playerHealth.isDead) return;

        if (isLocalPlayer)
        {
            NetworkSpeed = new Vector3(moveVelocity.x, 0, moveVelocity.z).magnitude;
            if (CurrentState != syncedState) CmdSetState(CurrentState);
        }
        else
        {
            Vector3 flatCurrent = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatLast = new Vector3(lastPosition.x, 0, lastPosition.z);
            NetworkSpeed = Vector3.Distance(flatCurrent, flatLast) / Time.deltaTime;
            lastPosition = transform.position;
        }

        ExecuteHitboxAndCamera();
    }

    [Command]
    private void CmdSetState(MovementState st) { syncedState = st; }

    public void ProcessMovement(PlayerInputData input)
    {
        if (!isLocalPlayer) return;
        if (playerHealth != null && playerHealth.isDead) return;

        if (isMovementLocked)
        {
            input.moveInput = Vector2.zero;
            input.jumpPressed = false;
            input.sprintHeld = false;
            input.crouchHeld = false;
            input.crouchPressed = false;
        }

        CheckGround();
        DetermineState(input);
        ExecuteMovement(input);

        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        CurrentSpeed = horizontalVelocity.magnitude;
    }

    private void CheckGround()
    {
        bool wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;

        if (Physics.SphereCast(transform.position + controller.center, controller.radius, Vector3.down, out RaycastHit hit, (controller.height / 2f) + 0.3f, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            CurrentSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            if (!isGrounded && wasGrounded && verticalVelocity <= 0) isGrounded = true;
        }
        else
        {
            groundNormal = Vector3.up;
            CurrentSlopeAngle = 0f;
        }

        if (!wasGrounded && isGrounded) lastLandedTime = Time.time;
        if (isGrounded && verticalVelocity < 0) verticalVelocity = -groundStickForce;
    }

    private void DetermineState(PlayerInputData input)
    {
        if (!isGrounded) { CurrentState = MovementState.Airborne; return; }

        if (CurrentState == MovementState.Sliding)
        {
            if (input.jumpPressed && !isJumpLocked) { Jump(); StopSlide(); return; }
            if (!input.crouchHeld) { StopSlide(); return; }

            if (!isSlidingDownhill)
            {
                SlideTimeElapsed += Time.deltaTime;
                if (SlideTimeElapsed >= maxSlideTime || CurrentSpeed < slideMinSpeed) StopSlide();
            }
            return;
        }

        if (input.crouchPressed && CurrentState == MovementState.Sprinting && sprintTimer >= minSprintTimeToSlide && Time.time >= lastSlideTime + slideCooldown)
        {
            StartSlide();
            return;
        }

        if (input.sprintHeld && input.moveInput.magnitude > 0.1f && !input.crouchHeld)
        {
            CurrentState = MovementState.Sprinting;
            sprintTimer += Time.deltaTime;
        }
        else if (input.crouchHeld)
        {
            CurrentState = MovementState.Crouching;
            sprintTimer = 0f;
        }
        else
        {
            CurrentState = MovementState.Walking;
            sprintTimer = 0f;
        }

        if (input.jumpPressed && CurrentState != MovementState.Crouching && !isJumpLocked) Jump();
    }

    private void StartSlide()
    {
        CurrentState = MovementState.Sliding;
        SlideTimeElapsed = 0f;
        isSlidingDownhill = false;
        Vector3 slideDir = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        moveVelocity = slideDir * CurrentSpeed * slideStartBoost;
    }

    private void StopSlide() { CurrentState = MovementState.Crouching; lastSlideTime = Time.time; isSlidingDownhill = false; }

    private void Jump()
    {
        verticalVelocity = jumpForce;
        CurrentState = MovementState.Airborne;
        float timeSinceLanded = Time.time - lastLandedTime;
        bool isBHop = timeSinceLanded <= bHopWindow;

        if (isBHop && CurrentSpeed > minSpeedForBhop)
        {
            float speedExcess = CurrentSpeed - minSpeedForBhop;
            float dynamicBoost = 1f + (speedExcess * bHopSpeedMultiplier);
            moveVelocity.x *= dynamicBoost;
            moveVelocity.z *= dynamicBoost;
        }
        momentumCap = Mathf.Max(CurrentSpeed, moveVelocity.magnitude);
    }

    private void ExecuteMovement(PlayerInputData input)
    {
        Vector3 targetDir = (transform.right * input.moveInput.x + transform.forward * input.moveInput.y).normalized;

        if (CurrentState == MovementState.Sliding)
        {
            float currentSlideSpeed = moveVelocity.magnitude;
            if (targetDir.magnitude > 0.1f)
            {
                Vector3 desiredDir = Vector3.Slerp(moveVelocity.normalized, targetDir, slideSteeringControl * Time.deltaTime);
                moveVelocity = desiredDir * currentSlideSpeed;
            }

            isSlidingDownhill = false;
            SlopeMultiplier = 0f;

            if (CurrentSlopeAngle > minSlopeAngle)
            {
                Vector3 slopeDownDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                float slopeDot = Vector3.Dot(moveVelocity.normalized, slopeDownDir);
                SlopeMultiplier = slopeDot;
                float slopeSeverity = Mathf.Clamp01((CurrentSlopeAngle - minSlopeAngle) / (maxSlopeAngleAllowed - minSlopeAngle));

                if (slopeDot > 0.1f)
                {
                    isSlidingDownhill = true;
                    float dynamicBoost = downhillAcceleration * slopeSeverity * slopeDot;
                    moveVelocity += slopeDownDir * dynamicBoost * Time.deltaTime;
                    if (moveVelocity.magnitude > maxDownhillSpeed) moveVelocity = Vector3.ClampMagnitude(moveVelocity, maxDownhillSpeed);
                }
                else if (slopeDot < -0.1f)
                {
                    float dynamicPenalty = uphillPenalty * slopeSeverity * Mathf.Abs(slopeDot);
                    moveVelocity = Vector3.MoveTowards(moveVelocity, Vector3.zero, dynamicPenalty * Time.deltaTime);
                }
            }

            if (!isSlidingDownhill)
            {
                float currentFriction = baseSlideFriction * slideFrictionCurve.Evaluate(NormalizedSlideTime);
                moveVelocity = Vector3.MoveTowards(moveVelocity, Vector3.zero, currentFriction * Time.deltaTime);
            }
        }
        else if (CurrentState == MovementState.Airborne)
        {
            moveVelocity += targetDir * airControl * Time.deltaTime;
            if (moveVelocity.magnitude > momentumCap) moveVelocity = Vector3.ClampMagnitude(moveVelocity, momentumCap);
        }
        else
        {
            // THE FIX: Multiply goal speed mathematically by the dynamic Debuff Status
            float speedMult = statusManager != null ? statusManager.currentSpeedMultiplier : 1f;

            float goalSpeed = walkSpeed * speedMult;
            if (CurrentState == MovementState.Sprinting) goalSpeed = sprintSpeed * speedMult;
            if (CurrentState == MovementState.Crouching) goalSpeed = crouchSpeed * speedMult;

            currentTargetSpeed = Mathf.MoveTowards(currentTargetSpeed, goalSpeed, speedAcceleration * Time.deltaTime);
            Vector3 targetVelocity = targetDir * currentTargetSpeed;
            float accel = (targetDir.magnitude > 0) ? groundAcceleration : groundDeceleration;
            moveVelocity = Vector3.Lerp(moveVelocity, targetVelocity, accel * Time.deltaTime);
        }

        verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        CurrentVelocity = moveVelocity + Vector3.up * verticalVelocity;
        controller.Move(CurrentVelocity * Time.deltaTime);
    }

    private void ExecuteHitboxAndCamera()
    {
        bool wantsToCrouch = ActiveState == MovementState.Crouching || ActiveState == MovementState.Sliding;

        if (isLocalPlayer && !wantsToCrouch && !HasHeadroom()) wantsToCrouch = true;

        float targetHeight = wantsToCrouch ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        controller.center = new Vector3(0, controller.height / 2f, 0);

        if (headMount != null)
        {
            float headY = Mathf.Lerp(headMount.localPosition.y, controller.height - 0.2f, crouchTransitionSpeed * Time.deltaTime);
            headMount.localPosition = new Vector3(0, headY, 0);
        }
    }

    private bool HasHeadroom() { return !Physics.SphereCast(transform.position + controller.center, controller.radius, Vector3.up, out _, standHeight - controller.height, groundMask, QueryTriggerInteraction.Ignore); }

    [Command]
    private void CmdPushRigidbody(NetworkIdentity target, Vector3 pushDir, float playerSpeed)
    {
        if (target != null)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                float pushForce = Mathf.Max(playerSpeed, 3f) * (rb.mass * 0.8f);
                rb.AddForce(pushDir * pushForce, ForceMode.Impulse);

                Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                float maxSpeed = Mathf.Max(playerSpeed * 1.2f, 4f);

                if (flatVel.magnitude > maxSpeed)
                {
                    flatVel = flatVel.normalized * maxSpeed;
                    rb.linearVelocity = new Vector3(flatVel.x, rb.linearVelocity.y, flatVel.z);
                }
            }
        }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isLocalPlayer) return;
        if (playerHealth != null && playerHealth.isDead) return;

        if (ActiveState == MovementState.Sliding && hit.normal.y < 0.1f)
        {
            moveVelocity = Vector3.zero; StopSlide();
        }

        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic || hit.moveDirection.y < -0.3f) return;

        if (body.transform.root.GetComponentInChildren<PlayerHealth>() != null) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z).normalized;
        if (pushDir == Vector3.zero) return;

        NetworkIdentity netId = body.GetComponent<NetworkIdentity>();

        if (netId != null)
        {
            if (Time.time - lastPushTime > 0.05f)
            {
                CmdPushRigidbody(netId, pushDir, CurrentSpeed);
                lastPushTime = Time.time;
            }

            if (hit.normal.y < 0.5f && isGrounded)
            {
                verticalVelocity = -10f;
            }
        }
        else
        {
            float effectiveSpeed = Mathf.Max(CurrentSpeed, 2f);
            Vector3 force = pushDir * (effectiveSpeed * 0.5f);
            body.AddForceAtPosition(force, hit.point, ForceMode.Impulse);

            Vector3 flatVel = new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);
            if (flatVel.magnitude > effectiveSpeed)
            {
                flatVel = flatVel.normalized * effectiveSpeed;
                body.linearVelocity = new Vector3(flatVel.x, body.linearVelocity.y, flatVel.z);
            }
        }
    }
}