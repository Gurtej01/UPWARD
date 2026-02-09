using UnityEngine;

/// <summary>
/// Reads player state and triggers animations using triggers
/// </summary>
public class CharacterAnimationController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Animator animator;

    private bool wasGrounded = true;

    void Update()
    {
        if (playerController == null || animator == null) return;

        // Check player states
        bool isIdle = playerController.IsIdle();
        bool isRunning = playerController.IsRunning();
        bool isJumping = playerController.IsJumping();
        bool isFalling = playerController.IsFalling();

        // Ground animations
        if (isRunning)
        {
            animator.SetTrigger("run");
        }
        else if (isIdle)
        {
            animator.SetTrigger("idle");
        }

        // Air animations
        if (isJumping)
        {
            animator.SetTrigger("jump");
        }
        else if (isFalling)
        {
            animator.SetTrigger("idle"); // ← Use idle when falling!
        }
    }
}
