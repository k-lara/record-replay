using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Avatar = Ubiq.Avatars.Avatar;


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

    public TakeoverSettings takeoverSetting = TakeoverSettings.Start;
    
    public enum TakeoverSettings
    {
        Start, // takeover just overwrites the current avatar's data starting at the beginning (this should be default for now)
        End // takeover starts at the end of the current avatar's data
    }
    public bool isTakingOver { get; private set; }
    public GameObject takeoverPrefab;
    
    private Recorder recorder;
    private Replayer replayer;
    private XROrigin xrOrigin; // for positioning the player where the replayable is

    private NetworkScene networkScene;
    private AvatarManager avatarManager;
    private AvatarInput playerInput;
    private TakeoverSelector takeoverSelector;

    private GameObject playerPrefab; // the players prefab before and after takeover
    private Recordable recordable;
    // private Replayable.ReplayablePose previousPose;
    // private float previousFrameFloat;
    
    // these variables change between takeovers or need to be reset
    private float t; // current interpolation factor
    private Replayable replayable; // the replayable to take over
    private Dictionary<int, Recordable.RecordablePose> currentTakeoverOverwrite = new();
    private int takeoverStart; // starting frame of takeover (from replayable 100%, player 0% to replayable 0%, player 100%)
    private int takeoverEnd; // ending frame of takeover (from replayable 0%, player 100% to replayable 100%, player 0%)
    
    // Start is called before the first frame update
    void Start()
    {
        recorder = GetComponent<Recorder>();
        recorder.onRecordingStop += OnRecordingStop;
        
        takeoverSelector = GetComponent<TakeoverSelector>();
        takeoverSelector.onTakeoverSelected += OnTakeoverSelected;
        
        replayer = GetComponent<Replayer>();
        avatarManager = AvatarManager.Find(this);
        avatarManager.OnAvatarCreated.AddListener(OnAvatarCreated);
        avatarManager.OnAvatarDestroyed.AddListener(OnAvatarDestroyed);
        
        // TODO maybe: make avatar input and replay relative to networkSceneRoot, not necessarily necessary...
        networkScene = NetworkScene.Find(this); 
        
        playerInput = avatarManager.input;
        xrOrigin = FindObjectOfType<XROrigin>();
        
    }
    
    private void OnAvatarCreated(Avatar avatar)
    {
        if (isTakingOver)
        {
            // we add a recordable to the new player avatar which we might have changed for takeover
            recordable = avatarManager.LocalAvatar.gameObject.AddComponent<Recordable>();
            recordable.OnUpdateRecordablePose += OnUpdateRecordablePose;
            recordable.IsTakingOver = isTakingOver;
        }
        else
        {
            // add recordable to our initial avatar prefab from before (materials are opaque by default)
            var recordable = avatarManager.LocalAvatar.gameObject.AddComponent<Recordable>();
            recordable.prefabName = avatarManager.avatarPrefab.name;
        }
    }

    private void OnAvatarDestroyed(Avatar avatar)
    {
        recordable = null;
    }
    
    private void SetTakeoverStart()
    {
        switch (takeoverSetting)
        {
            case (TakeoverSettings.Start):

                takeoverStart = 0;
                replayer.SetCurrentFrameForTakeover(takeoverStart);
                
                break;

            case TakeoverSettings.End:
                // get last frame of this replayable's replay
                // replay is in 10 fps and we want maybe 2 seconds of takeover overwrite?
                // make sure there are enough frames to guarantee that takeover time otherwise take what is there
                var numFrames = replayer.recording.recordableDataDict[replayable.replayableId].numFrames;
                takeoverStart = 0;
                if (numFrames > recorder.fps * takeoverDuration)
                {
                    takeoverStart = numFrames - (int)(recorder.fps * takeoverDuration);
                }
                replayer.SetCurrentFrameForTakeover(takeoverStart);
                break;
            
            default:
                Debug.Log("No valid setting");
                break;
        }
        
        Debug.Log("Set takeover start to frame: " + takeoverStart);
    }
    
    // select a replayable in the scene for takeover
    // we make a default takeover prefab that is spawned while taking over an avatar, this helps to distinguish
    // between our avatar and the one we take over
    // TODO needs to change: per default the takeover start is a few seconds from the end of the selected replayable's data
    // TODO if the user has set their own range, the takeover start is set to a few seconds before that range
    private void OnTakeoverSelected(object o, GameObject go)
    {
        Debug.Log("Takeover selected: " + go.name);
        isTakingOver = true;
        replayable = go.GetComponent<Replayable>();
        // this has to be called once we know which replayable to take over
        replayable.OnUpdateReplayablePose += OnUpdateReplayablePose;
        
        // make replayable invisible
        if (replayable.gameObject.TryGetComponent<UbiqMetaAvatarEntity>(out var metaAvatar))
        {
            Debug.Log("SelectTakeoverReplayable: Hide replayable avatar!");
            metaAvatar.SetView(Oculus.Avatar2.CAPI.ovrAvatar2EntityViewFlags.None);
        }
        
        Debug.Log("Change the player's prefab to the takeover prefab");   
        // once the new prefab has been spawned OnAvatarCreated will be called where we add the recordable

        if (avatarManager.avatarPrefab == takeoverPrefab)
        {
            Debug.Log("Takeover prefab is the same as player prefab!");
            recordable = avatarManager.LocalAvatar.gameObject.GetComponent<Recordable>();
            recordable.OnUpdateRecordablePose += OnUpdateRecordablePose;
            recordable.IsTakingOver = isTakingOver;
        }
        
        //save original prefab for later
        playerPrefab = avatarManager.avatarPrefab;
        avatarManager.avatarPrefab = takeoverPrefab;
        replayer.SetIsTakingOver(isTakingOver);
        
        // init undo stack (if already initialized it should add to the existing stack)
        recorder.AddUndoState(UndoManager.UndoType.Edit, replayable.replayableId, replayer.recording.recordableDataDict[replayable.replayableId]);
        
        SetTakeoverStart(); // replayable needs to be set before this
        // don't do this for user study as we need real world scale and can't teleport!
        // if (PlayerPosToReplayablePos(replayable))
        // {
        //     Debug.Log("Player position set to replayable position!");
        // }
    }

    private bool PlayerPosToReplayablePos(Replayable replayable)
    {
        // set player position to replayable position
        if (xrOrigin != null)
        {
            // find forward vector of replayable (use head rotation without pitch and roll)
            var yRot = replayable._replayablePose.head.rotation.eulerAngles.y;
            var forward = Quaternion.Euler(0, yRot, 0) * Vector3.forward;
            //
            var success = xrOrigin.MoveCameraToWorldLocation(replayable._replayablePose.head.position);
            success = success && xrOrigin.MatchOriginUpCameraForward(xrOrigin.transform.up, forward);
            return success;
        }

        return false;
    }
    
    private void OnRecordingStop(object o, Recording.Flags flags)
    {
        if (isTakingOver)
        {
            isTakingOver = false;
            replayable.OnUpdateReplayablePose -= OnUpdateReplayablePose;

            ChangeBackPlayerPrefab();
            
            if (!recorder.allInputValid)
            {
                recorder.Undo(true); // will remove the undo state that was added in SelectTakeoverReplayable
                
                // we don't want to overwrite any data if it became invalid at some point
                currentTakeoverOverwrite.Clear();
            }
            else
            {
                // interpolate between the original replay and the currentTakeoverOverwrite
                // there should be recorder.fps * takeoverDuration frames - 1 between which we need to interpolate
                // the first one we don't get because the Recordable Update() due to the + deltaTime is already past frame 0
                // but this shouldn't matter because frame 0 is 100% replayable anyway
                // this is running in one frame anyway unfortunately... let's see if this is fast enough for longer recordings
                StartCoroutine(OverwriteData(flags));
            }
        }
        else
        {
            flags.SaveReady = true;
        }
    }

    private void ChangeBackPlayerPrefab()
    {
        if (playerPrefab != null)
        {

            if (playerPrefab == takeoverPrefab)
            {
                Debug.Log("Don't need to change back! Takeover prefab is the same as player prefab!");
                
            }
            else
            {
                Debug.Log("Change back player prefab to: " + playerPrefab.name);
                // change player prefab back to previous prefab
                avatarManager.avatarPrefab = playerPrefab;
            }
            playerPrefab = null; // reset
            
            // make replayable visible again
            if (replayable.gameObject.TryGetComponent<UbiqMetaAvatarEntity>(out var metaAvatar))
            {
                Debug.Log("Takeover Done: Show replayable avatar again!");
                metaAvatar.SetView(Oculus.Avatar2.CAPI.ovrAvatar2EntityViewFlags.ThirdPerson);
            }
        }
       
        recordable.OnUpdateRecordablePose -= OnUpdateRecordablePose;
        
        // we don't set isTakingOver to false for the Replayer as it can do it itself and might need to do it after this method has been called
        // we also don't set it back for the recordable, because OnRecording stop is called after this
        // and we need to now OnRecordingStop that this was a takeover, so the recordable is setting it to false itself
        // recordable.IsTakingOver = isTakingOver;
    }

    private IEnumerator OverwriteData(Recording.Flags flags)
    {
        var numFrames = replayer.recording.recordableDataDict[replayable.replayableId].numFrames;
        switch (takeoverSetting)
        {
            case(TakeoverSettings.Start):
                // here all we need to do is overwrite the existing data and remove any old data at the end
                // no interpolation needed
                for (int i = takeoverStart; i < currentTakeoverOverwrite.Count; i++)
                {
                    replayer.recording.AddDataFrame(replayable.replayableId, i, currentTakeoverOverwrite[i]);
                }
                // remove old data if there is any
                if (numFrames > currentTakeoverOverwrite.Count)
                {
                    replayer.recording.RemoveDataFrames(replayable.replayableId, currentTakeoverOverwrite.Count, numFrames - currentTakeoverOverwrite.Count);
                }
                numFrames = currentTakeoverOverwrite.Count;
                
                break;
            
            case(TakeoverSettings.End):
                // don't need to interpolate first frame because it would be 100% replayable anyway
                // currentTakeoverOverwrite indexing starts at 0! and corresponds to takeoverStart + 1 in the replayable data
                // Debug.Log("Overwrite data => Takeover start: " + takeoverStart + " old numFrames: " + numFrames);
                int j = 0; 
                for (int i = takeoverStart + 1; i < numFrames; i++)
                {
                    // compute t
                    t = (float)(i - takeoverStart) / (int)(recorder.fps * takeoverDuration);
                    j = i - (takeoverStart + 1);
                    Debug.Log(i + " " + j + " t: " + t);
                    
                    // interpolate
                    var dataFrame = replayer.recording.recordableDataDict[replayable.replayableId].dataFrames[i];
                    
                    var headPos = Vector3.Lerp(new Vector3(dataFrame.xPosHead, dataFrame.yPosHead, dataFrame.zPosHead), currentTakeoverOverwrite[j].head.position, t);
                    var headRot = Quaternion.Lerp(new Quaternion(dataFrame.xRotHead, dataFrame.yRotHead, dataFrame.zRotHead, dataFrame.wRotHead), currentTakeoverOverwrite[j].head.rotation, t);
                    
                    var leftHandPos = Vector3.Lerp(new Vector3(dataFrame.xPosLeftHand, dataFrame.yPosLeftHand, dataFrame.zPosLeftHand), currentTakeoverOverwrite[j].leftHand.position, t);
                    var leftHandRot = Quaternion.Lerp(new Quaternion(dataFrame.xRotLeftHand, dataFrame.yRotLeftHand, dataFrame.zRotLeftHand, dataFrame.wRotLeftHand), currentTakeoverOverwrite[j].leftHand.rotation, t);
                    
                    var rightHandPos = Vector3.Lerp(new Vector3(dataFrame.xPosRightHand, dataFrame.yPosRightHand, dataFrame.zPosRightHand), currentTakeoverOverwrite[j].rightHand.position, t);
                    var rightHandRot = Quaternion.Lerp(new Quaternion(dataFrame.xRotRightHand, dataFrame.yRotRightHand, dataFrame.zRotRightHand, dataFrame.wRotRightHand), currentTakeoverOverwrite[j].rightHand.rotation, t);
                    
                    // update data
                    var newPose = new Recordable.RecordablePose(){head = new Pose(headPos, headRot), leftHand = new Pose(leftHandPos, leftHandRot), rightHand = new Pose(rightHandPos, rightHandRot)};
                    replayer.recording.AddDataFrame(replayable.replayableId, i, newPose);
                    
                    // TODO: add hand tracking stuff! not needed right now for my studies but might be suitable for later...
                }
                // add new recorded data to the replayable
                // Debug.Log("add new data to replayable from: " + (j + 1) + " to " + currentTakeoverOverwrite.Count);
                for (var i = j + 1; i < currentTakeoverOverwrite.Count; i++)
                {
                    Debug.Log(numFrames + " i " + i);
                    replayer.recording.AddDataFrame(replayable.replayableId, numFrames, currentTakeoverOverwrite[i]);
                    numFrames++;
                }
                break;
        }
        // update meta data
        replayer.recording.UpdateMetaData(replayable.replayableId, numFrames, replayer.recording.recordableDataDict[replayable.replayableId].fps, replayer.recording.recordableDataDict[replayable.replayableId].prefabName);
        replayer.UpdateMaxFrameNumber();
        
        // add undo state to UndoManager
        recorder.AddUndoState(UndoManager.UndoType.Edit, replayable.replayableId, replayer.recording.recordableDataDict[replayable.replayableId]);
        
        // clear the takeover data
        currentTakeoverOverwrite.Clear();
        
        // set replayable pose to first frame in new data
        replayable.SetReplayablePose(takeoverStart);
        
        flags.SaveReady = true;
        
        yield return null;
    }
    
    private void OnUpdateRecordablePose(object o, Recordable.RecordablePoseArgs args)
    {
        // Debug.Log("add takeover overwrite data: " + args.frameNr + " " + args.recordablePose.head.position);
        currentTakeoverOverwrite.Add(args.frameNr, args.recordablePose);
        // Debug.Log(currentTakeoverOverwrite[0].head.position);
    }
    
    // interpolate between the replayable and the player pose to create a smooth takeover
    private void OnUpdateReplayablePose(object o, Replayable.ReplayablePose rp)
    {
        switch (takeoverSetting)
        {
            case TakeoverSettings.Start:
                break;
            case TakeoverSettings.End:
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
                
                // TODO: not needed right now for my studies but might be suitable for later...
                if (playerInput.TryGet(out IHandSkeletonInput srcSkel))
                {
                    // this might not work because of index 1 which is the palm which we don't tend to set because we don't need it
                    // so values might be invalid
                    for (var i = 0; i < srcSkel.leftHandSkeleton.poses.Count; i++)
                    {
                        var pos =Vector3.Lerp(rp.leftHandSkeleton[i].value.position, srcSkel.leftHandSkeleton.poses[i].value.position, t);
                        var rot = Quaternion.Lerp(rp.leftHandSkeleton[i].value.rotation, srcSkel.leftHandSkeleton.poses[i].value.rotation, t);
                        rp.leftHandSkeleton[i] = new InputVar<Pose>(new Pose(pos, rot));
                    }
                }
                
                break;
        }
        
    }
}
