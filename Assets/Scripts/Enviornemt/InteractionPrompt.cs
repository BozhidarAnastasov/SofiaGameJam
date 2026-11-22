using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionPrompt : MonoBehaviour
{
    private TextMeshPro label;
    private SpriteRenderer circle;
    private InputDevice lastDevice;

    public void Initialize()
    {
        if (label != null) return;
        var textObject = new GameObject("Button Label");
        textObject.transform.SetParent(transform, false);
        label = textObject.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(0.5f, 0.5f);
        label.fontSize = 4f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = 4f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.richText = false;
        circle = GetComponent<SpriteRenderer>();
        if (circle == null) return;
        label.sortingLayerID = circle.sortingLayerID;
        label.sortingOrder = circle.sortingOrder + 1;
    }

    public void SetDevice(InputDevice device) => lastDevice = device;

    public void SetAlpha(float alpha)
    {
        label.alpha = alpha;
        label.enabled = alpha > 0f;
        if (circle == null) return;
        var color = circle.color;
        color.a = alpha;
        circle.color = color;
        circle.enabled = alpha > 0f;
    }

    public void Refresh(InputAction action)
    {
        if (label == null || action == null) return;
        InputControl selected = null;
        foreach (InputControl control in action.controls)
        {
            if (control.device == lastDevice) { selected = control; break; }
            if (selected == null || control.device is Keyboard) selected = control;
        }
        if (selected == null) { label.text = "?"; return; }
        int binding = action.GetBindingIndexForControl(selected);
        string button = binding >= 0 ? action.GetBindingDisplayString(binding,
            InputBinding.DisplayStringOptions.DontIncludeInteractions) : selected.displayName;
        // Cabinet labels are A-F, rather than the joystick's generic Trigger/Button names.
        if (!(selected.device is Keyboard) && !(selected.device is Gamepad)
            && action.actionMap?.name == "Arcade" && action.name.StartsWith("Button"))
            button = action.name.Substring("Button".Length);
        if (label.text != button) label.text = button;
    }
}
