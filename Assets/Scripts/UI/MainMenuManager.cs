using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject mapSelectionPanel;
    public GameObject newWorldPanel;

    [Header("Map Selection")]
    public Transform mapListContent;
    public GameObject mapListItemPrefab;
    public Button playSelectedButton;
    public Button deleteSelectedButton;

    [Header("New World")]
    public TMP_InputField worldNameInput;
    public TMP_InputField seedInput;

    private string selectedSaveName;

    private void Start()
    {
        ShowMainPanel();
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    public void ShowMainPanel()
    {
        PlayClickSound();
        mainPanel.SetActive(true);
        mapSelectionPanel.SetActive(false);
        newWorldPanel.SetActive(false);
    }

    public void ShowMapSelectionPanel()
    {
        PlayClickSound();
        mainPanel.SetActive(false);
        mapSelectionPanel.SetActive(true);
        newWorldPanel.SetActive(false);
        RefreshMapList();
        if (playSelectedButton) playSelectedButton.interactable = false;
        if (deleteSelectedButton) deleteSelectedButton.interactable = false;
    }

    public void ShowNewWorldPanel()
    {
        PlayClickSound();
        mainPanel.SetActive(false);
        mapSelectionPanel.SetActive(false);
        newWorldPanel.SetActive(true);
        if (worldNameInput) worldNameInput.text = "New World";
        if (seedInput) seedInput.text = Random.Range(0, 100000).ToString();
    }

    public void RefreshMapList()
    {
        foreach (Transform child in mapListContent)
        {
            Destroy(child.gameObject);
        }

        List<string> saves = SaveManager.GetSaveList();
        foreach (string save in saves)
        {
            GameObject item = Instantiate(mapListItemPrefab, mapListContent);
            item.GetComponentInChildren<TextMeshProUGUI>().text = save;
            string saveName = save; // Capture for lambda
            item.GetComponent<Button>().onClick.AddListener(() => SelectMap(saveName));
        }
    }

    public void SelectMap(string saveName)
    {
        selectedSaveName = saveName;
        if (playSelectedButton) playSelectedButton.interactable = true;
        if (deleteSelectedButton) deleteSelectedButton.interactable = true;
    }

    public void OnPlaySelectedClicked()
    {
        PlayClickSound();
        if (!string.IsNullOrEmpty(selectedSaveName))
        {
            PlayerPrefs.SetString("LoadSaveName", selectedSaveName);
            SceneManager.LoadScene("GameScene");
        }
    }

    public void OnDeleteSelectedClicked()
    {
        PlayClickSound();
        if (!string.IsNullOrEmpty(selectedSaveName))
        {
            SaveManager.DeleteSave(selectedSaveName);
            RefreshMapList();
            if (playSelectedButton) playSelectedButton.interactable = false;
            if (deleteSelectedButton) deleteSelectedButton.interactable = false;
            selectedSaveName = null;
        }
    }

    public void OnCreateWorldClicked()
    {
        PlayClickSound();
        string name = worldNameInput.text;
        if (string.IsNullOrEmpty(name)) return;

        int seed = 0;
        int.TryParse(seedInput.text, out seed);

        // Create initial save data
        GameSaveData data = new GameSaveData();
        data.saveName = name;
        data.lastPlayed = System.DateTime.Now.Ticks;
        data.worldData = new WorldData();
        data.worldData.seed = seed;
        
        // Save it
        SaveManager.SaveGame(data);

        // Load it
        PlayerPrefs.SetString("LoadSaveName", name);
        SceneManager.LoadScene("GameScene");
    }

    public void QuitGame()
    {
        PlayClickSound();
        Application.Quit();
    }
}
