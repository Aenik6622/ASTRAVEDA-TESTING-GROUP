using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 6f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    public float inputDeadzone = 0.1f;

    private CharacterController controller;
    private BaseCharacter baseCharacter;
    private StreetParkourAbility streetParkourAbility;
    private DadaKaRaajUltimateAbility dadaKaRaajUltimateAbility;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        baseCharacter = GetComponent<BaseCharacter>();
        streetParkourAbility = GetComponent<StreetParkourAbility>();
        dadaKaRaajUltimateAbility = GetComponent<DadaKaRaajUltimateAbility>();
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(x) < inputDeadzone)
        {
            x = 0f;
        }

        if (Mathf.Abs(z) < inputDeadzone)
        {
            z = 0f;
        }

        Vector3 move = transform.right * x + transform.forward * z;

        bool movementOverridden =
            (streetParkourAbility != null && streetParkourAbility.IsMovementOverridden) ||
            (dadaKaRaajUltimateAbility != null && dadaKaRaajUltimateAbility.IsMovementOverridden);

        if (movementOverridden)
        {
            velocity.y = 0f;
            return;
        }

        if (controller == null || !controller.enabled)
        {
            return;
        }

        isGrounded = controller.isGrounded;

        float finalSpeed = speed * (baseCharacter != null ? baseCharacter.CurrentMovementMultiplier : 1f);
        controller.Move(move * finalSpeed * Time.deltaTime);

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
