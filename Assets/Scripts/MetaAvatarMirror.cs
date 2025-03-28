using System.Collections;
using System.Collections.Generic;
using Oculus.Avatar2;
using Oculus.Avatar2.Experimental;
using Ubiq.Avatars;
using UnityEngine;
using UnityEngine.XR.OpenXR.Input;

public class MetaAvatarMirror : MonoBehaviour
{
    // the avatar manager gives us information about the current avatar which we can mirror
    public AvatarManager avatarManager;

    private SampleAvatarEntity activeMirrorAvatar;
    private Dictionary<string, SampleAvatarEntity> avatarEntities = new Dictionary<string, SampleAvatarEntity>();
    private bool init = false;
    
    private UbiqInputManager ubiqInputManager;
    
    void Awake()
    {
        avatarManager.OnAvatarCreated.AddListener(OnAvatarCreated);
        InitDictionary();
    }

    private void InitDictionary()
    {
        var entities = GetComponentsInChildren<SampleAvatarEntity>(true);
        Debug.Log("Init mirror dictionary with " + entities.Length + " entities");
        foreach (var entity in entities)
        {
            Debug.Log("Mirror avatars: " + entity.name.Remove(entity.name.Length-7));
            // name without the mirror postfix (Meta Avatar Variant 12| Mirror|)
            avatarEntities.Add(entity.name.Remove(entity.name.Length-7), entity);
        }
        init = true;
    }

    private void OnAvatarCreated(Ubiq.Avatars.Avatar avatar)
    {
        ubiqInputManager = avatar.GetComponent<UbiqInputManager>();
        
        if (activeMirrorAvatar)
        {
            activeMirrorAvatar.gameObject.SetActive(false); // hide the previous mirror avatar
        }
        activeMirrorAvatar = avatarEntities[avatarManager.avatarPrefab.name];
        activeMirrorAvatar.gameObject.SetActive(true); // show the new mirror avatar

        // use the user avatar inputs for the mirror avatar
        activeMirrorAvatar.SetInputManager(ubiqInputManager);
        activeMirrorAvatar.SetEyePoseProvider(avatar.GetComponent<EyePoseBehavior>());
        activeMirrorAvatar.SetFacePoseProvider(avatar.GetComponent<FacePoseBehavior>());
    }
}
