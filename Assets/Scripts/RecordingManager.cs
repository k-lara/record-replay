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

    private LoadManager loadManager; // handles loading a recording and thumbnails from disc
    private SaveManager saveManager; // handles saving recordings to disc

    public RecordingFlags recordingFlags = new();
    public Recording Recording { get; private set; } // the current/prospective recording in memory (has recording id)
    private List<Recording.ThumbnailData> recordingThumbnails = new(); // of most recent save
    private int currentRecordingIndex;
    
    [Tooltip("The maximum number of saves we keep on the device for each recording")]
    public int maxSaves = 5;
    
    public Dictionary<string, GameObject> prefabCatalogue { get; private set; }
    private NetworkSpawnManager spawnManager;
    private List<GameObject> thumbnailObjects = new(); // the spawned prefabs for the current thumbnail
    
    public event EventHandler<List<GameObject> > onThumbnailSpawned;
    public event EventHandler onRecordingSaved;
    public event EventHandler onRecordingLoaded;
    public event EventHandler onRecordingDeleted;
    
    public class RecordingFlags
    {
        public bool SaveReady;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        pathToRecordings = Application.persistentDataPath;
        spawnManager = NetworkSpawnManager.Find(this);
        
        prefabCatalogue = new Dictionary<string, GameObject>();
        foreach (var prefab in spawnManager.catalogue.prefabs)
        {
            prefabCatalogue.Add(prefab.name, prefab);
        }
        
        loadManager = new LoadManager(spawnManager, pathToRecordings);
        saveManager = new SaveManager(pathToRecordings, 2); 
        
        StartCoroutine(PrepareThumbnails());
        
    }
    
    private IEnumerator PrepareThumbnails()
    {
        var thumbnailsTask = Task.Run(loadManager.LoadThumbnailData);
        thumbnailsTask.Wait();
        recordingThumbnails = thumbnailsTask.Result;
        
        Recording = new Recording(new Guid(recordingThumbnails[^1].recordingId));
        
        yield return loadManager.CreateThumbnail(recordingThumbnails[^1], thumbnailObjects);
        
        onThumbnailSpawned?.Invoke(this, thumbnailObjects);
    }

    public void InitNewRecording()
    {
        Recording = new Recording();
        Recording.InitNew();
    }

    // public IEnumerator SpawnNextThumbnail()
    // {
    //     if (currentRecordingIndex < dirInfoRecordings.Count - 1)
    //         currentRecordingIndex++;
    //     else
    //         currentRecordingIndex = 0;
    //     yield return SpawnThumbnailObjects(new Guid(dirInfoRecordings[currentRecordingIndex].Name));
    // }
    //
    // public IEnumerator SpawnPreviousThumbnail()
    // {
    //     if (currentRecordingIndex > 0)
    //         currentRecordingIndex--;
    //     else
    //         currentRecordingIndex = dirInfoRecordings.Count - 1;
    //     yield return SpawnThumbnailObjects(new Guid(dirInfoRecordings[currentRecordingIndex].Name));
    // }
}
