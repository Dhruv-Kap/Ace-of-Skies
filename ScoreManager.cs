using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    [Header("Scoring Settings")]
    [SerializeField] private int pointsPerKill = 10;
    [SerializeField] private int pointsLostPerDeath = -3;
    [SerializeField] private int winningScore = 150;

    [Header("Scoreboard UI")]
    [SerializeField] private TextMeshProUGUI playerScoreText; // Shows player's current score
    [SerializeField] private TextMeshProUGUI topScoreText; // Shows highest score

    [Header("Game Over UI")]
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Score tracking
    private Dictionary<string, int> entityScores = new Dictionary<string, int>();
    private Dictionary<string, int> killCounts = new Dictionary<string, int>();
    private Dictionary<string, int> deathCounts = new Dictionary<string, int>();

    // Game state
    private bool gameOver = false;
    private string playerName = "You"; // Set this to your player's name

    // Events
    public System.Action<string, int> OnScoreChanged; // (entityName, newScore)
    public System.Action<string> OnGameOver; // (winnerName)

    void Start()
    {
        InitializeScoreManager();
    }

    void InitializeScoreManager()
    {
        // Find and subscribe to all Health components (bots)
        Health[] botHealths = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (Health health in botHealths)
        {
            RegisterEntity(health.gameObject.name);
            health.OnKilled += OnEntityKilled;
        }

        // Find and subscribe to PlayerHealth
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            RegisterEntity(playerName);
            playerHealth.OnKilled += OnEntityKilled;
        }

        // Initialize UI
        UpdateScoreUI();

        if (showDebugInfo)
        {
            Debug.Log($"[ScoreManager] Initialized with {entityScores.Count} entities");
        }
    }

    void RegisterEntity(string entityName)
    {
        if (!entityScores.ContainsKey(entityName))
        {
            entityScores[entityName] = 0;
            killCounts[entityName] = 0;
            deathCounts[entityName] = 0;
        }
    }

    void OnEntityKilled(GameObject victim, GameObject killer)
    {
        if (gameOver) return;

        string victimName = victim.name;
        string killerName = killer != null ? killer.name : "Unknown";

        // Handle special case for PLAYER weapon only (be more specific)
        if (killer != null && (killerName == "GunFirePoint" || killerName.Contains("F16GunFire")))
        {
            killerName = playerName; // Only attribute YOUR weapon kills to player
        }

        // Register entities if they don't exist
        RegisterEntity(victimName);
        if (killer != null) RegisterEntity(killerName);

        // Update death count and score for victim
        deathCounts[victimName]++;
        entityScores[victimName] += pointsLostPerDeath;

        // Update kill count and score for killer
        if (killer != null)
        {
            killCounts[killerName]++;
            entityScores[killerName] += pointsPerKill;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[ScoreManager] {killerName} killed {victimName}. " +
                     $"Killer: {entityScores[killerName]} points ({killCounts[killerName]} kills), " +
                     $"Victim: {entityScores[victimName]} points ({deathCounts[victimName]} deaths)");
        }

        // Fire score changed events
        OnScoreChanged?.Invoke(victimName, entityScores[victimName]);
        if (killer != null)
            OnScoreChanged?.Invoke(killerName, entityScores[killerName]);

        // Update UI
        UpdateScoreUI();

        // Check for game over
        CheckForGameOver();
    }

    void CheckForGameOver()
    {
        if (gameOver) return;

        // Find highest score
        string winner = "";
        int highestScore = int.MinValue;

        foreach (var kvp in entityScores)
        {
            if (kvp.Value > highestScore)
            {
                highestScore = kvp.Value;
                winner = kvp.Key;
            }
        }

        // Check if someone reached winning score
        if (highestScore >= winningScore)
        {
            EndGame(winner, highestScore);
        }
    }

    void EndGame(string winner, int finalScore)
    {
        gameOver = true;

        if (showDebugInfo)
        {
            Debug.Log($"[ScoreManager] Game Over! Winner: {winner} with {finalScore} points");
        }

        // Fire game over event
        OnGameOver?.Invoke(winner);

        // Show appropriate screen
        if (winner == playerName)
        {
            ShowVictoryScreen(winner, finalScore);
        }
        else
        {
            ShowDefeatScreen(winner, finalScore);
        }

        // Disable UI elements during game over
        if (playerScoreText != null) playerScoreText.gameObject.SetActive(false);
        if (topScoreText != null) topScoreText.gameObject.SetActive(false);
    }

    void ShowVictoryScreen(string winner, int score)
    {
        if (victoryScreen != null)
        {
            victoryScreen.SetActive(true);

            // Find text components in victory screen
            TextMeshProUGUI[] victoryTexts = victoryScreen.GetComponentsInChildren<TextMeshProUGUI>();
            if (victoryTexts.Length > 0)
            {
                victoryTexts[0].text = $"VICTORY!\nYou won with {score} points!\nKills: {GetKillCount(playerName)} | Deaths: {GetDeathCount(playerName)}";
            }
        }
    }

    void ShowDefeatScreen(string winner, int score)
    {
        if (defeatScreen != null)
        {
            defeatScreen.SetActive(true);

            // Find text components in defeat screen
            TextMeshProUGUI[] defeatTexts = defeatScreen.GetComponentsInChildren<TextMeshProUGUI>();
            if (defeatTexts.Length > 0)
            {
                defeatTexts[0].text = $"DEFEAT!\n{winner} won with {score} points\nYour Score: {GetScore(playerName)} | K/D: {GetKillCount(playerName)}/{GetDeathCount(playerName)}";
            }
        }
    }

    void UpdateScoreUI()
    {
        // Update player score text
        if (playerScoreText != null)
        {
            int playerScore = GetScore(playerName);
            playerScoreText.text = $"Score: {playerScore}";
        }

        // Update top score text (find highest score among all entities)
        if (topScoreText != null)
        {
            string topPlayer = "";
            int topScore = int.MinValue;

            foreach (var kvp in entityScores)
            {
                if (kvp.Value > topScore)
                {
                    topScore = kvp.Value;
                    topPlayer = kvp.Key;
                }
            }

            topScoreText.text = $"Top: {topPlayer} ({topScore})";
        }
    }

    // Public methods for external access
    public int GetScore(string entityName)
    {
        return entityScores.ContainsKey(entityName) ? entityScores[entityName] : 0;
    }

    public int GetKillCount(string entityName)
    {
        return killCounts.ContainsKey(entityName) ? killCounts[entityName] : 0;
    }

    public int GetDeathCount(string entityName)
    {
        return deathCounts.ContainsKey(entityName) ? deathCounts[entityName] : 0;
    }

    public bool IsGameOver()
    {
        return gameOver;
    }

    public string GetLeader()
    {
        string leader = "";
        int highestScore = int.MinValue;

        foreach (var kvp in entityScores)
        {
            if (kvp.Value > highestScore)
            {
                highestScore = kvp.Value;
                leader = kvp.Key;
            }
        }

        return leader;
    }

    public Dictionary<string, int> GetAllScores()
    {
        return new Dictionary<string, int>(entityScores);
    }

    // Method to restart game (for future use)
    public void RestartGame()
    {
        gameOver = false;

        // Reset all scores
        foreach (string key in new List<string>(entityScores.Keys))
        {
            entityScores[key] = 0;
            killCounts[key] = 0;
            deathCounts[key] = 0;
        }

        // Show UI elements again
        if (playerScoreText != null) playerScoreText.gameObject.SetActive(true);
        if (topScoreText != null) topScoreText.gameObject.SetActive(true);

        // Hide game over screens
        if (victoryScreen != null) victoryScreen.SetActive(false);
        if (defeatScreen != null) defeatScreen.SetActive(false);

        UpdateScoreUI();

        if (showDebugInfo)
        {
            Debug.Log("[ScoreManager] Game restarted");
        }
    }
}