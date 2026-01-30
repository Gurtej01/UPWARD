using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles ONLY player movement and jumping. Nothing else.
/// </summary>
public class PlayerController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement Settings")]
    [Tooltip("Force applied when moving left/right")]
    [SerializeField] private float moveSpeed = 50f;

    [Tooltip("Maximum horizontal velocity in m/s")]
    [SerializeField] private float maxSpeed = 4f;

    [Tooltip("Air control as percentage of ground speed")]
    [SerializeField] private float airControlMultiplier = 0.5f;

    [Header("Jump Settings")]
    [Tooltip("Desired jump height in meters")]
    [SerializeField] private float jumpHeight = 2f;

    [Header("Ground Detection")]
    [Tooltip("Distance to check below player for ground")]
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Tooltip("What layers count as ground")]
    [SerializeField] private LayerMask groundLayer;
    #endregion

    #region Private Fields
    private Rigidbody rb;
    private float horizontalInput = 0f;
    private bool jumpPressed = false;
    private float jumpForce;
    private bool isGrounded = false;
    private Vector2 joystickInput = Vector2.zero;
    private bool canMove = true; // Controlled externally (e.g., by KnockbackHandler)
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        jumpForce = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
    }

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        if (canMove)
        {
            ApplyMovement();
            ClampVelocity();
        }

        HandleJump();
    }
    #endregion

    #region Input System
    public void OnMove(InputAction.CallbackContext ctx)
    {
        joystickInput = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) jumpPressed = true;
    }
    #endregion

    #region Private Methods
    private void HandleInput()
    {
        // Keyboard input
        float keyboard = 0f;
        if (Input.GetKey(KeyCode.D)) keyboard = 1f;
        if (Input.GetKey(KeyCode.A)) keyboard = -1f;

        // Combine keyboard + joystick
        horizontalInput = Mathf.Clamp(keyboard + joystickInput.x, -1f, 1f);

        // Keyboard jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpPressed = true;
        }
    }

    private void ApplyMovement()
    {
        rb.AddForce(Vector3.right * horizontalInput * moveSpeed);
    }

    private void ClampVelocity()
    {
        Vector3 clampedVelocity = rb.linearVelocity;

        float effectiveMaxSpeed = isGrounded ? maxSpeed : maxSpeed * airControlMultiplier;
        clampedVelocity.x = Mathf.Clamp(clampedVelocity.x, -effectiveMaxSpeed, effectiveMaxSpeed);

        rb.linearVelocity = clampedVelocity;
    }

    private void HandleJump()
    {
        if (jumpPressed && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpPressed = false;
        }
    }

    private bool CheckGrounded()
    {
        Vector3 rayOrigin = transform.position;
        bool hit = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance, groundLayer);
        return hit;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Enable or disable player movement (e.g., during stun)
    /// </summary>
    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
    }

    /// <summary>
    /// Check if player is currently grounded
    /// </summary>
    public bool IsGrounded()
    {
        return isGrounded;
    }
    #endregion
}