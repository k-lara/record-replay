using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvatarMaterials : MonoBehaviour
{
    public void Start()
    {
        if (materialsOpaque.Count == 0)
        {
            Debug.LogError("No opaque materials found in AvatarMaterials");
        }
        if (materialsFade.Count == 0)
        {
            Debug.LogError("No fade materials found in AvatarMaterials");
        }
    }
    
    // get materials by name
    public Material GetMaterialOpaque(string name)
    {
        foreach (var material in materialsOpaque)
        {
            // Debug.Log(material.name + " " + name);
            if (material.name == name)
            {
                return material;
            }
        }
        return null;
    }
    
    public Material GetMaterialFade(string name)
    {
        foreach (var material in materialsFade)
        {
            // Debug.Log(material.name + " " + name);
            if (material.name == name)
            {
                return material;
            }
        }
        return null;
    }
    
    [Tooltip("All opaque materials that are used on this avatar. Should be in the same order as the the fade materials. Make sure names are without whitespace!")]
    public List<Material> materialsOpaque = new();
    [Tooltip("Should be in the same order as materialsOpaque. Make sure names are without whitespace!")]
    public List<Material> materialsFade = new();

}
