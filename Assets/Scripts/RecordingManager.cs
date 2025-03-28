using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ubiq.Spawning;
using UnityEngine;
using UnityEngine.PlayerLoop;

/*
 * The RecordingManager handles saving and loading of recordings and the thumbnails that are shown for each recording.
 */
public class RecordingManager : MonoBehaviour
{
    /*
     * folder hierarchy:
     *
     * persistentDataPath
     *     - [recordingID]
     *          -[date-time]_save
         *          - thumbnail.txt
         *          - [recordableID]_motion.txt
         *          - [recordableID]_audio.txt
     */
    public string pathToRecordings { get; set; } // the path to the directory that has all the recording folders
    public int poolCapacity; // the capacity of the recordable pool
    public int listCapacity; // the capacity of the recordable lists
    public int undoSaves; // the number of undo saves to keep
    public int backupSaves;

    // for user study
    // there are base recordings to which all the participants have to react to
    // each participant's recordings are saved in a separate folder
    // the base recordings have to be copied over to each participant's folder
    // the base recording will be in the root folder together with the participant folders
    public bool hasBaseRecordings;
    
    private RecordableListPool listPool;
    private UndoManager undoManager;
    private LoadManager loadManager; // handles loading a recording and thumbnails from disc
    private SaveManager saveManager; // handles saving recordings to disc
    private bool saveInProgress;

    public Recording Recording { get; private set; } // the current/prospective recording in memory (has recording id)
    private List<Recording.ThumbnailData> recordingThumbnails = new(); // of most recent save
    private int currentThumbnailIndex;
    private List<GameObject> spawnedObjects = new(); // the spawned prefabs for the current thumbnail
    
    private Dictionary<string, GameObject> prefabCatalogue { get; set; }
    private NetworkSpawnManager spawnManager;
    
    private Task<List<Recording.ThumbnailData>> thumbnailsTask;
    private Task loadTask;
    
    public event EventHandler<List<GameObject> > onThumbnailSpawned;
    
    // this is not a great name, because it is not quite clear what we do here
    // this is also called after having loaded a recording when replayables have already been spawned...
    public event EventHandler onReplayablesSpawnedAndLoaded;
    public event EventHandler onUnspawned;
    public event EventHandler<(UndoManager.UndoType, List<Guid>)> onRecordingUndo;
    public event EventHandler<(UndoManager.UndoType, List<Replayable>)> onRecordingRedo;
    public event EventHandler onRecordingSaved; // TODO WHEN TO DO THAT IN CODE? AFTER RECORDING STOPS? BUT NOT ALWAYS!
    public event EventHandler onRecordingLoaded;
    public event EventHandler onRecordingUnloaded;

    public void OnDestroy()
    {
        Debug.Log("RecordingManager OnDestroy()");
        // finish tasks if they are still running
        if (loadTask != null)
        {
            Debug.Log("Disposing load task");
            loadTask.Dispose();
        }
        if (thumbnailsTask != null)
        {
            Debug.Log("Disposing thumbnails task");
            thumbnailsTask.Dispose();
        }
        
        saveManager.Dispose(); // disposes save task
    }

    /*
     * Creates a new recording with new guid and empty data dict.
     * also clears the current thumbnail as no data is loaded.
     */
    void Start()
    {
        spawnManager = NetworkSpawnManager.Find(this);
        prefabCatalogue = new Dictionary<string, GameObject>();
        foreach (var prefab in spawnManager.catalogue.prefabs)
        {
            Debug.Log("Add: " + prefab.name);
            prefabCatalogue.Add(prefab.name, prefab);
        }

        if (!hasBaseRecordings) // do everything as usual if we don't have base recordings
        {
            pathToRecordings = Application.persistentDataPath;
        }
        else
        {
            pathToRecordings = Application.persistentDataPath + "/" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var newDirInfo = new DirectoryInfo(pathToRecordings);
            newDirInfo.Create();
            
            // copy base recordings to the new folder
            var baseDir = new DirectoryInfo(Application.persistentDataPath);
            // only get folders that are not a date time formated string but a guid
            var baseRecordings = baseDir.EnumerateDirectories().Where(d => Guid.TryParse(d.Name, out _)).ToList();

            foreach (var baseRecording in baseRecordings)
            {
                newDirInfo.CreateSubdirectory(baseRecording.Name);
                var newPath = Path.Combine(newDirInfo.FullName, baseRecording.Name);
                foreach (var file in baseRecording.EnumerateFiles())
                {
                    Debug.Log("Copy: " + file.Name + " to: " + newPath);
                    file.CopyTo(Path.Combine(newPath, file.Name));
                }
            }
        }
        
        loadManager = new LoadManager(spawnManager, prefabCatalogue, pathToRecordings);
        saveManager = new SaveManager(pathToRecordings, backupSaves);
        listPool = new RecordableListPool(poolCapacity, listCapacity);
        undoManager = new UndoManager(spawnManager, prefabCatalogue, listPool, undoSaves);

        Recording = new Recording(listPool);

        currentThumbnailIndex = -1;
        StartCoroutine(PrepareThumbnails());
        
    }
    void Update()
    {
        // autosave
        if (saveManager.ReadyToSave())
        {
            SaveRecording();
        }
    }
    public void InitNewRecording()
    {
        // listPool.Clear(); // is called in UnloadRecording() so we shouldn't need it here
        Recording = new Recording(listPool);
        Recording.InitNew();
        
        // ClearThumbnail();
    }

