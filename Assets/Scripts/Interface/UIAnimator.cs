using UnityEngine;

public class UIAnimator : MonoBehaviour
{
    void Update()
    {
        // Use unscaled delta time to keep UI animations running during slow or freeze time
        float rotationSpeed = 100f;
        transform.Rotate(0, 0, rotationSpeed * Time.unscaledDeltaTime);
    }
}
