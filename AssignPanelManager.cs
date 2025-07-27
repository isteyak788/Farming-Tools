// Assets/Scripts/UI/AssignPanelManager.cs
// This script orchestrates the UI for assigning crops and farmers to a field.
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI; // Make sure this is included for UI elements like Button, Text, Image, etc.
using TMPro; // Include if you are using TextMeshPro for UI Text
using System.Linq; // For OrderBy, FirstOrDefault

public class AssignPanelManager : MonoBehaviour
{
    [Header("Main UI Panels & Essential Buttons")]
    [Tooltip("The main UI panel GameObject that contains all assignment UI (e.g., crop selection, farmer selection).")]
    public GameObject mainAssignmentPanel;

    [Tooltip("Reference to the FarmerSelectionPanel script component within this manager's UI hierarchy.")]
    public FarmerSelectionPanel farmerSelectionPanel; // This references the SCRIPT component

    [Tooltip("Drag the UI Button that confirms the field setup (crop and farmers) here.")]
    public Button confirmFieldSetupButton;

    // Removed: "Optional UI Sub-Panels & Toggle Buttons" header and its fields

    [Header("Crop Display UI")]
    [Tooltip("The prefab for a UI element that displays the currently selected crop (e.g., an icon and name).")]
    public GameObject assignedCropDisplayPrefab;
    [Tooltip("The parent RectTransform where the assigned crop display prefab will be instantiated and updated.")]
    public RectTransform assignedCropDisplayParent;

    [Header("Assigned Farmer Display UI")] // Renamed from Worker to Farmer
    [Tooltip("The prefab for a UI element that displays an individual assigned farmer.")]
    public GameObject assignedFarmerDisplayPrefab;
    [Tooltip("The parent RectTransform where assigned farmer display prefabs will be instantiated.")]
    public RectTransform assignedFarmerDisplayParent;


    [Header("Dynamic Button Generation")]
    [Tooltip("Prefab for a single crop selection UI button. This prefab should have a Button, TextMeshProUGUI, and Image component.")]
    public GameObject cropButtonPrefab;
    [Tooltip("Parent RectTransform where crop selection buttons will be instantiated.")]
    public RectTransform cropButtonParent;

    [Tooltip("Optional: Drag your CropType ScriptableObjects here to manually assign them instead of loading from Resources.")]
    public List<CropType> cropInventoryList; // NEW: For manual assignment of CropType SOs

    [Tooltip("Prefab for a single farmer selection UI button. This prefab should have a Button, TextMeshProUGUI, and Image component.")]
    public GameObject farmerButtonPrefab;
    [Tooltip("Parent RectTransform where farmer selection buttons will be instantiated.")]
    public RectTransform farmerButtonParent;


    // Public property to hold the currently selected CropType from UI
    [HideInInspector]
    public CropType currentlySelectedCropType;

    // --- Events for other scripts to listen to ---
    public static event System.Action<string, CropType, List<AIWorkState>> OnFieldSetupConfirmed;

    // --- Internal State ---
    private string _currentFieldIDBeingConfigured;
    private List<AIWorkState> _tempSelectedFarmers = new List<AIWorkState>();
    private GameObject _currentCropDisplayInstance;

    void Awake()
    {
        if (mainAssignmentPanel != null)
        {
            mainAssignmentPanel.SetActive(false); // Ensure the main panel starts hidden
        }
        else
        {
            Debug.LogError("MainAssignmentPanel is not assigned in AssignPanelManager!", this);
        }

        // Removed: Logic for cropSelectionSubPanel and farmerSelectionSubPanel from Awake
    }

    void OnEnable()
    {
        if (farmerSelectionPanel != null)
        {
            FarmerSelectionPanel.OnFarmersAssignedToField += HandleFarmersAssigned;
        }
        else
        {
            Debug.LogError("FarmerSelectionPanel reference is missing in AssignPanelManager OnEnable!", this);
        }

        if (confirmFieldSetupButton != null)
        {
            confirmFieldSetupButton.onClick.AddListener(ConfirmFieldSetup);
        }
        else
        {
            Debug.LogError("ConfirmFieldSetupButton is not assigned in AssignPanelManager!", this);
        }

        // Removed: Subscriptions for cropSelectionToggleButton and farmerSelectionToggleButton
    }

