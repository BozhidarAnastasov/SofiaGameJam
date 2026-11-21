using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private InputActionReference interactActionReference;
    [SerializeField] private GameObject interactionIcon;

    private Collider2D colliderInRange;
    private InputAction interactAction;
    private InteractionPrompt prompt;
    private bool enabledAction;
    private Interactable Target => colliderInRange != null && colliderInRange.isActiveAndEnabled
        ? colliderInRange.GetComponentInParent<Interactable>() : null;

    private void Awake()
    {
        if (interactionIcon == null) return;
        prompt = interactionIcon.GetComponent<InteractionPrompt>();
        if (prompt == null) prompt = interactionIcon.AddComponent<InteractionPrompt>();
        prompt.Initialize();
        interactionIcon.SetActive(false);
    }

    private void OnEnable()
    {
        interactAction = interactActionReference != null ? interactActionReference.action : null;
        if (interactAction == null)
        {
            Debug.LogWarning("Assign an interaction action to the InteractionDetector.", this);
            return;
        }
        interactAction.performed += OnInteract;
        enabledAction = !interactAction.enabled;
        if (enabledAction) interactAction.Enable();
        InputSystem.onActionChange += OnActionChange;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
        if (interactAction != null)
        {
            interactAction.performed -= OnInteract;
            if (enabledAction) interactAction.Disable();
        }
        colliderInRange = null;
        if (interactionIcon != null) interactionIcon.SetActive(false);
    }

    private void Update() => RefreshPrompt();

    private void RefreshPrompt()
    {
        Interactable target = Target;
        bool canInteract = target != null && target.CanInteract();
        if (interactionIcon != null)
            interactionIcon.SetActive(canInteract && interactAction != null && interactAction.enabled);
        if (prompt != null) prompt.Refresh(interactAction);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        Interactable target = Target;
        if (target != null && target.CanInteract()) target.Interact();
        RefreshPrompt(); // Hide the icon immediately after opening a cupboard.
    }

    private void OnTriggerEnter2D(Collider2D other) => OnTriggerStay2D(other);

    private void OnTriggerStay2D(Collider2D other)
    {
        Interactable currentTarget = Target;
        if (currentTarget != null && currentTarget.CanInteract()) return;
        Interactable target = other.GetComponentInParent<Interactable>();
        if (target != null && target.CanInteract()) colliderInRange = other;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == colliderInRange) colliderInRange = null;
    }

    private void OnActionChange(object source, InputActionChange change)
    {
        // Movement switches the prompt before Interact is pressed.
        if (change == InputActionChange.ActionPerformed && source is InputAction action && prompt != null)
            prompt.SetDevice(action.activeControl?.device);
    }
}
