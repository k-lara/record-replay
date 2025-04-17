using System;
using System.Collections.Generic;
using Ubiq.Spawning;
using UnityEngine;

public class Replayer : MonoBehaviour
{
    private Recorder _recorder;

    private RecordingManager _recordingManager;

    public Recording recording => _recordingManager.Recording;

    // as long as the recorder has not recorded anything there is no recording
    // also, the recording is not loaded until the user decides to load it
    private NetworkSpawnManager _spawnManager;

    // recorded data for all avatars in the scene
    // replayables ordered based on their network id
    private Dictionary<Guid, Replayable> _replayablesDict;
    
    private bool _isLoaded;
    public bool IsPlaying => _isPlaying;
    private bool _isPlaying;
    private bool _isTakingOver;
    public float currentFrame { get; set; } // could be between two frames
    public int frameOffset { get; set; }

    private int _frameNr;
    private float _deltaTime;
    private string _currentRecordingPath;
    
    public event EventHandler onReplayStart; // start replaying
    public event EventHandler onReplayStop; // stop replaying
    public event EventHandler<Dictionary<Guid, Replayable>> onReplaySpawned; // replays spawned, data is not necessarily loaded
    public event EventHandler onReplayUnspawned; // replayables have been unspawned
    public event EventHandler<bool> onFrameUpdate; // true if we need interpolated pose, false if not
    
    public int GetFrameNr() => _frameNr;
    
    // Start is called before the first frame update
    void Awake()
    {
        _recorder = GetComponent<Recorder>();
        _recorder.onRecordingStart += OnRecordingStart;
        _recorder.onRecordingStop += OnRecordingStop;
        
        _recordingManager = GetComponent<RecordingManager>();
        _recordingManager.onThumbnailSpawned += OnThumbnailSpawned;
        _recordingManager.onReplayablesSpawnedAndLoaded += OnReplayablesSpawned;
        _recordingManager.onRecordingLoaded += OnRecordingLoaded;
        _recordingManager.onRecordingUnloaded += OnRecordingUnloaded;
        _recordingManager.onUnspawned += OnReplayUnspawned;
        _recordingManager.onRecordingUndo += OnRecordingUndo;
        _recordingManager.onRecordingRedo += OnRecordingRedo;
        
        _spawnManager = NetworkSpawnManager.Find(this);

        _replayablesDict = new Dictionary<Guid, Replayable>();

    }
    
    void Update()
    {
        if (!_isLoaded) return;
        if (_isPlaying)
        {
            _deltaTime += Time.deltaTime;
            
            // sometimes we want to start in the middle of a recording
            // then we need to add a frameOffset otherwise the delta time would give us the wrong frame
            // e.g. if we start at frame 0 and pause at frame 50 and then move the slider to frame 55
            // then we have set our offset and need to reset deltaTime, since we don't want delta time to be added to the frameOffset
           
            // current frame is a float, so we know between which two frames we are
            currentFrame = _deltaTime * _recorder.fps + frameOffset;
            
            onFrameUpdate?.Invoke(this, true);

            if (currentFrame >= _frameNr - 1)
            {
                currentFrame = 0.0f;
                _deltaTime = 0.0f;
                StopReplay();
                // if we are doing base recordings (like for the user study)
                // every subsequent recording will only be as long as the base recording)
                if (_recordingManager.hasBaseRecordings)
                {
                    _recorder.StopRecording();
                }
            }
        }
    }

    public float GetCurrentFrameNormalized()
    {
        if (!_isLoaded || _frameNr == 0) return 0.0f;
        return currentFrame / _frameNr;
    }
    
    // this comes from a slider, so the normalized frame is between 0 and 1
    // and will be mapped to the number of frames in the recording
    // this way the slider doesn't need to know how many frames there are
    public void SetCurrentFrameManually(float normalizedFrame)
    {
        if (!_isLoaded) return; // can't set a frame if we don't have data loaded
        if (_isPlaying) return; // probably better not to jump between frames while playing
        
        frameOffset = Mathf.FloorToInt(normalizedFrame * _frameNr);
        currentFrame = frameOffset; // need to set this too because onFrameUpdate needs the currentFrame
        _deltaTime = 0.0f;
        
        onFrameUpdate?.Invoke(this, false);
    }
    
