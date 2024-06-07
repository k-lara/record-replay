using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ubiq.Messaging;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/*
 * The Recorder is responsible for managing the recording of avatars, virtual characters,
 * and other objects that should be recorded.
 * Currently, all it does is start and stop a recording.
 * The recordable objects manage the saving of their data themselves.
 *
 * The Recorder only saves a meta file that can be used for making a thumbnail of the recording.
 * This includes: the name (which is the date and time) of the recording, the duration of the recording,
 * the characters, and the first pose of each character
 */
public class Recorder : MonoBehaviour
{
    // we can use the thumbnail to load a recording, without needing to load all the movement data etc.
    [System.Serializable]
    public class ThumbnailData
    {
        public string name; // date and time of the recording
        public float duration; // computed from fps and number of frames
        public List<string> uniquePrefabs; // a recording can consist of several prefabs or just multiple instances of the same
        public List<int> numInstances; // number of instances of each unique prefab
        public List<string> recordablePrefabs; // prefabs required to spawn for the replay
        public List<string> recordableIds; // important to cross reference thumbnail created characters with their recorded data
        public List<Recordable.RecordableDataFrame> firstPoses; // in order of the prefabs

        public ThumbnailData(string name)
        {
            this.name = name;
            uniquePrefabs = new List<string>();
            numInstances = new List<int>();
            recordablePrefabs = new List<string>();
            recordableIds = new List<string>();
            firstPoses = new List<Recordable.RecordableDataFrame>();
        }
    }
    
    private bool _isRecording;
    private string _pathLastRecording;
    
    // private Replayer _replayer;

    public int fps = 10; // fps for recording
    public UnityEvent onRecordingStart;
    public UnityEvent<string> onRecordingStop;
    public UnityEvent<GameObject> onRemoveReplayedObject;
    public UnityEvent onAddThumbnailData;
    
    private string _savePath;
    private ThumbnailData thumbnailData;
    
    void Start()
    {
        // _replayer = GetComponent<Replayer>();
        _savePath = Application.persistentDataPath;
    }

    public void AddToThumbnail(NetworkId id, string recordableName, int numFrames, Recordable.RecordableDataFrame firstPose)
    {
        var idx = thumbnailData.uniquePrefabs.IndexOf(recordableName);
        // if we don't have it in the lists we add it
        if (idx == -1)
        {
            thumbnailData.uniquePrefabs.Add(recordableName);
            thumbnailData.numInstances.Add(1);
        }
        // otherwise we increase the number of instances
        else
        {
            thumbnailData.numInstances[idx] += 1;
        }


        thumbnailData.recordablePrefabs.Add(recordableName);
        thumbnailData.recordableIds.Add(id.ToString());
        thumbnailData.firstPoses.Add(firstPose);
        // whichever recordable adds it last defines the duration
        // usually recordings only differ by 1 frame, so the error is negligible for the thumbnail
        thumbnailData.duration = numFrames / (float)fps;  
    }

    public void RemoveReplayedObject(GameObject go)
    {
        onRemoveReplayedObject.Invoke(go);
    }
    
    public void StartRecording()
    {
        Debug.Log("Start recording!");
        _isRecording = true;
        onRecordingStart.Invoke();
    }

    public void StopRecording()
    {
        Debug.Log("Stop recording!");
        _isRecording = false;
        // open a file and save the data
        // get current date time string
        string dateTimeString = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        // create folder for new recording
        Directory.CreateDirectory(Path.Join(_savePath, dateTimeString));
        _pathLastRecording = Path.Join(_savePath,dateTimeString); // path to folder of last recording, but currently also the recording we want to save

        thumbnailData = new ThumbnailData(dateTimeString);
        // every recordable should add its thumbnail data to the meta file
        onAddThumbnailData.Invoke();
        SaveThumbnail(_pathLastRecording);
        
        // every recordable adds its own data in a file to the recording folder
        onRecordingStop.Invoke(_pathLastRecording);
    }

    private void SaveThumbnail(string pathToFolder)
    {
        File.WriteAllText(Path.Join(pathToFolder, "thumbnail.txt"), JsonUtility.ToJson(thumbnailData, true));
    }
}

// [CustomEditor(typeof(Recorder))]
// public class RecorderEditor : Editor
// {
//     string _text = "Start Recording";
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();
//         if (!Application.isPlaying) return;
//         
//         Recorder recorder = (Recorder) target;
//
//         // add a button that changes text depending on whether a recording needs to be started or stopped
//         if (!GUILayout.Button(_text)) return;
//         
//         if (_text == "Start Recording")
//         {
//             recorder.StartRecording();
//             _text = "Stop Recording";
//         }
//         else
//         {
//             recorder.StopRecording();
//             _text = "Start Recording";
//         }
//
//     }
// }
