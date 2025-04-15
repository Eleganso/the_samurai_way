using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TimeController : MonoBehaviour
{
    [Header("Slow Time Settings")]
    [SerializeField] private float slowFactor = 0.5f; // Slow time to 50%
    [SerializeField] private float slowDuration = 5f; // Duration in seconds

    [Header("Freeze Time Settings")]
    [SerializeField] private float freezeDuration = 2f; // Duration in seconds

    [Header("Visual Feedback")]
    [SerializeField] private Image slowTimeOverlay; // Assign via Inspector
    [SerializeField] private Image freezeTimeOverlay; // Assign via Inspector

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip slowTimeSound; // Assign via Inspector (optional)
    [SerializeField] private AudioClip freezeTimeSound; // Assign via Inspector (optional)
    [SerializeField] private AudioSource audioSource; // Assign via Inspector (optional)

    [Header("Screen Shake Settings")]
    [SerializeField] private CinemachineImpulseSource impulseSource; // Assign the impulse source (Cinemachine)

    private bool isTimeSlowed = false;
    private bool isTimeFrozen = false;

    private void OnEnable()
    {
        // Subscribe to sceneLoaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Attempt to assign overlays if not already assigned (for the initial scene)
        AssignOverlays();
    }

    /// <summary>
    /// Handler for when a new scene is loaded.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignOverlays();
    }

    /// <summary>
    /// Assigns the overlay images by finding them in the current scene's Canvas.
    /// </summary>
    private void AssignOverlays()
    {
        // Assign SlowTimeOverlay if not already assigned
        if (slowTimeOverlay == null)
        {
            GameObject slowOverlayObj = GameObject.Find("SlowTimeOverlay");
            if (slowOverlayObj != null)
            {
                slowTimeOverlay = slowOverlayObj.GetComponent<Image>();
                Debug.Log("SlowTimeOverlay found and assigned.");
            }
            else
            {
                Debug.LogWarning("SlowTimeOverlay not found in the scene.");
            }
        }

        // Assign FreezeTimeOverlay if not already assigned
        if (freezeTimeOverlay == null)
        {
            GameObject freezeOverlayObj = GameObject.Find("FreezeTimeOverlay");
            if (freezeOverlayObj != null)
            {
                freezeTimeOverlay = freezeOverlayObj.GetComponent<Image>();
                Debug.Log("FreezeTimeOverlay found and assigned.");
            }
            else
            {
                Debug.LogWarning("FreezeTimeOverlay not found in the scene.");
            }
        }
    }

    /// <summary>
    /// Activates slow time effect.
    /// </summary>
    public void ActivateSlowTime()
    {
        if (isTimeSlowed || isTimeFrozen)
            return; // Prevent overlapping effects

        StartCoroutine(SlowTimeCoroutine());
    }

    /// <summary>
    /// Activates freeze time effect.
    /// </summary>
    public void ActivateFreezeTime()
    {
        if (isTimeFrozen)
            return; // Prevent overlapping freeze effects

        StartCoroutine(FreezeTimeCoroutine());
    }

    /// <summary>
    /// Coroutine to handle slow time effect with visual and audio feedback.
    /// </summary>
    private IEnumerator SlowTimeCoroutine()
    {
        isTimeSlowed = true;

        // Apply slow time
        Time.timeScale = slowFactor;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Adjust fixedDeltaTime for physics

        // Enable slow time overlay
        if (slowTimeOverlay != null)
        {
            slowTimeOverlay.enabled = true;
        }

        // Play slow time sound if available
        if (audioSource != null && slowTimeSound != null)
        {
            audioSource.PlayOneShot(slowTimeSound);
        }

        // Start screen shake
        ScreenShake();

        // Wait for the duration in real time
        yield return new WaitForSecondsRealtime(slowDuration);

        // Reset time scale
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Disable slow time overlay
        if (slowTimeOverlay != null)
        {
            slowTimeOverlay.enabled = false;
        }

        isTimeSlowed = false;
    }

    /// <summary>
    /// Coroutine to handle freeze time effect with visual and audio feedback.
    /// </summary>
    private IEnumerator FreezeTimeCoroutine()
    {
        isTimeFrozen = true;

        // Apply freeze time
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Essentially stops physics

        // Enable freeze time overlay
        if (freezeTimeOverlay != null)
        {
            freezeTimeOverlay.enabled = true;
        }

        // Play freeze time sound if available
        if (audioSource != null && freezeTimeSound != null)
        {
            audioSource.PlayOneShot(freezeTimeSound);
        }

        // Start screen shake
        ScreenShake();

        // Wait for the duration in real time
        yield return new WaitForSecondsRealtime(freezeDuration);

        // Reset time scale
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Disable freeze time overlay
        if (freezeTimeOverlay != null)
        {
            freezeTimeOverlay.enabled = false;
        }

        isTimeFrozen = false;
    }

    /// <summary>
    /// Triggers the Cinemachine impulse to shake the camera.
    /// </summary>
    public void ScreenShake()
{
    if (impulseSource != null)
    {
        impulseSource.GenerateImpulse(); // Trigger the shake
        Debug.Log("Cinemachine impulse shake triggered.");
    }
    else
    {
        Debug.LogError("Cinemachine Impulse Source is missing or not assigned.");
    }
}


    /// <summary>
    /// Ensures that time scale resets when the game object is destroyed or the game quits.
    /// </summary>
    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Disable overlays to prevent them from persisting
        if (slowTimeOverlay != null)
        {
            slowTimeOverlay.enabled = false;
        }
        if (freezeTimeOverlay != null)
        {
            freezeTimeOverlay.enabled = false;
        }
    }

    private void OnApplicationQuit()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
