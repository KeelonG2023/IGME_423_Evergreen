using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool onlyWhenSelected = false;
    [SerializeField] private bool drawLabels = true;
    [SerializeField] private float axisLength = 10f;
    [SerializeField] private float frustumDrawDistance = 150f;
    [SerializeField] private Color boundsColor = new Color(0.25f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color rawBoundsColor = new Color(0.25f, 0.95f, 0.45f, 0.3f);
    [SerializeField] private Color frustumColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    [SerializeField] private Color footprintColor = new Color(0.3f, 0.75f, 1f, 0.9f);

    // Extra slack added to the inspector bounds in Start(). Kept here so the gizmos can preview it in edit mode.
    private const float PadMinX = 20f, PadMaxX = 20f, PadMinZ = 200f, PadMaxZ = 50f;
    private bool boundsPadded;

    private Vector2 moveInput;
    private float rotateDirection;
    private bool dragPanMoveActive = false;
    private Vector2 previousMousePosition;
    private float targetFOV = 50.0f;

    private void Start()
    {
        minX -= PadMinX;
        maxX += PadMaxX;
        minZ -= PadMinZ;
        maxZ += PadMaxZ;
        boundsPadded = true;
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
        // Scrolling over the HUD (e.g. the build panel's card list) scrolls the UI, not the camera.
        bool pointerOverUi = UnityEngine.EventSystems.EventSystem.current != null
                             && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!pointerOverUi && Input.mouseScrollDelta.y > 0)
        {
            targetFOV -= 5;
        }
        if (!pointerOverUi && Input.mouseScrollDelta.y < 0)
        {
            targetFOV += 5;
        }

        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
        cinemachineCamera.m_Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.m_Lens.FieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }

    // ---------------------------------------------------------------- Gizmos

    private void OnDrawGizmos()
    {
        if (drawGizmos && !onlyWhenSelected) DrawCameraGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmos && onlyWhenSelected) DrawCameraGizmos();
    }

    // Bounds the rig is actually clamped to (inspector values + Start() padding).
    private void GetEffectiveBounds(out float x0, out float x1, out float z0, out float z1)
    {
        if (boundsPadded) { x0 = minX; x1 = maxX; z0 = minZ; z1 = maxZ; }
        else { x0 = minX - PadMinX; x1 = maxX + PadMaxX; z0 = minZ - PadMinZ; z1 = maxZ + PadMaxZ; }
    }

    private void DrawCameraGizmos()
    {
        Vector3 p = transform.position;
        float groundY = p.y;

        // Pan bounds: solid = effective clamp, faint = raw inspector values (edit mode only; Start() overwrites them in play).
        GetEffectiveBounds(out float x0, out float x1, out float z0, out float z1);
        Gizmos.color = boundsColor;
        DrawRect(x0, x1, z0, z1, groundY);
        if (!boundsPadded)
        {
            Gizmos.color = rawBoundsColor;
            DrawRect(minX, maxX, minZ, maxZ, groundY);
        }

        // Rig pivot + local axes (blue = forward/W, red = right/D).
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(p, axisLength * 0.12f);
        Gizmos.color = Color.blue;
        DrawArrow(p, transform.forward * axisLength);
        Gizmos.color = Color.red;
        DrawArrow(p, transform.right * axisLength);

        // Live keyboard move direction.
        if (Application.isPlaying && moveInput.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.cyan;
            Vector3 dir = transform.forward * moveInput.y + transform.right * moveInput.x;
            DrawArrow(p + Vector3.up * 0.1f, dir.normalized * axisLength * 0.8f);
        }

        // Out of bounds (edge scroll / drag pan aren't clamped): line back to the nearest legal point.
        Vector3 clamped = new Vector3(Mathf.Clamp(p.x, x0, x1), p.y, Mathf.Clamp(p.z, z0, z1));
        bool outside = (clamped - p).sqrMagnitude > 0.0001f;
        if (outside)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(p, clamped);
            Gizmos.DrawWireSphere(clamped, axisLength * 0.08f);
        }

        DrawVirtualCameraGizmos(groundY);

