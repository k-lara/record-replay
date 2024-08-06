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
    
    // as long as the recorder has not recorded anything there is no recording
    // also, the recording is not loaded until the user decides to load it
    public Recording recording {get; private set;}
    private NetworkSpawnManager _spawnManager;

    private Dictionary<string, GameObject> _prefabCatalogue;
    // recorded data for all avatars in the scene
    // replayables ordered based on their network id
    private Dictionary<Guid, Replayable> _replayablesDict;
    
    private bool _isCreated;
    private bool _isPlaying;
    public float currentFrame { get; set; } // could be between two frames
    public int frameOffset { get; set; }

    private int _frameNr;
    private float _deltaTime;
    private string _currentRecordingPath;
    
    public event EventHandler onReplayStart; // start replaying
    public event EventHandler onReplayStop; // stop replaying
    public event EventHandler onReplayCreated; // replay loaded
    public event EventHandler onReplayDeleted; // replay deleted
    
    
    // Start is called before the first frame update
    void Start()
    {
        _recorder = GetComponent<Recorder>();
        _recorder.onRecordingStart += OnRecordingStart;
        _recorder.onRecordingStop += OnRecordingStop;
        
        _spawnManager = NetworkSpawnManager.Find(this);

        _replayablesDict = new Dictionary<Guid, Replayable>();
        _prefabCatalogue = new Dictionary<string, GameObject>();
        foreach (var prefab in _spawnManager.catalogue.prefabs)
        {
            _prefabCatalogue.Add(prefab.name, prefab);
        }

    }
    
    void Update()
    {
        if (!_isCreated) return;
        if (_isPlaying)
        {
            _deltaTime += Time.deltaTime;
            // current frame is a float, so we know between which two frames we are
            currentFrame = _deltaTime * _recorder.fps + frameOffset;

            if (currentFrame >= _frameNr - 1)
            {
                currentFrame = 0.0f;
                _deltaTime = 0.0f;
                StopReplay();
            }
        }
    }

    public void StartReplay()
    {
        if (!_isCreated) return; // can't start if we don't have the spawned avatars
        
        Debug.Log("Start replay!");
        
        if(_isPlaying)
            _deltaTime = 0.0f;
        
        _isPlaying = true;
        onReplayStart?.Invoke(this, EventArgs.Empty);
    }

    public void StopReplay()
    {
        if (!_isPlaying) return; // no point in stopping if we are not playing
        
        Debug.Log("Stop Replay!");
        _isPlaying = false;
        onReplayStop?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecordingStart(object o, EventArgs e)
    {
        recording = _recorder.recording;
        
        if (!_isCreated) return;
        if (_isPlaying) return;
        
        // if we have stopped a replay pressing start will resume it
        StartReplay();
    }
    
    private void OnRecordingStop(object o, EventArgs e)
    {
        // when we stop recording, do we want to take off a few frames before we stopped
        // OR do we want to start from the beginning???
        // could depend on whether we are taking over an avatar or creating a new one
        // in that case we start from beginning per default
        StopReplay();
        _deltaTime = 0.0f;
        StartCoroutine(SpawnReplayableObjects());
    }
    

    private IEnumerator SpawnReplayableObjects()
    {
        Debug.Log("Spawn replayable objects!");
        foreach (var entry in recording.recordableDataDict)
        {
            if (_replayablesDict.ContainsKey(entry.Key)) continue;
            
            var frames = entry.Value.dataFrames.Count;
            if (frames > _frameNr)
            {
                _frameNr = frames;
            }
            Debug.Log(entry.Value.prefabName);
            var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[entry.Value.prefabName]);
            // var replayable = go.AddComponent<Replayable>(); // player doesn't need this, so add it here
            var replayable = go.GetComponent<Replayable>();
            replayable.replayableId = entry.Key;
            replayable.isLocal = true;
            _replayablesDict.Add(entry.Key, replayable);
            Debug.Log("Spawned new replayable (id: " + entry.Key + ")");
        }

        _isCreated = true;
        onReplayCreated?.Invoke(this, EventArgs.Empty);
        
        yield return null;
        
    }

    private void DeleteReplay()
    {
        _frameNr = 0;
        _deltaTime = 0.0f;
        StartCoroutine(DespawnReplayableObjects());
    }

    private IEnumerator DespawnReplayableObjects()
    {
        foreach(var replayable in _replayablesDict.Values)
        {
            _spawnManager.Despawn(replayable.gameObject);
        }
        _replayablesDict.Clear();
        _isCreated = false;
        onReplayDeleted?.Invoke(this, EventArgs.Empty);
        yield return null;
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

    private void OnDestroy()
    {
        _recorder.onRecordingStart -= OnRecordingStart;
        _recorder.onRecordingStop -= OnRecordingStop;
    }
}

[CustomEditor(typeof(Replayer))]
public class ReplayerEditor : Editor
{
    private SerializedProperty _recordingFolder;
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        
        Replayer replayer = (Replayer)target;
        _recordingFolder = serializedObject.FindProperty("_recordingFolder");
        
        
        if (GUILayout.Button("Start Replay"))
        {
            serializedObject.Update();
            replayer.StartReplay();
        }
        if (GUILayout.Button("Stop Replay"))
        {
            replayer.StopReplay();
        }
        // if (GUILayout.Button("Delete Replay"))
        // {
        //     replayer.DeleteReplay();
        // }
        
        // if (GUILayout.Button("Load Replay"))
        // {
        //     serializedObject.Update();
        // }
    }
}
