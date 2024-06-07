using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ubiq.Geometry;
using Ubiq.Messaging;
using Ubiq.Spawning;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
/*
 * The Replayer is responsible for managing the replaying of recorded data.
 * It can load a recording from a folder, start and stop the replay, and delete the replay.
 * The Replayer also manages the spawning of the objects that are being replayed.
 * The Replayer is also responsible for cleaning up the spawned objects after the replay is done.
 * The Replayer also listens to the Recorder for when a recording is of a replay is done, so it can then safely delete the object.
 */
[RequireComponent(typeof(Recorder))]
public class Replayer : MonoBehaviour
{
    private Recorder _recorder;
    private Dictionary<NetworkId ,Recordable.RecordableData> _recordableDataDict; // string is identifier (mostly this will be the network id)
    private string _recordingFolder; // path to the folder of a specific recording (folder has all the motion files, etc.)
    private bool _isLoaded;
    private bool _isPlaying;
    private bool _fromThumbnail; // true if we have created a replay from the thumbnail (so we don't need to create it again for replay)
    
    private NetworkSpawnManager _spawnManager;
    private Dictionary<string, GameObject> _prefabCatalogue; 
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private int _counter; // for counting the number of spawned objects that have requested deletion
    
    public UnityEvent onReplayCreated; // replay created
    public UnityEvent onReplayStart; // start replaying
    public UnityEvent onReplayStop; // stop replaying
    public UnityEvent onReplayDeleted; // replay deleted
    public UnityEvent<Recorder.ThumbnailData> onThumbnailCreated;
    
    private Recorder.ThumbnailData _thumbnailData;
    
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
        
        _recordableDataDict = new Dictionary<NetworkId, Recordable.RecordableData>();
        
