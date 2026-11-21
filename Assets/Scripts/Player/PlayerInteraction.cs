
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField]
    private InputActionReference interactActionReference;

    private void OnEnable()
    {
        interactActionReference.action.performed += OnInteract;
        interactActionReference.action.Enable();
    }

    private void OnDisable()
    {
        interactActionReference.action.performed -= OnInteract;
        interactActionReference.action.Disable();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        Debug.Log("Interaction pressed!");

        // Check for a nearby interactable object here.
    }
}
