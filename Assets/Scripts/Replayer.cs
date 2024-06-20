using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ubiq.Avatars;
using Ubiq.Geometry;
using Ubiq.Messaging;
using Ubiq.Spawning;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
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
    private AvatarManager _avatarManager;
    private Dictionary<NetworkId ,Recordable.RecordableData> _recordableDataDict; // string is identifier (mostly this will be the network id)
    // only serialize it for access in custom inspector
    [SerializeField] private string _recordingFolder; // path to the folder of a specific recording (folder has all the motion files, etc.)
    private bool _isLoaded;
    private bool _isPlaying;
    private bool _fromThumbnail; // true if we have created a replay from the thumbnail (so we don't need to create it again for replay)
    
    private NetworkSpawnManager _spawnManager;
    private Dictionary<string, GameObject> _prefabCatalogue; 
    private List<GameObject> _spawnedObjects = new List<GameObject>();
    private int _counter; // for counting the number of spawned objects that have requested deletion
    
    public event EventHandler<string> onReplayCreated; // replay created
    public event EventHandler onReplayStart; // start replaying
    public event EventHandler onReplayStop; // stop replaying
    public event EventHandler onReplayDeleted; // replay deleted
    public event EventHandler<Recorder.ThumbnailData> onThumbnailCreated;
    
    private Recorder.ThumbnailData _thumbnailData;

    public List<GameObject> GetSpawnedObjectsList()
    {
        return _spawnedObjects;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        _recorder = GetComponent<Recorder>();
        _recorder.onRecordingStart += StartReplay;
        _recorder.onRecordingStop += OnRecordingStop;
        _recorder.onRemoveReplayedObject += RemoveReplayedRecordedObject;
        
        _avatarManager = AvatarManager.Find(this);
        
        _spawnManager = NetworkSpawnManager.Find(this);
        _prefabCatalogue = new Dictionary<string, GameObject>();
        foreach (var prefab in _spawnManager.catalogue.prefabs)
        {
            _prefabCatalogue.Add(prefab.name, prefab);
        }
        
        _recordableDataDict = new Dictionary<NetworkId, Recordable.RecordableData>();
        
        LoadMostRecentReplayOnStartup();
    }

    private void OnDestroy()
    {
        // remove all listeners
        _recorder.onRecordingStart -= StartReplay;
        _recorder.onRecordingStop -= OnRecordingStop;
        _recorder.onRemoveReplayedObject -= RemoveReplayedRecordedObject;
    }

    // this gets called by the recorder when the recording is done and the object's recording is saved
    // once the recording is saved we can safely delete the object if it was a replayed object
    // this is always called when an object is done saving the recording
    // this gets also called by the peers, who do not need their avatar removed
    // we still count the peers so we can be sure that everyone is done saving the data before we load the replay
    private void RemoveReplayedRecordedObject(object o, GameObject go)
    {
        _counter++;
        
        if (_spawnedObjects.Count + _avatarManager.Avatars.Count() == _counter)
        {
            _counter = 0;
            StartCoroutine(CreateThumbnailAndLoadReplay());
        }
    }

    public IEnumerator CreateThumbnailAndLoadReplay()
    {
        CreateThumbnail(_recordingFolder); 
        LoadReplay(_recordingFolder);
        yield return true;
    }

    // a replay has to be loaded for this to work
    // if we start a recording and a replay is loaded, we record over the loaded replay
    // folder is not needed here
    public void StartReplay(object o, string folder)
    {
        if (!_isLoaded) return;
        Debug.Log("Start replay");
        _isPlaying = true;
        onReplayStart?.Invoke(this, EventArgs.Empty);
    }
    
    // a replay has to be loaded and playing for this to work
    public void StopReplay()
    {
        if (!_isLoaded || !_isPlaying) return;
        Debug.Log("Stop replay!");
        _isPlaying = false;
        onReplayStop?.Invoke(this, EventArgs.Empty);
    }
    
    // a replay has to be loaded (it can be playing or paused)
    public void DeleteReplay()
    {
        StopReplay();
        onReplayDeleted?.Invoke(this, EventArgs.Empty);
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
    
    public void LoadReplay(string folder)
    {
        if (_isLoaded) return; // don't need to load again when we already have a replay loaded
        
        Debug.Log("Load replay from folder!");
        Task task = Task.Run(() => LoadRecordedDataFromFolder(folder));
        task.Wait();
        
        // cannot create game objects asynchronously. we do it in coroutine (which runs on the main thread)
        // to prevent potential freezing of the game.
        StartCoroutine(CreateReplay());
     
    }
    private void OnRecordingStop(object o, string folder)
    {
        _recordingFolder = folder;
        // we also stop the replay when the recording is stopped
        StopReplay();
        // the next replay gets loaded when all recordables have saved their data and been deleted
    }
    
    // providing the option to add the spawned object to the list of spawned objects
    // when we recycle objects we do not want to add newly spawned directly to the list because
    // we want to keep track of the objects that are not used anymore and can be deleted
    private Replayable SpawnReplayableObject(string prefabName, bool addToSpawnedObjects = true)
    {
        var go = _spawnManager.SpawnWithPeerScope(_prefabCatalogue[prefabName]);
        
        if (addToSpawnedObjects)
            _spawnedObjects.Add(go);
        
        // normal users don't have the AudioReplayable on their avatar
        // it is only used for replayed avatars
        Debug.Log("Add AudioReplayable");
        go.AddComponent<AudioReplayable>();
        
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

            Replayable rep;
            // if we found a matching game object from the previous thumbnail we reuse it and add it to our list
            // then we remove it from the old list of spawned objects because we only want to keep objects in the list that are unused
            // so we can delete them later
            if (match != null)
            {
                Debug.Log("Found match for: " + current.recordablePrefabs[i]);
                newSpawnedObjects.Add(match);
                _spawnedObjects.Remove(match);
                rep = match.GetComponent<Replayable>();
            }
            else // if we cannot reuse a previous object we need to spawn a new one.
            {
                Debug.Log("no match for: " + current.recordablePrefabs[i] + " creating new object!");
                rep = SpawnReplayableObject(current.recordablePrefabs[i], false);
                newSpawnedObjects.Add(rep.gameObject);
                match = rep.gameObject;
            }
            var firstPose = new Recordable.RecordableData
            {
                recordableData = new List<Recordable.RecordableDataFrame> {current.firstPoses[i]}
            };
            rep.SetReplayableData(firstPose, true);
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
        _recordingFolder = folder;
        // make sure file exists, but it should if the folder exists
        if (File.Exists(Path.Join(folder, "thumbnail.txt")))
        {
            using (StreamReader sr = new StreamReader(Path.Join(folder, "thumbnail.txt")))
            {
                var thumbnailDataJson = sr.ReadToEnd();
                
                // if we already have a previous thumbnail loaded, let's see if we can reuse the objects
                var newThumbnailData = JsonUtility.FromJson<Recorder.ThumbnailData>(thumbnailDataJson);

                // if previous thumbnail is loaded we try to recycle the objects
                RecyclePreviousThumbnail(newThumbnailData, _thumbnailData);
                
                // if (_fromThumbnail)
                // {
                // }
                // not sure if this is ever called... might remove this
                // else // otherwise, we just create all the new objects again
                // {
                //     Debug.Log("Thumbnail: Create all objects from scratch!");
                //     for (var i = 0; i < newThumbnailData.recordableIds.Count; i++)
                //     {
                //         var prefabName = newThumbnailData.recordablePrefabs[i];
                //         var replayable = SpawnReplayableObject(prefabName);
                //         Debug.Log(_spawnedObjects.Count);
                //         
                //         // instead of setting motion data etc. we only set the first pose from the thumbnail
                //         var firstPose = new Recordable.RecordableData
                //         {
                //             recordableData = new List<Recordable.RecordableDataFrame> {newThumbnailData.firstPoses[i]}
                //         };
                //         replayable.SetReplayableData(firstPose, true);
                //     }
                // }
                _thumbnailData = newThumbnailData;
            }
            _fromThumbnail = true; // we don't need to create the replayable objects again
            Debug.Log("Replayables created from thumbnail!");
            onThumbnailCreated?.Invoke(this, _thumbnailData);
        }
        else
        {
            Debug.LogError("Thumbnail file does not exist!");
        }
    }
    
    // possible to create a replay without the movement data just from the thumbnail
    private IEnumerator CreateReplay()
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
        onReplayCreated?.Invoke(this, _recordingFolder);
        Debug.Log("Replay created!");
        _isLoaded = true;
        
        yield return null;
    }

    private async Task LoadRecordedDataFromFolder(string folder)
    {
        // get all motion files
        var files = Directory.GetFiles(folder, "m*.txt");
        
        _recordableDataDict.Clear();
        foreach (var file in files)
        {
            using (StreamReader sr = new StreamReader(file))
            {
                var recordableDataJson = await sr.ReadToEndAsync();
                
                Recordable.RecordableData recordableData = JsonUtility.FromJson<Recordable.RecordableData>(recordableDataJson);
                _recordableDataDict.Add(new NetworkId(recordableData.metaData[0]), recordableData);
            }
        }
    }
    
    // on startup automatically load the most recent replay!
    private void LoadMostRecentReplayOnStartup()
    {
        var directories = Directory.GetDirectories(Application.persistentDataPath);
        DateTime lastWriteTime = DateTime.MinValue;
        
        // don't load anything if the directory is empty
        if (directories.Length == 0) return;
        
        // find the most recent recording
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
        
        _recordingFolder = Path.Join(Application.persistentDataPath, lastRecording);

        StartCoroutine(CreateThumbnailAndLoadReplay());
        
        // LoadReplay(_recordingFolder);
        // load thumbnail data too
        // using (StreamReader sr = new StreamReader(Path.Join(Application.persistentDataPath, lastRecording + "/thumbnail.txt")))
        // {
        //     var thumbnailDataJson = sr.ReadToEnd();
        //     _thumbnailData = JsonUtility.FromJson<Recorder.ThumbnailData>(thumbnailDataJson);
        //     // strictly speaking, we did not load the replay from the thumbnail
        //     // but we want to have this thumbnail for potential next thumbnails and for the UI info
        //     _fromThumbnail = true;
        //     onThumbnailCreated.Invoke(_thumbnailData);
        // }
        Debug.Log("Loaded most recent replay on startup!");
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
            replayer.StartReplay(this, _recordingFolder.stringValue);
        }
        if (GUILayout.Button("Stop Replay"))
        {
            replayer.StopReplay();
        }
        if (GUILayout.Button("Delete Replay"))
        {
            replayer.DeleteReplay();
        }
        
        if (GUILayout.Button("Load Replay"))
        {
            serializedObject.Update();
            replayer.CreateThumbnail(_recordingFolder.stringValue);
            replayer.LoadReplay(_recordingFolder.stringValue);
        }
    }
}
