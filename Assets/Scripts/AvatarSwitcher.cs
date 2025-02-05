using Ubiq.Avatars;
using UnityEngine;

public class AvatarSwitcher : MonoBehaviour
{
    public GameObject[] avatars;
    private int currentAvatarIndex = 0;
    private AvatarManager avatarManager;

    // check if we have a saved avatar index in player prefs
    private void Awake()
    {
        avatarManager = GetComponent<AvatarManager>();

        if (avatars.Length == 0) return;
        
        if (PlayerPrefs.HasKey("AvatarIndex"))
        {
            currentAvatarIndex = PlayerPrefs.GetInt("AvatarIndex");
        }

        avatarManager.avatarPrefab = avatars[currentAvatarIndex];
    }

    
    
    
    public void Next()
    {
        currentAvatarIndex++;
        if (currentAvatarIndex < avatars.Length)
        {
            avatarManager.avatarPrefab = avatars[currentAvatarIndex];
        }
        else
        {
            currentAvatarIndex = 0;
            avatarManager.avatarPrefab = avatars[currentAvatarIndex];
        }
        PlayerPrefs.SetInt("AvatarIndex", currentAvatarIndex);
    }
    
    public void Previous()
    {
        currentAvatarIndex--;
        if (currentAvatarIndex >= 0)
        {
            avatarManager.avatarPrefab = avatars[currentAvatarIndex];
        }
        else
        {
            currentAvatarIndex = avatars.Length - 1;
            avatarManager.avatarPrefab = avatars[currentAvatarIndex];
        }
        PlayerPrefs.SetInt("AvatarIndex", currentAvatarIndex);
    }
}
