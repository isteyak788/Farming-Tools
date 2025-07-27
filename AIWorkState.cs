// Assets/Scripts/AI/AIWorkState.cs
using UnityEngine;

/// <summary>
/// Attached to AI Farmer GameObjects to manage their working state.
/// </summary>
public class AIWorkState : MonoBehaviour
{
    [Header("AI Farmer State")]
    [Tooltip("Is this AI farmer currently assigned to a task?")]
    public bool isWorking = false;

    [Tooltip("The ID of the field/mesh this AI farmer is currently assigned to. Use null or empty string if not assigned.")]
    public string assignedFieldID = ""; // To link a farmer to a specific field

    [Tooltip("The display name of this AI farmer.")]
    public string farmerName = "New AI Farmer"; // A unique name for display purposes
    
    [Tooltip("The Image That will be displayed in Ui")]
    public Sprite farmerIcon;

    void Awake()
    {
        // Ensure a default name if none is set
        if (string.IsNullOrEmpty(farmerName))
        {
            farmerName = gameObject.name;
        }
    }

    /// <summary>
    /// Sets the farmer to a working state for a specific field.
    /// </summary>
    /// <param name="fieldID">The unique ID of the field the farmer is assigned to.</param>
    public void AssignToWork(string fieldID)
    {
        isWorking = true;
        assignedFieldID = fieldID;
        Debug.Log($"{farmerName} is now assigned to work on field: {fieldID}");
    }

    /// <summary>
    /// Sets the farmer to a non-working state.
    /// </summary>
    public void FinishWork()
    {
        isWorking = false;
        assignedFieldID = "";
        Debug.Log($"{farmerName} has finished work.");
    }
}
