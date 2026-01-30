using System;
using UnityEngine;

/// <summary>
/// Tracks game time and broadcasts events. Reusable for any game mode.
/// </summary>
public class GameTimer : MonoBehaviour
{
    #region Events
    public static event Action<int> OnSecondPassed;
    #endregion

    #region Serialized Fields
    [Header("Timer Settings")]
    [Tooltip("Start broadcasting after this delay")]
    [SerializeField] private float startDelay = 0f;
    #endregion

    #region Private Fields
    private int secondsElapsed = 0;
    private bool isRunning = false;
    #endregion

    #region Unity Lifecycle
    void Start()
    {
        StartTimer();
    }

    void OnDestroy()
    {
        StopTimer();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Start the timer
    /// </summary>
    public void StartTimer()
    {
        if (isRunning) return;

        isRunning = true;
        InvokeRepeating(nameof(IncrementSecond), startDelay, 1f);
    }

    /// <summary>
    /// Stop the timer
    /// </summary>
    public void StopTimer()
    {
        if (!isRunning) return;

        isRunning = false;
        CancelInvoke(nameof(IncrementSecond));
    }

    /// <summary>
    /// Reset timer to zero
    /// </summary>
    public void ResetTimer()
    {
        secondsElapsed = 0;
    }

    /// <summary>
    /// Get current elapsed seconds
    /// </summary>
    public int GetElapsedSeconds()
    {
        return secondsElapsed;
    }
    #endregion

    #region Private Methods
    private void IncrementSecond()
    {
        secondsElapsed++;
        OnSecondPassed?.Invoke(secondsElapsed);
    }
    #endregion
}