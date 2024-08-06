using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq;
using Ubiq.Avatars;
using Ubiq.Messaging;
using UnityEngine;


// > when existing avatar is taken over by the user, the existing data of that avatar is extended by the new movements of the user
// > this means we do not need to add a new entry to our thumbnail. we only need to update the existing entry with the new data!
// > a new thumbnail entry is only created when a new avatar is recorded by the user
// > this means the Recordable needs to know if it is recording a new avatar or extending the recording of an existing one!
// > (maybe disable Recordable for replayed avatars unless they are taken over by the user)
// > if the user takes over an avatar, their own Recordable must not record, instead the taken over avatar's Recordable must record given the input from user's AvatarHints

// select avatar we want to take over
public class AvatarTakeover : MonoBehaviour
{
    public float takeoverDuration = 2.0f; // duration of the takeover in seconds
    
    private Recorder recorder;
    private Replayer replayer;

    private NetworkScene networkScene;
    private AvatarManager avatarManager;
    private AvatarInput playerInput;
    
    private Recordable recordable;
    // private Replayable.ReplayablePose previousPose;
    // private float previousFrameFloat;
    
    // these variables change between takeovers or need to be reset
    private float t; // current interpolation factor
    public Replayable replayable; // the replayable to take over
    private Dictionary<int, Recordable.RecordablePose> currentTakeoverOverwrite = new();
    private int takeoverStart; // starting frame of takeover (from replayable 100%, player 0% to replayable 0%, player 100%)
    private int takeoverEnd; // ending frame of takeover (from replayable 0%, player 100% to replayable 100%, player 0%)
    
    // Start is called before the first frame update
    void Start()
    {
        recorder = GetComponent<Recorder>();
        recorder.onRecordingStop += OnRecordingStop;
        
        replayer = GetComponent<Replayer>();
        avatarManager = AvatarManager.Find(this);
        // TODO maybe: make avatar input and replay relative to networkSceneRoot, not necessarily necessary...
        networkScene = NetworkScene.Find(this); 
        
        playerInput = avatarManager.input;

        // previousPose = new Replayable.ReplayablePose();

        recordable = avatarManager.LocalAvatar.gameObject.GetComponent<Recordable>();
        recordable.OnUpdateRecordablePose += OnUpdateRecordablePose;
    }
    
    // TODO replayable selection
    public void SelectTakeoverReplayable(GameObject go)
    {
        replayable = go.GetComponent<Replayable>();
        // this has to be called once we know which replayable to take over
        replayable.OnUpdateReplayablePose += OnUpdateReplayablePose;
        
        // get last frame of this replayable's replay
        // replay is in 10 fps and we want maybe 2 seconds of takeover overwrite?
        // make sure there are enough frames to guarantee that takeover time otherwise take what is there
        var numFrames = replayer.recording.recordableDataDict[replayable.replayableId].numFrames;
        takeoverStart = 0;
        if (numFrames > recorder.fps * takeoverDuration)
        {
            takeoverStart = numFrames - (int)(recorder.fps * takeoverDuration);
        }

        replayer.frameOffset = takeoverStart;
        replayer.currentFrame = takeoverStart; // so the recorder also knows where to start
        recordable.IsTakingOver = true;
    }
    
    // TODO replayable visibility 
    // TODO per default takeover happens to extend an existing replayable
    // TODO initiate takeover, prepare replayable/recordable, compute frameOffset correctly from last frame of replayable
    // TODO later, we can specify ranges between which we want to change the replayable

    private void OnRecordingStop(object o, EventArgs e)
    {
        if (recordable.IsTakingOver)
        {
            recordable.IsTakingOver = false;
            
            // interpolate between the original replay and the currentTakeoverOverwrite
            // there should be recorder.fps * takeoverDuration frames - 1 between which we need to interpolate
            // the first one we don't get because the Recordable Update() due to the + deltaTime is already past frame 0
            // but this shouldn't matter because frame 0 is 100% replayable anyway
            StartCoroutine(OverwriteData());

        }
    }

