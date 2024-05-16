using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ubiq.Geometry;
using Ubiq.Spawning;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Recorder))]
public class Replayer : MonoBehaviour
{
    private Recorder _recorder;
    private List<Recordable.RecordableData> _recordableDataList;
    private string _recordingFolder; // path to the folder of a specific recording (folder has all the motion files, etc.)
    private bool _isLoaded;
    private bool _isPlaying;
    
    private NetworkSpawnManager _spawnManager;
    private Dictionary<string, GameObject> _prefabCatalogue; 
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    
    public UnityEvent onReplayCreated; // replay created
    public UnityEvent onReplayStart; // start replaying
    public UnityEvent onReplayStop; // stop replaying
    public UnityEvent onReplayDeleted; // replay deleted
    
    // Start is called before the first frame update
    void Start()
    {
        _recorder = GetComponent<Recorder>();
        _recorder.onRecordingStart.AddListener(StartReplay);
        _recorder.onRecordingStop.AddListener(OnRecordingStop);
        _recorder.onRemoveReplayedObject.AddListener(RemoveReplayedRecordedObject);
        
        _spawnManager = NetworkSpawnManager.Find(this);
        _prefabCatalogue = new Dictionary<string, GameObject>();
        foreach (var prefab in _spawnManager.catalogue.prefabs)
        {
            _prefabCatalogue.Add(prefab.name, prefab);
        }
        
        _recordableDataList = new List<Recordable.RecordableData>();
        
        LoadMostRecentReplayOnStartup();
    }
    
    // this gets called by the recorder when the recording is done and the object's recording is saved
    // once the recording is saved we can safely delete the object if it was a replayed object
    // this is always called when an object is done saving the recording
    private void RemoveReplayedRecordedObject(GameObject go)
    {
        if (_spawnedObjects.Contains(go))
        {
            _spawnManager.Despawn(go);
            _spawnedObjects.Remove(go);
        }
        Debug.Log("Spawned objects left: " + _spawnedObjects.Count);
        
        // if no more spawned objects are left we can load the recording we just did
        if (_spawnedObjects.Count == 0)
        {
            LoadReplay(_recordingFolder);
        }
    }

    // a replay has to be loaded for this to work
    // if we start a recording and a replay is loaded, we record over the loaded replay
    public void StartReplay()
    {
        if (!_isLoaded) return;
        Debug.Log("Start replay");
        _isPlaying = true;
        onReplayStart.Invoke();
    }
    
    // a replay has to be loaded and playing for this to work
    public void StopReplay()
    {
        if (!_isLoaded || !_isPlaying) return;
        Debug.Log("Stop replay!");
        _isPlaying = false;
        onReplayStop.Invoke();
    }
    
    // a replay has to be loaded (it can be playing or paused)
    public void DeleteReplay()
    {
        StopReplay();
        onReplayDeleted.Invoke();
        CleanupReplay();
        _isLoaded = false;
        Debug.Log("Replay deleted!");
    }

    private void CleanupReplay()
    {
        for(var i = 0; i < _spawnedObjects.Count; i++)
        {
            _spawnManager.Despawn(_spawnedObjects[i]);
        }
        _spawnedObjects.Clear();
    }
    
    public void LoadReplay(string folder = null)
    {   
        Debug.Log( folder == null ? "Load last recording!" : "Load recording from" + folder);
        // if no file path is given we do not load a recording from file but use the data from the last recording
        if (folder == null)
        {
           CreateReplay();
        }
        else
        {
            StartCoroutine(LoadRecordedDataFromFolder(folder));
        }
        onReplayCreated.Invoke();
        _isLoaded = true;
    }
    private void OnRecordingStop(string folder)
    {
        _recordingFolder = folder;
        // we also stop the replay when the recording is stopped
        StopReplay();
    }

    public void CreateReplay()
    {
        foreach (var record in _recordableDataList)
        {
            // spawn prefab based on name stored in recordable data
            // this does not work because the name is the network id of the avatar not the prefab name
            var prefabName = record.metaData[record.metaDataLabels.IndexOf("prefab")];
            var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[prefabName]);
            _spawnedObjects.Add(go);
            
            var replayable = go.GetComponent<Replayable>();
            replayable.SetReplayableData(record);
            
        }
        Debug.Log("Replay created!");
    }

    private IEnumerator LoadRecordedDataFromFolder(string folder)
    {
        Debug.Log("Load replay from folder!");
        
        // get all motion files
        var files = Directory.GetFiles(folder, "m*.txt");
        
        _recordableDataList.Clear();
        foreach (var file in files)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                var recordableDataJson = sr.ReadToEnd();
                
                Recordable.RecordableData recordableData = JsonUtility.FromJson<Recordable.RecordableData>(recordableDataJson);
                _recordableDataList.Add(recordableData);
            }
        }
        CreateReplay();
        yield return null;
    }
    
    // on startup automatically load the most recent replay!
    private void LoadMostRecentReplayOnStartup()
    {
        var directories = Directory.GetDirectories(Application.persistentDataPath);
        DateTime lastWriteTime = DateTime.MinValue;
        for (var i = 0; i < directories.Length; i++)
        {
            var recName = new DirectoryInfo(directories[i]).Name;
            Debug.Log(recName);
            var dateTime = DateTime.ParseExact(recName, "yyyy-MM-dd_HH-mm-ss", null);

            if (dateTime > lastWriteTime)
                lastWriteTime = dateTime;
        }
        // once we've found the last recording, we can load it
        var lastRecording = lastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss");
        
        LoadReplay(Path.Join(Application.persistentDataPath, lastRecording));
    }
    
}
[CustomEditor(typeof(Replayer))]
public class ReplayerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        
        Replayer replayer = (Replayer)target;
        
        if (GUILayout.Button("Start Replay"))
        {
            replayer.StartReplay();
        }
        if (GUILayout.Button("Stop Replay"))
        {
            replayer.StopReplay();
        }
        if (GUILayout.Button("Delete Replay"))
        {
            replayer.DeleteReplay();
        }
    }
}
