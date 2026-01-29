using UnityEngine;

/// <summary>
/// Handles dodge platform falling behavior and self-destruction
/// </summary>
public class DodgePlatform : MonoBehaviour
{
    #region Serialized Fields
    [Header("Movement Settings")]
    [Tooltip("Speed at which platform falls (units per second)")]
    [SerializeField] private float fallSpeed = 2f;

    [Header("Destruction Settings")]
    [Tooltip("Y position below which platform destroys itself")]
    [SerializeField] private float destroyHeight = -5f;
    #endregion

    #region Unity Lifecycle
    void Update()
    {
        MovePlatform();
        CheckDestruction();
    }
    #endregion

    #region Private Methods
    private void MovePlatform()
    {
        // Move downward at constant speed
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
    }

    private void CheckDestruction()
    {
        // Destroy platform when it falls below screen
        if (transform.position.y < destroyHeight)
        {
            Destroy(gameObject);
        }
    }
    #endregion
}