using System;
using System.Collections;
using Ubiq.Avatars;
using UnityEngine;

[RequireComponent(typeof(Replayer))]
public class Recorder : MonoBehaviour
{
    private RecordingManager _recordingManager;
    public Recording recording => _recordingManager.Recording;
    
    private bool _isRecording;
    public bool _replayLoaded { get; private set; }
    public bool _replayPlaying { get; private set; }
    
    public int fps = 10;

    public bool allInputValid {get; private set;}
    
    public event EventHandler onRecordingStart;
    public event EventHandler<Recording.Flags> onRecordingStop; // tell everyone to stop and add the data

    public event EventHandler onInvalidRecording;
    
    public event EventHandler onSaveReady;
    
    private Replayer _replayer;
    
    // private AvatarManager _avatarManager;
    
    // Start is called before the first frame update
    void Start()
    {
        _recordingManager = GetComponent<RecordingManager>();
        _recordingManager.onRecordingLoaded += OnReplayLoaded;
        _recordingManager.onRecordingUnloaded += OnReplayUnloaded;
        
        _replayer = GetComponent<Replayer>();
        _replayer.onReplayStart += OnReplayStart;
    }

    // we use this when tracking has been lost and we need to start over but don't have any valid replayable data yet.
    public void ClearRecording()
    {
        _recordingManager.UnloadRecording();
    }

    public void RecordingValid(bool valid)
    {
        allInputValid = valid;

        if (!valid)
        {
            onInvalidRecording?.Invoke(this, EventArgs.Empty);
        }
    }

    public void StartRecording()
    {
        if (_isRecording) return;
        Debug.Log("Start recording!");

        // this could also be because a recording we just started lost tracking
        // in that case, we want to continue with that recording, and not a new one
        // so we also check if the recording id is empty or nit
        if (!recording.flags.DataLoaded && recording.recordingId == Guid.Empty) 
        {
            _recordingManager.InitNewRecording();
        }
        
        if (!_replayLoaded)
        {
        }
        
        recording.flags.SaveReady = false;
        recording.flags.NewDataAvailable = true; // so recording manager knows data is available to save next time
        recording.flags.IsRecording = _isRecording = true;
        onRecordingStart?.Invoke(this, EventArgs.Empty);
    }

    public void StopRecording(bool save = true)
    {
        if (!_isRecording) return;
        Debug.Log("Stop recording!");
        
        recording.flags.IsRecording = _isRecording = false;
        // each recordable adds its metadata to the thumbnail in their OnRecordingStop()
        onRecordingStop?.Invoke(this, recording.flags);
        // Debug.Log(recording.ToString());

        if (save)
        {
            StartCoroutine(WaitForSaveReady());
        }
    }

    public void InvalidRecordingFrom(Guid guid)
    {
        
    }
    
    public void AddUndoState(UndoManager.UndoType type, Guid id, Recording.RecordableData data)
    {
        // Debug.Log("Add undo stack");
        _recordingManager.AddUndoState(type, id, data);
    }

    // we currently use this when a recording has become invalid and to remove the last added undo state
    // (which is the state that gets added before an UndoType.New in OnRecordingStart)
    public void Undo(bool invalidRecording)
    {
        _recordingManager.Undo(invalidRecording);
    }
    
    private IEnumerator WaitForSaveReady()
    {
        yield return new WaitUntil(() => recording.flags.SaveReady);
        // onSaveReady?.Invoke(this, EventArgs.Empty);
        // Debug.Log("Recorder: Save ready!");
        // _recordingManager.SaveRecording();
    }
    
    private void OnReplayStart(object o, EventArgs e)
    {
        _replayPlaying = true;
    }
    
    private void OnReplayLoaded(object o, EventArgs e)
    {
        _replayLoaded = true;
    }

    private void OnReplayUnloaded(object o, EventArgs e)
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
}
