using System.Collections;
using System.Collections.Generic;
using Oculus.Avatar2;
using UnityEngine;

public class EyePoseBehavior : OvrAvatarEyePoseBehavior
{
    public OVREyeGaze EyeGazeLeft { get; private set; }
    public OVREyeGaze EyeGazeRight { get; private set; }

    public Pose recordedLeftEye;
    public Pose recordedRightEye;
    public bool recordedValid = false;
    
    [HideInInspector]
    public UbiqInputManager inputManager;
    
    public override OvrAvatarEyePoseProviderBase EyePoseProvider
    {
        get
        {
            InitializeEyePoseProvider();

            return _eyePoseProvider;
        }
    }
    private OvrAvatarEyePoseProviderBase _eyePoseProvider;

    public void Start()
    {
        inputManager = GetComponent<UbiqInputManager>();
        if (!gameObject.TryGetComponent(out Replayable _))
        {
            Debug.Log("(Real User) Get EyeGazeLeft and EyeGazeRight");
            var eyes = GetComponentsInChildren<OVREyeGaze>();
            EyeGazeLeft = eyes[0];
            EyeGazeRight = eyes[1];
        }
        else
        {
            Debug.Log("(Replay)");
        }
    }
    
    private void InitializeEyePoseProvider()
    {
        if (_eyePoseProvider != null) return;

        _eyePoseProvider = new EyeTrackingProvider(this);
    }
}

public class EyeTrackingProvider : OvrAvatarEyePoseProviderBase
{
    private EyePoseBehavior _eyePoseBehavior;
    
    public EyeTrackingProvider(EyePoseBehavior eyePoseBehavior)
    {
        _eyePoseBehavior = eyePoseBehavior;
    }
    
    protected override bool GetEyePose(OvrAvatarEyesPose eyePose)
    {
        if (_eyePoseBehavior.EyeGazeLeft)
        {
            // Debug.Log("Eye pose is not null");
            if (_eyePoseBehavior.EyeGazeLeft.EyeTrackingEnabled)
            {
            // Debug.Log("Eye pose behavior, tracking enabled");
                eyePose.leftEye = new CAPI.ovrAvatar2EyePose()
                {
                    orientation = _eyePoseBehavior.EyeGazeLeft.transform.rotation,
                    position = _eyePoseBehavior.EyeGazeLeft.transform.position,
                    isValid = true
                };
                eyePose.rightEye = new CAPI.ovrAvatar2EyePose()
                {
                    orientation = _eyePoseBehavior.EyeGazeRight.transform.rotation,
                    position = _eyePoseBehavior.EyeGazeRight.transform.position,
                    isValid = true
                };
                return true;
            }
        }
        else
        {
            if (!_eyePoseBehavior.recordedValid)
            {
                _eyePoseBehavior.inputManager.eyeTrackingValid = false;
                return false;
            }
            
            eyePose.leftEye = new CAPI.ovrAvatar2EyePose()
            {
                orientation = _eyePoseBehavior.recordedLeftEye.rotation,
                position = _eyePoseBehavior.recordedLeftEye.position,
                isValid = true
            };
            
            eyePose.rightEye = new CAPI.ovrAvatar2EyePose()
            {
                orientation = _eyePoseBehavior.recordedRightEye.rotation,
                position = _eyePoseBehavior.recordedRightEye.position,
                isValid = true
            };
            return true;
        }
        _eyePoseBehavior.inputManager.eyeTrackingValid = false;
        return false;
    }

}
