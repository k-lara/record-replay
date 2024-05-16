using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
/*
 * The Recorder is responsible for managing the recording of avatars, virtual characters,
 * and other objects that should be recorded.
 * It collects the recordable data and puts them in a format suitable for saving.
 * 
 */
public class Recorder : MonoBehaviour
{
    private bool _isRecording;

    private string _pathLastRecording;
    
    private Replayer _replayer;

    public int fps = 10; // fps for recording
    public UnityEvent onRecordingStart;
    public UnityEvent<string> onRecordingStop;
    public UnityEvent<GameObject> onRemoveReplayedObject;

    private string _savePath;

    void Start()
    {
        _replayer = GetComponent<Replayer>();
        _savePath = Application.persistentDataPath;
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
        
        _pathLastRecording = Path.Join(_savePath,dateTimeString); // path to folder of last recording
        onRecordingStop.Invoke(_pathLastRecording);
    }
}

[CustomEditor(typeof(Recorder))]
public class RecorderEditor : Editor
{
    string _text = "Start Recording";
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        
        Recorder recorder = (Recorder) target;

        // add a button that changes text depending on whether a recording needs to be started or stopped
        if (!GUILayout.Button(_text)) return;
        
        if (_text == "Start Recording")
        {
            recorder.StartRecording();
            _text = "Stop Recording";
        }
        else
        {
            recorder.StopRecording();
            _text = "Start Recording";
        }

    }
}
