using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ubiq;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

public class Recordable : MonoBehaviour
{
    public string prefabName;

    private bool _isRecording;
    private bool _isTakingOver;
    [HideInInspector] public bool IsTakingOver
    {
        get => _isTakingOver;
        set => _isTakingOver = value;
    }
    private Guid _guid; // the guid of this recordable
    private int _currentFrameNr;
    
    private Recorder _recorder;
    private Avatar _avatar;
    private AvatarInput _avatarInput;
    
    private int _sizeData;
    private readonly int _numTrackingPoints = 3;
    private int _numBlendshapes;
    
    private float _frameInterval; // time that has passed since last 10 fps frame
    private float _previousPoseTime; // time of the last pose update (this is usually slightly higher than 1/fps)
    private float _t; // interpolation factor
    private bool _interpolate;
    
    public event EventHandler<RecordablePose> OnUpdateRecordablePose;
    public class RecordablePose
    {
        public Pose head;
        public Pose leftHand;
        public Pose rightHand;
    }

    private RecordablePose _recordablePose;
    private Pose _prevHead;
    private Pose _prevLeftHand;
    private Pose _prevRightHand;
    
    // Start is called before the first frame update
    void Start()
    {
        _recorder = GameObject.FindWithTag("Recorder").GetComponent<Recorder>();
        
        _recorder.onRecordingStart += OnRecordingStart;
        _recorder.onRecordingStop += OnRecordingStop;

        _avatar = GetComponent<Avatar>();
        _avatarInput = _avatar.input;
        _prevHead = new Pose();
        _prevLeftHand = new Pose();
        _prevRightHand = new Pose();
        _recordablePose = new RecordablePose();
        
        // _hhAvatar = GetComponent<HeadAndHandsAvatar>();
        // _hhAvatar.OnHeadUpdate.AddListener(OnHeadUpdate);
        // _hhAvatar.OnLeftHandUpdate.AddListener(OnLeftHandUpdate);
        // _hhAvatar.OnRightHandUpdate.AddListener(OnRightHandUpdate);

        _sizeData = (_numTrackingPoints * 3 + _numTrackingPoints * 4) + _numBlendshapes;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isRecording) return;

        _frameInterval += Time.deltaTime;
        
