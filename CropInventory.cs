// Assets/Scripts/CropInventory.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCropInventory", menuName = "Farming/Crop Inventory")]
public class CropInventory : ScriptableObject
{
    [Tooltip("List of all available CropType Scriptable Objects in your game.")]
    public List<CropType> availableCropTypes;
}