    void OnDisable()
    {
        if (farmerSelectionPanel != null)
        {
            FarmerSelectionPanel.OnFarmersAssignedToField -= HandleFarmersAssigned;
        }

        if (confirmFieldSetupButton != null)
        {
            confirmFieldSetupButton.onClick.RemoveListener(ConfirmFieldSetup);
        }

        // Removed: Unsubscriptions for cropSelectionToggleButton and farmerSelectionToggleButton
    }

    /// <summary>
    /// Called by a CropManager instance when its world UI icon is clicked.
    /// Opens the main assignment UI panel and initializes display.
    /// </summary>
    /// <param name="fieldID">The unique ID of the field to configure.</param>
    /// <param name="currentCrop">The CropType currently assigned to this field (can be null if unassigned).</param>
    /// <param name="currentFarmers">The list of AIWorkStates currently assigned to this field.</param>
    public void OpenAssignPanel(string fieldID, CropType currentCrop, List<AIWorkState> currentFarmers)
    {
        _currentFieldIDBeingConfigured = fieldID;
        currentlySelectedCropType = currentCrop;
        _tempSelectedFarmers = new List<AIWorkState>(currentFarmers);

        if (mainAssignmentPanel != null)
        {
            mainAssignmentPanel.SetActive(true); // Show the main assignment panel
        }

        // Removed: Logic for cropSelectionSubPanel and farmerSelectionSubPanel from OpenAssignPanel

        // Generate dynamic buttons for selection each time the panel opens
        GenerateCropSelectionButtons();
        GenerateFarmerSelectionButtons();

        // Pass existing assignments to FarmerSelectionPanel to show current state
        if (farmerSelectionPanel != null)
        {
            farmerSelectionPanel.OpenPanel(fieldID, currentFarmers);
        }

        // Update Crop Display UI on panel open
        UpdateCropDisplayUI(currentlySelectedCropType);
        // Update Farmer Display UI on panel open (now showing ALL selected farmers)
        UpdateAssignedFarmerDisplay(_tempSelectedFarmers);
    }

    /// <summary>
    /// Called to close the main assignment UI panel.
    /// </summary>
    public void CloseAssignPanel()
    {
        if (mainAssignmentPanel != null)
        {
            mainAssignmentPanel.SetActive(false); // Hide the main assignment panel
        }
        // Removed: Also ensure sub-panels are hidden on close
    }

    /// <summary>
    /// This method is called when the "Confirm Field Setup" UI Button is clicked.
    /// It finalizes the selections and notifies the relevant CropManager.
    /// </summary>
    public void ConfirmFieldSetup()
    {
        if (string.IsNullOrEmpty(_currentFieldIDBeingConfigured))
        {
            Debug.LogWarning("No field ID is being configured. Cannot confirm setup.");
            CloseAssignPanel();
            return;
        }

        if (currentlySelectedCropType == null)
        {
            Debug.LogWarning($"No CropType selected for field {_currentFieldIDBeingConfigured}. Please select a crop.");
            return;
        }

        OnFieldSetupConfirmed?.Invoke(_currentFieldIDBeingConfigured, currentlySelectedCropType, _tempSelectedFarmers);

        Debug.Log($"Field {_currentFieldIDBeingConfigured} setup confirmed with Crop: {currentlySelectedCropType.cropName} and {_tempSelectedFarmers.Count} farmers.");

        CloseAssignPanel();
    }

    /// <summary>
    /// Called by dynamically generated UI buttons to select a CropType.
    /// </summary>
    /// <param name="crop">The CropType ScriptableObject selected by the player.</param>
    public void SelectCropType(CropType crop)
    {
        currentlySelectedCropType = crop;
        UpdateCropDisplayUI(crop);
        Debug.Log($"Crop selected in UI: {crop.cropName}");

        // Optionally, close crop selection sub-panel after selection
        // if (cropSelectionSubPanel != null && cropSelectionSubPanel.activeSelf)
        // {
        //      cropSelectionSubPanel.SetActive(false);
        // }
    }

    /// <summary>
    /// Internal callback method that receives the list of selected farmers from FarmerSelectionPanel.
    /// </summary>
    /// <param name="selectedFarmersFromPanel">The list of AIWorkState objects chosen by the player.</param>
    /// <param name="fieldID">The field ID associated with the selection.</param>
    private void HandleFarmersAssigned(List<AIWorkState> selectedFarmersFromPanel, string fieldID)
    {
        if (fieldID == _currentFieldIDBeingConfigured)
        {
            _tempSelectedFarmers = selectedFarmersFromPanel;
            UpdateAssignedFarmerDisplay(selectedFarmersFromPanel); // Update display with ALL selected farmers
            Debug.Log($"AssignPanelManager received {selectedFarmersFromPanel.Count} selected farmers for field {fieldID}.");
        }
    }

