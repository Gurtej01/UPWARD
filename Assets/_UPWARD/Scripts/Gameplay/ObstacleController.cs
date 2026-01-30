using Unity.Android.Gradle.Manifest;
using UnityEngine;

/// <summary>
/// Simple obstacle that moves in a given direction. Direction set by spawner.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ObstacleController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement Settings")]
    [Tooltip("Force applied continuously when on ground")]
    [SerializeField] private float groundMovementForce = 2f;

    [Tooltip("Gravity multiplier (1 = normal, <1 = floaty, >1 = heavy)")]
    [SerializeField] private float gravityMultiplier = 0.05f;

    [Header("Ground Detection")]
    [Tooltip("Distance to raycast for ground detection")]
    [SerializeField] private float groundCheckDistance = 1f;

    [Tooltip("Layers considered as ground")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Destruction")]
    [Tooltip("Y position below which obstacle destroys itself")]
    [SerializeField] private float destroyHeight = -5f;
    #endregion

    #region Private Fields
    private Rigidbody rb;
    private Vector3 movementDirection;
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        CheckDestruction();
    }

    void FixedUpdate()
    {
        ApplyCustomGravity();
        MoveOnGround();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Set the horizontal movement direction for this obstacle
    /// </summary>
    public void SetMovementDirection(Vector3 direction)
    {
        movementDirection = direction.normalized;
    }

    /// <summary>
    /// Apply initial push force (called by spawner)
    /// </summary>
    public void ApplyInitialPush(float force)
    {
        if (movementDirection == Vector3.zero)
        {
            Debug.LogWarning("Movement direction not set! Call SetMovementDirection first!");
            return;
        }

        rb.AddForce(movementDirection * force, ForceMode.Impulse);
    }
    #endregion

    #region Private Methods
    private void ApplyCustomGravity()
    {
        // Cancel default gravity and apply custom amount
        rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
    }

    private void MoveOnGround()
    {
        if (IsGrounded() && movementDirection != Vector3.zero)
        {
            rb.AddForce(movementDirection * groundMovementForce);
        }
    }

    private bool IsGrounded()
    {
        Vector3 rayOrigin = transform.position;
        bool hit = Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance, groundLayer);
        return hit;
    }

    private void CheckDestruction()
    {
        if (transform.position.y <= destroyHeight)
        {
            Destroy(gameObject);
        }
    }
    #endregion
}