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

    // NEW: Toggle for drawing mode
    [Header("Drawing Mode")]
    public DrawingMode currentDrawingMode = DrawingMode.BoxDraw; // Default mode

    public enum DrawingMode
    {
        BoxDraw,
        CircleDraw
    }

    [Header("Activation Settings")]
    [Tooltip("Drag the UI Buttons here that should activate drawing.")]
    public Button[] activateDrawingButtons;

    [Header("Mesh Creation Settings")]
    [Tooltip("Material for the generated mesh. IMPORTANT: Assign a visible material here.")]
    public Material meshMaterial;

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

    [Header("Circle Draw Settings")] // NEW: Settings for circle drawing
    [Tooltip("Number of segments for the circle mesh. Higher values make the circle smoother.")]
    [Range(8, 64)]
    public int circleSegments = 32;

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

    void Awake()
    {
        previewMeshFilter = GetComponent<MeshFilter>();
        previewMeshRenderer = GetComponent<MeshRenderer>();
        previewMeshRenderer.enabled = false;
    }

    void OnEnable()
    {
        if (isSpawner && activateDrawingButtons != null)
        {
            foreach (Button button in activateDrawingButtons)
            {
                if (button != null)
                {
                    button.onClick.AddListener(StartNewDrawingSessionFromButton);
                    Debug.Log($"Subscribed to '{button.name}' click event.");
                }
            }
        }
    }

    void OnDisable()
    {
        if (isSpawner && activateDrawingButtons != null)
        {
            foreach (Button button in activateDrawingButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(StartNewDrawingSessionFromButton);
                    Debug.Log($"Unsubscribed from '{button.name}' click event.");
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

        if (isSpawner && isDrawingSessionActive)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Start drag
            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
                {
                    startDragPoint = hit.point + Vector3.up * groundOffset;
                    currentDrawingInstance = this;
                    SetupPreviewMesh();
                    Debug.Log($"{currentDrawingMode} drawing started.");
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
                    Vector3 endDragPoint = hit.point + Vector3.up * groundOffset;
                    FinalizeShapeMesh(startDragPoint.Value, endDragPoint);
                    CleanupPreviewMesh();
                    startDragPoint = null;
                    isDrawingSessionActive = false;
                    Debug.Log($"{currentDrawingMode} mesh finalized and spawner reset. Ready for next mesh.");
                }
                else
                {
                    Debug.LogWarning("Mouse released but did not hit any object on the specified Ground Layer!");
                    CleanupPreviewMesh();
                    startDragPoint = null;
                    isDrawingSessionActive = false;
                }
            }

            // Reset drawing with 'R' key
            if (Input.GetKeyDown(KeyCode.R))
            {
                CleanupPreviewMesh();
                startDragPoint = null;
                isDrawingSessionActive = false;
                Debug.Log("Drawing reset.");
            }
        }
    }

    public void StartNewDrawingSessionFromButton()
    {
        if (isSpawner)
        {
            if (!isDrawingSessionActive)
            {
                isDrawingSessionActive = true;
                Debug.Log($"Drawing session activated for {currentDrawingMode}. Click and drag on the ground to draw.");
            }
            else
            {
                Debug.LogWarning("Cannot start a new drawing session. An existing drawing is in progress. Please finalize or reset the current drawing.");
            }
        }
        else
        {
            Debug.LogError("This ShapeMeshManager instance is not marked as the spawner. The button should call the spawner's method.");
        }
    }

    private void SetupPreviewMesh()
    {
        previewMeshFilter.mesh = new Mesh();
        previewMeshRenderer.material = meshMaterial;
        previewMeshRenderer.enabled = true;
    }

    private void UpdatePreviewMesh(Vector3 p1, Vector3 p2)
    {
        if (currentDrawingMode == DrawingMode.BoxDraw)
        {
            GenerateQuadMesh(previewMeshFilter.mesh, p1, p2, transform, true); // True for preview (no new gameobject)
        }
        else if (currentDrawingMode == DrawingMode.CircleDraw)
        {
            GenerateCircleMesh(previewMeshFilter.mesh, p1, p2, transform, true); // True for preview
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
        GameObject finalizedMeshGameObject = new GameObject("GeneratedMesh_" + currentDrawingMode.ToString() + "_" + System.DateTime.Now.ToString("HHmmss"));
        MeshFilter newMeshFilter = finalizedMeshGameObject.AddComponent<MeshFilter>();
        MeshRenderer newMeshRenderer = finalizedMeshGameObject.AddComponent<MeshRenderer>();
        MeshCollider newMeshCollider = finalizedMeshGameObject.AddComponent<MeshCollider>();
        newMeshCollider.convex = makeConvex;

        finalizedMeshGameObject.transform.position = this.transform.position;

        if (currentDrawingMode == DrawingMode.BoxDraw)
        {
            GenerateQuadMesh(newMeshFilter.mesh, p1, p2, finalizedMeshGameObject.transform, false); // False for final mesh (uses new gameobject)
        }
        else if (currentDrawingMode == DrawingMode.CircleDraw)
        {
            GenerateCircleMesh(newMeshFilter.mesh, p1, p2, finalizedMeshGameObject.transform, false); // False for final mesh
        }

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

    // Renamed from GenerateBoxMesh to be more generic for quad generation
    private void GenerateQuadMesh(Mesh mesh, Vector3 p1, Vector3 p2, Transform relativeTransform, bool isPreview)
    {
        Vector3 c1 = new Vector3(Mathf.Min(p1.x, p2.x), p1.y, Mathf.Min(p1.z, p2.z));
        Vector3 c2 = new Vector3(Mathf.Max(p1.x, p2.x), p1.y, Mathf.Min(p1.z, p2.z));
        Vector3 c3 = new Vector3(Mathf.Min(p1.x, p2.x), p1.y, Mathf.Max(p1.z, p2.z));
        Vector3 c4 = new Vector3(Mathf.Max(p1.x, p2.x), p1.y, Mathf.Max(p1.z, p2.z));

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

    // NEW: Method for generating a circle mesh
    private void GenerateCircleMesh(Mesh mesh, Vector3 centerPoint, Vector3 currentMousePoint, Transform relativeTransform, bool isPreview)
    {
        // Calculate radius based on distance from centerPoint to currentMousePoint
        // Project currentMousePoint to the same Y level as centerPoint for radius calculation
        Vector3 flatCenter = new Vector3(centerPoint.x, 0, centerPoint.z);
        Vector3 flatMouse = new Vector3(currentMousePoint.x, 0, currentMousePoint.z);
        float radius = Vector3.Distance(flatCenter, flatMouse);

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Center vertex
        Vector3 localCenterVertex = relativeTransform.InverseTransformPoint(centerPoint);
        vertices.Add(localCenterVertex);
        uvs.Add(new Vector2(0.5f, 0.5f)); // Center of UV

        // Outer circle vertices
        for (int i = 0; i <= circleSegments; i++) // <= to close the loop
        {
            float angle = (float)i / circleSegments * 2 * Mathf.PI;
            float x = centerPoint.x + radius * Mathf.Cos(angle);
            float z = centerPoint.z + radius * Mathf.Sin(angle);

            Vector3 circlePoint = new Vector3(x, centerPoint.y, z);

            // Project circlePoint to ground
            RaycastHit hit;
            Vector3 rayOrigin = new Vector3(circlePoint.x, Camera.main.transform.position.y + 100f, circlePoint.z);
            float raycastDistance = Camera.main.transform.position.y + 200f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayer))
            {
                circlePoint.y = hit.point.y + groundOffset;
            }
            else
            {
                Debug.LogWarning($"Circle point at X:{circlePoint.x}, Z:{circlePoint.z} did not hit ground. Point will use its calculated Y.");
            }

            vertices.Add(relativeTransform.InverseTransformPoint(circlePoint));
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
        }

        // Triangles (fan from center)
        int centerIndex = 0;
        for (int i = 1; i <= circleSegments; i++)
        {
            int currentOuter = i;
            int nextOuter = (i % circleSegments) + 1; // Ensures loop back to first outer vertex

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
