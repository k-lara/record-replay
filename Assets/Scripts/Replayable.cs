using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ubiq;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

public class Replayable : MonoBehaviour, IHeadAndHandsInput
{
    public int priority { get; set; }
    public bool active => isActiveAndEnabled;
    public InputVar<Pose> head => new(_replayablePose.head);
    public InputVar<Pose> leftHand => new(_replayablePose.leftHand);
    public InputVar<Pose> rightHand => new(_replayablePose.rightHand);
    public InputVar<float> leftGrip => InputVar<float>.invalid;
    public InputVar<float> rightGrip => InputVar<float>.invalid;
    
    private Replayer _replayer;
    // this is the id of the avatar that was recorded and links to the data recorded by that avatar
    // by changing this id, we can change what data is replayed by this replayable
    // the replayable data is stored for all replayables in the Replayer
    public Guid replayableId { get; set; }
    // public bool takenOver { get; set; }
    public bool isLocal { get; set; }
    private bool _isPlaying;
    
    // same as in ThreePointTrackedAvatar
    private NetworkContext _context;
    private Transform _networkSceneRoot;
    private float _lastTransmitTime;
    
    private AudioReplayable _audioReplayable;
    
    private HeadAndHandsAvatar _trackedAvatar;
    private Avatar _avatar; // remove this only need it for debugging
    private int _trackingPoints;
    private int _fps;
    private float _deltaTime; // time that has progressed since the replay started
    private int _frameNr; // current frame number
    private float _t; // time between two frames 4.6 means 60% of the way between frame 4 and 5
    private float _tFrac; // fraction of the way between two frames
    
    public ReplayablePose _replayablePose { get; private set; }

    public class ReplayablePose
    {
        public Pose head;
        public Pose leftHand;
        public Pose rightHand;
    }

    public event EventHandler<ReplayablePose> OnUpdateReplayablePose;

    private AvatarInput _avatarInput;

    void Awake()
    {
        _replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();
        _replayer.onReplayStart += OnReplayStart;
        _replayer.onReplayStop += OnReplayStop;
        
        _trackedAvatar = GetComponent<HeadAndHandsAvatar>();
        _avatar = GetComponent<Avatar>();
        
    }
    
    private void Start()
    {
        
        _avatarInput.Add(this);
        _avatar.SetInput(_avatarInput);

        _replayablePose = new ReplayablePose();

        // _context = NetworkScene.Register(this, NetworkId.Create(_avatar.NetworkId, "Replayable"));
        // _networkSceneRoot = _context.Scene.transform;
        // _lastTransmitTime = Time.time;
    }

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

    private void OnReplayStop(object o, EventArgs e)
    {
        _isPlaying = false;
    }

    private void UpdateReplayablePose()
    {
        var f0 = _replayer.recording.recordableDataDict[replayableId].dataFrames[_frameNr];
        var f1 = _replayer.recording.recordableDataDict[replayableId].dataFrames[_frameNr + 1];
        
        var pos = Vector3.Lerp(new Vector3(f0.xPosHead, f0.yPosHead, f0.zPosHead), new Vector3(f1.xPosHead, f1.yPosHead, f1.zPosHead), _tFrac);
        var rot = Quaternion.Lerp(new Quaternion(f0.xRotHead, f0.yRotHead, f0.zRotHead, f0.wRotHead), new Quaternion(f1.xRotHead, f1.yRotHead, f1.zRotHead, f1.wRotHead), _tFrac);
        _replayablePose.head = new Pose(pos, rot);
        
        pos = Vector3.Lerp(new Vector3(f0.xPosLeftHand, f0.yPosLeftHand, f0.zPosLeftHand), new Vector3(f1.xPosLeftHand, f1.yPosLeftHand, f1.zPosLeftHand), _tFrac);
        rot = Quaternion.Lerp(new Quaternion(f0.xRotLeftHand, f0.yRotLeftHand, f0.zRotLeftHand, f0.wRotLeftHand), new Quaternion(f1.xRotLeftHand, f1.yRotLeftHand, f1.zRotLeftHand, f1.wRotLeftHand), _tFrac);
        _replayablePose.leftHand = new Pose(pos, rot);
        
        pos = Vector3.Lerp(new Vector3(f0.xPosRightHand, f0.yPosRightHand, f0.zPosRightHand), new Vector3(f1.xPosRightHand, f1.yPosRightHand, f1.zPosRightHand), _tFrac);
        rot = Quaternion.Lerp(new Quaternion(f0.xRotRightHand, f0.yRotRightHand, f0.zRotRightHand, f0.wRotRightHand), new Quaternion(f1.xRotRightHand, f1.yRotRightHand, f1.zRotRightHand, f1.wRotRightHand), _tFrac);
        _replayablePose.rightHand = new Pose(pos, rot);
        
        // AvatarTakeover only subscribes to the one event from the avatar it is taking over
        OnUpdateReplayablePose?.Invoke(this, _replayablePose);
    }

    // Update is called once per frame
    void Update()
    {
        if (!isLocal) return;
        if (_isPlaying)
        {
            _deltaTime += Time.deltaTime;
            // during replay we interpolate between the frames to get smooth movements
            // because we know the fps we always know where we are between two consecutive frames and can interpolate accordingly
            _t = _deltaTime * _replayer.recording.recordableDataDict[replayableId].fps + _replayer.frameOffset;
            _frameNr = Mathf.FloorToInt(_t);
            _tFrac = _t - _frameNr;
            
            if (_frameNr < _replayer.recording.recordableDataDict[replayableId].numFrames - 1)
            {
                // if we change the replayableId, we can make a Replayable replay any data we want (as long as the data structure is the same)
                UpdateReplayablePose();
            }
            
            // TODO what to do when we are done?
            // other replayables might still be going because they have more frames
            // we can just do nothing and wait on the last frame for other replayables to finish
        }
    }
}
