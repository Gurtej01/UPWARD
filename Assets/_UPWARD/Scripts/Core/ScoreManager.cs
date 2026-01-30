using UnityEngine;

/// <summary>
/// Tracks player score based on survival time
/// </summary>
public class ScoreManager : MonoBehaviour
{
    #region Private Fields
    private int currentScore = 0;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        GameTimer.OnSecondPassed += UpdateScore;
    }

    private void OnDisable()
    {
        GameTimer.OnSecondPassed -= UpdateScore;
    }
    #endregion

    #region Private Methods
    private void UpdateScore(int secondsElapsed)
    {
        currentScore = secondsElapsed;
        // TODO: Broadcast score update event when UIManager exists
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get current score value
    /// </summary>
    public int GetScore()
    {
        return currentScore;
    }

    /// <summary>
    /// Reset score to zero
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
    }
    #endregion
}