using System.Net.NetworkInformation;
using UnityEngine;

/// <summary>
/// Handles player movement and jumping with physics-based controls
/// </summary>
public class PlayerController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement Settings")]
    [Tooltip("Force applied when moving left/right")]
    [SerializeField] private float moveSpeed = 50f;

    [Tooltip("Maximum horizontal velocity in m/s")]
    [SerializeField] private float maxSpeed = 4f;

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
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Calculate jump force from desired height
        jumpForce = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
    }

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        ApplyMovement();
        ClampVelocity();
        HandleJump();
    }
    #endregion

    #region Private Methods
    private void HandleInput()
    {
        // Reset input
        horizontalInput = 0f;

        // Read horizontal movement
        if (Input.GetKey(KeyCode.D))
        {
            horizontalInput = 1f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            horizontalInput = -1f;
        }

        // Read jump input
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
        if (isGrounded)
        {
            clampedVelocity.x = Mathf.Clamp(clampedVelocity.x, -maxSpeed, maxSpeed);
        }
        else 
        {
            clampedVelocity.x = Mathf.Clamp(clampedVelocity.x, -maxSpeed * 0.09f, maxSpeed * 0.09f);
        }
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
        float rayDistance = groundCheckDistance;
        Vector3 rayOrigin = transform.position;


        bool hit = Physics.Raycast(rayOrigin, Vector3.down, rayDistance, groundLayer);

        return hit;
    }
    #endregion
}