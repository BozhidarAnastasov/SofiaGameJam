using UnityEngine;

public class ChaserKillTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    // Fires automatically if the Chaser's collider has "Is Trigger" CHECKED
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            KillPlayer();
        }
    }

    // Fires automatically if the Chaser uses a solid physics collider ("Is Trigger" UNCHECKED)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(playerTag))
        {
            KillPlayer();
        }
    }

    private void KillPlayer()
    {
        // Finds your HUD_Canvas script automatically and launches the 4-frame face fade!
        DeathScreenController deathUI = FindFirstObjectByType<DeathScreenController>();
        if (deathUI != null)
        {
            deathUI.TriggerDeath();
        }
        else
        {
            Debug.LogWarning("Chaser caught Player, but no DeathScreenController was found in the scene!");
        }
    }
}