#if UNITY_EDITOR
        if (drawLabels)
        {
            string label = $"CameraSystem\nX {x0:0}..{x1:0}  Z {z0:0}..{z1:0}";
            if (cinemachineCamera != null)
            {
                var lens = cinemachineCamera.m_Lens;
                label += lens.Orthographic
                    ? $"\nOrtho size {lens.OrthographicSize:0.#}"
                    : $"\nFOV {lens.FieldOfView:0.#} (target {targetFOV:0.#}, {minFOV:0}-{maxFOV:0})";
            }
            if (outside) label += "\nOUT OF BOUNDS";
            Handles.color = outside ? Color.red : Color.white;
            Handles.Label(p + Vector3.up * axisLength * 0.4f, label);
        }
#endif
    }

    private void DrawVirtualCameraGizmos(float groundY)
    {
        if (cinemachineCamera == null) return;

        Transform vt = cinemachineCamera.transform;
        LensSettings lens = cinemachineCamera.m_Lens;
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float near = Mathf.Max(0.01f, lens.NearClipPlane);
        float far = Mathf.Min(lens.FarClipPlane, frustumDrawDistance);

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(vt.position, vt.rotation, Vector3.one);
        if (lens.Orthographic)
        {
            float h = lens.OrthographicSize * 2f;
            Gizmos.color = frustumColor;
            Gizmos.DrawWireCube(new Vector3(0f, 0f, (near + far) * 0.5f), new Vector3(h * aspect, h, far - near));
        }
        else
        {
            // Current view, plus faint min/max zoom limits.
            Gizmos.color = frustumColor;
            Gizmos.DrawFrustum(Vector3.zero, lens.FieldOfView, far, near, aspect);
            Color faint = frustumColor; faint.a *= 0.25f;
            Gizmos.color = faint;
            Gizmos.DrawFrustum(Vector3.zero, minFOV, far, near, aspect);
            Gizmos.DrawFrustum(Vector3.zero, maxFOV, far, near, aspect);
        }
        Gizmos.matrix = oldMatrix;

        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        // Look-at point on the ground.
        Ray look = new Ray(vt.position, vt.forward);
        if (ground.Raycast(look, out float d))
        {
            Vector3 hit = look.GetPoint(d);
            Gizmos.color = frustumColor;
            Gizmos.DrawLine(vt.position, hit);
            Gizmos.DrawWireSphere(hit, axisLength * 0.1f);
        }

        // What the screen covers on the ground.
        Vector2[] corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        Vector3[] pts = new Vector3[4];
        float tanY = Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        for (int i = 0; i < 4; i++)
        {
            Ray r;
            if (lens.Orthographic)
            {
                Vector3 offset = vt.right * corners[i].x * lens.OrthographicSize * aspect + vt.up * corners[i].y * lens.OrthographicSize;
                r = new Ray(vt.position + offset, vt.forward);
            }
            else
            {
                Vector3 local = new Vector3(corners[i].x * tanY * aspect, corners[i].y * tanY, 1f);
                r = new Ray(vt.position, vt.rotation * local);
            }
            if (!ground.Raycast(r, out float dist) || dist > frustumDrawDistance * 4f) return; // corner sees sky
            pts[i] = r.GetPoint(dist);
        }
        Gizmos.color = footprintColor;
        for (int i = 0; i < 4; i++) Gizmos.DrawLine(pts[i], pts[(i + 1) % 4]);
    }

    private static void DrawRect(float x0, float x1, float z0, float z1, float y)
    {
        Vector3 a = new Vector3(x0, y, z0), b = new Vector3(x1, y, z0), c = new Vector3(x1, y, z1), e = new Vector3(x0, y, z1);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, e); Gizmos.DrawLine(e, a);
    }

    private static void DrawArrow(Vector3 from, Vector3 v)
    {
        if (v.sqrMagnitude < 1e-6f) return;
        Vector3 to = from + v;
        Gizmos.DrawLine(from, to);
        float len = v.magnitude;
        Vector3 back = -v / len * len * 0.2f;
        Vector3 side = Vector3.Cross(Vector3.up, v / len) * len * 0.1f;
        Gizmos.DrawLine(to, to + back + side);
        Gizmos.DrawLine(to, to + back - side);
    }
}
