// Assets/Scripts/Input/InputHandler.cs
using UnityEngine;
using UnityEngine.EventSystems; // Required for IsPointerOverGameObject

public class InputHandler : MonoBehaviour
{
    [Tooltip("The LayerMask for objects that can be clicked to open assignment panels (e.g., your generated farm fields).")]
    public LayerMask clickableFieldLayer;

    void Update()
    {
        // Prevent interaction if pointer is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return; // Ignore clicks if over UI
        }

        // Detect Ctrl + Right-click
        if (Input.GetMouseButtonDown(1) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            Debug.Log("[InputHandler] Ctrl + Right-click detected. Performing raycast.");

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Raycast using the specified clickableFieldLayer
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableFieldLayer))
            {
                Debug.Log($"[InputHandler] Raycast hit: {hit.collider.gameObject.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");

                // Try to get the CropManager component from the hit object
                CropManager clickedCropManager = hit.collider.GetComponent<CropManager>();
                if (clickedCropManager != null)
                {
                    Debug.Log($"[InputHandler] Found CropManager on: {hit.collider.gameObject.name}. Opening assignment panel.");
                    clickedCropManager.OpenAssignmentPanel();
                }
                else
                {
                    Debug.Log($"[InputHandler] Ctrl + Right-clicked on: {hit.collider.gameObject.name}, but NO CropManager component found. (Is it on the correct layer?)");
                }
            }
            else
            {
                Debug.Log("[InputHandler] Ctrl + Right-click did not hit any object on the specified clickableFieldLayer.");
            }
        }
    }
}
