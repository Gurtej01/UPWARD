using UnityEngine;

/// <summary>
/// Handles respawning of player when falling off screen. Reusable for any object.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RespawnManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("Respawn Settings")]
    [Tooltip("Y position below which object respawns")]
    [SerializeField] private float respawnThreshold = -5f;

    [Tooltip("Reset velocity on respawn")]
    [SerializeField] private bool resetVelocity = true;
    #endregion

    #region Private Fields
    private Vector3 spawnPosition;
    private Rigidbody rb;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        spawnPosition = transform.position;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        CheckRespawn();
    }
    #endregion

    #region Private Methods
    private void CheckRespawn()
    {
        if (transform.position.y <= respawnThreshold)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        transform.position = spawnPosition;

        if (resetVelocity && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Manually trigger respawn
    /// </summary>
    public void TriggerRespawn()
    {
        Respawn();
    }

    /// <summary>
    /// Change spawn position
    /// </summary>
    public void SetSpawnPosition(Vector3 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
    }
    #endregion
}