    private IEnumerator OverwriteData()
    {
        var numFrames = replayer.recording.recordableDataDict[replayable.replayableId].numFrames;
        // don't need to interpolate first frame because it would be 100% replayable anyway
        // currentTakeoverOverwrite indexing starts at 0! and corresponds to takeoverStart + 1 in the replayable data
        Debug.Log("Overwrite data => Takeover start: " + takeoverStart);
        for (int i = takeoverStart + 1; i < numFrames; i++)
        {
            // compute t
            t = (float)(i - takeoverStart) / (int)(recorder.fps * takeoverDuration);
            
            // interpolate
            var dataFrame = replayer.recording.recordableDataDict[replayable.replayableId].dataFrames[i];
            
            var headPos = Vector3.Lerp(new Vector3(dataFrame.xPosHead, dataFrame.yPosHead, dataFrame.zPosHead), currentTakeoverOverwrite[i - (takeoverStart + 1)].head.position, t);
            var headRot = Quaternion.Lerp(new Quaternion(dataFrame.xRotHead, dataFrame.yRotHead, dataFrame.zRotHead, dataFrame.wRotHead), currentTakeoverOverwrite[i - (takeoverStart + 1)].head.rotation, t);
            
            var leftHandPos = Vector3.Lerp(new Vector3(dataFrame.xPosLeftHand, dataFrame.yPosLeftHand, dataFrame.zPosLeftHand), currentTakeoverOverwrite[i - (takeoverStart + 1)].leftHand.position, t);
            var leftHandRot = Quaternion.Lerp(new Quaternion(dataFrame.xRotLeftHand, dataFrame.yRotLeftHand, dataFrame.zRotLeftHand, dataFrame.wRotLeftHand), currentTakeoverOverwrite[i - (takeoverStart + 1)].leftHand.rotation, t);
            
            var rightHandPos = Vector3.Lerp(new Vector3(dataFrame.xPosRightHand, dataFrame.yPosRightHand, dataFrame.zPosRightHand), currentTakeoverOverwrite[i - (takeoverStart + 1)].rightHand.position, t);
            var rightHandRot = Quaternion.Lerp(new Quaternion(dataFrame.xRotRightHand, dataFrame.yRotRightHand, dataFrame.zRotRightHand, dataFrame.wRotRightHand), currentTakeoverOverwrite[i - (takeoverStart + 1)].rightHand.rotation, t);
            
            // update data
            replayer.recording.AddHeadTransform(replayable.replayableId, i, headPos, headRot);
            replayer.recording.AddLeftHandTransform(replayable.replayableId, i, leftHandPos, leftHandRot);
            replayer.recording.AddRightHandTransform(replayable.replayableId, i, rightHandPos, rightHandRot);
        }
        // add new recorded data to the replayable
        for (var i = (int)(recorder.fps * takeoverDuration); i < currentTakeoverOverwrite.Count; i++)
        {
            replayer.recording.TryAddEmptyDataFrame(replayable.replayableId, numFrames);
            replayer.recording.AddHeadTransform(replayable.replayableId, numFrames, currentTakeoverOverwrite[i].head.position, currentTakeoverOverwrite[i].head.rotation);
            replayer.recording.AddLeftHandTransform(replayable.replayableId, numFrames, currentTakeoverOverwrite[i].leftHand.position, currentTakeoverOverwrite[i].leftHand.rotation);
            replayer.recording.AddRightHandTransform(replayable.replayableId, numFrames, currentTakeoverOverwrite[i].rightHand.position, currentTakeoverOverwrite[i].rightHand.rotation);
            numFrames++;
        }

        yield return null;
    }
    
    private void OnUpdateRecordablePose(object o, Recordable.RecordablePose rp)
    {
        currentTakeoverOverwrite.Add(Mathf.FloorToInt(replayer.currentFrame), rp);
    }
    
    // interpolate between the replayable and the player pose to create a smooth takeover
    private void OnUpdateReplayablePose(object o, Replayable.ReplayablePose rp)
    {
        t = (replayer.currentFrame - takeoverStart) / (int)(recorder.fps * takeoverDuration);
        
        if (playerInput.TryGet(out IHeadAndHandsInput src))
        {
            rp.head.position = Vector3.Lerp(rp.head.position, src.head.value.position, t);
            rp.head.rotation = Quaternion.Lerp(rp.head.rotation, src.head.value.rotation, t);
            
            rp.leftHand.position = Vector3.Lerp(rp.leftHand.position, src.leftHand.value.position, t);
            rp.leftHand.rotation = Quaternion.Lerp(rp.leftHand.rotation, src.leftHand.value.rotation, t);
            
            rp.rightHand.position = Vector3.Lerp(rp.rightHand.position, src.rightHand.value.position, t);
            rp.rightHand.rotation = Quaternion.Lerp(rp.rightHand.rotation, src.rightHand.value.rotation, t);
        }
        // previousPose.head.position = rp.head.position;
        // previousPose.head.rotation = rp.head.rotation;
        // previousPose.leftHand.position = rp.leftHand.position;
        // previousPose.leftHand.rotation = rp.leftHand.rotation;
        // previousPose.rightHand.position = rp.rightHand.position;
        // previousPose.rightHand.rotation = rp.rightHand.rotation;
        // previousFrameFloat = replayer.currentFrame;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
