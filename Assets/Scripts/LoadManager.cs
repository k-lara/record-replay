using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Ubiq.Spawning;

public class LoadManager
{
    public LoadManager(NetworkSpawnManager spawnManager, string pathToRecordings)
    {
        this.spawnManager = spawnManager;
        this.pathToRecordings = pathToRecordings;
    }
    
    private NetworkSpawnManager spawnManager;
    
    private string pathToRecordings;
    
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
                var guid = new Guid(lastSave.Name);
                recordingThumbnails.Add(thumbnailData); // most recent thumbnail !!!
                
            }
        }
        return recordingThumbnails;
    }
    
    // we load the corresponding recording data of the thumbnail
    // all thumbnail objects should have already been created
    public async Task LoadRecordingData(Recording recording, Recording.ThumbnailData thumbnail)
    {
        var dirInfo = new DirectoryInfo(Path.Combine(pathToRecordings, recording.recordingId.ToString()));
        var saves = dirInfo.EnumerateDirectories().OrderBy(d => d.CreationTime).ToList();
        DirectoryInfo lastSave = saves[^1];
        
        var recordableDataDict = new Dictionary<Guid, Recording.RecordableData>();
        
        // load the recording data
        // var motionFiles = lastSave.EnumerateFiles().Where(f => f.Name.Contains("_motion.txt")).ToList();
        foreach (var recordableId in thumbnail.recordableIds)
        {
            // try to load the motion file of the current recordableId
            var filePath = Path.Combine(lastSave.FullName, recordableId + "_motion.txt");
            if (File.Exists(filePath))
            {
                var recordableDataJson = await File.ReadAllTextAsync(filePath);
                var recordableData = JsonUtility.FromJson<Recording.RecordableData>(recordableDataJson);
                // 
                recordableDataDict.Add(new Guid(recordableId), recordableData);
            }
        }
        recording.SetRecordableDataDict(recordableDataDict);
        
        // TODO audio files
        // var audioFiles = lastSave.EnumerateFiles().Where(f => f.Name.Contains("_audio.txt")).ToList();
    }
    
    // the recording should have been initialised already!
    public IEnumerator CreateThumbnail(Recording.ThumbnailData thumbnail, List<GameObject> currentSpawned)
    {
        List<GameObject> prefabs = new();
        for (var i = 0; i < thumbnail.prefabNames.Count; i++)
        {
            var name = thumbnail.prefabNames[i];
            var prefab = currentSpawned.Find(obj => obj.name == name + "(Clone)");
            Debug.Log("Found prefab? : " + prefab);
            Replayable replayable;
            if (prefab == null)
            {
                var go = spawnManager.SpawnWithPeerScope(prefab);
                replayable = go.AddComponent<Replayable>();
                prefabs.Add(go);
            }
            else
            {
                replayable = prefab.GetComponent<Replayable>();
                prefabs.Add(prefab);
                currentSpawned.Remove(prefab);
            }
            replayable.replayableId = new Guid(thumbnail.recordableIds[i]);
            replayable.SetReplayablePose(ToPose(thumbnail.firstPoses[i]));
            replayable.SetIsLocal(true);
        }
        currentSpawned = prefabs;
        
        yield return null;
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
