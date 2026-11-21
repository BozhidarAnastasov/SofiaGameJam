using System.Collections.Generic;
using UnityEngine;

/// The little "U" key shown over the girl's head when something can be used.
/// Interactables call Request(this, true/false); the key shows while at least one wants it.
/// Fades in/out and bobs gently. Lives on a child of the Player (Tools > Horror Room > 8).
public class InteractPrompt : MonoBehaviour
{
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobSpeed = 3f;

    private readonly HashSet<Object> requests = new HashSet<Object>();
    private Vector3 home;
    private float alpha;

    void Awake()
    {
        if (icon == null) icon = GetComponentInChildren<SpriteRenderer>(true);
        home = transform.localPosition;
        SetAlpha(0f);
    }

    public void Request(Object source, bool show)
    {
        if (show) requests.Add(source); else requests.Remove(source);
    }

    void Update()
    {
        requests.RemoveWhere(r => r == null);
        float target = requests.Count > 0 ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.deltaTime);
        SetAlpha(alpha);
        transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void SetAlpha(float a)
    {
        if (icon == null) return;
        var c = icon.color; c.a = a; icon.color = c;
        icon.enabled = a > 0.001f;
    }
}
