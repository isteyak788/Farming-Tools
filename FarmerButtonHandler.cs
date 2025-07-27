// Assets/Scripts/UI/FarmerButtonHandler.cs
using UnityEngine;

// This script should be attached to your farmerButtonPrefab's root GameObject.
public class FarmerButtonHandler : MonoBehaviour
{
    // The farmer associated with this UI button
    public AIWorkState AssociatedFarmer { get; private set; }

    /// <summary>
    /// Initializes this button handler with its associated farmer data.
    /// </summary>
    /// <param name="farmer">The AIWorkState instance this button represents.</param>
    public void Initialize(AIWorkState farmer)
    {
        AssociatedFarmer = farmer;
        // Optionally, set the button's name for debugging in Hierarchy
        gameObject.name = "FarmerButton_" + farmer.farmerName;
    }
}