        LoadMostRecentReplayOnStartup();
    }
    
    // this gets called by the recorder when the recording is done and the object's recording is saved
    // once the recording is saved we can safely delete the object if it was a replayed object
    // this is always called when an object is done saving the recording
    private void RemoveReplayedRecordedObject(GameObject go)
    {
        if (_spawnedObjects.Contains(go))
        {
            // instead of despawning the objects, we just decrement the _counter
            _counter++;
            // if we don't want to create a thumbnail, we can just uncomment this
            // _spawnManager.Despawn(go);
            // _spawnedObjects.Remove(go);
        }
        // Debug.Log("Spawned objects left: " + _spawnedObjects.Count);
        // if no more spawned objects are left we can load the recording we just did
        // NOTE for thumbnails, we don't want to despawn the objects but reuse them.
        if (_spawnedObjects.Count == _counter)
        {
            _counter = 0;
            // we set this to true so we can recycle the existing spawned objects
            _fromThumbnail = true;
            // if we don't want to create a thumbnail, we can just comment this out and do LoadReplay(folder) only
            CreateThumbnail(_recordingFolder); 
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
        _fromThumbnail = false;
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
        if (_isLoaded) return; // don't need to load again when we already have a replay loaded
      
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
        // the next replay gets loaded when all recordables have saved their data and been deleted
    }

    private Replayable SpawnReplayableObject(string prefabName)
    {
        var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[prefabName]);
        _spawnedObjects.Add(go);
                    
        var replayable = go.GetComponent<Replayable>();
        // this helps to distinguish local and remote replayables. someone could replay their own data while
        // at the same time receiving a replay from somebody else.
        replayable.isLocal = true;

        return replayable;
    }

    private void RecyclePreviousThumbnail(Recorder.ThumbnailData current, Recorder.ThumbnailData previous)
    {
        var newSpawnedObjects = new List<GameObject>();
        for (var i = 0; i < current.recordablePrefabs.Count; i++)
        {
            Debug.Log("Checking for match: " + current.recordablePrefabs[i] + "(Clone)");
            var match = _spawnedObjects.Find(go => go.name == current.recordablePrefabs[i] + "(Clone)");
            Debug.Log(match);
            // if we found a matching game object from the previous thumbnail we reuse it and add it to our list
            // then we remove it from the old list of spawned objects because we only want to keep objects in the list that are unused
            // so we can delete them later
            if (match != null)
            {
                Debug.Log("Found match for: " + current.recordablePrefabs[i]);
                newSpawnedObjects.Add(match);
                _spawnedObjects.Remove(match);
            }
            else // if we cannot reuse a previous object we need to spawn a new one.
            {
                Debug.Log("no match for: " + current.recordablePrefabs[i] + " creating new object!");
                var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[current.recordablePrefabs[i]]);
                newSpawnedObjects.Add(go);
                match = go;
            }
            var replayable = match.GetComponent<Replayable>();
            replayable.isLocal = true; // helps to distinguish local from remote replayables
            var firstPose = new Recordable.RecordableData
            {
                recordableData = new List<Recordable.RecordableDataFrame> {current.firstPoses[i]}
            };
            replayable.SetReplayableData(firstPose, true);
        }
        // when we have either reused or created the new objects, we need to make sure
        // to delete remaining objects from previous thumbnails that we are not using anymore.
        CleanupReplay(); // will delete the remaining objects in _spawnedObjects
        
        _spawnedObjects = newSpawnedObjects; // then we can assign our new list of spawned objects
    }
    
    // we load thumbnail data from the file and create the replayable objects in their starting pose
    // to make it a bit more efficient, when people flip through the thumbnails, we can leave existing objects and just change their pose
    // NOTE a thumbnail could be created by flipping through the recordings even when a replay is already loaded
    // we therefore have to make that in case a replay is loaded, we ignore this and set _isLoaded back to false
    public void CreateThumbnail(string folder)
    {
        _isLoaded = false;
        // make sure file exists, but it should if the folder exists
        if (File.Exists(Path.Join(folder, "thumbnail.txt")))
        {
            using (StreamReader sr = new StreamReader(Path.Join(folder, "thumbnail.txt")))
            {
                var thumbnailDataJson = sr.ReadToEnd();
                
                // if we already have a previous thumbnail loaded, let's see if we can reuse the objects
                var newThumbnailData = JsonUtility.FromJson<Recorder.ThumbnailData>(thumbnailDataJson);

                if (_fromThumbnail)
                {
                    // if previous thumbnail is loaded we try to recycle the objects
                    RecyclePreviousThumbnail(newThumbnailData, _thumbnailData);
                }
                else // otherwise, we just create all the new objects again
                {
                    Debug.Log("Thumbnail: Create all objects from scratch!");
                    for (var i = 0; i < newThumbnailData.recordableIds.Count; i++)
                    {
                        var prefabName = newThumbnailData.recordablePrefabs[i];
                        var replayable = SpawnReplayableObject(prefabName);
                        Debug.Log(_spawnedObjects.Count);
                        
                        // instead of setting motion data etc. we only set the first pose from the thumbnail
                        var firstPose = new Recordable.RecordableData
                        {
                            recordableData = new List<Recordable.RecordableDataFrame> {newThumbnailData.firstPoses[i]}
                        };
                        replayable.SetReplayableData(firstPose, true);
                    }
                }
                _thumbnailData = newThumbnailData;
            }
            _fromThumbnail = true; // we don't need to create the replayable objects again
            Debug.Log("Replayables created from thumbnail!");
            onThumbnailCreated.Invoke(_thumbnailData);
        }
        else
        {
            Debug.LogError("Thumbnail file does not exist!");
        }
    }
    
    // possible to create a replay without the movement data just from the thumbnail
    public void CreateReplay()
    {
        if (!_fromThumbnail)
        {   
            // if we haven't created the replayables from the thumbnail, then we have loaded the data already
            // and only need to add it to the objects once they are spawned
            foreach (var record in _recordableDataDict.Values)
            {
                var prefabName = record.metaData[record.metaDataLabels.IndexOf("prefab")];
                var replayable = SpawnReplayableObject(prefabName);
                replayable.SetReplayableData(record);
            }
        }
        else // otherwise, we already spawned the objects from the thumbnail and now need to add the recorded data
        {
            for (var i = 0; i < _thumbnailData.recordableIds.Count; i++)
            {
                // the spawned objects will be in the order given by the lists in the thumbnail data
                // now we only need to know order of ids in the replayable data and fetch the motion data
                var id = new NetworkId(_thumbnailData.recordableIds[i]);
                var record = _recordableDataDict[id];
                _spawnedObjects[i].GetComponent<Replayable>().SetReplayableData(record);
            }
        }
        Debug.Log("Replay created!");
    }

    private IEnumerator LoadRecordedDataFromFolder(string folder)
    {
        Debug.Log("Load replay from folder!");
        
        // get all motion files
        var files = Directory.GetFiles(folder, "m*.txt");
        
        _recordableDataDict.Clear();
        foreach (var file in files)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                var recordableDataJson = sr.ReadToEnd();
                
                Recordable.RecordableData recordableData = JsonUtility.FromJson<Recordable.RecordableData>(recordableDataJson);
                _recordableDataDict.Add(new NetworkId(recordableData.metaData[0]), recordableData);
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
        
        // don't load anything if the directory is empty
        if (directories.Length == 0) return;
        
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
        
        // load thumbnail data too
        using (StreamReader sr = new StreamReader(Path.Join(Application.persistentDataPath, lastRecording + "/thumbnail.txt")))
        {
            var thumbnailDataJson = sr.ReadToEnd();
            _thumbnailData = JsonUtility.FromJson<Recorder.ThumbnailData>(thumbnailDataJson);
            // strictly speaking, we did not load the replay from the thumbnail
            // but we want to have this thumbnail for potential next thumbnails and for the UI info
            _fromThumbnail = true;
            onThumbnailCreated.Invoke(_thumbnailData);
        }
    }
    
}
// [CustomEditor(typeof(Replayer))]
// public class ReplayerEditor : UnityEditor.Editor
// {
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();
//         if (!Application.isPlaying) return;
//         
//         Replayer replayer = (Replayer)target;
//         
//         if (GUILayout.Button("Start Replay"))
//         {
//             replayer.StartReplay();
//         }
//         if (GUILayout.Button("Stop Replay"))
//         {
//             replayer.StopReplay();
//         }
//         if (GUILayout.Button("Delete Replay"))
//         {
//             replayer.DeleteReplay();
//         }
//     }
// }
