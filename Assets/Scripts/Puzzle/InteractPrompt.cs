using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(InteractionPrompt))]
public class InteractPrompt : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobSpeed = 3f;

    private readonly Dictionary<Component, InputAction> requests = new Dictionary<Component, InputAction>();
    private InteractionPrompt display;
    private Vector3 home;
    private float alpha;

    void Awake()
    {
        display = GetComponent<InteractionPrompt>();
        display.Initialize();
        display.SetAlpha(0f);
        home = transform.localPosition;
    }

    void OnEnable() => InputSystem.onActionChange += OnActionChange;
    void OnDisable() => InputSystem.onActionChange -= OnActionChange;

    private void OnActionChange(object source, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed && source is InputAction action && action.activeControl != null)
            display.SetDevice(action.activeControl.device);
    }

    public void Request(Component source, bool show, InputAction action = null)
    {
        if (show && action != null && action.enabled) requests[source] = action;
        else requests.Remove(source);
    }

    void LateUpdate()
    {
        InputAction action = null;
        float nearest = float.PositiveInfinity;
        foreach (var request in requests)
        {
            if (request.Key == null || !request.Key.gameObject.activeInHierarchy || !request.Value.enabled) continue;
            float distance = (request.Key.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            action = request.Value;
        }
        requests.Clear();

        if (action != null) display.Refresh(action);
        alpha = Mathf.MoveTowards(alpha, action != null ? 1f : 0f, fadeSpeed * Time.deltaTime);
        display.SetAlpha(alpha);
        transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }
}
