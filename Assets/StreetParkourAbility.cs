using UnityEngine;

[AddComponentMenu("Abilities/Street Parkour Ability")]
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class StreetParkourAbility : Ability
{
    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform visualRoot;

    [Header("Wall Detection")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private float wallCheckDistance = 1.1f;
    [SerializeField] private float minWallApproachDot = 0.1f;

    [Header("Climb Tuning")]
    [SerializeField] private float maxClimbDuration = 2.2f;
    [SerializeField] private float wallClimbSpeed = 7.15f;
    [SerializeField] private float wallSlideSpeed = -1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float wallStickForce = 3f;
    [SerializeField] private float wallJumpUpForce = 7.5f;
    [SerializeField] private float wallJumpAwayForce = 5.5f;
    [SerializeField] private float reattachDelay = 0.2f;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Vault Tuning")]
    [SerializeField] private float vaultCheckHeight = 1.1f;
    [SerializeField] private float vaultForwardCheck = 0.8f;
    [SerializeField] private float vaultDropCheck = 2f;
    [SerializeField] private float vaultForwardForce = 5.5f;
    [SerializeField] private float vaultUpForce = 4.5f;

    private Vector3 velocity;
    private float climbTimeRemaining;
    private float reattachTimer;
    private bool isWallClimbing;
    private bool externalVelocityActive;
    private RaycastHit currentWallHit;

    public bool IsWallClimbing => isWallClimbing;
    public bool IsMovementOverridden => isWallClimbing || externalVelocityActive;

    protected void Awake()
    {
        if (owner == null)
        {
            owner = GetComponent<BaseCharacter>();
        }

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        cooldown = 0f;
        climbTimeRemaining = maxClimbDuration;
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (controller.isGrounded)
        {
            if (velocity.y < 0f)
            {
                velocity.y = -2f;
            }

            climbTimeRemaining = maxClimbDuration;
        }

        reattachTimer = Mathf.Max(0f, reattachTimer - Time.deltaTime);

        bool hasWall = TryGetWall(moveInput, out currentWallHit);
        bool wantsClimb = moveInput.sqrMagnitude > 0.01f && hasWall && CanUseWall(moveInput, currentWallHit);

        if (wantsClimb)
        {
            TryUse();
        }
        else if (isWallClimbing)
        {
            if (!TryVaultOverLedge())
            {
                StopWallClimb();
            }
        }

        if (isWallClimbing && Input.GetKeyDown(jumpKey))
        {
            PerformWallJump();
        }

        if (!isWallClimbing && externalVelocityActive)
        {
            velocity.y += gravity * Time.deltaTime;
        }

        if (isWallClimbing || externalVelocityActive)
        {
            controller.Move(velocity * Time.deltaTime);
        }

        if (externalVelocityActive && controller.isGrounded && velocity.y <= 0f)
        {
            externalVelocityActive = false;
            velocity = Vector3.zero;
        }
    }

    public override bool CanUse()
    {
        return climbTimeRemaining > 0f && reattachTimer <= 0f;
    }

    protected override void Activate()
    {
        isWallClimbing = true;
        climbTimeRemaining = Mathf.Max(0f, climbTimeRemaining - Time.deltaTime);
        externalVelocityActive = false;

        velocity.x = 0f;
        velocity.z = 0f;
        velocity.y = wallClimbSpeed;
        velocity += -currentWallHit.normal * wallStickForce * Time.deltaTime;
    }

    private bool CanUseWall(Vector2 moveInput, RaycastHit wallHit)
    {
        if (controller.isGrounded || climbTimeRemaining <= 0f || reattachTimer > 0f)
        {
            return false;
        }

        Vector3 moveDirection = GetMoveDirection(moveInput);
        float intoWall = Vector3.Dot(moveDirection, -wallHit.normal);
        return intoWall >= minWallApproachDot;
    }

    private bool TryGetWall(Vector2 moveInput, out RaycastHit wallHit)
    {
        Vector3 direction = GetMoveDirection(moveInput);
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = visualRoot.forward;
        }

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(controller.height * 0.5f, 1f);
        return Physics.Raycast(origin, direction, out wallHit, wallCheckDistance, climbableLayers, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetMoveDirection(Vector2 moveInput)
    {
        Vector3 forward = visualRoot.forward;
        Vector3 right = visualRoot.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        return ((forward * moveInput.y) + (right * moveInput.x)).normalized;
    }

    private void PerformWallJump()
    {
        Vector3 jumpDirection = (currentWallHit.normal + Vector3.up * 1.15f).normalized;
        velocity.x = jumpDirection.x * wallJumpAwayForce;
        velocity.z = jumpDirection.z * wallJumpAwayForce;
        velocity.y = wallJumpUpForce;
        externalVelocityActive = true;
        reattachTimer = reattachDelay;
        StopWallClimb();
    }

    private bool TryVaultOverLedge()
    {
        Vector3 forward = -currentWallHit.normal;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = visualRoot.forward;
        }

        forward.Normalize();

        Vector3 topProbeOrigin = transform.position + (Vector3.up * vaultCheckHeight);
        if (Physics.Raycast(topProbeOrigin, forward, wallCheckDistance, climbableLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Vector3 landingProbeOrigin = topProbeOrigin + (forward * vaultForwardCheck);
        if (!Physics.Raycast(landingProbeOrigin, Vector3.down, out RaycastHit landingHit, vaultDropCheck, climbableLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        velocity = (forward * vaultForwardForce) + (Vector3.up * vaultUpForce);
        externalVelocityActive = true;
        reattachTimer = reattachDelay;
        isWallClimbing = false;
        return true;
    }

    private void StopWallClimb()
    {
        isWallClimbing = false;
        velocity.y = Mathf.Max(velocity.y, wallSlideSpeed);
    }
}
