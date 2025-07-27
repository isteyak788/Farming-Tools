// Assets/Scripts/UI/CropSelectionSubPanel.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class CropSelectionSubPanel : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The GameObject representing the 'CropSelectionPanel' itself.")]
    public GameObject cropSelectionPanelGameObject;
    [Tooltip("The parent GameObject where crop selection buttons will be instantiated.")]
    public Transform cropButtonParent;
    [Tooltip("Prefab for a single crop selection button (needs Button, Image for icon, and TextMeshProUGUI for name).")]
    public GameObject cropButtonPrefab;

    [Header("Data Source")]
    [Tooltip("Assign the CropInventory ScriptableObject here.")]
    public CropInventory cropInventory;

    // Event to notify AssignPanelManager about the selected crop
    public static event System.Action<CropType> OnCropSelectedFromPanel;

    private CropType _currentlyHighlightedCrop;

    void Awake()
    {
        if (cropSelectionPanelGameObject != null)
        {
            cropSelectionPanelGameObject.SetActive(false); // Start inactive
        }
    }

    /// <summary>
    /// Opens the crop selection sub-panel and populates it.
    /// </summary>
    /// <param name="currentAssignedCrop">The crop currently assigned to the field, to pre-highlight it.</param>
    public void OpenPanel(CropType currentAssignedCrop)
    {
        _currentlyHighlightedCrop = currentAssignedCrop;
        GenerateCropSelectionButtons();

        if (cropSelectionPanelGameObject != null)
        {
            cropSelectionPanelGameObject.SetActive(true);
            Debug.Log("Crop Selection Sub-Panel opened.");
        }
        else
        {
            Debug.LogError("Crop Selection Panel GameObject is not assigned in CropSelectionSubPanel.");
        }
    }

    /// <summary>
    /// Closes the crop selection sub-panel.
    /// </summary>
    public void ClosePanel()
    {
        if (cropSelectionPanelGameObject != null)
        {
            cropSelectionPanelGameObject.SetActive(false);
            Debug.Log("Crop Selection Sub-Panel closed.");
        }
    }

    void GenerateCropSelectionButtons()
    {
        // Clear existing buttons
        foreach (Transform child in cropButtonParent)
        {
            Destroy(child.gameObject);
        }

        if (cropInventory == null || cropInventory.availableCropTypes == null)
        {
            Debug.LogError("Crop Inventory or availableCropTypes list is not set in CropSelectionSubPanel.");
            return;
        }

        foreach (CropType crop in cropInventory.availableCropTypes)
        {
            GameObject buttonGO = Instantiate(cropButtonPrefab, cropButtonParent);
            Button button = buttonGO.GetComponent<Button>();
            Image iconImage = buttonGO.GetComponentInChildren<Image>(); // Assuming icon is on an Image child
            TextMeshProUGUI buttonText = buttonGO.GetComponentInChildren<TextMeshProUGUI>(); // Assuming name is on a TMP_Text child
            // Optional: A child GameObject named "SelectionHighlight" with an Image component
            Image selectionHighlight = buttonGO.transform.Find("SelectionHighlight")?.GetComponent<Image>();

            if (button != null)
            {
                CropType currentCrop = crop; // Capture for lambda
                button.onClick.AddListener(() => OnCropSelected(currentCrop));

                // Set initial highlight state
                if (selectionHighlight != null)
                {
                    selectionHighlight.gameObject.SetActive(_currentlyHighlightedCrop == currentCrop);
                }
            }

            if (iconImage != null && crop.cropIcon != null)
            {
                iconImage.sprite = crop.cropIcon;
                iconImage.enabled = true;
            }
            else if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            if (buttonText != null)
            {
                buttonText.text = crop.cropName;
            }
        }
    }

    void OnCropSelected(CropType selectedCrop)
    {
        _currentlyHighlightedCrop = selectedCrop; // Update for future re-opening
        OnCropSelectedFromPanel?.Invoke(selectedCrop); // Notify AssignPanelManager
        Debug.Log($"Crop selected: {selectedCrop.cropName}. Closing Crop Selection Panel.");
        ClosePanel(); // Automatically close after single selection
    }
}
