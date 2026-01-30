using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns and configures obstacles with increasing difficulty
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    #region Serialized Fields
    [Header("Prefab")]
    [Tooltip("Obstacle prefab to spawn")]
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Spawn Positions")]
    [Tooltip("Left side spawn position")]
    [SerializeField] private Vector3 leftSpawnPosition = new Vector3(-1.94f, 2.01f, 0f);

    [Tooltip("Right side spawn position")]
    [SerializeField] private Vector3 rightSpawnPosition = new Vector3(1.94f, 2.01f, 0f);

    [Header("Obstacle Configuration")]
    [Tooltip("Initial push force applied to spawned obstacles")]
    [SerializeField] private float initialPushForce = 5f;

    [Header("Difficulty Scaling")]
    [Tooltip("Initial spawn interval in seconds")]
    [SerializeField] private float startInterval = 10f;

    [Tooltip("Minimum spawn interval in seconds")]
    [SerializeField] private float minInterval = 3f;

    [Tooltip("Time in seconds to reach minimum interval")]
    [SerializeField] private float timeToMinInterval = 120f;

    [Tooltip("Delay before first spawn")]
    [SerializeField] private float initialDelay = 7f;
    #endregion

    #region Private Fields
    private float currentSpawnInterval;
    private Coroutine spawnRoutine;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        GameTimer.OnSecondPassed += AdjustDifficulty;
        currentSpawnInterval = startInterval;

        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnLoop());
        }
    }

    private void OnDisable()
    {
        GameTimer.OnSecondPassed -= AdjustDifficulty;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
    #endregion

    #region Private Methods
    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            SpawnObstacle();
            yield return new WaitForSeconds(currentSpawnInterval);
        }
    }

    private void SpawnObstacle()
    {
        // Randomly choose spawn side
        bool spawnLeft = Random.Range(0, 2) == 0;
        Vector3 spawnPosition = spawnLeft ? leftSpawnPosition : rightSpawnPosition;
        Vector3 moveDirection = spawnLeft ? Vector3.right : Vector3.left;

        // Instantiate obstacle
        GameObject obstacleObj = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity);
        ObstacleController obstacle = obstacleObj.GetComponent<ObstacleController>();

        if (obstacle != null)
        {
            // Configure obstacle
            obstacle.SetMovementDirection(moveDirection);
            obstacle.ApplyInitialPush(initialPushForce);
        }
        else
        {
            Debug.LogError("ObstacleController component not found on prefab!");
        }
    }

    private void AdjustDifficulty(int secondsElapsed)
    {
        // Gradually decrease spawn interval over time
        float t = Mathf.Clamp01(secondsElapsed / timeToMinInterval);
        currentSpawnInterval = Mathf.Lerp(startInterval, minInterval, t);
    }
    #endregion
}