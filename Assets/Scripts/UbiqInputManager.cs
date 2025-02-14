using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Oculus.Avatar2;
using Oculus.Avatar2.Experimental;
using Ubiq;
using Ubiq.Geometry;
using Unity.XR.CoreUtils;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;
using CAPI = Oculus.Avatar2.CAPI;

/**
 * Setting tracking input coming from Ubiq on an avatar entity.
 */
public class UbiqInputManager : OvrAvatarInputManager
{
    // public Vector3 hiklary;
    public Vector4 qmul1 = new Vector4(0, 0, 0, 1);
    public Vector4 qmul2 = new Vector4(0, 0, 0, 1);
    public Vector4 qmul3 = new Vector4(0, 0, 0, 1);
    public Avatar ubiqAvatar;
    // the hand skeletons for animating the hands
    private UbiqMetaAvatarEntity _avatarEntity;

    public GameObject leftHand;
    public GameObject rightHand;
    public Transform xrOrigin;
    public bool updateLeftHand = true;
    
    [Serializable]
    // controller state
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
        leftHand = GameObject.FindWithTag("MetaInputLeft");
        rightHand = GameObject.FindWithTag("MetaInputRight");

        xrOrigin = GameObject.FindAnyObjectByType<XROrigin>().transform;
    }

    // if this avatar is not a user controlled avatar, we want it to be third-person with head!
    protected void Start()
    {
        _avatarEntity = gameObject.GetComponent<UbiqMetaAvatarEntity>();
        // not sure how else to check... the peer uuid is empty? null or ""?
        // but a Replayable is definitely not user controlled so should work as a check too
        if (_avatarEntity && gameObject.TryGetComponent(out Replayable replayable))
        { 
            _avatarEntity.SetView(CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
        }
    }
    
    protected override void OnTrackingInitialized()
    {
        Debug.Log("Ubiq Ovr Avatar tracking initialized");
        _inputTrackingProvider = new OvrAvatarInputTrackingDelegatedProvider(new UbiqInputTrackingDelegate(this));
        _inputControlProvider = new OvrAvatarInputControlDelegatedProvider(new UbiqInputControlDelegate());
        _handTrackingProvider = new OvrAvatarHandTrackingDelegatedProvider(new UbiqHandTrackingDelegate(this));
    }
}

public class UbiqHandTrackingDelegate : IOvrAvatarHandTrackingDelegate
{
    private UbiqInputManager _inputManager;
    

    public UbiqHandTrackingDelegate(UbiqInputManager inputManager)
    {
        _inputManager = inputManager;
        // instantiate an empty game object to test the left hand
    }
    
