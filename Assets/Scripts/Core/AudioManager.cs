using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip[] backgroundMusicTracks;

    [Header("UI Sounds")]
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip inventoryOpenSound;
    [SerializeField] private AudioClip inventoryCloseSound;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-create AudioSources if not assigned
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.spatialBlend = 0f; // 2D sound
                musicSource.volume = 0.5f;
            }

            if (uiSource == null)
            {
                uiSource = gameObject.AddComponent<AudioSource>();
                uiSource.spatialBlend = 0f; // 2D sound
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        PlayBackgroundMusic();
    }

    public void PlayBackgroundMusic()
    {
        if (backgroundMusicTracks.Length > 0 && musicSource != null)
        {
            if (!musicSource.isPlaying)
            {
                int randomIndex = Random.Range(0, backgroundMusicTracks.Length);
                musicSource.clip = backgroundMusicTracks[randomIndex];
                musicSource.loop = true;
                musicSource.Play();
            }
        }
    }

    public void PlayButtonClick()
    {
        if (uiSource != null && buttonClickSound != null)
        {
            uiSource.PlayOneShot(buttonClickSound);
        }
    }

    public void PlayInventoryOpen()
    {
        if (uiSource != null && inventoryOpenSound != null)
        {
            uiSource.PlayOneShot(inventoryOpenSound);
        }
    }

    public void PlayInventoryClose()
    {
        if (uiSource != null && inventoryCloseSound != null)
        {
            uiSource.PlayOneShot(inventoryCloseSound);
        }
    }

    public void PlaySoundAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
