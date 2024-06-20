using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using Ubiq.Spawning;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

/// <summary>
/// a game object that has Replayable might have only been created for replaying
/// we don't need to know when we were created and when we get deleted, because the Replayer will handle this
/// we only need to know when to start and stop replaying the data we got sent from the Replayer
///
/// To avoid modifying ThreePointTrackedAvatar and any class that is responsible for animating the avatar
/// we network this object and send the replayed data to the remote Replayable instead.
/// The remote Replayable then sends the data to its corresponding ThreePointTrackedAvatar
/// This means the Replayable needs to know if it is the local one.
/// </summary>
public class Replayable : MonoBehaviour
{
    private Replayer _replayer;
    private Recordable.RecordableData _replayableData;
    private bool _isPlaying; // true if a replay is playing
    public bool isLocal; // true if this is the local replayable, this gets set by the Replayer when the replay is created
    
    // same as in ThreePointTrackedAvatar
    private NetworkContext _context;
    private Transform _networkSceneRoot;
    private float _lastTransmitTime;
    
    private AudioReplayable _audioReplayable;
    
    private ThreePointTrackedAvatar _trackedAvatar;
    private Avatar _avatar; // remove this only need it for debugging
    private int _trackingPoints;
    private int _fps;
    private int _frames;
    private State[] _state; // current pose of the tracked avatar
    private float _deltaTime; // time that has progressed since the replay started

    // taken from ThreePointTrackedAvatar to create the same reference counted scene graph message
    [Serializable]
    private struct State
    {
        public PositionRotation head;
        public PositionRotation leftHand;
        public PositionRotation rightHand;
        public float leftGrip;
        public float rightGrip;
    }

    private void Awake()
    {
        _replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();
        _replayer.onReplayStart += OnReplayStart;
        _replayer.onReplayStop += OnReplayStop;
        
        _trackedAvatar = GetComponent<ThreePointTrackedAvatar>();
        _avatar = GetComponent<Avatar>();
    }

    private void Start()
    {
        _context = NetworkScene.Register(this, NetworkId.Create(_avatar.NetworkId, "Replayable"));
        _networkSceneRoot = _context.Scene.transform;
        _lastTransmitTime = Time.time;
    }
    
    // in theory, whenever someone presses start again, the replay starts from the beginning
    // only when it is on pause and the user presses start again, it should continue from where it was paused
    private void OnReplayStart(object o, EventArgs e)
    {
        if (!_audioReplayable)
        {
            _audioReplayable = GetComponent<AudioReplayable>();
        }
        // if we have stopped a replay pressing start will resume it
        // in case we are already playing pressing start will start the replay from the beginning
        if (_isPlaying)
            _deltaTime = 0.0f;
        
        _isPlaying = true;
    }
    // stop a replay without deleting the replay
    // this is only invoked from Replayer when the replay is already playing otherwise it is unnecessary
    private void OnReplayStop(object o, EventArgs e)
    {
        _isPlaying = false;
    }
    
    // we can also set replayable data from a thumbnail, namely the first pose only
    // thumbnail data does not have information about fps and number of frames
    public void SetReplayableData(Recordable.RecordableData data, bool fromThumbnail = false)
    {
        // if fromThumbnail is true, this only contains the first pose and nothing else
        // this will be overwritten by the full replay when it is loaded 
        _replayableData = data; 
        if (!fromThumbnail)
        {
            _trackingPoints = int.Parse(_replayableData.metaData[_replayableData.metaDataLabels.IndexOf("trackingPoints")]);
            _fps = int.Parse(_replayableData.metaData[_replayableData.metaDataLabels.IndexOf("fps")]);
            _frames = int.Parse(_replayableData.metaData[_replayableData.metaDataLabels.IndexOf("frames")]);
        }
        
        //put prefab in correct first pose for replay
        SetPoseFrame(0);
    }
    
    // set the pose of the tracked avatar to the pose of the frame
    // when loading a recording we set the pose of the recorded avatars to the first frame
    private void SetPoseFrame(int frame)
    {
        var firstDataFrame = _replayableData.recordableData[0];
        
        var pHead = new Vector3(firstDataFrame.data[0], firstDataFrame.data[1], firstDataFrame.data[2]);
        var pLeftHand = new Vector3(firstDataFrame.data[3], firstDataFrame.data[4], firstDataFrame.data[5]);
        var pRightHand = new Vector3(firstDataFrame.data[6], firstDataFrame.data[7], firstDataFrame.data[8]);
        
        var rHead = new Quaternion(firstDataFrame.data[9], firstDataFrame.data[10], firstDataFrame.data[11], firstDataFrame.data[12]);
        var rLeftHand = new Quaternion(firstDataFrame.data[13], firstDataFrame.data[14], firstDataFrame.data[15], firstDataFrame.data[16]);
        var rRightHand = new Quaternion(firstDataFrame.data[17], firstDataFrame.data[18], firstDataFrame.data[19], firstDataFrame.data[20]);

        _state = GetStateFrom(pHead, pLeftHand, pRightHand, rHead, rLeftHand, rRightHand);
        // we can't set the pose here because ThreePointTrackedAvatar has not initialised the networkSceneRoot yet
        // but we set it in update anyway so don't need to do anything here
        
    }
    
