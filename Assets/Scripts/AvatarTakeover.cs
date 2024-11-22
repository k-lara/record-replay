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
    
    private bool isTakingOver = false;
    
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


    private List<SkinnedMeshRenderer> playerMeshRenderers = new();
    private AvatarMaterials playerMaterials;
    private List<SkinnedMeshRenderer> replayableMeshRenderers = new();
    private AvatarMaterials replayableMaterials;
    // Start is called before the first frame update
    void Start()
    {
        recorder = GetComponent<Recorder>();
        recorder.onRecordingStop += OnRecordingStop;
        
        takeoverSelector = GetComponent<TakeoverSelector>();
        takeoverSelector.onTakeoverSelected += SelectTakeoverReplayable;
        
        replayer = GetComponent<Replayer>();
        avatarManager = AvatarManager.Find(this);
        avatarManager.OnAvatarCreated.AddListener(OnAvatarCreated);
        avatarManager.OnAvatarDestroyed.AddListener(OnAvatarDestroyed);
        
        // TODO maybe: make avatar input and replay relative to networkSceneRoot, not necessarily necessary...
        networkScene = NetworkScene.Find(this); 
        
        playerInput = avatarManager.input;
        xrOrigin = FindObjectOfType<XROrigin>();
        
    }
    
    private void GetMeshRenderers(GameObject go, ref List<SkinnedMeshRenderer> meshRenderers)
    {
        meshRenderers.Clear();
        go.GetComponentsInChildren(meshRenderers);
    }

    private void SetMaterialsToFade()
    {
        // make our avatar already semi transparent so we know visually that we are taking over
        GetMeshRenderers(recordable.gameObject, ref playerMeshRenderers);
        playerMaterials = recordable.GetComponent<AvatarMaterials>();
        foreach (var mr in playerMeshRenderers)
        {
            // replace opaque with fade material
            var idx = playerMaterials.materialsOpaque.IndexOf(mr.material);
            if (idx != -1 && idx < playerMaterials.materialsFade.Count)
            {
                mr.material = playerMaterials.materialsFade[idx]; // opaque and fade materials should be in the same order
                // set transparency
                var color = mr.material.color;
                color.a = 0.5f;
                mr.material.color = color;
            }
        }

        GetMeshRenderers(replayable.gameObject, ref replayableMeshRenderers);
        replayableMaterials = replayable.GetComponent<AvatarMaterials>();
        foreach (var mr in replayableMeshRenderers)
        {
            var idx = replayableMaterials.materialsOpaque.IndexOf(mr.material);
            if (idx != -1 && idx < replayableMaterials.materialsFade.Count)
            {
                mr.material = replayableMaterials.materialsFade[idx];
                var color = mr.material.color;
                color.a = 0.5f;
                mr.material.color = color;
            }
        }
    }

    private void OnAvatarCreated(Avatar avatar)
    {
        if (isTakingOver)
        {
            recordable = avatarManager.LocalAvatar.gameObject.AddComponent<Recordable>();
            recordable.OnUpdateRecordablePose += OnUpdateRecordablePose;
            recordable.IsTakingOver = isTakingOver;
            SetMaterialsToFade();
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
        // get last frame of this replayable's replay
        // replay is in 10 fps and we want maybe 2 seconds of takeover overwrite?
        // make sure there are enough frames to guarantee that takeover time otherwise take what is there
        var numFrames = replayer.recording.recordableDataDict[replayable.replayableId].numFrames;
        takeoverStart = 0;
        if (numFrames > recorder.fps * takeoverDuration)
        {
            takeoverStart = numFrames - (int)(recorder.fps * takeoverDuration);
        }
        replayer.frameOffset = takeoverStart; // this is for the internal replayer Update() so the deltaTime is correct and takes the right frame
        replayer.currentFrame = takeoverStart;
        replayer.SetCurrentFrame(takeoverStart);
    }
    
    // select a replayable in the scene for takeover
    // the player avatar takes the appearance of the replayable avatar
    // per default the takeover start is a few seconds from the end of the selected replayable's data
    // TODO if the user has set their own range, the takeover start is set to a few seconds before that range
    private void SelectTakeoverReplayable(object o, GameObject go)
    {
        isTakingOver = true;
        replayable = go.GetComponent<Replayable>();
        // this has to be called once we know which replayable to take over
        replayable.OnUpdateReplayablePose += OnUpdateReplayablePose;
        
        // if we don't need to change the avatarPrefab because it is already the same, we don't need to wait to get the recordable and set the materials
        if (avatarManager.avatarPrefab.name ==
            replayer.recording.recordableDataDict[replayable.replayableId].prefabName)
        {
            recordable = avatarManager.LocalAvatar.gameObject.GetComponent<Recordable>();
            recordable.OnUpdateRecordablePose += OnUpdateRecordablePose;
            recordable.IsTakingOver = isTakingOver;
            SetMaterialsToFade();
        }
        else
        {
            // once the new prefab has been spawned OnAvatarCreated will be called and we can add the recordable and modify the materials
            
            //save original prefab for later
            playerPrefab = avatarManager.avatarPrefab;
            avatarManager.avatarPrefab = replayable.gameObject;
                //replayer.prefabCatalogue[replayer.recording.recordableDataDict[replayable.replayableId].prefabName];
        }
        
        // init undo stack
        recorder.InitUndoStack(UndoManager.UndoType.Edit, replayable.replayableId, replayer.recording.recordableDataDict[replayable.replayableId]);
        
        SetTakeoverStart(); // replayable needs to be set before this
        if (PlayerPosToReplayablePos(replayable))
        {
            Debug.Log("Player position set to replayable position!");
        }
    }

    private bool PlayerPosToReplayablePos(Replayable replayable)
    {
        // set player position to replayable position
        if (xrOrigin != null)
        {
            // find forward vector of replayable (use head rotation without pitch and roll)
            var yRot = replayable._replayablePose.head.rotation.eulerAngles.y;
            var forward = Quaternion.Euler(0, yRot, 0) * Vector3.forward;
            
            var success = xrOrigin.MoveCameraToWorldLocation(recordable.transform.position);
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
            
            // set replayable materials back to opaque
            foreach (var mr in replayableMeshRenderers)
            {
                var idx = replayableMaterials.materialsFade.IndexOf(mr.material);
                if (idx != -1 && idx < replayableMaterials.materialsOpaque.Count)
                {
                    mr.material = replayableMaterials.materialsOpaque[idx];
                }
            }
            
            // interpolate between the original replay and the currentTakeoverOverwrite
            // there should be recorder.fps * takeoverDuration frames - 1 between which we need to interpolate
            // the first one we don't get because the Recordable Update() due to the + deltaTime is already past frame 0
            // but this shouldn't matter because frame 0 is 100% replayable anyway
            StartCoroutine(OverwriteData(flags));
            
            ChangeBackPlayerPrefab();
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
            // change player prefab back to previous prefab
            avatarManager.avatarPrefab = playerPrefab;
            playerPrefab = null; // reset
        }
        else
        {
            // set materials back to opaque for player avatar since we are not replacing the prefab
            foreach (var mr in playerMeshRenderers)
            {
                var idx = playerMaterials.materialsFade.IndexOf(mr.material);
                if (idx != -1 && idx < playerMaterials.materialsOpaque.Count)
                {
                    mr.material = playerMaterials.materialsOpaque[idx];
                }
                    
                recordable.OnUpdateRecordablePose -= OnUpdateRecordablePose;
                recordable.IsTakingOver = isTakingOver;
            }
        }
    }

    private IEnumerator OverwriteData(Recording.Flags flags)
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
            var newPose = new Recordable.RecordablePose(){head = new Pose(headPos, headRot), leftHand = new Pose(leftHandPos, leftHandRot), rightHand = new Pose(rightHandPos, rightHandRot)};
            replayer.recording.AddDataFrame(replayable.replayableId, i, newPose);
        }
        // add new recorded data to the replayable
        for (var i = (int)(recorder.fps * takeoverDuration); i < currentTakeoverOverwrite.Count; i++)
        {
            replayer.recording.AddDataFrame(replayable.replayableId, numFrames, currentTakeoverOverwrite[i]);
            numFrames++;
        }
        
        // update meta data
        replayer.recording.UpdateMetaData(replayable.replayableId, numFrames, replayer.recording.recordableDataDict[replayable.replayableId].fps, replayer.recording.recordableDataDict[replayable.replayableId].prefabName);
        
        // add undo state to UndoManager
        recorder.AddUndoState(UndoManager.UndoType.Edit, replayable.replayableId, replayer.recording.recordableDataDict[replayable.replayableId]);
        
        // clear the takeover data
        currentTakeoverOverwrite.Clear();
        
        // set replayable pose to last frame in new data
        replayable.SetReplayablePose(numFrames-1);
        
        replayable = null; // reset
        flags.SaveReady = true;
        
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
        
        UpdateTakeoverVisibility();
        
        // previousPose.head.position = rp.head.position;
        // previousPose.head.rotation = rp.head.rotation;
        // previousPose.leftHand.position = rp.leftHand.position;
        // previousPose.leftHand.rotation = rp.leftHand.rotation;
        // previousPose.rightHand.position = rp.rightHand.position;
        // previousPose.rightHand.rotation = rp.rightHand.rotation;
        // previousFrameFloat = replayer.currentFrame;
    }

    private void UpdateTakeoverVisibility()
    {
        var playerAlpha = 0.5f + 0.5f * t;
        var replayableAlpha = 1.0f - playerAlpha;

        foreach (var mr in playerMeshRenderers)
        {
            var color = mr.material.color;
            color.a = playerAlpha;
            mr.material.color = color;
        }

        foreach (var mr in replayableMeshRenderers)
        {
            var color = mr.material.color;
            color.a = replayableAlpha;
            mr.material.color = color;
        }
    }
}
