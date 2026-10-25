using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.PlayerSettings;
using Cinemachine;

public class CameraSystem : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera cinemachineCamera;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float dragSpeed;
    [SerializeField] private float rotateSpeed;
    [SerializeField] private int edgeScrollSize;
    [SerializeField] private bool useEdgeScrolling;
    [SerializeField] private bool useDragPan;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minFOV = 10;
    [SerializeField] private float maxFOV = 50;
    [SerializeField] private float minX;
    [SerializeField] private float maxX;
    [SerializeField] private float minZ;
    [SerializeField] private float maxZ;

    private Vector2 moveInput;
    private float rotateDirection;
    private bool dragPanMoveActive = false;
    private Vector2 previousMousePosition;
    private float targetFOV = 50.0f;

    private void Start()
    {
        minX -= 20;
        maxX += 20;
        minZ -= 200;
        maxZ += 50;
    }

    private void Update()
    {
        HandleMovement();
        HandleEdgeScrolling();
        HandleDragPan();
        HandleRotation();
        HandleZoom();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnRotate(InputAction.CallbackContext context)
    {
        rotateDirection = context.ReadValue<Vector2>().y;
    }

    private void HandleMovement()
    {
        // Position handling 
        Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
        Vector3 position = transform.position + moveDirection * moveSpeed * Time.deltaTime;
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);
        transform.position = position;
        //transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void HandleEdgeScrolling()
    {
        if (!useEdgeScrolling) return;
        Vector2 edgeMoveInput = Vector2.zero;

        if (Input.mousePosition.x < edgeScrollSize)
        {
            edgeMoveInput.x = -1f;
        }
        if (Input.mousePosition.y < edgeScrollSize)
        {
            edgeMoveInput.y = -1f;
        }
        if (Input.mousePosition.x > Screen.width - edgeScrollSize)
        {
            edgeMoveInput.x = 1f;
        }
        if (Input.mousePosition.x > Screen.height - edgeScrollSize)
        {
            edgeMoveInput.y = 1f;
        }

        // Position handling 
        Vector3 moveDirection = transform.forward * edgeMoveInput.y + transform.right * edgeMoveInput.x;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void HandleDragPan()
    {
        if (!useDragPan) return;
        Vector2 panMoveInput = Vector2.zero;

        if (Input.GetMouseButtonDown(1))
        {
            dragPanMoveActive = true;
            previousMousePosition = (Vector2)Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(1)) dragPanMoveActive = false;

        if (dragPanMoveActive)
        {
            Vector2 mouseDelta = (Vector2)Input.mousePosition - previousMousePosition;
            panMoveInput = mouseDelta * dragSpeed;
            previousMousePosition = (Vector2)Input.mousePosition;
        }

        // Position handling 
        Vector3 moveDirection = transform.forward * panMoveInput.y + transform.right * panMoveInput.x;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        transform.eulerAngles += new Vector3(0.0f, rotateDirection * rotateSpeed * Time.deltaTime, 0.0f);
    }

    private void HandleZoom()
    {
        if (Input.mouseScrollDelta.y > 0)
        {
            targetFOV -= 5;
        }
        if (Input.mouseScrollDelta.y < 0)
        {
            targetFOV += 5;
        }

        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
        cinemachineCamera.m_Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.m_Lens.FieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }
}
