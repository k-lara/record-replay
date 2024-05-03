using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ubiq.Geometry;
using Ubiq.Spawning;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Recorder))]
public class Replayer : MonoBehaviour
{
    private Recorder _recorder;
    private string _filePath; // file path to the saved data of the last recording
    private List<Recordable.RecordableData> _recordableDataList;
    private string _savePath; // get it from recorder
    
    private NetworkSpawnManager _spawnManager;
    private Dictionary<string, GameObject> _prefabCatalogue; 
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    
    public UnityEvent onReplayStart;
    public UnityEvent onReplayStop;
    public UnityEvent onReplayCreated;
    public UnityEvent onReplayDeleted;
    
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

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartReplay()
    {
        onReplayStart.Invoke();
    }
    
    public void StopReplay()
    {
        onReplayStop.Invoke();
    }
    
    public void LoadReplay(string filePath = null)
    {   
        // if no file path is given we do not load a recording from file but use the data from the last recording
        if (filePath == null)
        {
           CreateReplay();
        }
        else
        {
            StartCoroutine(LoadRecordedDataFromFile(filePath));
        }
    }

    private void OnRecordingStop()
    {
        _filePath = _recorder.GetPathLastRecording();
    }
    
    private void OnRecordingSaved(List<Recordable.RecordableData> recordableDataList)
    {
        _recordableDataList = recordableDataList;
    }

    public void CreateReplay()
    {
        foreach (var record in _recordableDataList)
        {
            // spawn prefab based on name stored in recordable data
            var prefabName = record.metaData[record.metaDataLabels.IndexOf("prefab")];
            var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[prefabName]);
            _spawnedObjects.Add(go);
            
            var replayable = go.GetComponent<Replayable>();
            replayable.SetReplayableData(record);
        }
    }

    private IEnumerator LoadRecordedDataFromFile(string filePath)
    {
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
    
    private void Replay()
    {
        
    }
}
