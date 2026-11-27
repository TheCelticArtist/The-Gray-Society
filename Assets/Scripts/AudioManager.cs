using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

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

    public const string VolumeKeyPrefix = "Options_Volume_";
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer (the same one used by the Options)")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] AudioMixerGroup musicGroup;
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

    AudioSource sfxSource, musicSource;
    bool musicPaused;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = CreateSource(sfxGroup, false);
        musicSource = CreateSource(musicGroup, true);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    AudioSource CreateSource(AudioMixerGroup group, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.outputAudioMixerGroup = group;
        return source;
    }

    void Start()
    {
        if (Instance != this) return;

        if (mixer != null)
            foreach (VolumeSetting setting in volumes)
                if (!string.IsNullOrEmpty(setting.parameter))
                    SetMixerVolume(mixer, setting.parameter, GetSavedVolume(setting.parameter));

        PlayMusicForScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlayMusicForScene(scene.buildIndex);

    void PlayMusicForScene(int sceneIndex)
    {
        SetMusicPaused(false);

        if (musicByScene == null || sceneIndex < 0 || sceneIndex >= musicByScene.Length) return;
        AudioClip clip = musicByScene[sceneIndex];
        if (clip == null || clip == musicSource.clip) return;
        musicSource.clip = clip;
        musicSource.Play();
    }

    void SetMusicPaused(bool paused)
    {
        if (musicPaused == paused) return;
        musicPaused = paused;
        if (paused) musicSource.Pause();
        else musicSource.UnPause();
    }

    public static void PauseMusic() { if (Instance != null) Instance.SetMusicPaused(true); }
    public static void ResumeMusic() { if (Instance != null) Instance.SetMusicPaused(false); }

    public static float GetSavedVolume(string parameter)
    {
        AudioManager manager = Instance != null ? Instance : FindAnyObjectByType<AudioManager>();
        float defaultVolume = 1f;
        if (manager != null)
            foreach (VolumeSetting setting in manager.volumes)
                if (setting.parameter == parameter) { defaultVolume = setting.defaultVolume; break; }

        return PlayerPrefs.GetFloat(VolumeKeyPrefix + parameter, defaultVolume);
    }

    public static void SetMixerVolume(AudioMixer mixer, string parameter, float value)
    {
        mixer.SetFloat(parameter, Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
    }

    public static void Play(Sfx sfx)
    {
        if (Instance == null) return;

        AudioClip clip = sfx switch
        {
            Sfx.Button => Instance.buttonClick,
            Sfx.Hover => Instance.buttonHover,
            Sfx.Pickup => Instance.pickup,
            Sfx.Shoot => Instance.shoot,
            Sfx.Jump => Instance.jump,
            Sfx.EnemyHit => Instance.enemyHit,
            Sfx.SpikeHit => Instance.spikeHit,
            _ => null
        };
        if (clip != null) Instance.sfxSource.PlayOneShot(clip);
    }
}