        if (_avatarInput.TryGet(out IHeadAndHandsInput src))
        {
            if (_frameInterval >= 1.0f / _recorder.fps)
            {
                _t = (1.0f/_recorder.fps - _previousPoseTime) / (_frameInterval - _previousPoseTime);
                
                // adding a data frame that we will fill with the interpolated data
                // if we take over an avatar there is already data in the recording
                // we might need to overwrite some of that data instead of adding the frame
                _recorder.recording.TryAddEmptyDataFrame(_guid, _currentFrameNr);

                var headRotN = src.head.value.rotation.normalized;
                _recordablePose.head.position = Vector3.Lerp(_prevHead.position, src.head.value.position, _t);
                _recordablePose.head.rotation = Quaternion.Lerp(_prevHead.rotation, headRotN, _t);
                
                var leftHandRotN = src.leftHand.value.rotation.normalized;
                _recordablePose.leftHand.position = Vector3.Lerp(_prevLeftHand.position, src.leftHand.value.position, _t);
                _recordablePose.leftHand.rotation = Quaternion.Lerp(_prevLeftHand.rotation, leftHandRotN, _t);
                
                var rightHandRotN = src.rightHand.value.rotation.normalized;
                _recordablePose.rightHand.position = Vector3.Lerp(_prevRightHand.position, src.rightHand.value.position, _t);
                _recordablePose.rightHand.rotation = Quaternion.Lerp(_prevRightHand.rotation, rightHandRotN, _t);

                if (_isTakingOver)
                {
                    // the takeover gathers the data and merges it with the original replay data after recording is done
                    OnUpdateRecordablePose?.Invoke(this, _recordablePose);
                }
                else
                {
                    _recorder.recording.AddHeadTransform(_guid, _currentFrameNr, _recordablePose.head.position, _recordablePose.head.rotation);
                    _recorder.recording.AddLeftHandTransform(_guid, _currentFrameNr, _recordablePose.leftHand.position, _recordablePose.leftHand.rotation);
                    _recorder.recording.AddRightHandTransform(_guid, _currentFrameNr, _recordablePose.rightHand.position, _recordablePose.rightHand.rotation);
                }
                
                _currentFrameNr++;
                _frameInterval = _previousPoseTime - 1.0f / _recorder.fps;
            }
            
            _prevHead.position = src.head.value.position;
            _prevHead.rotation = src.head.value.rotation.normalized;
            _prevLeftHand.position = src.leftHand.value.position;
            _prevLeftHand.rotation = src.leftHand.value.rotation.normalized;
            _prevRightHand.position = src.rightHand.value.position;
            _prevRightHand.rotation = src.rightHand.value.rotation.normalized;
            
        }
        _previousPoseTime = _frameInterval;
    }

    // for additive recordings a recording could start in the middle of a replay 
    // so the currentFrameNr would depend on the current replay frame
    // TODO for taken over avatars we have to start before the end of the data!
    private void OnRecordingStart(object o, EventArgs e)
    {
        _frameInterval = _recorder.GetCurrentFrameFloat() - Mathf.FloorToInt(_recorder.GetCurrentFrameFloat());
        _currentFrameNr = Mathf.CeilToInt(_recorder.GetCurrentFrameFloat()); // get the next int frame to write on
        
        // if this is the player it will only get a guid assigned when it starts a new recording
        // if this is an existing replay who we want to take over then we already have a guid from the replayer
        if (!_isTakingOver)
        {
            Debug.Log("Create new recording entry!");
            _guid = _recorder.recording.CreateNewRecordableData();
            // so we already have the prefab name saved, even if frame number is not correct yet
            _recorder.recording.UpdateMetaData(_guid, _currentFrameNr, _recorder.fps, prefabName);

            // if we start a new recording and the current frame is > 0 we need to insert dummy frames
            if (_currentFrameNr > 0)
            {
                // we fill up the RecordableData up to the currentFrame - 1
                _recorder.recording.TryAddEmptyDataFrame(_guid, _currentFrameNr-1);
            }
        }
        _isRecording = true;
    }

    private void OnRecordingStop(object o, EventArgs e)
    {
        _isRecording = false;

        _recorder.recording.UpdateMetaData(_guid, _currentFrameNr, _recorder.fps, prefabName);
    }
    
    // if this is a taken over avatar, the data we get from the ThreePointTrackedAvatar should already be
    // correctly interpolated between the avatar's movements and the user's movements
    private void OnHeadUpdate(InputVar<Pose> pose)
    {
        if (!_isRecording) return;

        var rotN = pose.value.rotation.normalized;
        if (_interpolate)
        {
            var interpPos = Vector3.Lerp(_prevHead.position, pose.value.position, _t);
            var interpRot = Quaternion.Lerp(_prevHead.rotation, rotN, _t);
            
            _recorder.recording.AddHeadTransform(_guid, _currentFrameNr, interpPos, interpRot);
            
            // _dataFrameReady = true;
        }
        
        _prevHead = new Pose
        {
            position = pose.value.position,
            rotation = rotN
        };
    }
    private void OnLeftHandUpdate(InputVar<Pose> pose)
    {
        if (!_isRecording) return;
        
        var rotN = pose.value.rotation.normalized;
        if (_interpolate)
        {
            var interpPos = Vector3.Lerp(_prevLeftHand.position, pose.value.position, _t);
            var interpRot = Quaternion.Lerp(_prevLeftHand.rotation, rotN, _t);
            
            _recorder.recording.AddLeftHandTransform(_guid, _currentFrameNr, interpPos, interpRot);
        }
        
        _prevLeftHand = new Pose
        {
            position = pose.value.position,
            rotation = rotN
        };
    }
    private void OnRightHandUpdate(InputVar<Pose> pose)
    {
        if (!_isRecording) return;
        
        var rotN = pose.value.rotation.normalized;
        if (_interpolate)
        {
            var interpPos = Vector3.Lerp(_prevRightHand.position, pose.value.position, _t);
            var interpRot = Quaternion.Lerp(_prevRightHand.rotation, rotN, _t);
            
            _recorder.recording.AddRightHandTransform(_guid, _currentFrameNr, interpPos, interpRot);
        }
        
        _prevRightHand = new Pose
        {
            position = pose.value.position,
            rotation = rotN
        };
    }

    private void OnDestroy()
    {
        _recorder.onRecordingStart -= OnRecordingStart;
        _recorder.onRecordingStop -= OnRecordingStop;
    }
}
