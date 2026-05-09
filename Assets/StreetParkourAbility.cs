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
    [SerializeField] private WeaponLoadout weaponLoadout;

    [Header("Wall Detection")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private float wallCheckDistance = 1.1f;
    [SerializeField] private float minWallFacingDot = 0.15f;
    [SerializeField] private float wallProbeRadius = 0.2f;

    [Header("Climb Tuning")]
    [SerializeField] private float maxClimbDuration = 2.2f;
    [SerializeField] private float wallClimbSpeed = 7.15f;
    [SerializeField] private float wallSlideSpeed = -1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float wallStickForce = 3f;
    [SerializeField] private float wallJumpUpForce = 7.5f;
    [SerializeField] private float wallJumpAwayForce = 5.5f;
    [SerializeField] private float reattachDelay = 0.2f;
    [SerializeField] private KeyCode climbKey = KeyCode.LeftShift;
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
    private bool abilityLockActive;
    private RaycastHit currentWallHit;
    private Ability[] cachedAbilities;

    public bool IsWallClimbing => isWallClimbing;
    public bool IsMovementOverridden => isWallClimbing || externalVelocityActive;
    public override string AbilityDisplayName => "Street Parkour";
    public override string AbilityBindingLabel => "Shift Near Wall";
    public override string AbilityHudIconPath => @"C:/Users/Admin/Pictures/street.jpeg";

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

        if (weaponLoadout == null)
        {
            weaponLoadout = GetComponent<WeaponLoadout>();
        }

        cachedAbilities = GetComponents<Ability>();
        AbilityHudOverlay.EnsureFor(gameObject);
        cooldown = Mathf.Max(0f, cooldown);
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

        bool hasWall = TryGetWall(moveInput, out RaycastHit wallHit);
        if (hasWall)
        {
            currentWallHit = wallHit;
        }

        bool isHoldingClimb = Input.GetKey(climbKey);

        if (!isWallClimbing && isHoldingClimb && hasWall && CanUseWall(wallHit))
        {
            TryUse();
        }

        if (isWallClimbing)
        {
            if (!isHoldingClimb)
            {
                StopWallClimb();
            }
            else if (hasWall)
            {
                ContinueWallClimb();
            }
            else if (!TryVaultOverLedge())
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
            UpdateAbilityLock();
        }

        UpdateAbilityLock();
    }

    public override bool CanUse()
    {
        return !isWallClimbing && !externalVelocityActive && Time.time >= lastUseTime + cooldown && climbTimeRemaining > 0f && reattachTimer <= 0f;
    }

    protected override void Activate()
    {
        StartWallClimb();
    }

    private void StartWallClimb()
    {
        if (!CanUse())
        {
            return;
        }

        isWallClimbing = true;
        lastUseTime = Time.time;
        externalVelocityActive = false;
        ContinueWallClimb();
    }

    private void ContinueWallClimb()
    {
        if (climbTimeRemaining <= 0f)
        {
            StopWallClimb();
            return;
        }

        climbTimeRemaining = Mathf.Max(0f, climbTimeRemaining - Time.deltaTime);
        externalVelocityActive = false;

        velocity.x = 0f;
        velocity.z = 0f;
        velocity.y = wallClimbSpeed;
        velocity += -currentWallHit.normal * wallStickForce * Time.deltaTime;
    }

    private bool CanUseWall(RaycastHit wallHit)
    {
        if (climbTimeRemaining <= 0f || reattachTimer > 0f)
        {
            return false;
        }

        Vector3 facingDirection = visualRoot != null ? visualRoot.forward : transform.forward;
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.001f)
        {
            facingDirection = transform.forward;
        }

        facingDirection.Normalize();
        float facingWall = Vector3.Dot(facingDirection, -wallHit.normal);
        return facingWall >= minWallFacingDot;
    }

    private bool TryGetWall(Vector2 moveInput, out RaycastHit wallHit)
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(controller.height * 0.5f, 1f);
        Vector3[] probeDirections = BuildProbeDirections(moveInput);
        float probeRadius = Mathf.Max(0.01f, wallProbeRadius);

        for (int i = 0; i < probeDirections.Length; i++)
        {
            Vector3 direction = probeDirections[i];
            if (direction.sqrMagnitude <= 0.001f)
            {
                continue;
            }

            if (Physics.SphereCast(origin, probeRadius, direction, out wallHit, wallCheckDistance, climbableLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
        }

        wallHit = default;
        return false;
    }

    private Vector3[] BuildProbeDirections(Vector2 moveInput)
    {
        Vector3 facingForward = visualRoot != null ? visualRoot.forward : transform.forward;
        Vector3 facingRight = visualRoot != null ? visualRoot.right : transform.right;
        facingForward.y = 0f;
        facingRight.y = 0f;

        if (facingForward.sqrMagnitude <= 0.001f)
        {
            facingForward = transform.forward;
        }

        if (facingRight.sqrMagnitude <= 0.001f)
        {
            facingRight = transform.right;
        }

        facingForward.Normalize();
        facingRight.Normalize();

        Vector3 moveDirection = ((facingForward * moveInput.y) + (facingRight * moveInput.x));
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            moveDirection.Normalize();
        }

        return new[]
        {
            moveDirection,
            facingForward,
            -facingForward,
            facingRight,
            -facingRight
        };
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
        UpdateAbilityLock();
    }

    private void UpdateAbilityLock()
    {
        bool shouldLockAbilities = isWallClimbing || externalVelocityActive;
        if (abilityLockActive == shouldLockAbilities)
        {
            return;
        }

        abilityLockActive = shouldLockAbilities;
        weaponLoadout?.SetLoadoutLocked(shouldLockAbilities);

        if (cachedAbilities == null)
        {
            cachedAbilities = GetComponents<Ability>();
        }

        for (int i = 0; i < cachedAbilities.Length; i++)
        {
            Ability ability = cachedAbilities[i];
            if (ability == null || ability == this)
            {
                continue;
            }

            ability.enabled = !shouldLockAbilities;
        }
    }

    private void OnDisable()
    {
        isWallClimbing = false;
        externalVelocityActive = false;
        abilityLockActive = false;
        weaponLoadout?.SetLoadoutLocked(false);

        if (cachedAbilities == null)
        {
            return;
        }

        for (int i = 0; i < cachedAbilities.Length; i++)
        {
            Ability ability = cachedAbilities[i];
            if (ability == null || ability == this)
            {
                continue;
            }

            ability.enabled = true;
        }
    }
}
