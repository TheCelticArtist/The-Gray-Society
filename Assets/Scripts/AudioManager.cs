using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// elenco dei suoni del gioco
public enum Sfx { Button, Hover, Pickup, Shoot, Jump, EnemyHit, SpikeHit }

public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class VolumeSetting
    {
        [Tooltip("Same name used in the OptionsPanel Mixer Parameter field, e.g. MusicVolume")]
        public string parameter;
        [Tooltip("Volume on the very first launch (1 = max), until the player changes it in the Options")]
        [Range(0.0001f, 1f)] public float defaultVolume = 1f;
    }

    public static AudioManager Instance { get; private set; }

    const string VolumeKeyPrefix = "Options_Volume_";   // la stessa chiave usata dall'OptionsPanel

    [Header("Audio Mixer (the same one used by the Options)")]
    [SerializeField] AudioMixer mixer;
    [Tooltip("Mixer group for the music")]
    [SerializeField] AudioMixerGroup musicGroup;
    [Tooltip("Mixer group for the sound effects")]
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Default Volumes")]
    [SerializeField]
    VolumeSetting[] volumes =
    {
        new VolumeSetting { parameter = "MasterVolume", defaultVolume = 0.8f },
        new VolumeSetting { parameter = "MusicVolume",  defaultVolume = 0.5f },
        new VolumeSetting { parameter = "SFXVolume",    defaultVolume = 0.7f },
    };

    [Header("Music (Element 0 = scene 0 = menu, Element 1 = level 1, ...)")]
    [Tooltip("Leave an element empty to keep playing the music of the previous scene")]
    [SerializeField] AudioClip[] musicByScene;

    [Header("Menu")]
    [SerializeField] AudioClip buttonClick;
    [SerializeField] AudioClip buttonHover;

    [Header("Items")]
    [SerializeField] AudioClip pickup;

    [Header("Player")]
    [SerializeField] AudioClip shoot;
    [SerializeField] AudioClip jump;
    [SerializeField] AudioClip enemyHit;
    [SerializeField] AudioClip spikeHit;

    AudioSource sfxSource;
    AudioSource musicSource;
    bool musicPaused;
    static bool warned;

    void Awake()
    {
        // se esiste già un AudioManager (portato dalla scena precedente), questo doppione si elimina
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);   // suoni e musica non si interrompono cambiando scena

        // sorgente per gli effetti
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.outputAudioMixerGroup = sfxGroup;

        // sorgente per la musica, separata dagli effetti
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.outputAudioMixerGroup = musicGroup;

        if (musicGroup == null || sfxGroup == null)
            Debug.LogWarning("AudioManager: Music Group or Sfx Group is empty, those sounds will ignore the Options volumes.");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (Instance != this) return;

        ApplySavedVolumes();   // il mixer ignora i volumi impostati in Awake, quindi li applico qui
        PlayMusicForScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.buildIndex);
    }

    // ---------------- Musica ----------------

    void PlayMusicForScene(int sceneIndex)
    {
        // cambiando scena (es. "Menu principale" dalla pausa) la musica non resta mai in pausa
        if (musicPaused)
        {
            musicPaused = false;
            musicSource.UnPause();
        }

        if (musicByScene == null || sceneIndex < 0 || sceneIndex >= musicByScene.Length) return;

        AudioClip clip = musicByScene[sceneIndex];

        // elemento vuoto o stessa musica (es. dopo una morte): continua senza ripartire da capo
        if (clip == null || clip == musicSource.clip) return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    // la usa il menu di pausa: ferma la musica dov'è
    public static void PauseMusic()
    {
        if (Instance == null || Instance.musicPaused) return;

        Instance.musicSource.Pause();
        Instance.musicPaused = true;
    }

    // la usa il menu di pausa: la musica riparte da dove si era fermata
    public static void ResumeMusic()
    {
        if (Instance == null || !Instance.musicPaused) return;

        Instance.musicSource.UnPause();
        Instance.musicPaused = false;
    }

    // ---------------- Volumi ----------------

    // volume salvato dal giocatore, oppure quello di default se non l'ha mai cambiato (lo usa anche l'OptionsPanel)
    public static float GetSavedVolume(string parameter)
    {
        return PlayerPrefs.GetFloat(VolumeKeyPrefix + parameter, GetDefaultVolume(parameter));
    }

    static float GetDefaultVolume(string parameter)
    {
        // funziona anche se l'AudioManager non ha ancora eseguito Awake
        AudioManager manager = Instance != null ? Instance : FindFirstObjectByType<AudioManager>();

        if (manager != null)
        {
            foreach (VolumeSetting setting in manager.volumes)
            {
                if (setting.parameter == parameter)
                    return setting.defaultVolume;
            }
        }

        return 1f;
    }

    void ApplySavedVolumes()
    {
        if (mixer == null)
        {
            Debug.LogWarning("AudioManager: the Mixer is missing, saved volumes are not applied.");
            return;
        }

        foreach (VolumeSetting setting in volumes)
        {
            if (string.IsNullOrEmpty(setting.parameter)) continue;

            float value = GetSavedVolume(setting.parameter);
            if (!mixer.SetFloat(setting.parameter, Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f))
                Debug.LogWarning("AudioManager: the parameter '" + setting.parameter + "' is not exposed in the Audio Mixer.");
        }
    }

    // ---------------- Effetti ----------------

    // da qualsiasi script: AudioManager.Play(Sfx.Jump);
    public static void Play(Sfx sfx)
    {
        if (Instance == null)
        {
            if (!warned)
            {
                Debug.LogWarning("AudioManager: no AudioManager in the scene, sounds are not played.");
                warned = true;
            }
            return;
        }

        AudioClip clip = Instance.GetClip(sfx);
        if (clip != null)
            Instance.sfxSource.PlayOneShot(clip);
    }

    AudioClip GetClip(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Button: return buttonClick;
            case Sfx.Hover: return buttonHover;
            case Sfx.Pickup: return pickup;
            case Sfx.Shoot: return shoot;
            case Sfx.Jump: return jump;
            case Sfx.EnemyHit: return enemyHit;
            case Sfx.SpikeHit: return spikeHit;
        }
        return null;
    }
}