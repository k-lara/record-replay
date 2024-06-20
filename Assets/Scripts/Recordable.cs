using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using UnityEngine;
using UnityEngine.Events;
using Avatar = Ubiq.Avatars.Avatar;

// attach this at the top of the hierarchy of an avatar prefab
public class Recordable : MonoBehaviour
{
    [System.Serializable]
    public class RecordableData
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
    public class RecordableDataFrame
    {
        // frame number
        public int frameNr;
        // can be positional vector data or quaternions (x,y,z,w),
        // or single float values for blend shapes or button presses
        public float[] data;
    }
    
    public string prefabName;
    
    private Recorder _recorder;

    private bool _isRecording;
    private int _currentFrameNr;
    
    private ThreePointTrackedAvatar _trackedAvatar;
    private Avatar _avatar;

    private List<RecordableDataFrame> _dataFrames = new(); // make sure to clear this before next recording starts!
    private RecordableDataFrame _dataFrame;
    private bool _dataFrameReady; // make sure to set this to false at the end of the recording (just in case)
    
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
    
    // meta data is data that describes what is being recorded and how (e.g. what prefab, frames, fps, etc...)
    private readonly List<string> _metaDataLabels = new List<string>()
    {
        "id", "frames", "fps", "prefab", "trackingPoints"
    };
    // Start is called before the first frame update
    void Start()
    {
        _dataLabels = Enum.GetNames(typeof(DataLabels)).ToList();
        
        _recorder = GameObject.FindWithTag("Recorder").GetComponent<Recorder>();
        
        _recorder.onRecordingStart += OnRecordingStart;
        _recorder.onRecordingStop += OnRecordingStop;
        _recorder.onAddThumbnailData += AddThumbnailData;
        
        _trackedAvatar = GetComponent<ThreePointTrackedAvatar>();
        _avatar = GetComponent<Avatar>();
        
        _trackedAvatar.OnHeadUpdate.AddListener(TrackedAvatarOnHeadUpdate);
        _trackedAvatar.OnLeftHandUpdate.AddListener(TrackedAvatarOnLeftHandUpdate);
        _trackedAvatar.OnRightHandUpdate.AddListener(TrackedAvatarOnRightHandUpdate);

        _sizeData = (_numTrackingPoints * 3 + _numTrackingPoints * 4) + _numBlendshapes; 
    }

    private void OnDestroy()
    {
        // unsubscribe from events
        _recorder.onRecordingStart -= OnRecordingStart;
        _recorder.onRecordingStop -= OnRecordingStop;
        _recorder.onAddThumbnailData -= AddThumbnailData;
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
        // frameInterval has to be set to 0 at the start of each recording otherwise there are bad desync problems (forgot to do that earlier :()
        // Could it be that when the framerate is really low, it might make such big jumps that we miss a frame?
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

    private void OnRecordingStart(object o, string pathToRecording)
    {
        _isRecording = true;
        _currentFrameNr = 0;
        _frameInterval = 0;
    }
    
    // this is invoked when the recording is stopped just before the onRecordingStop event
    private void AddThumbnailData(object o, EventArgs e)
    {
        _recorder.AddToThumbnail(_avatar.NetworkId, prefabName, _currentFrameNr, _dataFrames[0]);
    }
    
    // when recorder stops recording we assemble our recorded data and meta data
    // then we start a coroutine to write the data to a file
    // each object gets a separate file for its recording in a folder named after the recording date and time
    private void OnRecordingStop(object o, string pathToRecording)
    {
        _isRecording = false;
        RecordableData recordableData = new RecordableData
        {
            metaDataLabels = _metaDataLabels,
            metaData = new List<string>()
            {
                _avatar.NetworkId.ToString(),
                _currentFrameNr.ToString(),
                _recorder.fps.ToString(),
                prefabName,
                _numTrackingPoints.ToString()
            },
            dataLabels = _dataLabels,
            recordableData = _dataFrames
        };
        StartCoroutine(SaveRecordedData(pathToRecording, recordableData));
    }
    
    // save the recorded data to a file
    // once we are done, we tell the recorder and ask it
    // to forward the object to be removed to the replayer
    private IEnumerator SaveRecordedData(string path, RecordableData recordableData)
    {
        using (var streamWriter = new StreamWriter(Path.Join(path, "motion_" + _avatar.NetworkId.ToString() + ".txt")))
        {
            Task task = streamWriter.WriteLineAsync(JsonUtility.ToJson(recordableData, true));
            yield return new WaitUntil(() => task.IsCompleted);
            Debug.Log("Saved recordable data from:" + recordableData.metaData[0]);
        }

        _dataFrameReady = false;
        _dataFrames.Clear(); // make sure to clear the list before starting a new recording
        
        // removing the replayed objects also calls the load replay eventually where we need access
        // to the file stream again, so we need to make sure that it is closed before we do that
        // this will do nothing for the local or remote players!
        // we need to make sure that the replay is not loaded as long as the local/remote players are
        // still saving the data
        _recorder.RemoveReplayedObject(this.gameObject);
        yield return null;
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
        
        // Debug.Log(_avatar.NetworkId.ToString() + " " + pos.x);
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
