using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Ubiq.Spawning;
using UnityEngine.Pool;

public class LoadManager
{
    public LoadManager(NetworkSpawnManager spawnManager, Dictionary<string, GameObject> prefabCatalogue, string pathToRecordings)
    {
        this.spawnManager = spawnManager;
        this.prefabCatalogue = prefabCatalogue;
        this.pathToRecordings = pathToRecordings;
    }
    
    private NetworkSpawnManager spawnManager;
    private Dictionary<string, GameObject> prefabCatalogue;
    
    private string pathToRecordings;
    
    // on startup all existing thumbnails are loaded so that the user can choose which recording to load
    public async Task<List<Recording.ThumbnailData>> LoadThumbnailData()
    {
        var recordingThumbnails = new List<Recording.ThumbnailData>();
        
        var dirInfos = new DirectoryInfo(pathToRecordings).EnumerateDirectories().OrderBy(d => d.CreationTime).ToList();

        foreach (var info in dirInfos)
        {
            // get most recent folder
            info.Refresh();
            var saves = info.EnumerateDirectories().OrderBy(d => d.CreationTime).ToList();

            DirectoryInfo lastSave = saves[^1];
            
            // get thumbnail data from file
            var thumbnailPath = Path.Combine(lastSave.FullName, "thumbnail.txt");
            if (File.Exists(thumbnailPath))
            {
                var fromFile = await File.ReadAllTextAsync(thumbnailPath);
                var thumbnailData = JsonUtility.FromJson<Recording.ThumbnailData>(fromFile);
                Debug.Log("Thumbnail data last save: " + lastSave.Name);
                recordingThumbnails.Add(thumbnailData); // most recent thumbnail !!!
                
            }
        }
        return recordingThumbnails;
    }
    
    // we load the corresponding recording data of the thumbnail
    // all thumbnail objects should have already been created
    public async Task LoadRecordingData(RecordableListPool pool, Recording recording, Recording.ThumbnailData thumbnail)
    {
        recording.SetRecordingId(new Guid(thumbnail.recordingId)) ;
        var dirInfo = new DirectoryInfo(Path.Combine(pathToRecordings, recording.recordingId.ToString()));
        var saves = dirInfo.EnumerateDirectories().OrderBy(d => d.CreationTime).ToList();
        DirectoryInfo lastSave = saves[^1];
        
        var recordableDataDict = new Dictionary<Guid, Recording.RecordableData>();
        
        // load the recording data
        foreach (var recordableId in thumbnail.recordableIds)
        {
            // try to load the motion file of the current recordableId
            var filePath = Path.Combine(lastSave.FullName, recordableId + "_motion.txt");
            if (File.Exists(filePath))
            {
                // TODO not sure if this works! 
                // we want to save the data frames into the lists from the list pool
                // but I am not sure if it just saves it in there or creates a new one...???
                var recordableDataJson = await File.ReadAllTextAsync(filePath);
                var recordableData = new Recording.RecordableData();
                recordableData.dataFrames = pool.GetList();
                JsonUtility.FromJsonOverwrite(recordableDataJson, recordableData);
                
                recordableDataDict.Add(new Guid(recordableId), recordableData);
            }
        }
        recording.SetRecordableDataDict(recordableDataDict);
        
        // TODO audio files
        // var audioFiles = lastSave.EnumerateFiles().Where(f => f.Name.Contains("_audio.txt")).ToList();
    }
    /*
     * Spawns prefabs as given by the currently loaded thumbnail.
     * If we start a new recording and haven't saved it yet, there is no thumbnail to load from.
     * In this case we need to spawn prefabs from the recording data (see CreateFromRecording).
     */
    public List<GameObject> CreateFromThumbnail(Recording.ThumbnailData thumbnail, ref List<GameObject> currentSpawned)
    {
        Debug.Log("Create from thumbnail");
        List<GameObject> newlySpawned = new();
        for (var i = 0; i < thumbnail.prefabNames.Count; i++)
        {
            var name = thumbnail.prefabNames[i];
            var prefab = currentSpawned.Find(obj => obj.name == name + "(Clone)");
            Replayable replayable;
            if (prefab == null)
            {
                Debug.Log("Create fresh prefab: " + name);
                var go = spawnManager.SpawnWithPeerScope(prefabCatalogue[name]);
                replayable = go.AddComponent<Replayable>();
                newlySpawned.Add(go);
            }
            else
            {
                Debug.Log("Reuse old prefab: " + name);
                replayable = prefab.GetComponent<Replayable>();
                newlySpawned.Add(prefab);
                currentSpawned.Remove(prefab);
            }
            replayable.replayableId = new Guid(thumbnail.recordableIds[i]);
            replayable.SetReplayablePose(ToPose(thumbnail.firstPoses[i]));
            replayable.SetIsLocal(true);
        }
        
        // currentSpawned = newPrefabs;
        Debug.Log("spawnedObjects currentSpawned: " + currentSpawned.Count + "");
        return newlySpawned;
    }
    
    public List<GameObject> CreateFromRecording(Recording recording, Dictionary<Guid, Replayable> replayablesDict)
    {
        var newSpawned = new List<GameObject>();
        
        Debug.Log("Spawn new replayable objects if any!");
        foreach (var entry in recording.recordableDataDict)
        {
            Debug.Log(entry.Key + " is spawned: " + replayablesDict.ContainsKey(entry.Key));
            if (replayablesDict.ContainsKey(entry.Key)) continue;
            
            Debug.Log(entry.Value.prefabName);
            var go = spawnManager.SpawnWithPeerScope(prefabCatalogue[entry.Value.prefabName]);
            newSpawned.Add(go);
            
            var replayable = go.AddComponent<Replayable>(); // player doesn't need this, so add it here
            replayable.replayableId = entry.Key;
            // set to last pose or first pose (last pose is a bit annoying when wanting to record from the beginning)
            // replayable.SetReplayablePose(entry.Value.dataFrames.Count - 1);
            // we set it to the first valid pose (there should be one at least at some point)
            // even if the valid pose does not start at 0, it will find the next valid pose
            replayable.SetReplayablePose(0); 
            replayable.SetIsLocal(true);
            replayablesDict.Add(entry.Key, replayable);
            Debug.Log("Spawned new replayable (id: " + entry.Key + ")");
        }
        return newSpawned;
    }
    
    private Replayable.ReplayablePose ToPose(Recording.RecordableDataFrame dataFrame)
    {
        var pose = new Replayable.ReplayablePose
        {
            head = new Pose(new Vector3(dataFrame.xPosHead, dataFrame.yPosHead, dataFrame.zPosHead), new Quaternion(dataFrame.xRotHead, dataFrame.yRotHead, dataFrame.zRotHead, dataFrame.wRotHead)),
            leftHand = new Pose(new Vector3(dataFrame.xPosLeftHand, dataFrame.yPosLeftHand, dataFrame.zPosLeftHand), new Quaternion(dataFrame.xRotLeftHand, dataFrame.yRotLeftHand, dataFrame.zRotLeftHand, dataFrame.wRotLeftHand)),
            rightHand = new Pose(new Vector3(dataFrame.xPosRightHand, dataFrame.yPosRightHand, dataFrame.zPosRightHand), new Quaternion(dataFrame.xRotRightHand, dataFrame.yRotRightHand, dataFrame.zRotRightHand, dataFrame.wRotRightHand))
        };
        return pose;
    }
}
