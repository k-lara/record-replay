using System;
using Oculus.Avatar2;
using Oculus.Avatar2.Experimental;
using Ubiq;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;
using CAPI = Oculus.Avatar2.CAPI;

/**
 * Setting tracking input coming from Ubiq on an avatar entity.
 */
public class UbiqInputManager : OvrAvatarInputManager
{
    public Avatar ubiqAvatar;
    private UbiqMetaAvatarEntity _avatarEntity;
    private OvrAvatarAnimationBehavior _avatarAnimation;
    
    [Serializable]
    private struct State
    {
        public Pose head;
        public Pose leftHand;
        public Pose rightHand;
        public float leftGrip;
        public float rightGrip;
    }
    
    private State[] state = new State[1];

    protected void Awake()
    {
        if (!ubiqAvatar)
        {
            ubiqAvatar = GetComponent<Avatar>();
        }
    }

    // if this avatar is not a user controlled avatar, we want it to be third-person with head!
    protected void Start()
    {
        _avatarEntity = gameObject.GetComponent<UbiqMetaAvatarEntity>();
        _avatarAnimation = gameObject.GetComponent<OvrAvatarAnimationBehavior>();
        
        // not sure how else to check... the peer uuid is empty? null or ""?
        // but a Replayable is definitely not user controlled so should work as a check too
        if (_avatarEntity && gameObject.TryGetComponent(out Replayable replayable))
        {
            // here we hide the replayed avatar and spawn a loopback avatar
            // the loopback avatar will be controlled by this invisible avatar
            _avatarEntity.SetView(CAPI.ovrAvatar2EntityViewFlags.None);
        }
    }
    
    protected override void OnTrackingInitialized()
    {
        Debug.Log("Ubiq Ovr Avatar tracking initialized");
        _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new UbiqInputTrackingDelegate(this));
        _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(new UbiqInputControlDelegate());
    }
}

public class UbiqInputTrackingDelegate : OvrAvatarInputTrackingDelegate
{
    private UbiqInputManager _inputManager;

    public UbiqInputTrackingDelegate(UbiqInputManager inputManager)
    {
        _inputManager = inputManager;
    }
    
    public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
    {
        inputTrackingState = default;

        if (_inputManager.ubiqAvatar.input.TryGet(out IHeadAndHandsInput src))
        {
            inputTrackingState.headset = 
                new CAPI.ovrAvatar2Transform(src.head.value.position, src.head.value.rotation);
            inputTrackingState.leftController =
                new CAPI.ovrAvatar2Transform(src.leftHand.value.position, src.leftHand.value.rotation);
            inputTrackingState.rightController =
                new CAPI.ovrAvatar2Transform(src.rightHand.value.position, src.rightHand.value.rotation);
            
            inputTrackingState.headsetActive = true;
            inputTrackingState.leftControllerActive = true;
            inputTrackingState.rightControllerActive = true;
            inputTrackingState.leftControllerVisible = true;
            inputTrackingState.rightControllerVisible = true;
        }
        else
        {
            inputTrackingState.headsetActive = false;
            inputTrackingState.leftControllerActive = false;
            inputTrackingState.rightControllerActive = false;
            inputTrackingState.leftControllerVisible = false;
            inputTrackingState.rightControllerVisible = false;
            return false;
        }
        return true;
    }
}

public class UbiqInputControlDelegate: OvrAvatarInputControlDelegate
{
    public CAPI.ovrAvatar2ControllerType controllerType = CAPI.ovrAvatar2ControllerType.Invalid;
    public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
    {
        inputControlState = default;
        inputControlState.type = controllerType;

        return true;
    }
}
