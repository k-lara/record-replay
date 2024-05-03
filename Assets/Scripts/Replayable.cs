using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using Ubiq.Spawning;
using UnityEngine;

public class Replayable : MonoBehaviour
{
    private Replayer _replayer;
    private Recordable.RecordableData _replayableData;
    private bool _isReplaying;
    
    private ThreePointTrackedAvatar _trackedAvatar;
    private int _trackingPoints;
    private int _fps;
    private int _frames;
    private float _replayStartTime;
    
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
    
    // Start is called before the first frame update
    void Start()
    {
        _replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();
        _replayer.onReplayStart.AddListener(OnReplayStart);
        _replayer.onReplayStop.AddListener(OnReplayStop);
        
        _trackedAvatar = GetComponent<ThreePointTrackedAvatar>();
        
    }
    
    private void OnReplayStart()
    {
        _isReplaying = true;
        _replayStartTime = Time.time;
    }
    
    private void OnReplayStop()
    {
        _isReplaying = false;
    }

    public void SetReplayableData(Recordable.RecordableData data)
    {
        _replayableData = data;
        
        _trackingPoints = int.Parse(_replayableData.metaData[_replayableData.metaDataLabels.IndexOf("trackingPoints")]);
        _fps = int.Parse(_replayableData.metaData[_replayableData.metaDataLabels.IndexOf("fps")]);
        _frames = int.Parse(_replayableData.metaData[_replayableData.metaDataLabels.IndexOf("frames")]);
    }

    private ReferenceCountedSceneGraphMessage CreateRCSGMessage(State[] state)
    {
        var transformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<State>(state));
        var message = ReferenceCountedSceneGraphMessage.Rent(transformBytes.Length);
        transformBytes.CopyTo(new Span<byte>(message.bytes, message.start, message.length));
        return message;
    }
    
    // Update is called once per frame
    void Update()
    {
        if (!_isReplaying) return;
        
        // we have _frames in total which were recorded at _fps
        // during replay we interpolate between the frames to get smooth movements
        // because we know the fps we always know where we are between two consecutive frames and can interpolate accordingly
        var t = (Time.time - _replayStartTime) * _fps;
        var frameNr = Mathf.FloorToInt(t);
        t -= frameNr;

        if (frameNr < _frames - 1)
        {
            // Interpolate the pose between two frames in the recordable data and send it to the tracked avatar
            var state = InterpolatePose(_replayableData.recordableData[frameNr], _replayableData.recordableData[frameNr + 1], t);
            _trackedAvatar.ProcessMessage(CreateRCSGMessage(state));
        }
        else
        {
            // end of replay reached, start again
            _replayStartTime = Time.time;
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
    
    private void OnDestroy()
    {
        _replayer.onReplayStart.RemoveListener(OnReplayStart);
        _replayer.onReplayStop.RemoveListener(OnReplayStop);
    }
}
