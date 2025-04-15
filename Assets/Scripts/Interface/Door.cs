// Door.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    public string sceneToLoad;
    public string doorPointID;  // Identifier for the door point in the next scene

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player detected entering door.");
            GameManager.Instance.SaveEnemyStates(); // Save enemy states before transitioning
            PlayerManager.Instance.SaveHonorRecoveryData(); // Save honor recovery data before changing scenes
            GameManager.Instance.SetDoorPoint(doorPointID); // Set the door point for the new scene
            StartCoroutine(FadeAndLoadScene());
        }
    }

    private IEnumerator FadeAndLoadScene()
    {
        Debug.Log("Starting FadeAndLoadScene coroutine.");
        float fadeDuration = 0.1f;
        float fadeAmount;

        // Add your fade-out code here
        for (fadeAmount = 0; fadeAmount <= 1; fadeAmount += Time.deltaTime / fadeDuration)
        {
            // Assuming you have a fadeImage defined
            // fadeImage.color = new Color(0, 0, 0, fadeAmount);
            yield return null;
        }

        Debug.Log($"Loading scene: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }
}
