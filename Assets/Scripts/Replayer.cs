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
    private string _filePath; // file path to the saved data of the last recording
    private List<Recordable.RecordableData> _recordableDataList;
    private string _savePath; // get it from recorder
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
        _recorder.onRecordingStop.AddListener(OnRecordingStop);
        _recorder.onRecordingSaved.AddListener(OnRecordingSaved);
        _savePath = _recorder.GetSavePath();
        
        _spawnManager = NetworkSpawnManager.Find(this);
        _prefabCatalogue = new Dictionary<string, GameObject>();
        foreach (var prefab in _spawnManager.catalogue.prefabs)
        {
            _prefabCatalogue.Add(prefab.name, prefab);
        }
            
    }
    
    // a replay has to be loaded for this to work
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
    
    // a replay has to be loaded, can be playing or paused
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
    
    public void LoadReplay(string filePath = null)
    {   
        Debug.Log( filePath == null ? "Load last recording!" : "Load recording from" + filePath);
        // if no file path is given we do not load a recording from file but use the data from the last recording
        if (filePath == null)
        {
           CreateReplay();
        }
        else
        {
            StartCoroutine(LoadRecordedDataFromFile(filePath));
        }
        onReplayCreated.Invoke();
        _isLoaded = true;
    }
    
    // when we are recording a previous replay and the recording stops we automatically delete the current replay
    // this gets called right after a save file is created but before the newly recorded data is written to the file
    // once the data is written to file, the new recording is loaded as the new replay
    // this should be fine as we have already deleted the old replay
    private void OnRecordingStop()
    {
        _filePath = _recorder.GetPathLastRecording();
        if (_isPlaying)
        {
            DeleteReplay();
        }
    }
    
    private void OnRecordingSaved(List<Recordable.RecordableData> recordableDataList)
    {
        _recordableDataList = recordableDataList;
        LoadReplay();
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

    private IEnumerator LoadRecordedDataFromFile(string filePath)
    {
        Debug.Log("Load replay from file!");
        using StreamReader sr = new StreamReader(Path.Join(_savePath, filePath));
        
        string recordableDataJson;
        _recordableDataList.Clear(); // clear the list before loading new data
        while((recordableDataJson = sr.ReadLine()) != null)
        {
            Recordable.RecordableData recordableData = JsonUtility.FromJson<Recordable.RecordableData>(recordableDataJson);
            _recordableDataList.Add(recordableData);
        }
        CreateReplay();
        yield return null;
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
