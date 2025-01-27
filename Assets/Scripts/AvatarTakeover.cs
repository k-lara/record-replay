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
    
    public bool isTakingOver { get; private set; }
    
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

    // TODO: when accessing .material a new instance is created and I might need to destroy it manually myself when I am done with it
    private void SetMaterialsToFade()
    {
        // make our avatar already semi transparent so we know visually that we are taking over
        GetMeshRenderers(recordable.gameObject, ref playerMeshRenderers);
        GetMeshRenderers(replayable.gameObject, ref replayableMeshRenderers);
        playerMaterials = recordable.GetComponent<AvatarMaterials>();
        replayableMaterials = replayable.GetComponent<AvatarMaterials>();
        
        if (!playerMaterials || !replayableMaterials)
        {
            Debug.Log("AvatarMaterials component not found!");
            return;
        }
        
        // Debug.Log("Player mesh renderers count: " + playerMeshRenderers.Count + 
        //           "available materials (opaque/fade) " + playerMaterials.materialsOpaque.Count + " " + playerMaterials.materialsFade.Count);
        foreach (var mr in playerMeshRenderers)
        {
            // replace opaque with fade material
            var matFade = playerMaterials.GetMaterialFade(mr.material.name.Split(" ")[0] + "_Fade");
            if (matFade)
            {
                mr.material = matFade;
                // set transparency
                var color = mr.material.color;
                color.a = 0.5f;
                mr.material.color = color;
            }
            // var idx = playerMaterials.materialsOpaque.IndexOf(mr.material); // material has (Instance) appended to it so we can't find it this way
            // if (idx != -1 && idx < playerMaterials.materialsFade.Count)
            // {
            //     mr.material = playerMaterials.materialsFade[idx]; // opaque and fade materials should be in the same order
            //     // set transparency
            //     var color = mr.material.color;
            //     color.a = 0.5f;
            //     mr.material.color = color;
            // }
        }
        
        // Debug.Log("Replayable mesh renderers count: " + replayableMeshRenderers.Count + 
        //           "available materials (opaque/fade) " + replayableMaterials.materialsOpaque.Count + " " + replayableMaterials.materialsFade.Count);
        foreach (var mr in replayableMeshRenderers)
        {
            var matFade = replayableMaterials.GetMaterialFade(mr.material.name.Split(" ")[0] + "_Fade");
            if (matFade)
            {
                mr.material = matFade;
                // set transparency
                var color = mr.material.color;
                color.a = 0.5f;
                mr.material.color = color;
            }
            // var idx = replayableMaterials.materialsOpaque.IndexOf(mr.material);
            // if (idx != -1 && idx < replayableMaterials.materialsFade.Count)
            // {
            //     mr.material = replayableMaterials.materialsFade[idx];
            //     var color = mr.material.color;
            //     color.a = 0.5f;
            //     mr.material.color = color;
            // }
        }
    }
    
    // the recordable is
    private void OnAvatarCreated(Avatar avatar)
    {
        if (isTakingOver)
        {
            // we add a recordable to the new player avatar which we might have changed for takeover
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
        replayer.SetCurrentFrameForTakeover(takeoverStart);
        Debug.Log("Set takeover start to frame: " + takeoverStart);
    }
    
    // select a replayable in the scene for takeover
    // the player avatar takes the appearance of the replayable avatar
    // per default the takeover start is a few seconds from the end of the selected replayable's data
    // TODO if the user has set their own range, the takeover start is set to a few seconds before that range
    private void SelectTakeoverReplayable(object o, GameObject go)
    {
        Debug.Log("Takeover selected: " + go.name);
        isTakingOver = true;
        replayable = go.GetComponent<Replayable>();
        // this has to be called once we know which replayable to take over
        replayable.OnUpdateReplayablePose += OnUpdateReplayablePose;

        if (avatarManager.avatarPrefab.name == "MetaAvatar")
        {
            
        }
        
        // if we don't need to change the avatarPrefab because it is already the same, we don't need to wait to get the recordable and set the materials
        if (avatarManager.avatarPrefab.name ==
            replayer.recording.recordableDataDict[replayable.replayableId].prefabName)
        {
            Debug.Log("Avatar prefab is already the same as the replayable prefab!");
            recordable = avatarManager.LocalAvatar.gameObject.GetComponent<Recordable>();
            recordable.OnUpdateRecordablePose += OnUpdateRecordablePose;
            recordable.IsTakingOver = isTakingOver;
            replayer.SetIsTakingOver(isTakingOver);
            SetMaterialsToFade();
        }
        else
        {
            // once the new prefab has been spawned OnAvatarCreated will be called where we add the recordable
            // after that we can modify the materials
            Debug.Log("Avatar prefab is different from the replayable prefab!");   
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
            
            // set replayable materials back to opaque
            foreach (var mr in replayableMeshRenderers)
            {
                // material has "_Fade (Instance)" appended to it need to remove that first
                var matName = mr.material.name.Remove(mr.material.name.Length - 16);
                Debug.Log(matName);
                var matOpaque = replayableMaterials.GetMaterialOpaque(matName);
                if (matOpaque)
                {
                    mr.material = matOpaque;
                }
                // var idx = replayableMaterials.materialsFade.IndexOf(mr.material);
                // if (idx != -1 && idx < replayableMaterials.materialsOpaque.Count)
                // {
                //     mr.material = replayableMaterials.materialsOpaque[idx];
                // }
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
            Debug.Log("Change back player prefab to: " + playerPrefab.name);
            // change player prefab back to previous prefab
            avatarManager.avatarPrefab = playerPrefab;
            playerPrefab = null; // reset
        }
        else
        {
            if (playerMaterials)
            {
                Debug.Log("Change materials back to opaque, do not change player prefab!");
                // set materials back to opaque for player avatar since we are not replacing the prefab
                foreach (var mr in playerMeshRenderers)
                {
                    // material has "_Fade (Instance)" appended to it need to remove that first
                    var matName = mr.material.name.Remove(mr.material.name.Length - 16);
                    Debug.Log(matName);
                    var matOpaque = playerMaterials.GetMaterialOpaque(matName);
                    if (matOpaque)
                    {
                        mr.material = matOpaque;
                    }
                    // var idx = playerMaterials.materialsFade.IndexOf(mr.material);
                    // if (idx != -1 && idx < playerMaterials.materialsOpaque.Count)
                    // {
                    //     mr.material = playerMaterials.materialsOpaque[idx];
                    // }
                }
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
            
            // don't interpolate
            // var headPos = currentTakeoverOverwrite[j].head.position;
            // var headRot = currentTakeoverOverwrite[j].head.rotation;
            // var leftHandPos = currentTakeoverOverwrite[j].leftHand.position;
            // var leftHandRot = currentTakeoverOverwrite[j].leftHand.rotation;
            // var rightHandPos = currentTakeoverOverwrite[j].rightHand.position;
            // var rightHandRot = currentTakeoverOverwrite[j].rightHand.rotation;
            
            // update data
            var newPose = new Recordable.RecordablePose(){head = new Pose(headPos, headRot), leftHand = new Pose(leftHandPos, leftHandRot), rightHand = new Pose(rightHandPos, rightHandRot)};
            replayer.recording.AddDataFrame(replayable.replayableId, i, newPose);
        }
        // add new recorded data to the replayable
        // Debug.Log("add new data to replayable from: " + (j + 1) + " to " + currentTakeoverOverwrite.Count);
        for (var i = j + 1; i < currentTakeoverOverwrite.Count; i++)
        {
            Debug.Log(numFrames + " i " + i);
            replayer.recording.AddDataFrame(replayable.replayableId, numFrames, currentTakeoverOverwrite[i]);
            numFrames++;
        }
        
        // update meta data
        replayer.recording.UpdateMetaData(replayable.replayableId, numFrames, replayer.recording.recordableDataDict[replayable.replayableId].fps, replayer.recording.recordableDataDict[replayable.replayableId].prefabName);
        replayer.UpdateMaxFrameNumber();
        
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
    
    private void OnUpdateRecordablePose(object o, Recordable.RecordablePoseArgs args)
    {
        // Debug.Log("add takeover overwrite data: " + args.frameNr + " " + args.recordablePose.head.position);
        currentTakeoverOverwrite.Add(args.frameNr, args.recordablePose);
        // Debug.Log(currentTakeoverOverwrite[0].head.position);
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
