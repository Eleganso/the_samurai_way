using UnityEngine;

public class DontDestroyOnLoadUI : MonoBehaviour
{
    private static DontDestroyOnLoadUI instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Make this UI element persistent across scenes
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
    }
}
