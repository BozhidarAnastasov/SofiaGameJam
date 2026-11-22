using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private InputActionReference interactActionReference;

    private Collider2D colliderInRange;
    private InputAction interactAction;
    private InteractPrompt prompt;
    private bool enabledAction;
    private Interactable Target => colliderInRange != null && colliderInRange.isActiveAndEnabled
        ? colliderInRange.GetComponentInParent<Interactable>() : null;

    private void Awake()
    {
        var player = GetComponentInParent<TopDownMovement>();
        if (player != null) prompt = player.GetComponentInChildren<InteractPrompt>(true);
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
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteract;
            if (enabledAction) interactAction.Disable();
        }
        colliderInRange = null;
    }

    private void Update() => RefreshPrompt();

    private void RefreshPrompt()
    {
        Interactable target = Target;
        bool canInteract = target != null && target.CanInteract();
        if (canInteract && prompt != null) prompt.Request(colliderInRange, true, interactAction);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        Interactable target = Target;
        if (target != null && target.CanInteract()) target.Interact();
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
}
