using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ubiq.Avatars;
using Ubiq.Geometry;
using UnityEngine;

// attach this at the top of the hierarchy of an avatar prefab
public class Recordable : MonoBehaviour
{
    [System.Serializable]
    public struct RecordableData
    {
        public List<string> metaDataLabels;
        public List<string> metaData; // frames, fps, avatar representation, texture information

        /*
         * example labels:
         * "xPosHead", "yPosHead", "zPosHead", ... "wRotHead",
         * "blendShapeUpperLip", ..., "leftEyeBlink", ... "leftButtonPress", etc...
         */
        public List<string> dataLabels;
        // positional and rotational data, button presses, blend shape values, etc...
        public List<RecordableDataFrame> recordableData;
        
    }
    
    [System.Serializable]
    public struct RecordableDataFrame
    {
        // frame number
        public int frameNr;
        // can be positional vector data or quaternions (x,y,z,w),
        // or single float values for blend shapes or button presses
        public float[] data;
    }
    
    private Recorder _recorder;

    private bool _isRecording;
    private int _currentFrameNr;
    
    private ThreePointTrackedAvatar _trackedAvatar;

    private List<RecordableDataFrame> _dataFrames = new List<RecordableDataFrame>();
    private RecordableDataFrame _dataFrame;
    
    private List<string> _dataLabels;
    
    private readonly int _numTrackingPoints = 3;
    private int _numBlendshapes;
    private int _sizeData; // size of data array, depending on tracking points and whatever else we want to record

    private float _frameInterval; // time that has passed since last 10 fps frame
    private float _previousPoseTime; // time of the last pose update (this is usually slightly higher than 1/fps)
    private float _t; // interpolation factor
    private bool _interpolate;
    
    // position rotation structs for tracking points
    private PositionRotation _prevHead;
    private PositionRotation _prevLeftHand;
    private PositionRotation _prevRightHand;
    private bool _dataFrameReady = false;
    
    // meta data is data that describes what is being recorded and how (e.g. what prefab, frames, fps, etc...)
    private readonly List<string> _metaDataLabels = new List<string>()
    {
        "frames", "fps", "prefab", "trackingPoints"
    };
    // Start is called before the first frame update
    void Start()
    {
        _dataLabels = Enum.GetNames(typeof(DataLabels)).ToList();
        
        _recorder = GameObject.FindWithTag("Recorder").GetComponent<Recorder>();
        
        // add this recordable to the recorder
        _recorder.AddRecordable(this);
        _recorder.onRecordingStart.AddListener(OnRecordingStart);
        _recorder.onRecordingStop.AddListener(OnRecordingStop);
        
        _trackedAvatar = GetComponent<ThreePointTrackedAvatar>();

        _trackedAvatar.OnHeadUpdate.AddListener(TrackedAvatarOnHeadUpdate);
        _trackedAvatar.OnLeftHandUpdate.AddListener(TrackedAvatarOnLeftHandUpdate);
        _trackedAvatar.OnRightHandUpdate.AddListener(TrackedAvatarOnRightHandUpdate);

        _sizeData = (_numTrackingPoints * 3 + _numTrackingPoints * 4) + _numBlendshapes; 
    }
    
    // Update is called once per frame
    void Update()
    {
        if (!_isRecording) return;
        
        if (_dataFrameReady)
        {
            _dataFrame.frameNr = _currentFrameNr++;
            _dataFrames.Add(_dataFrame);
            _dataFrameReady = false;
        }
        
        // only record at 10 fps for now and interpolate data when replaying
        _frameInterval += Time.deltaTime;
        _interpolate = false;
        if (_frameInterval >= 1.0f / _recorder.fps)
        { 
            _t = (1.0f/_recorder.fps - _previousPoseTime) / (_frameInterval - _previousPoseTime);
            _dataFrame = new RecordableDataFrame()
            {
                data = new float[_sizeData]
            };
            _interpolate = true;
                
            _frameInterval = _previousPoseTime - 1.0f/_recorder.fps;
        }
        _previousPoseTime = _frameInterval;
    }

