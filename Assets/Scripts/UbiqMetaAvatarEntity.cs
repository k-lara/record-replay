using System.Collections.Generic;
using Oculus.Avatar2;
using Oculus.Avatar2.Experimental;
using Ubiq.Spawning;
using UnityEngine;
using CAPI = Oculus.Avatar2.CAPI;

/// <summary>
/// The UbiqMetaAvatarEntity receives its input from the UbiqMetaAvatarInputManager - thus from Ubiq
/// This means the MetaAvatar can be networked with Ubiq just like any other avatar.
/// Meta only seems to allow legs for remote avatars, at least I did not find a way to animate a local avatar with legs.
/// But we want our recorded avatars to have legs too, and they are local,
/// otherwise they won't get the simulated tracking input from the replay.
/// So we are create here another loopback avatar that gets the input from the MetaAvatar.
/// This avatar because it is seen as a remote avatar will have legs.
/// </summary>
public class UbiqMetaAvatarEntity : SampleAvatarEntity
{
    private OvrAvatarEntity loopbackAvatar;
    
    public void SetView(CAPI.ovrAvatar2EntityViewFlags view)
    {
        SetActiveView(view);

        // we also want full body animation
        GetComponent<OvrAvatarAnimationBehavior>()._enableLocalAnimationPlayback = true;
        
        // // instantiate another avatar for the loopback
        // loopbackAvatar = Instantiate(loopbackAvatarPrefab).GetComponent<SampleAvatarEntity>();
        // loopbackAvatar.SetIsLocal(false);
        //
        // var loopbackManager = gameObject.AddComponent<SampleRemoteLoopbackManager>();
        // loopbackAvatar.VerifyCanApplyStreaming();
        // loopbackManager.Configure(this, new List<OvrAvatarEntity>() { loopbackAvatar });
        //
        // add a collider to this avatar's head joint so that the takeover selector can take it over
        // get the head joint game object from this avatar
    }

    protected override void OnDestroyCalled()
    {
        if (loopbackAvatar != null)
        {
            Destroy(loopbackAvatar.gameObject);
        }        
    }
}
