using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Ubiq;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using Unity.VisualScripting;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

public class Replayable : MonoBehaviour, IHeadAndHandsInput, IHandSkeletonInput
{
    public int priority { get; set; }
    public bool active => isActiveAndEnabled;
    public InputVar<Pose> head => new(_replayablePose.head);
    public InputVar<Pose> leftHand => new(_replayablePose.leftHand);
    public InputVar<Pose> rightHand => new(_replayablePose.rightHand);
    public InputVar<float> leftGrip => InputVar<float>.invalid;
    public InputVar<float> rightGrip => InputVar<float>.invalid;

    public HandSkeleton leftHandSkeleton => new(HandSkeleton.Handedness.Left, new ReadOnlyCollection<InputVar<Pose>>(_replayablePose.leftHandSkeleton));
    public HandSkeleton rightHandSkeleton => new(HandSkeleton.Handedness.Right, new ReadOnlyCollection<InputVar<Pose>>(_replayablePose.rightHandSkeleton));
    
    private Replayer _replayer;
    // this is the id of the avatar that was recorded and links to the data recorded by that avatar
    // by changing this id, we can change what data is replayed by this replayable
    // the replayable data is stored for all replayables in the Replayer
    public Guid replayableId { get; set; }
    // public bool takenOver { get; set; }
    public bool isLocal { get; private set; }
    private bool _isPlaying;
    
    // same as in ThreePointTrackedAvatar
    private NetworkContext _context;
    private Transform _networkSceneRoot;
    private float _lastTransmitTime;
    
    private AudioReplayable _audioReplayable;
    
    private Avatar _avatar; // remove this only need it for debugging
    private int _trackingPoints;
    private int _fps;
    
    public ReplayablePose _replayablePose { get; private set; }
    private int previousFrame = -1; // to allow setting the frame to 0 when the avatar is created

    public class ReplayablePose
    {
        public Pose head;
        public Pose leftHand;
        public Pose rightHand;
        public InputVar<Pose>[] leftHandSkeleton = new InputVar<Pose>[(int)HandSkeleton.Joint.Count];
        public InputVar<Pose>[] rightHandSkeleton = new InputVar<Pose>[(int)HandSkeleton.Joint.Count];
    }

    public event EventHandler<ReplayablePose> OnUpdateReplayablePose;

    private AvatarInput _avatarInput = new();

    public void SetIsLocal(bool isLocal)
    {
        this.isLocal = isLocal;
        _avatar.IsLocal = isLocal;
    }

    void Awake()
    {
        _replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();
        _replayer.onReplayStart += OnReplayStart;
        _replayer.onReplayStop += OnReplayStop;
        _replayer.onFrameUpdate += OnFrameUpdate;
        
        _avatar = GetComponent<Avatar>();

        _replayablePose = new ReplayablePose();
        
        _avatarInput.Add(this as IHeadAndHandsInput);
        _avatarInput.Add(this as IHandSkeletonInput);
        _avatar.SetInput(_avatarInput);
        
    }

    private void OnDestroy()
    {
        _replayer.onReplayStart -= OnReplayStart;
        _replayer.onReplayStop -= OnReplayStop;
        _replayer.onFrameUpdate -= OnFrameUpdate;
    }
    
    private void OnReplayStart(object o, EventArgs e)
    {
        Debug.Log("Replayable OnReplayStart(): id: " + replayableId);
        
        _isPlaying = true;
    }

    private void OnReplayStop(object o, EventArgs e)
    {
        _isPlaying = false;
    }

