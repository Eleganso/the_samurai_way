using UnityEngine;
using UnityEngine.UI; // Necessary import for the Image component
using System.Collections;

public class Bush : MonoBehaviour
{
    private Collider2D bushCollider;

    [SerializeField] private AudioSource hideSound; // Sound that plays when the player is hidden
    [SerializeField] private Image hiddenIcon; // The hidden icon using UI Image component
    private Coroutine hidingCoroutine; // Store the coroutine for hiding delay

    private void Awake()
    {
        bushCollider = GetComponent<Collider2D>();
        if (!bushCollider.isTrigger)
        {
            bushCollider.isTrigger = true; // Ensure the bush collider is a trigger
        }

        if (hiddenIcon != null)
        {
            hiddenIcon.fillAmount = 0f; // Start with no fill
            hiddenIcon.gameObject.SetActive(false); // Hide the icon initially
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            if (player != null)
            {
                player.isInBush = true;

                if (player.IsCrouching())
                {
                    StartHidingProcess(player);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            if (player != null)
            {
                player.isInBush = false;
                player.isCrouchingInBushes = false;

                StopHidingProcess(player);
                HideIcon(); // Hide the icon when player exits the bush
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            if (player != null)
            {
                if (player.IsCrouching() && IsPlayerFullyInsideBush(player))
                {
                    if (!player.isCrouchingInBushes && hidingCoroutine == null)
                    {
                        StartHidingProcess(player);
                    }
                }
                else
                {
                    StopHidingProcess(player);
                    player.isCrouchingInBushes = false;
                    HideIcon();
                }
            }
        }
    }

    private void StartHidingProcess(Player player)
    {
        if (hidingCoroutine != null)
        {
            StopCoroutine(hidingCoroutine);
        }

        hidingCoroutine = StartCoroutine(HidePlayerAfterDelay(player));
        ShowIcon(); // Show the hidden icon when hiding starts
    }

    private void StopHidingProcess(Player player)
    {
        // Check if StealthSkill1 or StealthSkill2 is active
        if (!PlayerManager.Instance.IsStealthSkillActive && !PlayerManager.Instance.IsStealthSkill2Active)
        {
            player.SetTransparency(player.normalAlpha);
            player.SetLayerOverride("Player"); // Revert to default layer
            Debug.Log("Player exited bush. Transparency and layer reset to normal.");
        }
        else
        {
            Debug.Log("StealthSkill1 or StealthSkill2 is active. Skipping transparency and layer reset.");
        }

        HideIcon(); // Hide the icon when hiding stops
    }

    private IEnumerator HidePlayerAfterDelay(Player player)
    {
        float elapsedTime = 0f;
        float hidingDelay = PlayerManager.Instance.hidingDelay; // Get hiding delay from PlayerManager

        while (elapsedTime < hidingDelay)
        {
            if (!player.IsCrouching() || !IsPlayerFullyInsideBush(player))
            {
                StopHidingProcess(player);
                yield break;
            }

            // Update the fill amount of the hidden icon based on elapsed time
            UpdateIconFill(elapsedTime / hidingDelay);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Mark the player as hidden and exclude the "Enemy" layer
        player.isCrouchingInBushes = true;
        player.SetLayerOverride("HiddenPlayer"); // Switch player layer to "HiddenPlayer"
        player.SetTransparency(player.hiddenAlpha); // Apply transparency when hidden

        if (hideSound != null)
        {
            hideSound.Play();
        }
        Debug.Log("Player is now hidden in the bush.");
    }

    private void ShowIcon()
    {
        if (hiddenIcon != null)
        {
            hiddenIcon.gameObject.SetActive(true);
            hiddenIcon.fillAmount = 0f; // Start filling from 0
        }
    }

    private void HideIcon()
    {
        if (hiddenIcon != null)
        {
            hiddenIcon.gameObject.SetActive(false);
        }
    }

    private void UpdateIconFill(float fillAmount)
    {
        if (hiddenIcon != null)
        {
            hiddenIcon.fillAmount = fillAmount; // Update fill value
        }
    }

    private bool IsPlayerFullyInsideBush(Player player)
    {
        Collider2D playerCollider = player.IsCrouching() ? player.colliderCrouching : player.colliderStanding;
        Bounds playerBounds = playerCollider.bounds;
        Bounds bushBounds = bushCollider.bounds;

        float margin = 0.01f;

        bool isWithin =
            (playerBounds.min.x > bushBounds.min.x + margin) &&
            (playerBounds.min.y > bushBounds.min.y + margin) &&
            (playerBounds.max.x < bushBounds.max.x - margin) &&
            (playerBounds.max.y < bushBounds.max.y - margin);

        return isWithin;
    }
}
