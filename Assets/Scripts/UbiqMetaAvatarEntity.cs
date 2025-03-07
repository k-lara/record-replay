using System.Collections.Generic;
using Oculus.Avatar2;
using Oculus.Avatar2.Experimental;
using Ubiq.Spawning;
using UnityEngine;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// The UbiqMetaAvatarEntity receives its input from the UbiqMetaAvatarInputManager - thus from Ubiq
/// This means the MetaAvatar can be networked with Ubiq just like any other avatar.
/// </summary>
public class UbiqMetaAvatarEntity : SampleAvatarEntity
{
    public void SetView(CAPI.ovrAvatar2EntityViewFlags view)
    {
        SetActiveView(view);

        // we also want full body animation
        GetComponent<OvrAvatarAnimationBehavior>()._enableLocalAnimationPlayback = true;
    }
    
    public string GetCurrentAvatarAsset()
    {
        return _assets[0].path;
    }
}
