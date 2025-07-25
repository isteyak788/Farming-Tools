using System.Collections.Generic;
using UnityEngine;
using System.Linq; // For .OrderBy and .FirstOrDefault
using UnityEngine.EventSystems; // Required for UI checks
using UnityEngine.UI; // REQUIRED for Button type
using System; // Required for Type

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer))]
public class LineMeshManager : MonoBehaviour
{
    [Header("Manager Settings")]
    [Tooltip("Check this box ONLY for the LineMeshManager GameObject you place directly in the scene. This instance will manage spawning new meshes.")]
    public bool isSpawner = false; // Only one instance should have this true

    // MODIFIED: Array of Buttons for activating point placement
    [Header("Activation Settings")]
    [Tooltip("Drag the UI Buttons here that should activate point placement.")]
    public Button[] activateDrawingButtons; // Public slot for an array of UI Button references

    [Header("Mesh Creation Settings (Copied to new meshes)")]
    [Tooltip("Prefab to instantiate at each placed point (optional, for visual feedback).")]
    public GameObject pointPrefab;
    [Tooltip("Material for the generated mesh. IMPORTANT: Assign a visible material here.")]
    public Material meshMaterial;
    [Tooltip("Material for the LineRenderer drawing the outline.")]
    public Material lineMaterial;
    [Tooltip("Width of the line drawn by the LineRenderer.")]
    public float lineWidth = 0.1f;

    [Header("Curve Settings")]
    [Tooltip("Number of segments per curve section. Higher values make the curve smoother.")]
    [Range(2, 20)]
    public int curveSegments = 10;

    [Header("Normal Settings")]
    [Tooltip("If true, the mesh normals will be inverted (making the inside surface visible from the outside).")]
    public bool invertMesh = false;

    [Header("Ground Conformance")]
    [Tooltip("The LayerMask for the ground object(s) the mesh/line should conform to.")]
    public LayerMask groundLayer;
    [Tooltip("Offset above the ground where the line/mesh vertices will be placed.")]
    public float groundOffset = 0.05f;

    // NEW: Mesh Generation Settings for quad-like appearance
    [Header("Mesh Generation Details")]
    [Tooltip("Determines how far the first inner loop of vertices is from the outer spline. 0 = collapses to centroid, 1 = same as outer loop.")]
    [Range(0.0f, 0.9f)]
    public float innerLoopScale = 0.5f;

    [Tooltip("Determines how far the second (innermost) loop of vertices is from the outer spline. Must be less than Inner Loop Scale.")]
    [Range(0.0f, 0.89f)] // Max value set to be slightly less than innerLoopScale's max
    public float innermostLoopScale = 0.25f;

    // NEW: Toggle for MeshCollider.convex
    [Tooltip("If true, the MeshCollider will be marked as convex. Convex MeshColliders can interact with other convex colliders.")]
    public bool makeConvex = false; // Added this line

    // ***************************************************************
    // MODIFIED: Use Component[] to allow drag-and-drop for copying
    // ***************************************************************
    [Header("Dynamic Components")]
    [Tooltip("Drag and drop components (e.g., Rigidbody, Collider, your custom scripts) from other GameObjects here. Their type and *serializable* data will be copied to the generated mesh. Note: Only serializable fields will be copied.")]
    public Component[] componentsToCopy;
    // ***************************************************************


    // Internal state for each LineMeshManager instance
    private List<Vector3> controlPoints = new List<Vector3>();
    private List<GameObject> pointVisuals = new List<GameObject>();
    private LineRenderer lineRenderer;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private bool meshFinalizedForThisInstance = false; // True if this instance's mesh is generated
    private bool initializedComponents = false; // To ensure Awake setup runs only once per instance

    // Static reference to the currently active drawing instance
    private static LineMeshManager currentDrawingInstance;

    // Flag to control if a drawing session is active (for spawner)
    private static bool isDrawingSessionActive = false; // Static to ensure only one session is active globally


    // NEW: Reference to the GameObject that will hold the *finalized* mesh.
    // This is the key to preventing the LineMeshManager from staying on generated meshes.
    private GameObject finalizedMeshGameObject;


