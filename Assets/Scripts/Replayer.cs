using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HVR;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Spawning;
using UnityEditor;
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
    private bool _isPlaying;
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
            // current frame is a float, so we know between which two frames we are
            currentFrame = _deltaTime * _recorder.fps + frameOffset;
            Debug.Log("Replayer Update(): current: " + currentFrame + " max frames:" + _frameNr);
            onFrameUpdate?.Invoke(this, true);

            if (currentFrame >= _frameNr - 1)
            {
                currentFrame = 0.0f;
                _deltaTime = 0.0f;
                StopReplay();
            }
        }
    }
    
    // only for takeover!
    public void SetCurrentFrame(int frame)
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
        _isPlaying = false;
        onReplayStop?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecordingStart(object o, EventArgs e)
    {
        if (!_isLoaded) return;
        if (_isPlaying) return;
        
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
        _recordingManager.SpawnReplayables(_replayablesDict);
    }
    
    /*
     * This is called when replayables have been spawned after a new recording or after loading recording data from a thumbnail.
     * This means we already have data from this recording and _isLoaded should already be true!
     * 
     */
    private void OnReplayablesSpawned(object o, EventArgs e)
    {
        // get the max number of frames
        foreach (var entry in recording.recordableDataDict)
        {
            if (_frameNr < entry.Value.dataFrames.Count)
            {
                _frameNr = entry.Value.dataFrames.Count;
            }
        }
        // _isLoaded = true;
        Debug.Log("Replayer: OnReplayablesSpawned(): max frames: " + _frameNr);
        onReplaySpawned?.Invoke(this, _replayablesDict);
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
        onReplaySpawned?.Invoke(this, _replayablesDict);
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
    private void OnRecordingUndo(object o, List<Guid> undoIds)
    {
        foreach (var id in undoIds)
        {
            _replayablesDict.Remove(id);
        }
    }

    private void OnRecordingRedo(object o, List<Replayable> redoReplayables)
    {
        foreach (var r in redoReplayables)
        {
            _replayablesDict.Add(r.replayableId, r);
        }
    }
    
    private void OnDestroy()
    {
        _recorder.onRecordingStart -= OnRecordingStart;
        _recorder.onRecordingStop -= OnRecordingStop;
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