    // Removed: ToggleCropSelectionPanel and ToggleFarmerAssignmentPanel methods


    /// <summary>
    /// Updates the UI elements showing the currently selected crop.
    /// This now instantiates a prefab to display the crop and sets its image.
    /// </summary>
    /// <param name="crop">The crop to display. Null clears the display.</param>
    private void UpdateCropDisplayUI(CropType crop)
    {
        if (assignedCropDisplayParent == null || assignedCropDisplayPrefab == null)
        {
            Debug.LogWarning("Crop display parent or prefab not assigned in AssignPanelManager.");
            return;
        }

        // Clear previous display instance
        if (_currentCropDisplayInstance != null)
        {
            Destroy(_currentCropDisplayInstance);
            _currentCropDisplayInstance = null;
        }

        if (crop != null)
        {
            _currentCropDisplayInstance = Instantiate(assignedCropDisplayPrefab, assignedCropDisplayParent);

            TextMeshProUGUI cropNameText = _currentCropDisplayInstance.GetComponentInChildren<TextMeshProUGUI>();
            Image cropIconImage = _currentCropDisplayInstance.GetComponentInChildren<Image>();

            if (cropNameText != null)
            {
                cropNameText.text = crop.cropName;
            }
            if (cropIconImage != null)
            {
                cropIconImage.sprite = crop.cropIcon;
                cropIconImage.gameObject.SetActive(crop.cropIcon != null);
            }
            _currentCropDisplayInstance.SetActive(true);
        }
        // If crop is null, _currentCropDisplayInstance is already null/destroyed
    }

    /// <summary>
    /// Updates the UI elements displaying the currently assigned farmers.
    /// This now instantiates multiple prefabs for all selected farmers.
    /// </summary>
    /// <param name="farmers">The list of AIWorkState objects of assigned farmers.</param>
    private void UpdateAssignedFarmerDisplay(List<AIWorkState> farmers)
    {
        if (assignedFarmerDisplayParent == null || assignedFarmerDisplayPrefab == null)
        {
            Debug.LogWarning("Farmer display parent or prefab not assigned in AssignPanelManager.");
            return;
        }

        // Clear existing farmer displays
        foreach (Transform child in assignedFarmerDisplayParent)
        {
            Destroy(child.gameObject);
        }

        // Instantiate new displays for each farmer
        if (farmers != null && farmers.Count > 0)
        {
            foreach (AIWorkState farmer in farmers)
            {
                if (farmer == null) continue; // Skip null entries

                GameObject farmerDisplay = Instantiate(assignedFarmerDisplayPrefab, assignedFarmerDisplayParent);
                TextMeshProUGUI farmerNameText = farmerDisplay.GetComponentInChildren<TextMeshProUGUI>();
                Image farmerIconImage = farmerDisplay.GetComponentInChildren<Image>();

                if (farmerNameText != null)
                {
                    farmerNameText.text = farmer.farmerName;
                }
                if (farmerIconImage != null)
                {
                    farmerIconImage.sprite = farmer.farmerIcon;
                    farmerIconImage.gameObject.SetActive(farmer.farmerIcon != null);
                }
                farmerDisplay.SetActive(true);
            }
        }
        else
        {
            // Optional: Display a "No farmers assigned" message if needed
            Debug.Log("No farmers currently assigned to display.");
        }
    }

