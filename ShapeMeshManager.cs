using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // Required for UI checks
using UnityEngine.UI; // REQUIRED for Button type
using System; // Required for Type

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ShapeMeshManager : MonoBehaviour
{
    [Header("Manager Settings")]
    [Tooltip("Check this box ONLY for the ShapeMeshManager GameObject you place directly in the scene. This instance will manage spawning new meshes.")]
    public bool isSpawner = false;

    // NEW: Separate arrays for activating drawing modes
    [Header("Activation Settings")]
    [Tooltip("Drag the UI Buttons here that should activate BOX drawing.")]
    public Button[] activateBoxDrawingButtons;
    [Tooltip("Drag the UI Buttons here that should activate CIRCLE drawing.")]
    public Button[] activateCircleDrawingButtons;

    [Header("Mesh Creation Settings")]
    [Tooltip("Material for the generated mesh. IMPORTANT: Assign a visible material here.")]
    public Material meshMaterial;

    [Tooltip("Material to use if the generated mesh is on an invalid slope.")]
    public Material invalidMaterial;

    [Header("Normal Settings")]
    [Tooltip("If true, the mesh normals will be inverted (making the inside surface visible from the outside).")]
    public bool invertMesh = false;

    [Header("Ground Conformance")]
    [Tooltip("The LayerMask for the ground object(s) the mesh should conform to.")]
    public LayerMask groundLayer;
    [Tooltip("Offset above the ground where the mesh vertices will be placed.")]
    public float groundOffset = 0.05f;

    [Header("Mesh Generation Details")]
    [Tooltip("If true, the MeshCollider will be marked as convex. Convex MeshColliders can interact with other convex colliders.")]
    public bool makeConvex = false;

    [Header("Circle Draw Settings")] // Settings for circle drawing
    [Tooltip("Number of segments for the circle mesh. Higher values make the circle smoother.")]
    [Range(8, 64)]
    public int circleSegments = 32;

    [Header("Slope Detection Settings")] // Slope detection settings
    [Tooltip("Maximum angle (in degrees) from vertical that the ground can have for the shape to be considered on a valid slope.")]
    [Range(0, 90)]
    public float maxSlopeAngle = 45f; // Default max slope angle

    // Components to copy to the generated mesh
    [Header("Dynamic Components")]
    [Tooltip("Drag and drop components (e.g., Rigidbody, Collider, your custom scripts) from other GameObjects here. Their type and *serializable* data will be copied to the generated mesh. Note: Only serializable fields will be copied.")]
    public Component[] componentsToCopy;

    // Internal state for drawing
    private Vector3? startDragPoint = null;
    private MeshFilter previewMeshFilter;
    private MeshRenderer previewMeshRenderer;

    private static ShapeMeshManager currentDrawingInstance;
    private static bool isDrawingSessionActive = false;
    private DrawingType _activeDrawingType = DrawingType.None; // New internal state for active drawing type

    private enum DrawingType
    {
        None,
        Box,
        Circle
    }

    // Store the last valid end drag point for clamping size
    private Vector3 _lastValidEndDragPoint;
    private bool _isCurrentlyInvalidSlope = false;


    void Awake()
    {
        previewMeshFilter = GetComponent<MeshFilter>();
        previewMeshRenderer = GetComponent<MeshRenderer>();
        previewMeshRenderer.enabled = false;

        if (meshMaterial == null)
        {
            Debug.LogError("Mesh Material is not assigned in ShapeMeshManager. Please assign one in the Inspector.");
        }
        if (invalidMaterial == null)
        {
            Debug.LogWarning("Invalid Material is not assigned in ShapeMeshManager. Shapes on invalid slopes will use the regular mesh material for finalization and a warning will be logged.");
        }
    }

    void OnEnable()
    {
        if (isSpawner)
        {
            // Subscribe for Box Drawing buttons
            if (activateBoxDrawingButtons != null)
            {
                foreach (Button button in activateBoxDrawingButtons)
                {
                    if (button != null)
                    {
                        button.onClick.AddListener(StartNewBoxDrawingSession);
                        Debug.Log($"Subscribed to '{button.name}' for Box Drawing.");
                    }
                }
            }

            // Subscribe for Circle Drawing buttons
            if (activateCircleDrawingButtons != null)
            {
                foreach (Button button in activateCircleDrawingButtons)
                {
                    if (button != null)
                    {
                        button.onClick.AddListener(StartNewCircleDrawingSession);
                        Debug.Log($"Subscribed to '{button.name}' for Circle Drawing.");
                    }
                }
            }
        }
    }

    void OnDisable()
    {
        if (isSpawner)
        {
            // Unsubscribe for Box Drawing buttons
            if (activateBoxDrawingButtons != null)
            {
                foreach (Button button in activateBoxDrawingButtons)
                {
                    if (button != null)
                    {
                        button.onClick.RemoveListener(StartNewBoxDrawingSession);
                        Debug.Log($"Unsubscribed from '{button.name}' for Box Drawing.");
                    }
                }
            }

            // Unsubscribe for Circle Drawing buttons
            if (activateCircleDrawingButtons != null)
            {
                foreach (Button button in activateCircleDrawingButtons)
                {
                    if (button != null)
                    {
                        button.onClick.RemoveListener(StartNewCircleDrawingSession);
                        Debug.Log($"Unsubscribed from '{button.name}' for Circle Drawing.");
                    }
                }
            }
        }
    }

    void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (isSpawner && isDrawingSessionActive && _activeDrawingType != DrawingType.None)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Start drag
            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
                {
                    startDragPoint = hit.point + Vector3.up * groundOffset;
                    _lastValidEndDragPoint = startDragPoint.Value; // Initialize last valid point
                    _isCurrentlyInvalidSlope = false; // Reset slope status
                    currentDrawingInstance = this;
                    SetupPreviewMesh();
                    Debug.Log($"{_activeDrawingType} drawing started.");
                }
                else
                {
                    Debug.LogWarning("Mouse click did not hit any object on the specified Ground Layer!");
                }
            }
            // During drag
            else if (Input.GetMouseButton(0) && startDragPoint.HasValue)
            {
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
                {
                    Vector3 currentPoint = hit.point + Vector3.up * groundOffset;
                    UpdatePreviewMesh(startDragPoint.Value, currentPoint);
                }
            }
            // End drag
            else if (Input.GetMouseButtonUp(0) && startDragPoint.HasValue)
            {
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
                {
                    // For finalization, use the _lastValidEndDragPoint if it's currently invalid,
                    // otherwise use the current endDragPoint.
                    Vector3 endDragPointForFinalization = _isCurrentlyInvalidSlope ? _lastValidEndDragPoint : (hit.point + Vector3.up * groundOffset);
                    
                    FinalizeShapeMesh(startDragPoint.Value, endDragPointForFinalization);
                    CleanupDrawingSession(); // New method to clean up all session state
                    Debug.Log($"{_activeDrawingType} drawing session ended.");
                }
                else
                {
                    Debug.LogWarning("Mouse released but did not hit any object on the specified Ground Layer!");
                    CleanupDrawingSession();
                }
            }

            // Reset drawing with 'R' key
            if (Input.GetKeyDown(KeyCode.R))
            {
                CleanupDrawingSession();
                Debug.Log("Drawing reset by R key.");
            }
        }
    }

    // NEW: Separate methods for starting box/circle drawing
    public void StartNewBoxDrawingSession()
    {
        StartNewDrawingSession(DrawingType.Box);
    }

    public void StartNewCircleDrawingSession()
    {
        StartNewDrawingSession(DrawingType.Circle);
    }

    private void StartNewDrawingSession(DrawingType type)
    {
        if (isSpawner)
        {
            if (!isDrawingSessionActive)
            {
                isDrawingSessionActive = true;
                _activeDrawingType = type;
                Debug.Log($"Drawing session activated for {_activeDrawingType}. Click and drag on the ground to draw.");
            }
            else
            {
                Debug.LogWarning($"Cannot start a new drawing session for {type}. An existing drawing session for {_activeDrawingType} is in progress. Please finalize or reset the current drawing.");
            }
        }
        else
        {
            Debug.LogError("This ShapeMeshManager instance is not marked as the spawner. The button should call the spawner's method.");
        }
    }

    private void CleanupDrawingSession()
    {
        CleanupPreviewMesh();
        startDragPoint = null;
        isDrawingSessionActive = false;
        _activeDrawingType = DrawingType.None; // Reset active drawing type
        _isCurrentlyInvalidSlope = false; // Reset slope status
    }

    private void SetupPreviewMesh()
    {
        previewMeshFilter.mesh = new Mesh();
        previewMeshRenderer.enabled = true;
        previewMeshRenderer.material = meshMaterial; // Start with valid material
    }

    private void UpdatePreviewMesh(Vector3 p1, Vector3 currentDragPoint)
    {
        List<Vector3> pointsForSlopeCheck = new List<Vector3>();
        Vector3 pointToDrawWith = currentDragPoint; // This will be the actual point used for mesh generation

        // Determine potential points for slope check based on active drawing mode
        if (_activeDrawingType == DrawingType.Box)
        {
            // For box, check the 4 corners
            Vector3 c1 = new Vector3(Mathf.Min(p1.x, currentDragPoint.x), currentDragPoint.y, Mathf.Min(p1.z, currentDragPoint.z));
            Vector3 c2 = new Vector3(Mathf.Max(p1.x, currentDragPoint.x), currentDragPoint.y, Mathf.Min(p1.z, currentDragPoint.z));
            Vector3 c3 = new Vector3(Mathf.Min(p1.x, currentDragPoint.x), currentDragPoint.y, Mathf.Max(p1.z, currentDragPoint.z));
            Vector3 c4 = new Vector3(Mathf.Max(p1.x, currentDragPoint.x), currentDragPoint.y, Mathf.Max(p1.z, currentDragPoint.z));
            pointsForSlopeCheck.AddRange(new Vector3[] { c1, c2, c3, c4 });
        }
        else if (_activeDrawingType == DrawingType.Circle)
        {
            // For circle, check the center and 8 points on circumference
            Vector3 flatCenter = new Vector3(p1.x, p1.y, p1.z);
            Vector3 flatMouse = new Vector3(currentDragPoint.x, p1.y, currentDragPoint.z);
            float radius = Vector3.Distance(flatCenter, flatMouse);

            pointsForSlopeCheck.Add(p1); // Center point

            int numCheckPoints = Mathf.Min(8, circleSegments);
            for (int i = 0; i < numCheckPoints; i++)
            {
                float angle = (float)i / numCheckPoints * 2 * Mathf.PI;
                float x = p1.x + radius * Mathf.Cos(angle);
                float z = p1.z + radius * Mathf.Sin(angle);

                // Raycast down from above to get the actual ground Y for slope check
                Vector3 checkPointRayOrigin = new Vector3(x, Camera.main.transform.position.y + 10f, z);
                RaycastHit hit;
                if (Physics.Raycast(checkPointRayOrigin, Vector3.down, out hit, Mathf.Infinity, groundLayer))
                {
                    pointsForSlopeCheck.Add(hit.point + Vector3.up * groundOffset);
                }
                else
                {
                    // Fallback if raycast fails, point will be at p1's y-level for slope check
                    pointsForSlopeCheck.Add(new Vector3(x, p1.y, z));
                }
            }
        }

        // Check slope validity
        bool isAnySlopeInvalid = false;
        foreach (Vector3 point in pointsForSlopeCheck)
        {
            if (!CheckSlopeValidity(point))
            {
                isAnySlopeInvalid = true;
                break;
            }
        }

        // Apply visual feedback and clamp size if needed
        if (isAnySlopeInvalid)
        {
            _isCurrentlyInvalidSlope = true;
            if (invalidMaterial != null)
            {
                previewMeshRenderer.material = invalidMaterial;
            }
            else
            {
                previewMeshRenderer.material = meshMaterial; // Fallback to normal material if invalid is not set
            }
            // Clamp currentDragPoint to the last valid point
            pointToDrawWith = _lastValidEndDragPoint;
        }
        else
        {
            _isCurrentlyInvalidSlope = false;
            previewMeshRenderer.material = meshMaterial;
            _lastValidEndDragPoint = currentDragPoint; // Update last valid point
            pointToDrawWith = currentDragPoint; // Use the actual current drag point
        }

        // Generate the preview mesh using the potentially clamped pointToDrawWith
        if (_activeDrawingType == DrawingType.Box)
        {
            GenerateQuadMesh(previewMeshFilter.mesh, p1, pointToDrawWith, transform, true);
        }
        else if (_activeDrawingType == DrawingType.Circle)
        {
            GenerateCircleMesh(previewMeshFilter.mesh, p1, pointToDrawWith, transform, true);
        }
    }

    private void CleanupPreviewMesh()
    {
        if (previewMeshFilter != null && previewMeshFilter.mesh != null)
        {
            Destroy(previewMeshFilter.mesh);
            previewMeshFilter.mesh = null;
        }
        if (previewMeshRenderer != null)
        {
            previewMeshRenderer.enabled = false;
        }
    }

    private void FinalizeShapeMesh(Vector3 p1, Vector3 p2)
    {
        // One final check for slope validity at the moment of release.
        // This is important because the user might have released on an invalid spot
        // after dragging back into a valid area or vice versa.
        List<Vector3> pointsForFinalSlopeCheck = new List<Vector3>();
        if (_activeDrawingType == DrawingType.Box)
        {
            Vector3 c1 = new Vector3(Mathf.Min(p1.x, p2.x), p2.y, Mathf.Min(p1.z, p2.z));
            Vector3 c2 = new Vector3(Mathf.Max(p1.x, p2.x), p2.y, Mathf.Min(p1.z, p2.z));
            Vector3 c3 = new Vector3(Mathf.Min(p1.x, p2.x), p2.y, Mathf.Max(p1.z, p2.z));
            Vector3 c4 = new Vector3(Mathf.Max(p1.x, p2.x), p2.y, Mathf.Max(p1.z, p2.z));
            pointsForFinalSlopeCheck.AddRange(new Vector3[] { c1, c2, c3, c4 });
        }
        else if (_activeDrawingType == DrawingType.Circle)
        {
            Vector3 flatCenter = new Vector3(p1.x, p2.y, p1.z);
            Vector3 flatMouse = new Vector3(p2.x, p2.y, p2.z);
            float radius = Vector3.Distance(flatCenter, flatMouse);

            pointsForFinalSlopeCheck.Add(p1); // Center point

            int numCheckPoints = Mathf.Min(8, circleSegments);
            for (int i = 0; i < numCheckPoints; i++)
            {
                float angle = (float)i / numCheckPoints * 2 * Mathf.PI;
                float x = p1.x + radius * Mathf.Cos(angle);
                float z = p1.z + radius * Mathf.Sin(angle);

                Vector3 checkPointRayOrigin = new Vector3(x, Camera.main.transform.position.y + 10f, z);
                RaycastHit hit;
                if (Physics.Raycast(checkPointRayOrigin, Vector3.down, out hit, Mathf.Infinity, groundLayer))
                {
                    pointsForFinalSlopeCheck.Add(hit.point + Vector3.up * groundOffset);
                }
                else
                {
                    pointsForFinalSlopeCheck.Add(new Vector3(x, p1.y, z));
                }
            }
        }

        bool finalSlopeCheckInvalid = false;
        foreach (Vector3 point in pointsForFinalSlopeCheck)
        {
            if (!CheckSlopeValidity(point))
            {
                finalSlopeCheckInvalid = true;
                break;
            }
        }

        // If the final check on release shows invalid slope, abort creation.
        if (finalSlopeCheckInvalid)
        {
            Debug.LogWarning($"Placement on invalid slope detected at release for {_activeDrawingType}. Mesh creation aborted.");
            return;
        }


        // Proceed with creating the mesh only if the slope is valid at the point of release
        GameObject finalizedMeshGameObject = new GameObject("GeneratedMesh_" + _activeDrawingType.ToString() + "_" + System.DateTime.Now.ToString("HHmmss"));
        MeshFilter newMeshFilter = finalizedMeshGameObject.AddComponent<MeshFilter>();
        MeshRenderer newMeshRenderer = finalizedMeshGameObject.AddComponent<MeshRenderer>();
        MeshCollider newMeshCollider = finalizedMeshGameObject.AddComponent<MeshCollider>();
        newMeshCollider.convex = makeConvex;

        finalizedMeshGameObject.transform.position = this.transform.position;

        if (_activeDrawingType == DrawingType.Box)
        {
            GenerateQuadMesh(newMeshFilter.mesh, p1, p2, finalizedMeshGameObject.transform, false);
        }
        else if (_activeDrawingType == DrawingType.Circle)
        {
            GenerateCircleMesh(newMeshFilter.mesh, p1, p2, finalizedMeshGameObject.transform, false);
        }

        // Apply the valid material for the finalized mesh
        if (meshMaterial != null)
        {
            newMeshRenderer.material = meshMaterial;
        }
        else
        {
            Debug.LogWarning("Mesh material not assigned for " + finalizedMeshGameObject.name + ". Using default Standard material.");
            newMeshRenderer.material = new Material(Shader.Find("Standard"));
        }

        newMeshCollider.sharedMesh = newMeshFilter.mesh;
        AddComponentsToMeshGameObject(finalizedMeshGameObject);

        Debug.Log(finalizedMeshGameObject.name + " generated with " + newMeshFilter.mesh.vertexCount + " vertices and " + newMeshFilter.mesh.triangles.Length / 3 + " triangles.");
    }

    /// <summary>
    /// Checks if the ground slope at a given world point is within the allowed angle.
    /// </summary>
    /// <param name="worldPoint">The world position to check.</param>
    /// <returns>True if the slope is valid, false otherwise.</returns>
    private bool CheckSlopeValidity(Vector3 worldPoint)
    {
        RaycastHit hit;
        // Adjust the ray origin to ensure it's above any potential terrain irregularities or the drawn shape itself
        Vector3 rayOrigin = new Vector3(worldPoint.x, Camera.main.transform.position.y + 10f, worldPoint.z); // Start from high above

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, Mathf.Infinity, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, hit.normal);
            return angle <= maxSlopeAngle;
        }
        // If the raycast doesn't hit the ground, we can't determine slope, so consider it invalid for placement.
        return false;
    }


    private void GenerateQuadMesh(Mesh mesh, Vector3 p1, Vector3 p2, Transform relativeTransform, bool isPreview)
    {
        Vector3 c1 = new Vector3(Mathf.Min(p1.x, p2.x), p2.y, Mathf.Min(p1.z, p2.z));
        Vector3 c2 = new Vector3(Mathf.Max(p1.x, p2.x), p2.y, Mathf.Min(p1.z, p2.z));
        Vector3 c3 = new Vector3(Mathf.Min(p1.x, p2.x), p2.y, Mathf.Max(p1.z, p2.z));
        Vector3 c4 = new Vector3(Mathf.Max(p1.x, p2.x), p2.y, Mathf.Max(p1.z, p2.z));

        Vector3[] vertices = new Vector3[4];
        vertices[0] = relativeTransform.InverseTransformPoint(c1);
        vertices[1] = relativeTransform.InverseTransformPoint(c2);
        vertices[2] = relativeTransform.InverseTransformPoint(c4);
        vertices[3] = relativeTransform.InverseTransformPoint(c3);

        int[] triangles;
        if (!invertMesh)
        {
            triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        }
        else
        {
            triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        }

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void GenerateCircleMesh(Mesh mesh, Vector3 centerPoint, Vector3 currentMousePoint, Transform relativeTransform, bool isPreview)
    {
        Vector3 flatCenter = new Vector3(centerPoint.x, currentMousePoint.y, centerPoint.z); // Use currentMousePoint.y for calculation consistency
        Vector3 flatMouse = new Vector3(currentMousePoint.x, currentMousePoint.y, currentMousePoint.z);
        float radius = Vector3.Distance(flatCenter, flatMouse);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Vector3 localCenterVertex = relativeTransform.InverseTransformPoint(centerPoint);
        vertices.Add(localCenterVertex);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * 2 * Mathf.PI;
            float x = centerPoint.x + radius * Mathf.Cos(angle);
            float z = centerPoint.z + radius * Mathf.Sin(angle);

            Vector3 circlePoint = new Vector3(x, centerPoint.y, z); // Start with centerPoint's Y

            RaycastHit hit;
            Vector3 rayOrigin = new Vector3(circlePoint.x, Camera.main.transform.position.y + 100f, circlePoint.z); // Ray from high above
            float raycastDistance = Camera.main.transform.position.y + 200f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayer))
            {
                circlePoint.y = hit.point.y + groundOffset;
            }
            else
            {
                Debug.LogWarning($"Circle point at X:{circlePoint.x}, Z:{circlePoint.z} did not hit ground during mesh generation. Point will use the y-coordinate of the center point.");
                circlePoint.y = centerPoint.y;
            }

            vertices.Add(relativeTransform.InverseTransformPoint(circlePoint));
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
        }

        int centerIndex = 0;
        for (int i = 1; i <= circleSegments; i++)
        {
            int currentOuter = i;
            // The nextOuter should wrap around to the first outer vertex (index 1) when i is circleSegments
            int nextOuter = (i == circleSegments) ? 1 : i + 1;

            if (!invertMesh)
            {
                triangles.Add(centerIndex);
                triangles.Add(currentOuter);
                triangles.Add(nextOuter);
            }
            else
            {
                triangles.Add(centerIndex);
                triangles.Add(nextOuter);
                triangles.Add(currentOuter);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddComponentsToMeshGameObject(GameObject targetGameObject)
    {
        if (componentsToCopy == null || componentsToCopy.Length == 0)
        {
            Debug.Log("No additional components specified to add to " + targetGameObject.name);
            return;
        }

        foreach (Component sourceComponent in componentsToCopy)
        {
            if (sourceComponent == null)
            {
                Debug.LogWarning("Skipping null component in the 'Components To Copy' list. Ensure all slots are filled.");
                continue;
            }

            Type componentType = sourceComponent.GetType();

            if (typeof(Component).IsAssignableFrom(componentType) && !componentType.IsAbstract && !componentType.IsInterface)
            {
                try
                {
                    Component newComponent = targetGameObject.AddComponent(componentType);
                    string jsonData = JsonUtility.ToJson(sourceComponent);
                    JsonUtility.FromJsonOverwrite(jsonData, newComponent);
                    Debug.Log($"Added component '{componentType.Name}' to {targetGameObject.name} and attempted to copy its serializable data.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to add or copy component '{componentType.Name}' to {targetGameObject.name}. Error: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"The object in 'Components To Copy' slot for '{componentType.Name}' is not a concrete Unity Component type that can be added to a GameObject. Please ensure you are dragging a specific component instance (e.g., Rigidbody, not a generic Component).");
            }
        }
    }
}
