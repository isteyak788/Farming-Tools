// Assets/Scripts/UI/FarmerSelectionPanel.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro; 
using System.Linq; 

// Assuming AIWorkState and CropType are defined elsewhere
// Make sure AIWorkState has: public string farmerName; public Sprite farmerIcon;

public class FarmerSelectionPanel : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent GameObject of the farmer selection UI panel.")]
    public GameObject farmerSelectionPanelGO; 

    [Tooltip("The TextMeshProUGUI component that displays the current field ID.")]
    public TextMeshProUGUI fieldIDText;

    [Tooltip("The Button to confirm the farmer selection.")]
    public Button confirmSelectionButton;

    [Header("Visual Feedback")]
    [Tooltip("The color for selected farmer buttons.")]
    public Color selectedColor = Color.green;
    [Tooltip("The color for unselected farmer buttons.")]
    public Color unselectedColor = Color.white;


    [Header("Internal State (for debugging)")]
    [SerializeField] private string _currentFieldID;
    [SerializeField] private List<AIWorkState> _selectedFarmers = new List<AIWorkState>();

    // Event to notify other scripts (like AssignPanelManager) about selected farmers
    public static event System.Action<List<AIWorkState>, string> OnFarmersAssignedToField;

    void Awake()
    {
        if (farmerSelectionPanelGO != null)
        {
            farmerSelectionPanelGO.SetActive(false); // Ensure the panel is initially hidden
        }
    }

    void OnEnable()
    {
        if (confirmSelectionButton != null)
        {
            confirmSelectionButton.onClick.AddListener(ConfirmFarmerSelection);
        }
        else
        {
            Debug.LogError("ConfirmSelectionButton is not assigned in FarmerSelectionPanel!", this);
        }
    }

    void OnDisable()
    {
        if (confirmSelectionButton != null)
        {
            confirmSelectionButton.onClick.RemoveListener(ConfirmFarmerSelection);
        }
    }

    /// <summary>
    /// Opens the farmer assignment panel and initializes it for a specific field.
    /// </summary>
    /// <param name="fieldID">The ID of the field for which farmers are being assigned.</param>
    /// <param name="currentAssignedFarmers">The list of farmers currently assigned to this field.</param>
    public void OpenPanel(string fieldID, List<AIWorkState> currentAssignedFarmers)
    {
        _currentFieldID = fieldID;
        // 4. Initialize _selectedFarmers with a new list to allow modifications
        _selectedFarmers = new List<AIWorkState>(currentAssignedFarmers); 

        if (fieldIDText != null)
        {
            fieldIDText.text = "Assign Farmers to: " + fieldID;
        }

        if (farmerSelectionPanelGO != null)
        {
            farmerSelectionPanelGO.SetActive(true);
        }

        // Update the visual state of all farmer buttons to reflect initial selection
        UpdateAllFarmerButtonVisuals();
    }

    /// <summary>
    /// Hides the farmer assignment panel.
    /// </summary>
    public void ClosePanel()
    {
        if (farmerSelectionPanelGO != null)
        {
            farmerSelectionPanelGO.SetActive(false);
        }
    }

    /// <summary>
    /// PUBLIC METHOD: Toggles the selection status of a farmer.
    /// This method is called by the dynamically generated farmer buttons.
    /// </summary>
    /// <param name="farmer">The AIWorkState object representing the farmer.</param>
    /// <param name="buttonImage">The Image component of the button that was clicked, for visual feedback.</param>
    public void ToggleFarmerSelection(AIWorkState farmer, Image buttonImage)
    {
        if (_selectedFarmers.Contains(farmer))
        {
            _selectedFarmers.Remove(farmer);
            Debug.Log($"Removed farmer: {farmer.farmerName}");
            if (buttonImage != null) buttonImage.color = unselectedColor; // Set to unselected color
        }
        else
        {
            _selectedFarmers.Add(farmer);
            Debug.Log($"Added farmer: {farmer.farmerName}");
            if (buttonImage != null) buttonImage.color = selectedColor; // Set to selected color
        }
    }

    /// <summary>
    /// Iterates through all existing farmer buttons and updates their visual state
    /// based on whether their associated farmer is in the _selectedFarmers list.
    /// This should be called when the panel opens to reflect initial selections.
    /// </summary>
    private void UpdateAllFarmerButtonVisuals()
    {
        // Find all FarmerButtonHandler components in the scene (or specifically under farmerButtonParent)
        // This assumes FarmerButtonHandler is attached to the root of your farmer button prefab
        FarmerButtonHandler[] allFarmerButtons = FindObjectsByType<FarmerButtonHandler>(FindObjectsSortMode.None);

        foreach (FarmerButtonHandler buttonHandler in allFarmerButtons)
        {
            if (buttonHandler != null && buttonHandler.AssociatedFarmer != null)
            {
                bool isSelected = _selectedFarmers.Contains(buttonHandler.AssociatedFarmer);
                Image buttonImage = buttonHandler.GetComponent<Image>(); // Assuming the image to color is on the same GO as FarmerButtonHandler

                if (buttonImage == null)
                {
                    // If the Image is on a child, adjust this path or add it to FarmerButtonHandler.
                    buttonImage = buttonHandler.GetComponentInChildren<Image>(); 
                }

                if (buttonImage != null)
                {
                    buttonImage.color = isSelected ? selectedColor : unselectedColor;
                }
            }
        }
    }


    /// <summary>
    /// Called when the confirm button within this panel is clicked.
    /// Notifies listeners that farmers have been selected.
    /// </summary>
    private void ConfirmFarmerSelection()
    {
        OnFarmersAssignedToField?.Invoke(_selectedFarmers, _currentFieldID);
        Debug.Log($"Confirmed farmer selection for field {_currentFieldID}. Selected: {_selectedFarmers.Count} farmers.");
        ClosePanel();
    }
}
