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
                var lr = _eyePoseBehavior.EyeGazeLeft.transform.rotation;
                var lp = _eyePoseBehavior.EyeGazeLeft.transform.position;
                var rr = _eyePoseBehavior.EyeGazeRight.transform.rotation;
                var rp = _eyePoseBehavior.EyeGazeRight.transform.position;

                lr = new Quaternion(-lr.x, -lr.y, lr.z, lr.w);
                rr = new Quaternion(-rr.x, -rr.y, rr.z, rr.w);
                
                eyePose.leftEye.orientation = lr;
                eyePose.leftEye.position = lp;
                eyePose.leftEye.isValid = true;
                eyePose.rightEye.orientation = rr;
                eyePose.rightEye.position = rp;
                eyePose.rightEye.isValid = true;
                
                // Debug.Log("GetEyePose() " + new Quaternion(
                //     eyePose.leftEye.orientation.x,
                //     eyePose.leftEye.orientation.y,
                //     eyePose.leftEye.orientation.z,
                //     eyePose.leftEye.orientation.w).eulerAngles
                // + " " + new Vector3(eyePose.leftEye.position.x, 
                //     eyePose.leftEye.position.y,
                //     eyePose.leftEye.position.z).ToString("F3")
                // );
                return true;
            }
        }
        else
        {
            if (!_eyePoseBehavior.recordedValid)
            {
                // Debug.Log(_eyePoseBehavior);
                // Debug.Log(_eyePoseBehavior.inputManager);
                // Debug.Log(_eyePoseBehavior.inputManager.eyeTrackingValid);
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
