using UnityEngine;

public class PlayerSoundManager : MonoBehaviour
{
    // Singleton Instance
    public static PlayerSoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource audioSource;           // Main AudioSource for general sounds
    [SerializeField] private AudioSource flaskAudioSource;      // Separate AudioSource for flask sounds

    [Header("Audio Clips")]
    [SerializeField] private AudioClip arrowHitClip;            // Assign in Inspector
    [SerializeField] private AudioClip shurikenHitClip;         // Assign in Inspector
    [SerializeField] private AudioClip fireballHitClip;         // Assign in Inspector
    [SerializeField] private AudioClip shurikenDeflectClip;     // Assign in Inspector
    [SerializeField] private AudioClip dodgeSoundClip;          // Assign in Inspector
    [SerializeField] private AudioClip evadeSoundClip;          // **New Evade Sound Clip** - Assign in Inspector
    [SerializeField] private AudioClip flaskUseClip;            // Assign in Inspector

    private void Awake()
    {
        // Implement Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes
        }
        else if (Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate instances
        }

        // Validate AudioSource Assignments
        if (audioSource == null)
        {
            Debug.LogError("Main AudioSource is not assigned in the Inspector.");
        }

        if (flaskAudioSource == null)
        {
            Debug.LogError("Flask AudioSource is not assigned in the Inspector.");
        }

        // Validate AudioClip Assignments
        if (arrowHitClip == null)
        {
            Debug.LogWarning("ArrowHitClip is not assigned.");
        }

        if (shurikenHitClip == null)
        {
            Debug.LogWarning("ShurikenHitClip is not assigned.");
        }

        if (fireballHitClip == null)
        {
            Debug.LogWarning("FireballHitClip is not assigned.");
        }

        if (shurikenDeflectClip == null)
        {
            Debug.LogWarning("ShurikenDeflectClip is not assigned.");
        }

        if (dodgeSoundClip == null)
        {
            Debug.LogWarning("DodgeSoundClip is not assigned.");
        }

        if (evadeSoundClip == null)
        {
            Debug.LogWarning("EvadeSoundClip is not assigned.");
        }

        if (flaskUseClip == null)
        {
            Debug.LogWarning("FlaskUseClip is not assigned.");
        }
    }

    /// <summary>
    /// Plays a given AudioClip on the main AudioSource.
    /// </summary>
    /// <param name="clip">AudioClip to play.</param>
    private void PlayMainSound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning("AudioClip is not assigned.");
        }
    }

    /// <summary>
    /// Plays the flask usage AudioClip on the flask AudioSource.
    /// </summary>
    /// <param name="clip">Flask usage AudioClip to play.</param>
    private void PlayFlaskSound(AudioClip clip)
    {
        if (clip != null)
        {
            // Stop any existing flask sound before playing a new one to prevent overlap
            flaskAudioSource.Stop();
            flaskAudioSource.clip = clip;
            flaskAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("Flask AudioClip is not assigned.");
        }
    }

    // ===========================
    // Sound Playback Methods
    // ===========================

    /// <summary>
    /// Plays the arrow hit sound.
    /// </summary>
    public void PlayArrowHitSound()
    {
        PlayMainSound(arrowHitClip);
    }

    /// <summary>
    /// Plays the shuriken hit sound.
    /// </summary>
    public void PlayShurikenHitSound()
    {
        PlayMainSound(shurikenHitClip);
    }

    /// <summary>
    /// Plays the fireball hit sound.
    /// </summary>
    public void PlayFireballHitSound()
    {
        PlayMainSound(fireballHitClip);
    }

    /// <summary>
    /// Plays the shuriken deflect sound.
    /// </summary>
    public void PlayShurikenDeflectSound()
    {
        PlayMainSound(shurikenDeflectClip);
    }

    /// <summary>
    /// Plays the dodge sound.
    /// </summary>
    public void PlayDodgeSound()
    {
        PlayMainSound(dodgeSoundClip);
    }

    /// <summary>
    /// Plays the evade sound.
    /// </summary>
    public void PlayEvadeSound()
    {
        PlayMainSound(evadeSoundClip);
    }

    /// <summary>
    /// Plays the flask usage sound effect on the flask AudioSource.
    /// </summary>
    public void PlayFlaskUseSound()
    {
        PlayFlaskSound(flaskUseClip);
    }

    /// <summary>
    /// Stops the flask usage sound effect.
    /// </summary>
    public void StopFlaskUseSound()
    {
        if (flaskAudioSource != null && flaskAudioSource.isPlaying)
        {
            flaskAudioSource.Stop();
            Debug.Log("Flask use sound stopped.");
        }
        else
        {
            Debug.LogWarning("Flask AudioSource is not playing or not assigned.");
        }
    }
}
