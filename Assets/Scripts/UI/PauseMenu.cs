using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused = false;

    [Header("UI References")]
    public GameObject pauseMenuUI;
    public Button resumeButton;
    public Button saveButton;
    public Button saveAndQuitButton;

    [Header("Scripts to Disable")]
    public MonoBehaviour[] scriptsToDisable; // e.g. MouseLook, PlayerController, BlockInteraction

    private InventoryUI inventoryUI;

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    private void Start()
    {
        // Ensure menu is hidden at start
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        
        IsPaused = false;
        Time.timeScale = 1f;

        // Setup buttons
        if (resumeButton) resumeButton.onClick.AddListener(Resume);
        if (saveButton) saveButton.onClick.AddListener(SaveGame);
        if (saveAndQuitButton) saveAndQuitButton.onClick.AddListener(SaveAndQuit);

        inventoryUI = Object.FindAnyObjectByType<InventoryUI>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                if (inventoryUI == null) inventoryUI = Object.FindAnyObjectByType<InventoryUI>();
                
                if (inventoryUI != null && inventoryUI.IsInventoryOpen())
                {
                    inventoryUI.CloseMainInventory();
                }
                else
                {
                    Pause();
                }
            }
        }
    }

    public void Resume()
    {
        PlayClickSound();
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        
        Time.timeScale = 1f;
        IsPaused = false;

        // Lock cursor back
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable scripts
        foreach (var script in scriptsToDisable)
        {
            if (script != null) script.enabled = true;
        }
    }

    public void Pause()
    {
        PlayClickSound();
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);
        
        Time.timeScale = 0f;
        IsPaused = true;

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable scripts (like MouseLook, PlayerController) to prevent movement/looking while paused
        foreach (var script in scriptsToDisable)
        {
            if (script != null) script.enabled = false;
        }
    }

    public void SaveGame()
    {
        PlayClickSound();
        if (WorldGenerator.Instance != null)
        {
            WorldGenerator.Instance.SaveWorld();
            Debug.Log("Game Saved.");
        }
    }

    public void SaveAndQuit()
    {
        PlayClickSound();
        SaveGame();
        Time.timeScale = 1f; // Reset time scale before leaving
        IsPaused = false;
        SceneManager.LoadScene("MainMenu");
    }
}
