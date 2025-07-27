using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic; // Required for List

public class UIPanelToggler : MonoBehaviour
{
    [System.Serializable]
    public class UIPanelEntry
    {
        public string panelName; // A descriptive name for the UI panel
        public Button toggleButton; // The UI Button that will toggle this panel
        public GameObject uiElement; // The UI GameObject (panel) to enable/disable
    }

    [Tooltip("List of UI panels to manage. Assign Buttons and UI GameObjects here.")]
    public List<UIPanelEntry> uiPanels;

    [Tooltip("If true, only one panel can be active at a time. Activating one will deactivate others.")]
    public bool singlePanelMode = false;

    private Dictionary<string, GameObject> panelDictionary = new Dictionary<string, GameObject>();

    void Awake()
    {
        // Populate the dictionary for quick lookup by panel name
        foreach (UIPanelEntry entry in uiPanels)
        {
            if (entry.uiElement != null)
            {
                if (panelDictionary.ContainsKey(entry.panelName))
                {
                    Debug.LogWarning($"Duplicate panel name '{entry.panelName}' found in UIPanelToggler on {gameObject.name}. Ensure panel names are unique.");
                    continue;
                }
                panelDictionary.Add(entry.panelName, entry.uiElement);
                // Ensure all panels are initially hidden, or set their initial state based on your preference
                entry.uiElement.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"UI Element for panel '{entry.panelName}' is not assigned in UIPanelToggler on {gameObject.name}.");
            }
        }
    }

    void OnEnable()
    {
        // Subscribe buttons to their toggle events
        foreach (UIPanelEntry entry in uiPanels)
        {
            if (entry.toggleButton != null)
            {
                // Remove any existing listeners to prevent double subscription
                entry.toggleButton.onClick.RemoveAllListeners();
                // Add the new listener
                entry.toggleButton.onClick.AddListener(() => TogglePanel(entry.panelName));
                Debug.Log($"Subscribed button '{entry.toggleButton.name}' to toggle panel '{entry.panelName}'.");
            }
            else
            {
                Debug.LogWarning($"Toggle Button for panel '{entry.panelName}' is not assigned in UIPanelToggler on {gameObject.name}.");
            }
        }
    }

    void OnDisable()
    {
        // Unsubscribe buttons when the script is disabled to prevent memory leaks
        foreach (UIPanelEntry entry in uiPanels)
        {
            if (entry.toggleButton != null)
            {
                entry.toggleButton.onClick.RemoveAllListeners();
            }
        }
    }

    /// <summary>
    /// Toggles the active state of a UI panel identified by its name.
    /// If singlePanelMode is true, other active panels will be deactivated.
    /// </summary>
    /// <param name="panelNameToToggle">The name of the panel to toggle.</param>
    public void TogglePanel(string panelNameToToggle)
    {
        if (panelDictionary.TryGetValue(panelNameToToggle, out GameObject panelToToggle))
        {
            bool currentState = panelToToggle.activeSelf;

            if (singlePanelMode && !currentState) // If activating a panel in single panel mode
            {
                DeactivateAllPanels();
            }

            panelToToggle.SetActive(!currentState);
            Debug.Log($"Panel '{panelNameToToggle}' visibility set to: {!currentState}");
        }
        else
        {
            Debug.LogWarning($"Panel with name '{panelNameToToggle}' not found in UIPanelToggler dictionary.");
        }
    }

    /// <summary>
    /// Activates a specific UI panel and deactivates others if singlePanelMode is true.
    /// </summary>
    /// <param name="panelNameToActivate">The name of the panel to activate.</param>
    public void ActivatePanel(string panelNameToActivate)
    {
        if (panelDictionary.TryGetValue(panelNameToActivate, out GameObject panelToActivate))
        {
            if (singlePanelMode)
            {
                DeactivateAllPanels();
            }
            panelToActivate.SetActive(true);
            Debug.Log($"Panel '{panelNameToActivate}' activated.");
        }
        else
        {
            Debug.LogWarning($"Panel with name '{panelNameToActivate}' not found for activation.");
        }
    }

    /// <summary>
    /// Deactivates a specific UI panel.
    /// </summary>
    /// <param name="panelNameToDeactivate">The name of the panel to deactivate.</param>
    public void DeactivatePanel(string panelNameToDeactivate)
    {
        if (panelDictionary.TryGetValue(panelNameToDeactivate, out GameObject panelToDeactivate))
        {
            panelToDeactivate.SetActive(false);
            Debug.Log($"Panel '{panelNameToDeactivate}' deactivated.");
        }
        else
        {
            Debug.LogWarning($"Panel with name '{panelNameToDeactivate}' not found for deactivation.");
        }
    }

    /// <summary>
    /// Deactivates all managed UI panels.
    /// </summary>
    public void DeactivateAllPanels()
    {
        foreach (var entry in panelDictionary)
        {
            if (entry.Value != null && entry.Value.activeSelf)
            {
                entry.Value.SetActive(false);
                Debug.Log($"Panel '{entry.Key}' deactivated by DeactivateAllPanels.");
            }
        }
    }
}