    private void UpdateReplayablePose()
    {
        // no need to update if frame is too large, we just stay on the previous frame
        if (_replayer.currentFrame >= _replayer.recording.recordableDataDict[replayableId].dataFrames.Count - 1) return;
        
        var f0 = _replayer.recording.recordableDataDict[replayableId].dataFrames[(int)_replayer.currentFrame];
        // when we start a recording in the middle of a replay, we add empty data frames until the current frame
        // these frames don't hold valid data, we can therefore not update the replayable pose!
        if (!f0.valid) return; 
        
        // Debug.Log("Update replayable pose: current frame: " + _replayer.currentFrame + " replayable id: " + replayableId);
        
        var f1 = _replayer.recording.recordableDataDict[replayableId].dataFrames[(int)_replayer.currentFrame + 1];
        
        var t = _replayer.currentFrame - (int)_replayer.currentFrame;
        // Debug.Log(_replayer.currentFrame + " t: " + t);
        
        var pos = Vector3.Lerp(new Vector3(f0.xPosHead, f0.yPosHead, f0.zPosHead), new Vector3(f1.xPosHead, f1.yPosHead, f1.zPosHead), t);
        var rot = Quaternion.Lerp(new Quaternion(f0.xRotHead, f0.yRotHead, f0.zRotHead, f0.wRotHead), new Quaternion(f1.xRotHead, f1.yRotHead, f1.zRotHead, f1.wRotHead), t);
        _replayablePose.head = new Pose(pos, rot);
        
        pos = Vector3.Lerp(new Vector3(f0.xPosLeftHand, f0.yPosLeftHand, f0.zPosLeftHand), new Vector3(f1.xPosLeftHand, f1.yPosLeftHand, f1.zPosLeftHand), t);
        rot = Quaternion.Lerp(new Quaternion(f0.xRotLeftHand, f0.yRotLeftHand, f0.zRotLeftHand, f0.wRotLeftHand), new Quaternion(f1.xRotLeftHand, f1.yRotLeftHand, f1.zRotLeftHand, f1.wRotLeftHand), t);
        _replayablePose.leftHand = new Pose(pos, rot);
        
        pos = Vector3.Lerp(new Vector3(f0.xPosRightHand, f0.yPosRightHand, f0.zPosRightHand), new Vector3(f1.xPosRightHand, f1.yPosRightHand, f1.zPosRightHand), t);
        rot = Quaternion.Lerp(new Quaternion(f0.xRotRightHand, f0.yRotRightHand, f0.zRotRightHand, f0.wRotRightHand), new Quaternion(f1.xRotRightHand, f1.yRotRightHand, f1.zRotRightHand, f1.wRotRightHand), t);
        _replayablePose.rightHand = new Pose(pos, rot);
        
        // hand tracking 
        if (f0.handDataValid)
        {
            // wrists
            _replayablePose.leftHandSkeleton[0] = new InputVar<Pose>(new Pose(Vector3.Lerp(f0.leftWrist.position, f1.leftWrist.position, t), 
                Quaternion.Lerp(f0.leftWrist.rotation, f1.leftWrist.rotation, t))); // valid true by default for this constructor
            _replayablePose.rightHandSkeleton[0] = new InputVar<Pose>(new Pose(Vector3.Lerp(f0.rightWrist.position, f1.rightWrist.position, t),
                Quaternion.Lerp(f0.rightWrist.rotation, f1.rightWrist.rotation, t)));
            for (var i = 1; i < f0.leftFingerRotations.Length; i++)
            {
                _replayablePose.leftHandSkeleton[i] = new InputVar<Pose>(new Pose(Vector3.zero, Quaternion.Lerp(f0.leftFingerRotations[i], f1.leftFingerRotations[i], t)));
                _replayablePose.rightHandSkeleton[i] = new InputVar<Pose>(new Pose(Vector3.zero, Quaternion.Lerp(f0.rightFingerRotations[i], f1.rightFingerRotations[i], t)));
            }
        }
        
        // AvatarTakeover only subscribes to the one event from the avatar it is taking over
        OnUpdateReplayablePose?.Invoke(this, _replayablePose);
    }

    public void SetReplayablePose(int frame)
    {
        if (frame == previousFrame) return;
        // this could have been set manually, so we want to show the closest frame possible
        if (!_replayer.recording.recordableDataDict[replayableId].dataFrames[frame].valid)
        {
            Debug.Log("SetReplayablePose: frame not valid, find next possible frame");
            for (var i = frame + 1; i < _replayer.recording.recordableDataDict[replayableId].dataFrames.Count; i++)
            {
                if (_replayer.recording.recordableDataDict[replayableId].dataFrames[i].valid)
                {
                    frame = i;
                    break;
                }
            }
        }
        
        if (frame > _replayer.recording.recordableDataDict[replayableId].dataFrames.Count - 1)
        {
            frame = _replayer.recording.recordableDataDict[replayableId].dataFrames.Count - 1;
        }
        if (frame < 0)
        {
            frame = 0;
        }
        Debug.Log("SetReplayablePose from frame: " + frame + "of " + _replayer.recording.recordableDataDict[replayableId].numFrames);
        var f = _replayer.recording.recordableDataDict[replayableId].dataFrames[frame];
        _replayablePose.head = new Pose(new Vector3(f.xPosHead, f.yPosHead, f.zPosHead), new Quaternion(f.xRotHead, f.yRotHead, f.zRotHead, f.wRotHead));
        _replayablePose.leftHand = new Pose(new Vector3(f.xPosLeftHand, f.yPosLeftHand, f.zPosLeftHand), new Quaternion(f.xRotLeftHand, f.yRotLeftHand, f.zRotLeftHand, f.wRotLeftHand));
        _replayablePose.rightHand = new Pose(new Vector3(f.xPosRightHand, f.yPosRightHand, f.zPosRightHand), new Quaternion(f.xRotRightHand, f.yRotRightHand, f.zRotRightHand, f.wRotRightHand));
        
        // set hand tracking data
        if (f.handDataValid)
        {
            // wrists
            _replayablePose.leftHandSkeleton[0] = new InputVar<Pose>(new Pose(f.leftWrist.position, f.leftWrist.rotation)); // valid true by default for this constructor 
            _replayablePose.rightHandSkeleton[0] = new InputVar<Pose>(new Pose(f.rightWrist.position, f.rightWrist.rotation));
            for (var i = 1; i < f.leftFingerRotations.Length; i++)
            {
                _replayablePose.leftHandSkeleton[i] = new InputVar<Pose>(new Pose(Vector3.zero, f.leftFingerRotations[i]));
                _replayablePose.rightHandSkeleton[i] = new InputVar<Pose>(new Pose(Vector3.zero, f.rightFingerRotations[i]));
            }
        }
        previousFrame = frame;
    }
    
    // here we don't have a recording with data loaded, so we have to set the pose from the frame we have in the thumbnail
    public void SetReplayablePose(ReplayablePose pose)
    {
        Debug.Log("SetReplayablePose from ReplayablePose: head pos x" + pose.head.position.x);
        _replayablePose = pose;
    }

    private void OnFrameUpdate(object o, bool interpolate)
    {
        if (!isLocal) return;

        if (interpolate)
        {
            // if we change the replayableId, we can make a Replayable replay any data we want (as long as the data structure is the same)
            UpdateReplayablePose();
        }
        else
        {
            SetReplayablePose((int)_replayer.currentFrame);
        }
    }
}
