
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference sprintAction;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isSprinting;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        sprintAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        sprintAction.action.Disable();
    }

    void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();
        isSprinting = sprintAction.action.IsPressed();
    }

    void FixedUpdate()
    {
        float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        rb.MovePosition(
            rb.position +
            moveInput.normalized * speed * Time.fixedDeltaTime
        );
    }
}