    private State[] GetStateFrom(Vector3 pHead, Vector3 pLeftHand, Vector3 pRightHand, Quaternion rHead, Quaternion rLeftHand, Quaternion rRightHand)
    {
        var state = new State[1];
        
        state[0].head = new PositionRotation
        {
            position = pHead,
            rotation = rHead
        };
        state[0].leftHand = new PositionRotation
        {
            position = pLeftHand,
            rotation = rLeftHand
        };
        state[0].rightHand = new PositionRotation
        {
            position = pRightHand,
            rotation = rRightHand
        };
        state[0].leftGrip = 0;
        state[0].rightGrip = 0;
        
        return state;
    }

    private ReferenceCountedSceneGraphMessage CreateRcsgMessage(State[] state)
    {
        var transformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<State>(state));
        var message = ReferenceCountedSceneGraphMessage.Rent(transformBytes.Length);
        transformBytes.CopyTo(new Span<byte>(message.bytes, message.start, message.length));
        return message;
    }
    
    // Update is called once per frame
    void Update()
    {
        if (!isLocal) return;
        if (_isPlaying)
        {
            _deltaTime += Time.deltaTime;
            // we have _frames in total which were recorded at _fps
            // during replay we interpolate between the frames to get smooth movements
            // because we know the fps we always know where we are between two consecutive frames and can interpolate accordingly
            var t = _deltaTime * _fps;
            var frameNr = Mathf.FloorToInt(t);
            t -= frameNr;
            // Debug.Log(_avatar.NetworkId + " " + frameNr + " " + t);
            if (frameNr < _frames - 1)
            {
                // Debug.Log(frameNr);
                // Debug.Log(_replayableData.recordableData.Count);
                // Interpolate the pose between two frames in the recordable data and send it to the tracked avatar
                _state = InterpolatePose(_replayableData.recordableData[frameNr],
                    _replayableData.recordableData[frameNr + 1], t);
                // Debug.Log(_avatar.NetworkId.ToString() + ": " + _state[0].head.position.x);
            }
            else
            {
                // end of replay reached, start again
                _deltaTime = 0.0f;
                if (_audioReplayable)
                {
                    _audioReplayable.ReplayAudioFromStart();
                }
                Debug.Log("Replay again from start!");
            }
        }
        
        if (_state != null)
        {
            // send the last state to the tracked avatar regardless of whether we are playing or not
            var message = CreateRcsgMessage(_state);
            _trackedAvatar.ProcessMessage(message);

            if (Time.time - _lastTransmitTime > (1.0f / _avatar.UpdateRate))
            {
                _lastTransmitTime = Time.time;
                _context.Send(message);
            }
        }
    }

    private State[] InterpolatePose(Recordable.RecordableDataFrame f1, Recordable.RecordableDataFrame f2, float t)
    {
        var state = new State[1];
        
        var pHead = Vector3.Lerp(new Vector3(f1.data[0], f1.data[1], f1.data[2]), new Vector3(f2.data[0], f2.data[1], f2.data[2]), t);
        var pLeftHand = Vector3.Lerp(new Vector3(f1.data[3], f1.data[4], f1.data[5]), new Vector3(f2.data[3], f2.data[4], f2.data[5]), t);
        var pRightHand = Vector3.Lerp(new Vector3(f1.data[6], f1.data[7], f1.data[8]), new Vector3(f2.data[6], f2.data[7], f2.data[8]), t);
       
        var rHead = Quaternion.Lerp(new Quaternion(f1.data[9], f1.data[10], f1.data[11], f1.data[12]), new Quaternion(f2.data[9], f2.data[10], f2.data[11], f2.data[12]), t);
        var rLeftHand = Quaternion.Lerp(new Quaternion(f1.data[13], f1.data[14], f1.data[15], f1.data[16]), new Quaternion(f2.data[13], f2.data[14], f2.data[15], f2.data[16]), t);
        var rRightHand = Quaternion.Lerp(new Quaternion(f1.data[17], f1.data[18], f1.data[19], f1.data[20]), new Quaternion(f2.data[17], f2.data[18], f2.data[19], f2.data[20]), t);
        
        return GetStateFrom(pHead, pLeftHand, pRightHand, rHead, rLeftHand, rRightHand);
    }
    
    // a remote replayable will receive the motion data from the Replayable here
    // and forward it to the tracked avatar
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        _trackedAvatar.ProcessMessage(message);
    }
    
    private void OnDestroy()
    {
        _replayer.onReplayStart -= OnReplayStart;
        _replayer.onReplayStop -= OnReplayStop; 
    }
}