    public bool GetHandData(OvrAvatarTrackingHandsState handData)
    {
        if (_inputManager.ubiqAvatar.input.TryGet(out IHandSkeletonInput src))
        {
            // don't bother doing anything if poses are not valid
            // seems like sometimes the poses collection is empty... but for some reason we still get here
            if (src.leftHandSkeleton.poses.Count == 0) return false;
            
            var posesLeft = src.leftHandSkeleton.poses;
            var posesRight = src.rightHandSkeleton.poses;
            
            var transformedLeft = new Quaternion[posesLeft.Count-2]; // exclude wrist and palm
            var transformedRight = new Quaternion[posesRight.Count-2];

            // wrist left
            var leftWrist = new Pose()
            {
                position = new Vector3(posesLeft[0].value.position.x, posesLeft[0].value.position.y,
                    -posesLeft[0].value.position.z),
                rotation = ConvertSpaceLeftHand(posesLeft[0].value.rotation)
            };
            
            // wrist right
            var rightWrist = new Pose()
            {
                position = new Vector3(posesRight[0].value.position.x, posesRight[0].value.position.y,
                    -posesRight[0].value.position.z),
                rotation = ConvertSpaceRightHand(posesRight[0].value.rotation)
            };
            
            handData.wristPosLeft = new CAPI.ovrAvatar2Transform(leftWrist.position, leftWrist.rotation);
            handData.wristPosRight = new CAPI.ovrAvatar2Transform(rightWrist.position, rightWrist.rotation);;
            
            // transform rotations
            for (int i = 2; i < posesLeft.Count; i++) // we start at 2 because we exclude wrist and palm
            {
                transformedLeft[i-2] = ConvertSpaceLeftHand(posesLeft[i].value.rotation);
                transformedRight[i-2] = ConvertSpaceRightHand(posesRight[i].value.rotation);
            }
            // for god's sake... this is complicated, ok finally figured it out!!!
            // each finger rotation needs to be relative to its parent rotation
            // the index, middle and ring fingers on the Avatar do not have a metacarpal bone, but that seems to be fine
            // there seems to be NO NEED to make the proximal bones relative to the metacarpal ones,
            // we can just make them relative to the wrist
            //left thumb (index in Quaternion array starts at 0 which is the thumb metacarpal
            handData.boneRotations[0] = Quaternion.identity; // trapezium (which we don't have in OpenXR)
            handData.boneRotations[1] = Quaternion.Inverse(leftWrist.rotation) * transformedLeft[0]; // meta
            handData.boneRotations[2] = Quaternion.Inverse(transformedLeft[0]) * transformedLeft[1]; // prox
            handData.boneRotations[3] = Quaternion.Inverse(transformedLeft[1]) * transformedLeft[2]; // dist
            // left index finger
            handData.boneRotations[4] = Quaternion.Inverse(leftWrist.rotation) * transformedLeft[5]; // prox
            handData.boneRotations[5] = Quaternion.Inverse(transformedLeft[5]) * transformedLeft[6]; // intermediate
            handData.boneRotations[6] = Quaternion.Inverse(transformedLeft[6]) * transformedLeft[7]; // dist
            // left middle finger
            handData.boneRotations[7] = Quaternion.Inverse(leftWrist.rotation) * transformedLeft[10]; //prox 
            handData.boneRotations[8] = Quaternion.Inverse(transformedLeft[10]) * transformedLeft[11]; // interm.
            handData.boneRotations[9] = Quaternion.Inverse(transformedLeft[11]) * transformedLeft[12]; // dist
            // left ring finger
            handData.boneRotations[10] = Quaternion.Inverse(leftWrist.rotation) * transformedLeft[15];
            handData.boneRotations[11] = Quaternion.Inverse(transformedLeft[15]) * transformedLeft[16];
            handData.boneRotations[12] = Quaternion.Inverse(transformedLeft[16]) * transformedLeft[17];
            // left pinky finger
            handData.boneRotations[13] = Quaternion.Inverse(leftWrist.rotation) * transformedLeft[19]; // meta
            handData.boneRotations[14] = Quaternion.Inverse(transformedLeft[19]) * transformedLeft[20]; // prox
            handData.boneRotations[15] = Quaternion.Inverse(transformedLeft[20]) * transformedLeft[21]; // interm.
            handData.boneRotations[16] = Quaternion.Inverse(transformedLeft[21]) * transformedLeft[22]; // dist
            ////////////////////////////////////////////////////////
            // right thumb
            handData.boneRotations[17] = Quaternion.identity;
            handData.boneRotations[18] = Quaternion.Inverse(rightWrist.rotation) * transformedRight[0];
            handData.boneRotations[19] = Quaternion.Inverse(transformedRight[0]) * transformedRight[1];
            handData.boneRotations[20] = Quaternion.Inverse(transformedRight[1]) * transformedRight[2];            
            // left index finger
            handData.boneRotations[21] = Quaternion.Inverse(rightWrist.rotation) * transformedRight[5];            
            handData.boneRotations[22] = Quaternion.Inverse(transformedRight[5]) * transformedRight[6];            
            handData.boneRotations[23] = Quaternion.Inverse(transformedRight[6]) * transformedRight[7];
            // left middle finger
            handData.boneRotations[24] = Quaternion.Inverse(rightWrist.rotation) * transformedRight[10];
            handData.boneRotations[25] = Quaternion.Inverse(transformedRight[10]) * transformedRight[11];
            handData.boneRotations[26] = Quaternion.Inverse(transformedRight[11]) * transformedRight[12];
            // left ring finger
            handData.boneRotations[27] = Quaternion.Inverse(rightWrist.rotation) * transformedRight[15];
            handData.boneRotations[28] = Quaternion.Inverse(transformedRight[15]) * transformedRight[16];
            handData.boneRotations[29] = Quaternion.Inverse(transformedRight[16]) * transformedRight[17];
            // left pinky finger
            handData.boneRotations[30] = Quaternion.Inverse(rightWrist.rotation) * transformedRight[19];
            handData.boneRotations[31] = Quaternion.Inverse(transformedRight[19]) * transformedRight[20];
            handData.boneRotations[32] = Quaternion.Inverse(transformedRight[20]) * transformedRight[21];
            handData.boneRotations[33] = Quaternion.Inverse(transformedRight[21]) * transformedRight[22];
            
            handData.isTrackedLeft = true;
            handData.isConfidentLeft = true;
            handData.isTrackedRight = true;
            handData.isConfidentRight = true;
            handData.handScaleLeft = 1;
            handData.handScaleRight = 1;
            return true;
        }
        return false;
    }

    // negates x and y component and rotates by 180 around x and 90 around y
    private Quaternion ConvertSpaceLeftHand(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, q.z, q.w) 
               * Quaternion.Euler(180, 90, 0);
    }
    private Quaternion ConvertSpaceRightHand(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, q.z, q.w)
            * Quaternion.Euler(0, -90, 0);
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
