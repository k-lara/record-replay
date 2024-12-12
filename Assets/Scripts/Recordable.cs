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
    
    // make an eventhandler that has RecordablePose and the current frame number
    
    public event EventHandler<RecordablePoseArgs> OnUpdateRecordablePose;
    
    public class RecordablePoseArgs : EventArgs
    {
        public RecordablePose recordablePose;
        public int frameNr;
        
        // when doing recordablePose = pose, it is just a reference and won't work
        public RecordablePoseArgs(RecordablePose pose, int frame)
        {
            frameNr = frame;
            recordablePose = new RecordablePose();
            recordablePose.head.position = pose.head.position;
            recordablePose.head.rotation = pose.head.rotation;
            recordablePose.leftHand.position = pose.leftHand.position;
            recordablePose.leftHand.rotation = pose.leftHand.rotation;
            recordablePose.rightHand.position = pose.rightHand.position;
            recordablePose.rightHand.rotation = pose.rightHand.rotation;
        }
    }
    
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
            // Debug.Log("Recording data!");
            if (_frameInterval >= 1.0f / _recorder.fps)
            {
                _t = (1.0f/_recorder.fps - _previousPoseTime) / (_frameInterval - _previousPoseTime);
                
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
                    // NOTE: in the constructor of RecordablePoseArgs we create a new RecordablePose and copy the data, otherwise it's just a reference
                    OnUpdateRecordablePose?.Invoke(this, new RecordablePoseArgs(_recordablePose, _currentFrameNr));
                }
                else
                {
                    _recorder.recording.AddDataFrame(_guid, _currentFrameNr, _recordablePose);
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
        if (_isRecording) return;

        if (!_isTakingOver)
        {
            var currentFloatFrame = _recorder.GetCurrentFrameFloat();
            
            _frameInterval = currentFloatFrame - Mathf.FloorToInt(currentFloatFrame);
            
            if (currentFloatFrame == 0)
            {
                Debug.Log("Start recording from frame 0");
                _currentFrameNr = 0;
            }
            else
            {
                // we want to record the next frame, and not the one we are currently at according to the replayer
                if (Mathf.CeilToInt(currentFloatFrame) == (int)currentFloatFrame)
                {
                    _currentFrameNr = (int)currentFloatFrame + 1;
                }
                else
                {
                    _currentFrameNr = (int)currentFloatFrame;
                }
                Debug.Log("Start recording from frame " + _currentFrameNr);
            }
        
            // if this is the player it will only get a guid assigned when it starts a new recording
            _guid = _recorder.recording.CreateNewRecordableData(Guid.Empty);
            // initialize the undo stack with a base state (if already initialized this does nothing)
            _recorder.InitUndoStack(UndoManager.UndoType.New, _guid, null);
            
            // so we already have the prefab name saved, even if frame number is not correct yet
            _recorder.recording.UpdateMetaData(_guid, _currentFrameNr, _recorder.fps, prefabName);

            // if we start a new recording and the current frame is > 0 we need to insert dummy frames
            if (_currentFrameNr > 0)
            {
                // we fill up the RecordableData up to the currentFrame - 1
                _recorder.recording.TryAddEmptyDataFrames(_guid, _currentFrameNr-1);
            }
        }
        else
        {
            // if this is an existing replay who we want to take over then we already have a guid from the replayer
            // so we don't need a new one (unlike above)
            Debug.Log("Takeover!");
            
            // if we are doing a takeover, we start from frame 0. we know where to insert this data later so frame number is not important
            // TODO: I hope this makes sense because I didn't have it like that before
            _currentFrameNr = 0;
        }
        _isRecording = true;
    }

    // when we are taking over, we don't add this metadata because this is not a new entry
    // the data from the takeover is stored in AvatarTakeover and is not saved directly in the recording
    // this recordable's _guid is not valid during takeover 
    // !!! it is possible that this is not even called !!!
    // because the AvatarTakeover switches the prefab back to the original player prefab
    private void OnRecordingStop(object o, Recording.Flags flags)
    {
        if (!_isRecording) return;
        _isRecording = false;

        if (!_isTakingOver)
        {
            Debug.Log(_recorder.recording.recordableDataDict[_guid].dataFrames.Count);

            _recorder.recording.UpdateMetaData(_guid, _currentFrameNr, _recorder.fps, prefabName);
            
            _recorder.AddUndoState(UndoManager.UndoType.New, _guid, _recorder.recording.recordableDataDict[_guid]);
        }
        else
        {
            // set this back here instead of in AvatarTakeover otherwise it might be set too soon and the above code gets executed
            _isTakingOver = false;
        }
    }
    private void OnDestroy()
    {
        _recorder.onRecordingStart -= OnRecordingStart;
        _recorder.onRecordingStop -= OnRecordingStop;
    }
}
