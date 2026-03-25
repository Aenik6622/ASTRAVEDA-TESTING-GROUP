using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 6f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    public float inputDeadzone = 0.1f;

    private CharacterController controller;
    private StreetParkourAbility streetParkourAbility;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        streetParkourAbility = GetComponent<StreetParkourAbility>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;

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
        controller.Move(move * speed * Time.deltaTime);

        if (streetParkourAbility != null && streetParkourAbility.IsMovementOverridden)
        {
            velocity.y = 0f;
            return;
        }

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
