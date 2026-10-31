using Possession;
using UnityEngine;
using UnityEngine.InputSystem; 

[RequireComponent(typeof(Rigidbody))]
public class DriverController : MonoBehaviour, IPossessable
{
    [Header("Input References")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference interactAction;
    
    [Header("Camera Profile")]
    [Tooltip("Offset yang lebih dekat untuk karakter pejalan kaki")]
    public Vector3 driverCameraOffset = new Vector3(0, 6f, -6f);

    public Transform CameraTarget => transform; 
    public Vector3 CameraOffset => driverCameraOffset;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 6f;
    [Tooltip("Kecepatan karakter berputar menghadap arah gerak")]
    public float rotationSpeed = 15f; 

    [Header("Interaction Settings")]
    public float interactRange = 2f;
    public LayerMask interactableLayer;

    [Header("Ground Detection")]
    public float characterHeight = 2f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool isPossessed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Kunci rotasi X dan Z agar karakter tidak terguling
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    // ==========================================
    // IMPLEMENTASI IPOSSESSABLE
    // ==========================================
    public void OnPossess()
    {
        isPossessed = true;
        gameObject.SetActive(true);

        if (moveAction != null) moveAction.action.Enable();
        if (interactAction != null) interactAction.action.Enable();
        
        if (jumpAction != null) 
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJumpPerformed; 
        }
    }

    public void OnUnpossess()
    {
        isPossessed = false;

        if (jumpAction != null) jumpAction.action.performed -= OnJumpPerformed; 

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        moveInput = Vector2.zero; 

        gameObject.SetActive(false);
    }

    public void ProcessInput()
    {
        GatherInputReference();
        HandleInteractionRaycast();
    }

    // ==========================================
    // ORCHESTRATOR & LOGIC
    // ==========================================
    private void Update()
    {
        CheckGroundedState();
    }

    private void FixedUpdate()
    {
        if (!isPossessed) return;
        ExecuteMovement();
    }

    private void GatherInputReference()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.action.ReadValue<Vector2>();
        }
    }

    private void HandleInteractionRaycast()
    {
        if (interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            // Bantuan visual di tab Scene saat Play (garis merah)
            Debug.DrawRay(transform.position, transform.forward * interactRange, Color.red, 2f);

            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, interactRange, interactableLayer))
            {
                if (hit.collider.TryGetComponent(out VehicleInteract carDoor))
                {
                    carDoor.carController.SetStoredDriver(this);
                    PossessionManager.Instance.Possess(carDoor.carController);
                    return; 
                }
            }
        }
    }

    private void CheckGroundedState()
    {
        float rayDistance = (characterHeight / 2f) + 0.1f;
        isGrounded = Physics.Raycast(transform.position, Vector3.down, rayDistance, groundLayer);
    }

    private void ExecuteMovement()
    {
        // 1. Kalkulasi Vektor Arah
        Vector3 moveDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        
        // 2. Terapkan Kecepatan Linear (Posisi)
        Vector3 targetVelocity = moveDir * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; 
        rb.linearVelocity = targetVelocity;

        // 3. Terapkan Rotasi (Menghadap Arah Gerak)
        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (!isPossessed) return;

        if (isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
}