    public string GetRecordingInfo()
    {
        if (currentThumbnailIndex == -1) return "";
        return recordingThumbnails[currentThumbnailIndex].recordingId;
    }

    public int GetRecordingCount()
    {
        return recordingThumbnails.Count;
    }

    public int GetCurrentRecordingNumber()
    {
        if (currentThumbnailIndex == -1) return -1;
        
        return currentThumbnailIndex + 1; // because we want to start counting at 1 and not 0
    }
    
    // Start is called before the first frame update
    
    /*
     * if data is already loaded, we don't need to load it again
     * if there are no spawned objects, we won't load data
     * (spawnedObjects are created when we load from thumbnail or from a recording)
     * if currentThumbnailIndex is -1, we don't have a thumbnail to load from
     * this could be either when we created a new recording that isn't saved yet and has no thumbnail
     * or when we cleared the recording and thumbnail and the scene is empty
     *
     * Load a recording given an existing thumbnail
     */
    public void LoadRecording()
    {
        Debug.Log("data loaded: " + Recording.flags.DataLoaded + " spawned objects: " + spawnedObjects.Count + " current thumbnail: " + currentThumbnailIndex);
        // don't load if data is already loaded, or there are no spawned objects or there are no thumbnails that we could load from
        if (Recording.flags.DataLoaded || spawnedObjects.Count == 0 || currentThumbnailIndex == -1) return;
        
        loadTask = Task.Run(() => loadManager.LoadRecordingData(listPool, Recording, recordingThumbnails[currentThumbnailIndex]));
        loadTask.Wait();
        Debug.Log("Recording loaded!");
        Recording.flags.DataLoaded = true;
        Debug.Log(Recording.ToString());
        Debug.Log(Recording.RecordingFlagsToString());
        onRecordingLoaded?.Invoke(this, EventArgs.Empty);
        onReplayablesSpawnedAndLoaded?.Invoke(this, EventArgs.Empty);
    }

    /*
     * Save the current recording to a file either on autosave after a predefined interval has passed or manually.
     * Only saves if there is new data available and the recording is not currently recording
     * and the recording is ready to be saved (meaning all new data has been added to the recording).
     */
    public void SaveRecording()
    {
        Debug.Log("save in progress: " + saveInProgress);
        if (saveInProgress) return;// to prevent manual save while autosave is in progress
        if (Recording.flags.NewDataAvailable && Recording.flags.SaveReady && !Recording.flags.IsRecording)
        {
            saveInProgress = true;
            var newThumbnailData = saveManager.SaveRecording(Recording);
            // check if we already have a thumbnail for this recording in the list, if yes update it
            if (currentThumbnailIndex >= 0)
            {
                recordingThumbnails[currentThumbnailIndex] = newThumbnailData;
            }
            else
            {
                recordingThumbnails.Add(newThumbnailData);
                currentThumbnailIndex = recordingThumbnails.Count - 1;
            }
            Debug.Log("Recording saved!");
            Recording.flags.NewDataAvailable = false;
            saveInProgress = false;
            onRecordingSaved?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Debug.Log("No new data to save!");
            saveManager.ResetSaveTimer(); // not sure if we should do this here as it means we have to wait for another interval to save again
        }
    }

    public void Undo()
    {
        // undoIds can be null if only edits and no respawning objects
        (UndoManager.UndoType, List<Guid>) undoInfo = undoManager.Undo(Recording, spawnedObjects);
        if (undoInfo.Item1 != UndoManager.UndoType.None)
        {
            onRecordingUndo?.Invoke(this, undoInfo);
        }
    }

    public void Redo()
    {
        // can be null if only edits and no respawning objects
        (UndoManager.UndoType, List<Replayable>) redoInfo = undoManager.Redo(Recording, spawnedObjects);
        if (redoInfo.Item1 != UndoManager.UndoType.None)
        {
            onRecordingRedo?.Invoke(this, redoInfo);
        }
    }

    public void AddUndoState(UndoManager.UndoType type, Guid id, Recording.RecordableData data)
    {
        undoManager.AddUndoState(type, id, data);
    }
    
