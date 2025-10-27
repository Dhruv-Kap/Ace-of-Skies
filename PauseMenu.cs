using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Simple pause menu - Press ESC to pause/unpause
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI Elements - Assign These!")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private TextMeshProUGUI exitText;
    [SerializeField] private GameObject yesButton;
    [SerializeField] private GameObject noButton;

    [Header("Settings")]
    [SerializeField] private string menuSceneName = "menu";

    private bool isPaused = false;

    void Start()
    {
        // Hide pause menu at start
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Check for ESC key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freeze game

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Resume game

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Hide cursor (optional - remove if you want cursor visible)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnYesButtonClicked()
    {
        Time.timeScale = 1f; // Resume time before loading scene
        SceneManager.LoadScene(menuSceneName);
    }

    public void OnNoButtonClicked()
    {
        ResumeGame();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // Resume time before reloading
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f; // Resume time before loading scene
        SceneManager.LoadScene(menuSceneName);
    }
}