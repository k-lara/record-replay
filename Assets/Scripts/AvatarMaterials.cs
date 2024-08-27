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
    
    [Tooltip("All opaque materials that are used on this avatar. Should be in the same order as the the fade materials.")]
    public List<Material> materialsOpaque = new();
    [Tooltip("Should be in the same order as materialsOpaque.")]
    public List<Material> materialsFade = new();

}