    private IEnumerator PrepareThumbnails()
    {
        Debug.Log("Prepare thumbnails");
        thumbnailsTask = Task.Run(loadManager.LoadThumbnailData);
        thumbnailsTask.Wait();
        recordingThumbnails = thumbnailsTask.Result;
        
        if (!hasBaseRecordings) // if we have base recordings we don't want to load them at startup
        // but once the participant has read the instructions etc...
        {
            // only spawn objects if we have at least one thumbnail
            if (recordingThumbnails.Count > 0)
            {
                currentThumbnailIndex = recordingThumbnails.Count - 1; // load most recent one
                yield return CreateFromThumbnail(currentThumbnailIndex);
            }
        }
    }
    
    
    /*
     * This only creates the objects from the info in the thumbnail.
     * Data needs to be loaded separately.
     * The Replayer cannot start a replay without loaded data.
     */
    private IEnumerator CreateFromThumbnail(int index)
    {
        Debug.Log("Creating from thumbnail idx: " + index);
        var newObjects = loadManager.CreateFromThumbnail(recordingThumbnails[index], ref spawnedObjects);
        DespawnObjects(); // clear the current spawned objects
        spawnedObjects = newObjects;
        Debug.Log("spawned objects: " + spawnedObjects.Count);
        yield return null;
        onThumbnailSpawned?.Invoke(this, spawnedObjects);
    }
    
    public void SpawnReplayables(Dictionary<Guid, Replayable> replayablesDict)
    {
        Recording.flags.DataLoaded = true; // the fact that this is called means we just did a recording and have data
        onRecordingLoaded?.Invoke(this, EventArgs.Empty);
        StartCoroutine(CreateFromRecording(replayablesDict));
    }
    
    /*
     * This is different than CreateFromThumbnail
     * Here we already have data and need to create the corresponding objects for replaying.
     * This means the replayer needs to know that there will be something to replay!
     */
    private IEnumerator CreateFromRecording(Dictionary<Guid, Replayable> replayablesDict)
    {
        Debug.Log("Create from recording");
        var newSpawned = loadManager.CreateFromRecording(Recording, replayablesDict);
        // add newly spawned objects to the list of thumbnailObjects
        foreach (var go in newSpawned)
        {
            spawnedObjects.Add(go);
        }
        yield return null;
        onReplayablesSpawnedAndLoaded?.Invoke(this, EventArgs.Empty);
    }

    /*
     * Unload the current recording from memory.
     * This is called when we want to start a new recording, without any of the previous data.
     * This does not unspawn the spawned objects.
     */
    public void UnloadRecording()
    {
        if (!Recording.flags.DataLoaded) return;
        undoManager.Clear(); // clear the undo stack
        listPool.Clear();
        Recording.Clear();
        Recording.flags.DataLoaded = false;
        onRecordingUnloaded?.Invoke(this, EventArgs.Empty);
    }

    public void ClearThumbnail()
    {
        DespawnObjects();
        currentThumbnailIndex = -1;
        onUnspawned?.Invoke(this, EventArgs.Empty);
    }

    private void DespawnObjects()
    {
        foreach(var spawnedObject in spawnedObjects)
        {
            Debug.Log("Despawning: " + spawnedObject);
            spawnManager.Despawn(spawnedObject);
        }
        spawnedObjects.Clear();
    }
    
    /*
     * Load the next or previous thumbnail.
     * When none is loaded, we load the last one per default.
     * We don't load a thumbnail if the same one is already loaded.
     * @param value: 1 for next, -1 for previous
     */
    public Recording.ThumbnailData GotoAdjacentThumbnail(int value)
    {
        var sign = (int)Mathf.Sign(value);
        var previousIndex = currentThumbnailIndex;
        
        // if there are no thumbnails, we can't go anywhere
        if (recordingThumbnails.Count == 0) return null;

        if (currentThumbnailIndex == -1)
        {   
            // load the last one per default if none is loaded
            currentThumbnailIndex = recordingThumbnails.Count - 1; 
        }
        else
        {
            var newIndex = currentThumbnailIndex + sign;
            if (newIndex >= 0 && newIndex < recordingThumbnails.Count)
            {
                currentThumbnailIndex = newIndex;
            }
            else
            {
                if (sign == 1)
                    currentThumbnailIndex = 0;
                else
                    currentThumbnailIndex = recordingThumbnails.Count - 1;
            }
        }
        // we don't load the same thumbnail again when it is already loaded
        if (previousIndex == currentThumbnailIndex) return null;
        
        // if the index is right, and we have a thumbnail for it, we load it
        UnloadRecording(); // just in case data is loaded
        StartCoroutine(CreateFromThumbnail(currentThumbnailIndex));
        return recordingThumbnails[currentThumbnailIndex];
    }

    public IEnumerator GotoThumbnail(int value)
    {
        if (recordingThumbnails.Count == 0) yield break;
        if (value == currentThumbnailIndex) yield break;

        if (value >= 0 && value < recordingThumbnails.Count)
        {
            Debug.Log("RecordingManager: Goto thumbnail: " + value);
            currentThumbnailIndex = value;
            // if the index is right, and we have a thumbnail for it, we load it
            UnloadRecording(); // just in case data is loaded
            yield return CreateFromThumbnail(currentThumbnailIndex);
        }
    }
    
    public int GetThumbnailCount()
    {
        return recordingThumbnails.Count;
    }

    public Recording.ThumbnailData GetCurrentThumbnail()
    {
        return recordingThumbnails[currentThumbnailIndex];
    }
}