    void Awake()
    {
        InitializeComponents();
    }

    void OnEnable()
    {
        // Subscribe to the button's click event when the script is enabled
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
        // Unsubscribe from the button's click event when the script is disabled
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

    // Ensures innermostLoopScale is always less than innerLoopScale in the editor
    void OnValidate()
    {
        if (innermostLoopScale >= innerLoopScale)
        {
            innermostLoopScale = innerLoopScale - 0.01f; // Force it to be smaller
            if (innermostLoopScale < 0) innermostLoopScale = 0; // Prevent negative
            Debug.LogWarning("Innermost Loop Scale must be less than Inner Loop Scale. Adjusted automatically.");
        }
    }

    private void InitializeComponents()
    {
        if (initializedComponents) return;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        else
        {
            Debug.LogWarning("Line material not assigned for " + gameObject.name + ". Using default white material for LineRenderer.");
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        meshRenderer.enabled = false;
        lineRenderer.enabled = true; // Ensure line is visible when starting a new drawing
        initializedComponents = true;
    }


    void Update()
    {
        // Check if pointer is over UI. If so, prevent interaction with world objects.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return; // Ignore clicks if over UI
        }

        // --- Spawner Logic (only for the instance marked as isSpawner) ---
        if (isSpawner)
        {
            // Only allow point placement if a drawing session is active
            if (isDrawingSessionActive)
            {
                if (Input.GetMouseButtonDown(0)) // Left-click to add point
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
                    {
                        Vector3 newPoint = hit.point + Vector3.up * groundOffset;

                        // If no current drawing, or the current one is finalized, spawn a new one
                        // 'this' refers to the spawner LineMeshManager
                        if (currentDrawingInstance == null || currentDrawingInstance.meshFinalizedForThisInstance)
                        {
                            // The spawner itself becomes the current drawing instance
                            currentDrawingInstance = this;
                            // Prepare the spawner for a new drawing session
                            currentDrawingInstance.ResetDrawingState(); // Clear any previous state
                            currentDrawingInstance.AddPoint(newPoint); // Add the first point
                            Debug.Log("Spawner is now the active drawing instance. First point added.");
                        }
                        else
                        {
                            // If there IS a current drawing (the spawner itself), pass the click to it.
                            currentDrawingInstance.AddPoint(newPoint);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Mouse click did not hit any object on the specified Ground Layer!");
                    }
                }
            }

            // Only the spawner handles the global reset (R key)
            if (Input.GetKeyDown(KeyCode.R))
            {
                // This will reset the currently active drawing, if any.
                if (currentDrawingInstance != null && !currentDrawingInstance.meshFinalizedForThisInstance)
                {
                    currentDrawingInstance.ResetDrawingState();
                    isDrawingSessionActive = false; // Deactivate session on reset
                    currentDrawingInstance = null; // Clear the static reference
                    Debug.Log("Spawner: Current drawing reset.");
                }
                else if (currentDrawingInstance != null && currentDrawingInstance.meshFinalizedForThisInstance)
                {
                    // If the current is finalized, pressing R on the spawner clears its state
                    currentDrawingInstance = null; // Clear reference to finished mesh
                    isDrawingSessionActive = false; // Deactivate session
                    Debug.Log("Spawner: Cleared reference to last finalized mesh. Ready to start new drawing.");
                }
                else if (currentDrawingInstance == null)
                {
                    // No active drawing, spawner is ready.
                    isDrawingSessionActive = false; // Ensure session is inactive
                    Debug.Log("Spawner: No active drawing to reset. Ready to start new drawing.");
                }
            }
        }
        // --- End Spawner Logic ---

        // --- Drawing and Mesh Generation Logic (only for the currentDrawingInstance, which is the spawner) ---
        if (currentDrawingInstance == this && isDrawingSessionActive) // Added isDrawingSessionActive check
        {
            // Enter key finalizes the current mesh
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlPoints.Count >= 3)
                {
                    GenerateMesh();
                    meshFinalizedForThisInstance = true; // Mark this instance as finalized
                    lineRenderer.enabled = false; // Hide the line for the finished mesh

                    // Crucially, reset the spawner's state after generating the mesh
                    // This prepares it for a new drawing session
                    ResetDrawingState();
                    currentDrawingInstance = null; // Clear the static reference
                    isDrawingSessionActive = false; // Deactivate session after finalization
                    Debug.Log(gameObject.name + " mesh finalized and spawner reset. Ready for next mesh.");
                }
                else
                {
                    Debug.LogWarning("Need at least 3 points to form a polygon to generate a mesh. Current control points: " + controlPoints.Count);
                }
            }

            // Right-click to delete the previous point
            if (Input.GetMouseButtonDown(1))
            {
                DeleteLastPoint();
            }
        }
    }

    /// <summary>
    /// Public method to be called by a UI button to start a new mesh drawing session.
    /// This method should only be called on the LineMeshManager instance that has 'isSpawner' set to true.
    /// </summary>
    public void StartNewDrawingSessionFromButton()
    {
        if (isSpawner)
        {
            if (currentDrawingInstance == null || currentDrawingInstance.meshFinalizedForThisInstance)
            {
                // Set the flag to true, allowing the next left click to spawn a mesh
                isDrawingSessionActive = true;
                currentDrawingInstance = this; // The spawner becomes the current drawing instance
                ResetDrawingState(); // Prepare spawner for new drawing
                Debug.Log("Drawing session activated. Click on the ground to place the first point.");
            }
            else
            {
                Debug.LogWarning("Cannot start a new drawing session. An existing drawing is in progress. Please finalize or reset the current drawing.");
            }
        }
        else
        {
            Debug.LogError("This LineMeshManager instance is not marked as the spawner. The button should call the spawner's method.");
        }
    }


    /// <summary>
    /// Called by the spawner to add a point to the current active drawing instance.
    /// </summary>
    public void AddPoint(Vector3 point)
    {
        if (meshFinalizedForThisInstance) return; // Cannot add points to a finalized mesh

        controlPoints.Add(point);
        if (pointPrefab != null)
        {
            GameObject visual = Instantiate(pointPrefab, point, Quaternion.identity, transform);
            pointVisuals.Add(visual);
        }
        UpdateLineRenderer();
    }


    /// <summary>
    /// Resets the current drawing state for this specific LineMeshManager instance.
    /// </summary>
    public void ResetDrawingState()
    {
        controlPoints.Clear();
        foreach (GameObject visual in pointVisuals)
        {
            Destroy(visual);
        }
        pointVisuals.Clear();

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = true; // Re-enable for new drawing if it was disabled
        if (meshFilter.mesh != null)
        {
            // IMPORTANT: If this is the spawner, clear its mesh to prepare for new drawing.
            // But don't destroy the asset if it's already being used by a finalized mesh.
            if (meshFilter.mesh != null && meshFilter.mesh != finalizedMeshGameObject?.GetComponent<MeshFilter>()?.sharedMesh)
            {
                 Destroy(meshFilter.mesh); // Destroy the old mesh asset ONLY if it's not the one already transferred
            }
            meshFilter.mesh = null; // Clear the spawner's mesh filter
        }
        meshRenderer.enabled = false;
        meshFinalizedForThisInstance = false;
        // Do NOT set currentDrawingInstance to null here directly, that's spawner's job if it decides to.
        Debug.Log(gameObject.name + " drawing reset.");

        // Clear the reference to the finalized mesh GameObject
        finalizedMeshGameObject = null;
    }

    /// <summary>
    /// Deletes the last added control point and its visual representation.
    /// </summary>
    private void DeleteLastPoint()
    {
        if (controlPoints.Count > 0)
        {
            controlPoints.RemoveAt(controlPoints.Count - 1);
            if (pointVisuals.Count > 0)
            {
                Destroy(pointVisuals[pointVisuals.Count - 1]);
                pointVisuals.RemoveAt(pointVisuals.Count - 1);
            }
            UpdateLineRenderer();
            Debug.Log("Last point deleted. Current control points: " + controlPoints.Count);
        }
        else
        {
            Debug.Log("No points to delete.");
        }
    }


    void UpdateLineRenderer()
    {
        if (controlPoints.Count < 2)
        {
            lineRenderer.positionCount = controlPoints.Count;
            if (controlPoints.Count == 1)
            {
                lineRenderer.SetPosition(0, controlPoints[0]);
            }
            return;
        }

        List<Vector3> splinePoints = GenerateSplinePoints(true);

        lineRenderer.positionCount = splinePoints.Count;
        lineRenderer.SetPositions(splinePoints.ToArray());
    }

    private List<Vector3> GenerateSplinePoints(bool closedLoop)
    {
        List<Vector3> splinePoints = new List<Vector3>();
        if (controlPoints.Count < 2) return splinePoints;

        List<Vector3> tempPoints = new List<Vector3>(controlPoints);

        if (closedLoop && controlPoints.Count >= 3)
        {
            tempPoints.Insert(0, controlPoints[controlPoints.Count - 1]);
            tempPoints.Add(controlPoints[0]);
            tempPoints.Add(controlPoints[1]);
        }
        else
        {
            if (controlPoints.Count == 2)
            {
                // For a line, duplicate start and end points to make Catmull-Rom work
                tempPoints.Insert(0, controlPoints[0]);
                tempPoints.Add(controlPoints[1]);
            }
            else if (controlPoints.Count > 0) // For single point or more than 2 but not enough for closed loop
            {
                // Duplicate first and last points for open spline
                tempPoints.Insert(0, tempPoints[0]);
                tempPoints.Add(tempPoints[tempPoints.Count - 1]);
            }
        }

        for (int i = 0; i < tempPoints.Count - 3; i++)
        {
            for (int j = 0; j <= curveSegments; j++)
            {
                float t = (float)j / curveSegments;
                // Avoid duplicating points at the start of each segment if not the very first point
                if (j == 0 && i > 0) continue;

                Vector3 interpolatedPoint = CalculateCatmullRom(tempPoints[i], tempPoints[i + 1], tempPoints[i + 2], tempPoints[i + 3], t);

                RaycastHit hit;
                // Raycast from above to conform to ground
                Vector3 rayOrigin = new Vector3(interpolatedPoint.x, Camera.main.transform.position.y + 100f, interpolatedPoint.z); // Start high above
                float raycastDistance = Camera.main.transform.position.y + 200f; // Ensure ray covers ground below

                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayer))
                {
                    interpolatedPoint.y = hit.point.y + groundOffset;
                }
                else
                {
                    Debug.LogWarning($"Spline point at X:{interpolatedPoint.x}, Z:{interpolatedPoint.z} did not hit ground. Point will use its calculated Y.");
                }
                splinePoints.Add(interpolatedPoint);
            }
        }
        return splinePoints;
    }

    private Vector3 CalculateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float a = 0.5f * (2f * p1.x + (-p0.x + p2.x) * t + (2f * p0.x - 5f * p1.x + 4f * p2.x - p3.x) * t2 + (-p0.x + 3f * p1.x - 3f * p2.x + p3.x) * t3);
        float b = 0.5f * (2f * p1.y + (-p0.y + p2.y) * t + (2f * p0.y - 5f * p1.y + 4f * p2.y - p3.y) * t2 + (-p0.y + 3f * p1.y - 3f * p2.y + p3.y) * t3);
        float c = 0.5f * (2f * p1.z + (-p0.z + p2.z) * t + (2f * p0.z - 5f * p1.z + 4f * p2.z - p3.z) * t2 + (-p0.z + 3f * p1.z - 3f * p2.z + p3.z) * t3);

        return new Vector3(a, b, c);
    }

    void GenerateMesh()
    {
        if (controlPoints.Count < 3)
        {
            Debug.LogWarning("Need at least 3 points to form a polygon. Current control points: " + controlPoints.Count);
            return;
        }

        Mesh newGeneratedMesh = new Mesh(); // Create a new mesh
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        List<Vector3> splineMeshPoints = GenerateSplinePoints(true); // Outer loop

        if (splineMeshPoints.Count < 3)
        {
            Debug.LogWarning("Not enough spline points generated to form a mesh.");
            return;
        }

        // Calculate centroid of the outer spline points
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 p in splineMeshPoints)
        {
            centroid += p;
        }
        centroid /= splineMeshPoints.Count;

        // Project centroid to ground
        RaycastHit centroidHit;
        Vector3 rayOriginCentroid = new Vector3(centroid.x, Camera.main.transform.position.y + 100f, centroid.z);
        if (Physics.Raycast(rayOriginCentroid, Vector3.down, out centroidHit, Camera.main.transform.position.y + 200f, groundLayer))
        {
            centroid.y = centroidHit.point.y + groundOffset;
        }
        else
        {
            Debug.LogWarning("Centroid did not hit ground. Using calculated Y.");
        }


        // Generate first inner loop points
        List<Vector3> innerSplinePoints = new List<Vector3>();
        foreach (Vector3 p in splineMeshPoints)
        {
            Vector3 innerPoint = Vector3.Lerp(centroid, p, innerLoopScale);
            // Project inner point to ground
            RaycastHit innerHit;
            Vector3 rayOriginInner = new Vector3(innerPoint.x, Camera.main.transform.position.y + 100f, innerPoint.z);
            if (Physics.Raycast(rayOriginInner, Vector3.down, out innerHit, Camera.main.transform.position.y + 200f, groundLayer))
            {
                innerPoint.y = innerHit.point.y + groundOffset;
            }
            else
            {
                Debug.LogWarning($"Inner spline point at X:{innerPoint.x}, Z:{innerPoint.z} did not hit ground. Point will use its calculated Y.");
            }
            innerSplinePoints.Add(innerPoint);
        }

        // Generate second (innermost) loop points
        List<Vector3> innermostSplinePoints = new List<Vector3>();
        foreach (Vector3 p in splineMeshPoints) // Base off outer points and innermostLoopScale
        {
            Vector3 innermostPoint = Vector3.Lerp(centroid, p, innermostLoopScale);
            // Project innermost point to ground
            RaycastHit innermostHit;
            Vector3 rayOriginInnermost = new Vector3(innermostPoint.x, Camera.main.transform.position.y + 100f, innermostPoint.z);
            if (Physics.Raycast(rayOriginInnermost, Vector3.down, out innermostHit, Camera.main.transform.position.y + 200f, groundLayer))
            {
                innermostPoint.y = innermostHit.point.y + groundOffset;
            }
            else
            {
                Debug.LogWarning($"Innermost spline point at X:{innermostPoint.x}, Z:{innermostPoint.z} did not hit ground. Point will use its calculated Y.");
            }
            innermostSplinePoints.Add(innermostPoint);
        }


        // Create a new GameObject to hold the finalized mesh
        finalizedMeshGameObject = new GameObject("GeneratedMesh_" + System.DateTime.Now.ToString("HHmmss"));
        MeshFilter newMeshFilter = finalizedMeshGameObject.AddComponent<MeshFilter>();
        MeshRenderer newMeshRenderer = finalizedMeshGameObject.AddComponent<MeshRenderer>();
        // Add MeshCollider
        MeshCollider newMeshCollider = finalizedMeshGameObject.AddComponent<MeshCollider>();
        newMeshCollider.convex = makeConvex; // Set convex based on the public variable [Cite: 1]


        // Set the position of the new GameObject to match the spawner's position
        // This ensures the local space transformation is correct.
        finalizedMeshGameObject.transform.position = this.transform.position;


        // Convert to local space for mesh, now relative to the new GameObject's transform
        Vector3[] localSplineVertices = new Vector3[splineMeshPoints.Count];
        Vector3[] localInnerSplineVertices = new Vector3[innerSplinePoints.Count];
        Vector3[] localInnermostSplineVertices = new Vector3[innermostSplinePoints.Count];


        for (int i = 0; i < splineMeshPoints.Count; i++)
        {
            // Now use finalizedMeshGameObject.transform for InverseTransformPoint
            localSplineVertices[i] = finalizedMeshGameObject.transform.InverseTransformPoint(splineMeshPoints[i]);
            localInnerSplineVertices[i] = finalizedMeshGameObject.transform.InverseTransformPoint(innerSplinePoints[i]);
            localInnermostSplineVertices[i] = finalizedMeshGameObject.transform.InverseTransformPoint(innermostSplinePoints[i]);
        }

        // Add all loop vertices
        vertices.AddRange(localSplineVertices);          // Indices 0 to N-1
        vertices.AddRange(localInnerSplineVertices);     // Indices N to 2N-1
        vertices.AddRange(localInnermostSplineVertices); // Indices 2N to 3N-1

        // UV calculation (still based on XZ bounds)
        Vector3 minBounds = localSplineVertices[0];
        Vector3 maxBounds = localSplineVertices[0];
        foreach (Vector3 p in localSplineVertices) { minBounds = Vector3.Min(minBounds, p); maxBounds = Vector3.Max(maxBounds, p); }
        foreach (Vector3 p in localInnerSplineVertices) { minBounds = Vector3.Min(minBounds, p); maxBounds = Vector3.Max(maxBounds, p); }
        foreach (Vector3 p in localInnermostSplineVertices) { minBounds = Vector3.Min(minBounds, p); maxBounds = Vector3.Max(maxBounds, p); }


        float rangeX = maxBounds.x - minBounds.x;
        float rangeZ = maxBounds.z - minBounds.z;
        if (rangeX == 0) rangeX = 1f;
        if (rangeZ == 0) rangeZ = 1f;

        // Add UVs for all loops
        for (int i = 0; i < localSplineVertices.Length; i++)
        {
            uvs.Add(new Vector2((localSplineVertices[i].x - minBounds.x) / rangeX, (localSplineVertices[i].z - minBounds.x) / rangeZ));
        }
        for (int i = 0; i < localInnerSplineVertices.Length; i++)
        {
            uvs.Add(new Vector2((localInnerSplineVertices[i].x - minBounds.x) / rangeX, (localInnerSplineVertices[i].z - minBounds.x) / rangeZ));
        }
        for (int i = 0; i < localInnermostSplineVertices.Length; i++)
        {
            uvs.Add(new Vector2((localInnermostSplineVertices[i].x - minBounds.x) / rangeX, (localInnermostSplineVertices[i].z - minBounds.x) / rangeZ));
        }


        int numPointsPerLoop = splineMeshPoints.Count;

        // Triangulate between Outer and First Inner loops (Strip 1)
        int outerLoopStartIdx = 0;
        int innerLoopStartIdx = numPointsPerLoop;

        for (int i = 0; i < numPointsPerLoop; i++)
        {
            int currentOuter = outerLoopStartIdx + i;
            int nextOuter = outerLoopStartIdx + (i + 1) % numPointsPerLoop;

            int currentInner = innerLoopStartIdx + i;
            int nextInner = innerLoopStartIdx + (i + 1) % numPointsPerLoop;

            // Quad 1 (two triangles): currentOuter, nextOuter, nextInner, currentInner
            AddTriangle(triangles, currentOuter, nextOuter, currentInner, invertMesh);
            AddTriangle(triangles, nextOuter, nextInner, currentInner, invertMesh);
        }

        // Triangulate between First Inner and Second (Innermost) loops (Strip 2)
        int innermostLoopStartIdx = 2 * numPointsPerLoop;

        for (int i = 0; i < numPointsPerLoop; i++)
        {
            int currentInner = innerLoopStartIdx + i;
            int nextInner = innerLoopStartIdx + (i + 1) % numPointsPerLoop;

            int currentInnermost = innermostLoopStartIdx + i;
            int nextInnermost = innermostLoopStartIdx + (i + 1) % numPointsPerLoop;

            // Quad 2 (two triangles): currentInner, nextInner, nextInnermost, currentInnermost
            AddTriangle(triangles, currentInner, nextInner, currentInnermost, invertMesh);
            AddTriangle(triangles, nextInner, nextInnermost, currentInnermost, invertMesh);
        }


        // Triangulate the innermost hole
        // If innermostLoopScale is very small (approaching 0), the innermost loop collapses to the centroid.
        // In that case, we triangulate the innermost part as a fan from the centroid.
        // Otherwise, triangulate the innermost polygon.
        if (innermostLoopScale <= 0.001f) // Innermost loop collapses to centroid
        {
            // Add centroid as the last vertex
            // Now use finalizedMeshGameObject.transform for InverseTransformPoint
            vertices.Add(finalizedMeshGameObject.transform.InverseTransformPoint(centroid));
            uvs.Add(new Vector2((centroid.x - minBounds.x) / rangeX, (centroid.z - minBounds.z) / rangeZ)); // UV for centroid
            int centroidIdx = vertices.Count - 1;

            // Triangulate from innermost loop to centroid
            for (int i = 0; i < numPointsPerLoop; i++)
            {
                int currentInnermost = innermostLoopStartIdx + i;
                int nextInnermost = innermostLoopStartIdx + (i + 1) % numPointsPerLoop;
                AddTriangle(triangles, currentInnermost, nextInnermost, centroidIdx, invertMesh);
            }
        }
        else // Innermost loop forms a polygon, triangulate it
        {
            // Simple fan triangulation from the first innermost point
            int firstInnermostPointIdx = innermostLoopStartIdx;
            for (int i = 1; i < numPointsPerLoop - 1; i++)
            {
                AddTriangle(triangles, firstInnermostPointIdx, innermostLoopStartIdx + i, innermostLoopStartIdx + i + 1, invertMesh);
            }
        }


        newGeneratedMesh.vertices = vertices.ToArray();
        newGeneratedMesh.triangles = triangles.ToArray();
        newGeneratedMesh.uv = uvs.ToArray();

        newGeneratedMesh.RecalculateNormals();
        newGeneratedMesh.RecalculateBounds();

        newMeshFilter.mesh = newGeneratedMesh; // Assign the generated mesh to the new GameObject
        newMeshCollider.sharedMesh = newGeneratedMesh; // Assign the mesh to the MeshCollider

        if (meshMaterial != null)
        {
            newMeshRenderer.material = meshMaterial;
        }
        else
        {
            Debug.LogWarning("Mesh material not assigned for " + finalizedMeshGameObject.name + ". Using default Standard material.");
            newMeshRenderer.material = new Material(Shader.Find("Standard"));
        }

        // ***************************************************************
        // MODIFIED: Integrate the new AddComponentsToMeshGameObject method
        AddComponentsToMeshGameObject(finalizedMeshGameObject);
        // ***************************************************************

        // The spawner's LineRenderer is now hidden, and its MeshRenderer is also hidden.
        // The new GameObject will display the mesh.
        meshRenderer.enabled = false;
        lineRenderer.enabled = false;

        Debug.Log(finalizedMeshGameObject.name + " mesh generated with " + newGeneratedMesh.vertexCount + " vertices and " + newGeneratedMesh.triangles.Length / 3 + " triangles.");
    }

    private void AddTriangle(List<int> list, int v1, int v2, int v3, bool invert)
    {
        if (!invert)
        {
            list.Add(v1);
            list.Add(v2);
            list.Add(v3);
        }
        else
        {
            list.Add(v1);
            list.Add(v3);
            list.Add(v2);
        }
    }

    // ***************************************************************
    // MODIFIED: Method to add components and copy data
    // ***************************************************************
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

            // Ensure the source is a valid Unity Component type that can be added
            if (typeof(Component).IsAssignableFrom(componentType) && !componentType.IsAbstract && !componentType.IsInterface)
            {
                try
                {
                    // 1. Add the new component of the same type
                    Component newComponent = targetGameObject.AddComponent(componentType);

                    // 2. Attempt to copy serializable data from the source to the new component
                    // This uses JsonUtility to serialize and then deserialize, effectively copying serializable fields.
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
    // ***************************************************************
}