    private void OnRecordingStart()
    {
        _isRecording = true;
        _currentFrameNr = 0;
    }

    private void OnRecordingStop()
    {
        _isRecording = false;
        RecordableData recordableData = new RecordableData
        {
            metaDataLabels = _metaDataLabels,
            metaData = new List<string>()
            {
                _currentFrameNr.ToString(),
                _recorder.fps.ToString(),
                gameObject.name,
                _numTrackingPoints.ToString()
            },
            dataLabels = _dataLabels,
            recordableData = _dataFrames
        };
        StartCoroutine(_recorder.AddRecordableData(recordableData));
    }


    private void TrackedAvatarOnHeadUpdate(Vector3 pos, Quaternion rot)
    {
        if (!_isRecording) return;

        var rotN = rot.normalized;
        if (_interpolate)
        {
            var interpPos = Vector3.Lerp(_prevHead.position, pos, _t);
            var interpRot = Quaternion.Lerp(_prevHead.rotation, rotN, _t);
            
            _dataFrame.data[(int)DataLabels.xPosHead] = interpPos.x;
            _dataFrame.data[(int)DataLabels.yPosHead] = interpPos.y;
            _dataFrame.data[(int)DataLabels.zPosHead] = interpPos.z;

            _dataFrame.data[(int)DataLabels.xRotHead] = interpRot.x;
            _dataFrame.data[(int)DataLabels.yRotHead] = interpRot.y;
            _dataFrame.data[(int)DataLabels.zRotHead] = interpRot.z;
            _dataFrame.data[(int)DataLabels.wRotHead] = interpRot.w;
            
            _dataFrameReady = true;
        }
        
        _prevHead = new PositionRotation
        {
            position = pos,
            rotation = rotN
        };
    }
    
    private void TrackedAvatarOnLeftHandUpdate(Vector3 pos, Quaternion rot)
    {
        if (!_isRecording) return;
        
        var rotN = rot.normalized;
        if (_interpolate)
        {
            var interpPos = Vector3.Lerp(_prevLeftHand.position, pos, _t);
            var interpRot = Quaternion.Lerp(_prevLeftHand.rotation, rotN, _t);
            
            _dataFrame.data[(int)DataLabels.xPosLeftHand] = interpPos.x;
            _dataFrame.data[(int)DataLabels.yPosLeftHand] = interpPos.y;
            _dataFrame.data[(int)DataLabels.zPosLeftHand] = interpPos.z;

            _dataFrame.data[(int)DataLabels.xRotLeftHand] = interpRot.x;
            _dataFrame.data[(int)DataLabels.yRotLeftHand] = interpRot.y;
            _dataFrame.data[(int)DataLabels.zRotLeftHand] = interpRot.z;
            _dataFrame.data[(int)DataLabels.wRotLeftHand] = interpRot.w;
        }
        
        _prevLeftHand = new PositionRotation
        {
            position = pos,
            rotation = rotN
        };
    }
    private void TrackedAvatarOnRightHandUpdate(Vector3 pos, Quaternion rot)
    {
        if (!_isRecording) return;
        
        var rotN = rot.normalized;
        if (_interpolate)
        {
            var interpPos = Vector3.Lerp(_prevRightHand.position, pos, _t);
            var interpRot = Quaternion.Lerp(_prevRightHand.rotation, rotN, _t);
            
            _dataFrame.data[(int)DataLabels.xPosRightHand] = interpPos.x;
            _dataFrame.data[(int)DataLabels.yPosRightHand] = interpPos.y;
            _dataFrame.data[(int)DataLabels.zPosRightHand] = interpPos.z;
            
            _dataFrame.data[(int)DataLabels.xRotRightHand] = interpRot.x;
            _dataFrame.data[(int)DataLabels.yRotRightHand] = interpRot.y;
            _dataFrame.data[(int)DataLabels.zRotRightHand] = interpRot.z;
            _dataFrame.data[(int)DataLabels.wRotRightHand] = interpRot.w;
        }
        
        _prevRightHand = new PositionRotation
        {
            position = pos,
            rotation = rotN
        };
    }
}
