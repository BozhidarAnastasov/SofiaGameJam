using System.Collections;
using UnityEngine;

public class BounceEffect : MonoBehaviour
{
    [SerializeField, Min(0f)] private float height = 0.35f;
    [SerializeField, Min(0.01f)] private float duration = 0.35f;

    private Coroutine bounce;
    private Vector3 restingPosition;

    public void StartBounce()
    {
        if (bounce != null)
        {
            StopCoroutine(bounce);
            transform.position = restingPosition;
        }
        restingPosition = transform.position;
        bounce = StartCoroutine(AnimateBounce());
    }

    // A single hop that lands exactly where the item was spawned. No tween library.
    private IEnumerator AnimateBounce()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = restingPosition + Vector3.up * (Mathf.Sin(elapsed / duration * Mathf.PI) * height);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = restingPosition;
        bounce = null;
    }

    private void OnDisable()
    {
        if (bounce == null) return;
        StopCoroutine(bounce);
        transform.position = restingPosition;
        bounce = null;
    }
}
