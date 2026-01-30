using UnityEngine;

/// <summary>
/// Handles collision with obstacles and applies knockback forces
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerController))]
public class KnockbackHandler : MonoBehaviour
{
    #region Serialized Fields
    [Header("Knockback Settings")]
    [Tooltip("Base knockback force magnitude")]
    [SerializeField] private float knockForce = 10f;

    [Tooltip("Duration of stun after being hit")]
    [SerializeField] private float stunDuration = 0.5f;

    [Header("Grounded Knockback")]
    [Tooltip("Upward force multiplier when hit on ground")]
    [SerializeField] private float groundedUpwardMultiplier = 2f;

    [Tooltip("Horizontal force multiplier when hit on ground")]
    [SerializeField] private float groundedHorizontalMultiplier = 0.5f;

    [Header("Airborne Knockback")]
    [Tooltip("Upward force multiplier when hit in air")]
    [SerializeField] private float airborneUpwardMultiplier = 0.5f;

    [Tooltip("Horizontal force multiplier when hit in air")]
    [SerializeField] private float airborneHorizontalMultiplier = 2f;
    #endregion

    #region Private Fields
    private Rigidbody rb;
    private PlayerController playerController;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle")) return;

        ApplyKnockback(collision.transform.position);
        ApplyStun();
    }
    #endregion

    #region Private Methods
    private void ApplyKnockback(Vector3 obstaclePosition)
    {
        // Calculate direction away from obstacle
        Vector3 knockbackDirection = (transform.position - obstaclePosition).normalized;

        bool grounded = playerController.IsGrounded();

        if (grounded)
        {
            // Grounded: Strong upward bounce, slight horizontal push (funny bounce!)
            Vector3 upwardForce = Vector3.up * knockForce * groundedUpwardMultiplier;
            Vector3 horizontalForce = knockbackDirection * knockForce * groundedHorizontalMultiplier;

            rb.AddForce(upwardForce, ForceMode.Impulse);
            rb.AddForce(horizontalForce, ForceMode.Impulse);
        }
        else
        {
            // Airborne: Strong horizontal push, slight upward lift
            Vector3 upwardForce = Vector3.up * knockForce * airborneUpwardMultiplier;
            Vector3 horizontalForce = knockbackDirection * knockForce * airborneHorizontalMultiplier;

            rb.AddForce(upwardForce, ForceMode.Impulse);
            rb.AddForce(horizontalForce, ForceMode.Impulse);
        }
    }

    private void ApplyStun()
    {
        playerController.SetCanMove(false);
        Invoke(nameof(ClearStun), stunDuration);
    }

    private void ClearStun()
    {
        playerController.SetCanMove(true);
    }
    #endregion
}