    /// <summary>
    /// Dynamically generates crop selection buttons based on available CropType ScriptableObjects.
    /// Prioritizes `cropInventoryList` if assigned, otherwise loads from Resources.
    /// </summary>
    private void GenerateCropSelectionButtons()
    {
        if (cropButtonParent == null || cropButtonPrefab == null)
        {
            Debug.LogWarning("Crop button parent or prefab not assigned. Cannot generate crop selection buttons.");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in cropButtonParent)
        {
            Destroy(child.gameObject);
        }

        List<CropType> allCropTypes = new List<CropType>();

        // 3. Check if cropInventoryList is populated, otherwise load from Resources
        if (cropInventoryList != null && cropInventoryList.Count > 0)
        {
            allCropTypes = cropInventoryList;
            Debug.Log($"Generating crop buttons from assigned Crop Inventory List ({allCropTypes.Count} crops).");
        }
        else
        {
            CropType[] loadedCropTypes = Resources.LoadAll<CropType>("CropTypes");
            if (loadedCropTypes != null && loadedCropTypes.Length > 0)
            {
                allCropTypes = loadedCropTypes.ToList();
                Debug.Log($"Generating crop buttons from Resources/CropTypes/ ({allCropTypes.Count} crops).");
            }
            else
            {
                Debug.LogWarning("No CropType ScriptableObjects found in `cropInventoryList` or `Resources/CropTypes/`. Please ensure they exist.");
                return;
            }
        }

        foreach (CropType crop in allCropTypes)
        {
            if (crop == null) continue;

            GameObject newButtonGO = Instantiate(cropButtonPrefab, cropButtonParent);
            Button button = newButtonGO.GetComponent<Button>();
            TextMeshProUGUI buttonText = newButtonGO.GetComponentInChildren<TextMeshProUGUI>();
            Image buttonImage = newButtonGO.GetComponentInChildren<Image>();

            if (buttonText != null)
            {
                buttonText.text = crop.cropName;
            }
            if (buttonImage != null && crop.cropIcon != null)
            {
                buttonImage.sprite = crop.cropIcon;
                buttonImage.gameObject.SetActive(true);
            } else if (buttonImage != null) {
                buttonImage.gameObject.SetActive(false);
            }

            button.onClick.AddListener(() => SelectCropType(crop));
        }
    }

    /// <summary>
    /// Dynamically generates farmer selection buttons based on available AIWorkState objects.
    /// </summary>
    private void GenerateFarmerSelectionButtons()
    {
        if (farmerButtonParent == null || farmerButtonPrefab == null)
        {
            Debug.LogWarning("Farmer button parent or prefab not assigned. Cannot generate farmer selection buttons.");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in farmerButtonParent)
        {
            Destroy(child.gameObject);
        }

        // --- PLACEHOLDER: HOW TO GET YOUR LIST OF AVAILABLE FARMERS ---
        // This is still using FindObjectsByType for simplicity. Adjust if you have a FarmerManager.
        AIWorkState[] allAvailableFarmersArray = FindObjectsByType<AIWorkState>(FindObjectsSortMode.None);
        List<AIWorkState> allAvailableFarmers = allAvailableFarmersArray.ToList();
        // --- END PLACEHOLDER ---

        if (allAvailableFarmers == null || allAvailableFarmers.Count == 0)
        {
            Debug.LogWarning("No AIWorkState (farmer) objects found in the scene. Please ensure they exist.");
            return;
        }

        foreach (AIWorkState farmer in allAvailableFarmers)
        {
            if (farmer == null) continue;

            GameObject newButtonGO = Instantiate(farmerButtonPrefab, farmerButtonParent);
            Button button = newButtonGO.GetComponent<Button>();
            TextMeshProUGUI buttonText = newButtonGO.GetComponentInChildren<TextMeshProUGUI>();
            Image buttonImage = newButtonGO.GetComponentInChildren<Image>();

            // Add a component to the button to hold a reference to the farmer it represents
            // This is crucial for FarmerSelectionPanel to know which button corresponds to which farmer for visual feedback.
            FarmerButtonHandler buttonHandler = newButtonGO.GetComponent<FarmerButtonHandler>();
            if (buttonHandler == null)
            {
                buttonHandler = newButtonGO.AddComponent<FarmerButtonHandler>();
            }
            buttonHandler.Initialize(farmer);


            if (buttonText != null)
            {
                buttonText.text = farmer.farmerName;
            }
            if (buttonImage != null && farmer.farmerIcon != null)
            {
                buttonImage.sprite = farmer.farmerIcon;
                buttonImage.gameObject.SetActive(true);
            } else if (buttonImage != null) {
                buttonImage.gameObject.SetActive(false);
            }

            if (farmerSelectionPanel != null)
            {
                // Pass the farmer and the button's image (for visual feedback)
                // The FarmerSelectionPanel.ToggleFarmerSelection will now handle the image change too.
                button.onClick.AddListener(() => farmerSelectionPanel.ToggleFarmerSelection(farmer, buttonImage));
            }
        }
    }
}
