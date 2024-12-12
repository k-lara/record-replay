using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class SaveManager
{
    public SaveManager(string pathToRecordings, int numBackupSaves)
    {
        this.pathToRecordings = pathToRecordings;
        this.numBackupSaves = numBackupSaves;
    }

    private string pathToRecordings;
    private int numBackupSaves;

    public float saveInterval = 300; // in seconds (5 minutes)
    private float timeSinceLastSave;

    private Task<Recording.ThumbnailData> t;

    public void Dispose()
    {
        if (t != null)
        {
            Debug.Log("Dispose save task");
            t.Dispose();
        }
    }

    public bool ReadyToSave()
    {
        timeSinceLastSave += Time.deltaTime;
        return timeSinceLastSave >= saveInterval;
    }
    
    public void ResetSaveTimer()
    {
        timeSinceLastSave = 0;
    }
    
    /**
     * Save the current recording to a file.
     * We keep 2 backup files in addition to the current recording.
     * @return the thumbnail data of the saved recording which we can add to our current list of thumbnails in memory
     */
    public Recording.ThumbnailData SaveRecording(Recording recording)
    {
        var idString = recording.recordingId.ToString();
        
        // check if we already have a folder for this recording
        var recordingPath = Path.Combine(pathToRecordings, idString);
        
        List<DirectoryInfo> previousSaves = new();
        
        if (Directory.Exists(recordingPath))
        {
            previousSaves = new DirectoryInfo(recordingPath).EnumerateDirectories()
                .OrderBy(d => d.CreationTime).ToList();    
        }
        else
        {
            // create a new folder for the recording
            Debug.Log("Creating new recording folder");
            var newDirInfo = new DirectoryInfo(recordingPath);
            newDirInfo.Create();
            
        }
        // add new save
        t = Task.Run(() => AddNewRecordingSave(recording, recordingPath));
        t.Wait();
        var thumbnailData = t.Result;
        
        // remove oldest save
        if (previousSaves.Count >= numBackupSaves)
        {
            previousSaves[0].Delete(true);
        }
        
        Debug.Log("New recording saved!");
        timeSinceLastSave = 0;
        return thumbnailData;
    }

    private async Task<Recording.ThumbnailData> AddNewRecordingSave(Recording recording, string recordingPath)
    {
        var newSave =
            new DirectoryInfo(Path.Combine(recordingPath, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_save"));
        newSave.Create();

        // save thumbnail data
        var thumbnail = recording.AssembleThumbnailData();
        var jsonThumbnail = JsonUtility.ToJson(thumbnail, true);
        await File.WriteAllTextAsync(Path.Combine(newSave.FullName, "thumbnail.txt"), jsonThumbnail);

        // save motion per recordable
        foreach (var entry in recording.recordableDataDict)
        {
            var jsonMotion = JsonUtility.ToJson(entry.Value);
            await File.WriteAllTextAsync(Path.Combine(newSave.FullName, entry.Key + "_motion.txt"), jsonMotion);
        }
        // TODO save audio per recordable

        return thumbnail;
    }
}