    // only for takeover!
    public void SetCurrentFrameForTakeover(int frame)
    {
        if (frame > _frameNr)
        {
            frameOffset = _frameNr - 1;
        }
        else if (frame < 0)
        {
            frameOffset = 0;
        }
        else
        {
            frameOffset = frame;
        }

        currentFrame = frameOffset;
        // if _deltaTime has been > 0 we need to reset it otherwise the start from the specific frame is somewhat wrong
        _deltaTime = 0.0f;
        onFrameUpdate?.Invoke(this, false);
        
    }

    public void StartReplay()
    {
        if (!_isLoaded) return; // can't start if we don't have the spawned avatars
        if (_isPlaying) return; // we are already playing
        
        Debug.Log("Start replay!");
        
        _isPlaying = true;
        onReplayStart?.Invoke(this, EventArgs.Empty);
    }
    
    // stopping a replay is fine whenever
    // we only need to be aware of manual frame changes
    public void StopReplay()
    {
        if (!_isPlaying) return; // no point in stopping if we are not playing
        
        Debug.Log("Stop Replay!");
        frameOffset = 0; //have to reset it here so the slider is updated correctly too
        _isPlaying = false;
        onReplayStop?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecordingStart(object o, EventArgs e)
    {
        if (!_isLoaded) return;
        if (_isPlaying) return;
        
        // check if a takeover is happening
        // if so, when recording stops, we don't want to spawn a new replayable
        // we check for isTakingOver here because OnRecordingStop might be called in the AvatarTakeover first
        // and then isTakingOver might already be false
        
        // if we have stopped a replay pressing start will resume it
        StartReplay();
    }
    
    private void OnRecordingStop(object o, Recording.Flags flags)
    {
        // when we stop recording, do we want to take off a few frames before we stopped
        // OR do we want to start from the beginning???
        // could depend on whether we are taking over an avatar or creating a new one
        // in that case we start from beginning per default
        StopReplay();
        _deltaTime = 0.0f;
        
        // if takeover: we don't want to spawn new replayables because we already have them
        if (!_isTakingOver)
        {
            if (_recorder.allInputValid) // don't want to spawn new replayables if recording was invalid
            {
                // this also updates _replayablesDict and adds newly spawned replayables!!!
                _recordingManager.SpawnReplayables(_replayablesDict);
            }
        }
        else
        {
            // takeover is done, set it back to false
            _isTakingOver = false;
        }
    }
    
    public void SetIsTakingOver(bool isTakingOver)
    {
        _isTakingOver = isTakingOver;
    }
    
    /*
     * This is called when replayables have been spawned after a new recording or after loading recording data from a thumbnail.
     * This means we already have data from this recording and _isLoaded should already be true!
     * 
     */
    private void OnReplayablesSpawned(object o, EventArgs e)
    {
        UpdateMaxFrameNumber();
        // _isLoaded = true;
        Debug.Log("Replayer: OnReplayablesSpawned(): max frames: " + _frameNr);
        onReplaySpawned?.Invoke(this, _replayablesDict);
    }

    public void UpdateMaxFrameNumber()
    {
        // get the max number of frames
        _frameNr = 0;
        foreach (var entry in recording.recordableDataDict)
        {
            if (_frameNr < entry.Value.dataFrames.Count)
            {
                _frameNr = entry.Value.dataFrames.Count;
            }
        }
        Debug.Log("Max frame number: " + _frameNr);
    }
    
    // Here we do not have data loaded.
    // This is called when we have spawned replayables from a thumbnail.
    // We can add them to our list of replayables. This list has nothing to do with the data of a recording.
    private void OnThumbnailSpawned(object o, List<GameObject> thumbnailObjects)
    {
        _replayablesDict.Clear();
        foreach (var go in thumbnailObjects)
        {
            var replayable = go.GetComponent<Replayable>();
            _replayablesDict.Add(replayable.replayableId, replayable);
        }
        Debug.Log("Replayer: OnThumbnailSpawned(): # Replayables: " + _replayablesDict.Count);
    }

    private void OnRecordingLoaded(object o, EventArgs e)
    {
        _isLoaded = true;
    }
    
    private void OnRecordingUnloaded(object o, EventArgs e)
    {
        _frameNr = 0;
        _deltaTime = 0.0f;
        _isLoaded = false;
    }
    
    private void OnReplayUnspawned(object o, EventArgs e)
    {
        _replayablesDict.Clear();
        onReplayUnspawned?.Invoke(this, EventArgs.Empty);
    }

    /*
     * When we undo a recording step and remove a replayable
     * then we need to update the replayablesDict and remove the replayable
     * (which should be null by then)
     */
    private void OnRecordingUndo(object o, (UndoManager.UndoType, List<Guid>) undoInfo)
    {
        
        // only when we undo a new recording we need to remove the replayable
        if (undoInfo.Item1 == UndoManager.UndoType.New)
        {
            foreach (var id in undoInfo.Item2)
            {
                _replayablesDict.Remove(id);
            }
        }

        // if we want to undo an edit we don't remove the replayable
        // but we need to update some data!
        if (undoInfo.Item1 == UndoManager.UndoType.Edit)
        {
            // so far this is only 1 replayable anyway!
            foreach (var id in undoInfo.Item2)
            {
                if (_replayablesDict.ContainsKey(id))
                {
                    // _replayablesDict[id].SetReplayablePose(recording.recordableDataDict[id].dataFrames.Count - 1);
                    _replayablesDict[id].SetReplayablePose(0);
                    UpdateMaxFrameNumber(); // max frame number could be different now so we check again!
                }
            }
            Debug.Log(recording.ToString());
        }
    }

    private void OnRecordingRedo(object o, (UndoManager.UndoType, List<Replayable>) redoInfo)
    {
        if (redoInfo.Item1 == UndoManager.UndoType.New)
        {
            foreach (var r in redoInfo.Item2)
            {
                _replayablesDict.Add(r.replayableId, r);
            }
        }

        if (redoInfo.Item1 == UndoManager.UndoType.Edit)
        {
            UpdateMaxFrameNumber();
        }
    }
    
    private void OnDestroy()
    {
        _recorder.onRecordingStart -= OnRecordingStart;
        _recorder.onRecordingStop -= OnRecordingStop;
        _recordingManager.onThumbnailSpawned -= OnThumbnailSpawned;
        _recordingManager.onReplayablesSpawnedAndLoaded -= OnReplayablesSpawned;
        _recordingManager.onRecordingLoaded -= OnRecordingLoaded;
        _recordingManager.onRecordingUnloaded -= OnRecordingUnloaded;
        _recordingManager.onUnspawned -= OnReplayUnspawned;
        _recordingManager.onRecordingUndo -= OnRecordingUndo;
        _recordingManager.onRecordingRedo -= OnRecordingRedo;
    }
    
    // private Replayable SpawnReplayableObject(string prefabName)
    // {
    //     var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[prefabName]);
    //     
    //     // normal users don't have the AudioReplayable on their avatar
    //     // it is only used for replayed avatars
    //     // if we don't have an AudioRecordable on the avatar, we also won't create a Replayable
    //     if (go.GetComponent<AudioRecordable>())
    //     {
    //         Debug.Log("Add AudioReplayable");
    //         go.AddComponent<AudioReplayable>();
    //     }
    //     // TODO ADD REPLAY INFO AGAIN ONCE THE REST IS WORKING
    //     // go.AddComponent<ReplayInfo>();
    //     
    //     var replayable = go.GetComponent<Replayable>();
    //     // this helps to distinguish local and remote replayables. someone could replay their own data while
    //     // at the same time receiving a replay from somebody else.
    //     replayable.isLocal = true;
    //
    //     return replayable;
    // }

    // private async Task LoadRecordedData(string pathToRecording)
    // {
    //     var files = System.IO.Directory.GetFiles(pathToRecording, "m*.txt");
    //     
    //     _recordableDataFramesDict.Clear();
    //
    //     foreach (var file in files)
    //     {
    //         // get file name without extension
    //         var fileName = Path.GetFileNameWithoutExtension(file);
    //         // remove the "motion_" prefix
    //         var networkId = new NetworkId(fileName.Substring(7));
    //         Debug.Log("Load recorded data from id: " + networkId);
    //         
    //         using (StreamReader sr = new StreamReader(file))
    //         {
    //             // one frame per line
    //             var dataFrames = new List<Recordable.RecordableDataFrame>();
    //             while (await sr.ReadLineAsync() is { } line)
    //             {
    //                 var dataFrame = JsonUtility.FromJson<Recordable.RecordableDataFrame>(line);
    //                 dataFrames.Add(dataFrame);
    //             }
    //             _recordableDataFramesDict.Add(networkId, dataFrames);
    //         }
    //     }
    // }
}
