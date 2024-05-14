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

    public int fps = 10; // fps for recording
    public UnityEvent onRecordingStart;
    public UnityEvent onRecordingStop;
    
    [System.Serializable]
    public class RecordingSavedEvent : UnityEvent<List<Recordable.RecordableData>> {}
    public RecordingSavedEvent onRecordingSaved;
    
    private StreamWriter _streamWriter;
    private List<Recordable> _recordables = new List<Recordable>();
    private List<Recordable.RecordableData> _recordableDataList = new List<Recordable.RecordableData>();
    private int _numSavedData = 0;
    
    private string _savePath = Application.dataPath + "/Recordings/";
    
    // each recordable calls this method to add its data to the file
    public IEnumerator AddRecordableData(Recordable.RecordableData recordableData)
    {
        // write the data to a file line by line
        _recordableDataList.Add(recordableData);
        var task = _streamWriter.WriteLineAsync(JsonUtility.ToJson(recordableData));
        yield return new WaitUntil(() => task.IsCompleted);
        // we check how many recordables have saved their data
        _numSavedData++;
        // if all recordables have saved their data, we close the file
        if (_numSavedData == _recordables.Count)
        {
            _streamWriter?.Dispose();
            _numSavedData = 0;
            onRecordingSaved.Invoke(new List<Recordable.RecordableData>(_recordableDataList));
            _recordableDataList.Clear();
        }
        Debug.Log("Added recordable data  from:" + recordableData.metaData[2]);
        
    }
    
    // each recordable adds itself to the list of recordables
    public void AddRecordable(Recordable recordable)
    {
        _recordables.Add(recordable);
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
        _pathLastRecording = Path.Join(_savePath, "rec" + dateTimeString + ".txt");
        _streamWriter = new StreamWriter(Path.Join(_savePath, "rec" + dateTimeString + ".txt"));
        onRecordingStop.Invoke();
    }
    
    public string GetPathLastRecording()
    {
        return _pathLastRecording;
    }

    public string GetSavePath()
    {
        return _savePath;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    }
    
    private void OnDestroy()
    {
        _streamWriter?.Dispose();
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
