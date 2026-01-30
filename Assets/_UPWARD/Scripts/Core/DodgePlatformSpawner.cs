using UnityEngine;

/// <summary>
/// Handles spawning and positioning of dodge platforms
/// </summary>
public class DodgePlatformSpawner : MonoBehaviour
{
    #region Serialized Fields
    [Header("Platform Settings")]
    [Tooltip("Platform prefab to spawn")]
    [SerializeField] private GameObject platformPrefab;

    [Header("Spawn Timing")]
    [Tooltip("Seconds between each platform spawn")]
    [SerializeField] private float spawnInterval = 3f;

    [Tooltip("Delay before first spawn")]
    [SerializeField] private float initialDelay = 1f;

    [Header("Spawn Position")]
    [Tooltip("Spawn height (Y position)")]
    [SerializeField] private float spawnHeight = 2.98f;

    [Tooltip("Right side spawn range - minimum X")]
    [SerializeField] private float rightMinX = 0.40f;

    [Tooltip("Right side spawn range - maximum X")]
    [SerializeField] private float rightMaxX = 0.78f;

    [Tooltip("Left side spawn range - minimum X")]
    [SerializeField] private float leftMinX = -0.78f;

    [Tooltip("Left side spawn range - maximum X")]
    [SerializeField] private float leftMaxX = -0.40f;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        InvokeRepeating(nameof(SpawnPlatform), initialDelay, spawnInterval);
    }
    #endregion

    #region Private Methods
    private void SpawnPlatform()
    {
        // Decide left or right side
        bool spawnRight = Random.Range(0, 2) == 0;
          
        // Generate random X position for chosen side
        float finalXpos = spawnRight
            ? Random.Range(rightMinX, rightMaxX)
            : Random.Range(leftMinX, leftMaxX);

        // Spawn platform at calculated position
        Vector3 spawnPosition = new Vector3(finalXpos, spawnHeight, 0f);
        Instantiate(platformPrefab, spawnPosition, Quaternion.identity);
    }
    #endregion
}