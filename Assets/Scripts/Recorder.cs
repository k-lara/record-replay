using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ubiq.Avatars;
using UnityEditor;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;
using Directory = UnityEngine.Windows.Directory;

[RequireComponent(typeof(Replayer))]
public class Recorder : MonoBehaviour
{
    private RecordingManager _recordingManager;
    public Recording recording => _recordingManager.Recording;
    
    private bool _isRecording;
    public bool _replayLoaded { get; private set; }
    public bool _replayPlaying { get; private set; }
    
    public int fps = 10;
    
    public event EventHandler onRecordingStart;
    public event EventHandler<RecordingManager.RecordingFlags> onRecordingStop; // tell everyone to stop and add the data
    
    private Replayer _replayer;
    
    private AvatarManager _avatarManager;
    
    // Start is called before the first frame update
    void Start()
    {
        _recordingManager = GetComponent<RecordingManager>();

        _replayer = GetComponent<Replayer>();
        
        _replayer.onReplayCreated += OnReplayLoaded;
        _replayer.onReplayDeleted += OnReplayDeleted;
        _replayer.onReplayStart += OnReplayStart;
        
        _avatarManager = AvatarManager.Find(this);
        var recordable = _avatarManager.LocalAvatar.gameObject.AddComponent<Recordable>();
        recordable.prefabName = _avatarManager.avatarPrefab.name;

    }

    public void StartRecording()
    {
        Debug.Log("Start recording!");

        if (recording == null)
        {
            _recordingManager.InitNewRecording();
        }
        
        if (!_replayLoaded)
        {
        }
        
        _isRecording = true;
        onRecordingStart?.Invoke(this, EventArgs.Empty);
    }

    public void StopRecording()
    {
        Debug.Log("Stop recording!");
        
        _isRecording = false;
        // each recordable adds its metadata to the thumbnail in their OnRecordingStop()
        onRecordingStop?.Invoke(this, _recordingManager.recordingFlags);

        StartCoroutine(WaitForSaveReady());
    }
    
    private IEnumerator WaitForSaveReady()
    {
        yield return new WaitUntil(() => _recordingManager.recordingFlags.SaveReady == true);
        _recordingManager.SaveRecording();
    }
    
    private void OnReplayStart(object o, EventArgs e)
    {
        _replayPlaying = true;
    }
    
    private void OnReplayLoaded(object o, Dictionary<Guid, Replayable> dict)
    {
        _replayLoaded = true;
    }

    private void OnReplayDeleted(object o, EventArgs e)
    {
        _replayLoaded = false;
    }
    
    // depends on frame of replayer 
    public float GetCurrentFrameFloat()
    {
        if (_replayLoaded || _replayPlaying)
        {
            return _replayer.currentFrame;
        }
        return 0;
    }
    
    // Update is called once per frame
    void Update()
    {
        
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

        Recorder recorder = (Recorder)target;

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
