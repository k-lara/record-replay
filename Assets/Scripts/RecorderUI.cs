
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Math.EC;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit;

/*
 * This is also the UI for the Replayer.
 */
public class RecorderUI : MonoBehaviour
{
    public float recordingCountdown = 5.0f; // countdown before recording starts

    public InteractableSphere recordSphere;
    public InteractableSphere loadSphere;
    private SphereCollider _loadSphereCollider; // when a replay is loaded we prevent loading again
    public InteractableSphere startSphere;
    public InteractableSphere stopSphere;
    public InteractableSphere deleteSphere;
    public InteractableSphere forwardSphere;
    public InteractableSphere backwardSphere;

    public TextMeshProUGUI recordButtonText;
    public TextMeshProUGUI recordCountdownText;
    public TextMeshProUGUI replayNumberText;
    public TextMeshProUGUI thumbnailInfoText;
    
    private Material _loadSphereMaterial;
    private Material _recordSphereMaterial;
    private Color transparentWhite;
    
    private Color _recordingOn;
    private Color _recordingOff;
    private Color _recordingLoaded;
    private AudioSource _audioSourceLowBeep;
    private AudioSource _audioSourceHighBeep;
    private AudioSource _audioSourceButtonPress;
    
    private Recorder _recorder;
    private bool _isRecording;
    private Replayer _replayer;
    private List<DirectoryInfo> _directories; // the replay directories in the persistent data path
    private int _currentReplayIndex;
    
    // Start is called before the first frame update
    void Start()
    {
        recordCountdownText.color = Color.clear; // countdown invisible at first
        transparentWhite = new Color(1f, 1f, 1f, 0.4f);

        _loadSphereCollider = loadSphere.gameObject.GetComponent<SphereCollider>();
        _loadSphereMaterial = loadSphere.gameObject.GetComponent<MeshRenderer>().material;
        _recordSphereMaterial = recordSphere.gameObject.GetComponent<MeshRenderer>().material;
        _recordingOff = _recordSphereMaterial.color;
        _recordingOn = recordSphere.color; // the color when sphere was interacted with
        
        
        var audioSources = GetComponents<AudioSource>();
        _audioSourceLowBeep = audioSources[0];
        _audioSourceHighBeep = audioSources[1];
        _audioSourceButtonPress = audioSources[2];
        
        _recorder = GameObject.FindWithTag("Recorder").GetComponent<Recorder>();
        _replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();
        _recorder.onRecordingStop += OnRecordingStop;
        _replayer.onReplayCreated += OnReplayCreated;
        _replayer.onThumbnailCreated += OnThumbnailCreated;
        
        recordSphere.onSphereSelected += RecordButtonPressed;
        loadSphere.onSphereSelected += LoadReplayButtonPressed;
        startSphere.onSphereSelected += StartReplayButtonPressed;
        stopSphere.onSphereSelected += StopReplayButtonPressed;
        deleteSphere.onSphereSelected += DeleteReplayButtonPressed;
        forwardSphere.onSphereSelected += ForwardButtonPressed;
        backwardSphere.onSphereSelected += BackwardButtonPressed;
        
        DirectoryInfo info = new DirectoryInfo(Application.persistentDataPath);
        _directories = info.GetDirectories().OrderBy(p => p.CreationTime).ToList();
        // in the Recorder we load the most recent replay on startup, this is the last one in the list
        replayNumberText.text = $"{_directories.Count:00}/{_directories.Count:00}";
        _currentReplayIndex = _directories.Count - 1;
    }

    private void OnThumbnailCreated(object o, Recorder.ThumbnailData thumbnailData)
    {
        // when a replay is already loaded, but we pressed the forward or backward buttons
        // a new thumbnail gets loaded which overwrites the previous data
        // therefore, we need to reset the button color to the "unloaded" color and allow it to be pressed again
        _loadSphereMaterial.color = transparentWhite;
        _loadSphereCollider.isTrigger = false;
        thumbnailInfoText.text = "\n" +
                                 "\n" +
                                 thumbnailData.name + "\n" +
                                 $"{thumbnailData.duration / 60:f2}" + " min\n" +
                                 thumbnailData.recordableIds.Count;
    }
    
    // we move one directory forward in the list and load the respective thumbnail
    // NOTE: if a replay has been loaded, we need to set the _isLoaded flag in the Replayer back to false.
    // the thumbnail will overwrite the loaded data in the Replayables and a new data needs to be loaded again for the new thumbnail
    public void ForwardButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        if (_currentReplayIndex == _directories.Count - 1)
        {
            _currentReplayIndex = 0;
        }
        else
        {
            _currentReplayIndex++;
        }
        _replayer.CreateThumbnail(_directories[_currentReplayIndex].FullName);
        replayNumberText.text = $"{_currentReplayIndex+1:00}/{_directories.Count:00}";

    }
    // we move one directory backward in the list and load the respective thumbnail
    // see NOTE in ForwardButtonPressed()
    public void BackwardButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        if (_currentReplayIndex == 0)
        {
            _currentReplayIndex = _directories.Count - 1;
        }
        else
        {
            _currentReplayIndex--;
        }
        _replayer.CreateThumbnail(_directories[_currentReplayIndex].FullName);
        replayNumberText.text = $"{_currentReplayIndex+1:00}/{_directories.Count:00}";

    }
    
    // special case when replay gets loaded on startup, then we haven't pressed the button
    // but we still want it to show the loaded color
    private void OnReplayCreated(object o, string folder)
    {
        _loadSphereMaterial.color = loadSphere.color;
        // when collider is a trigger it does not respond to the Interactable apparently...
        _loadSphereCollider.isTrigger = true; // prevent sphere from being interacted with while replay is loaded
    }

    private IEnumerator StartRecordingWithCountdown()
    {
        recordCountdownText.color = Color.white;
        // print a recording countdown that changes the text from white to red gradually
        for (int i = 0; i < recordingCountdown; i++)
        {
            recordCountdownText.text = (recordingCountdown - i).ToString();
            recordCountdownText.color = Color.Lerp(_recordingOff, _recordingOn, (float)i / recordingCountdown);
            _audioSourceLowBeep.Play();
            yield return new WaitForSeconds(1.0f);
        }

        _audioSourceHighBeep.Play();
        _recordSphereMaterial.color = _recordingOn;
        recordButtonText.text = "Stop";
        recordCountdownText.color = Color.clear;
        
        _recorder.StartRecording();
        _isRecording = true;
    }

    private void StopRecording()
    {
        _recorder.StopRecording();
        _isRecording = false;
        _recordSphereMaterial.color = _recordingOff;
        recordButtonText.text = "Record";
    }

    private void OnRecordingStop(object o, string pathToRecording)
    {
        _directories.Add(new DirectoryInfo(pathToRecording));
        replayNumberText.text = $"{_directories.Count:00}/{_directories.Count:00}";
        _currentReplayIndex = _directories.Count - 1;
    }

    public void DeleteReplayButtonPressed(object o, EventArgs e)
    {
        _loadSphereMaterial.color = transparentWhite;
        _loadSphereCollider.isTrigger = false;
        // _audioSourceButtonPress.Play();
        _replayer.DeleteReplay();
        
        // remove the thumbnail info too
        thumbnailInfoText.text = "";
        replayNumberText.text = $"{0:00}/{_directories.Count:00}";
    }

    public void StopReplayButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        _replayer.StopReplay();
    }

    public void StartReplayButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        _replayer.StartReplay(this, "");
    }
    
    // loading a replay needs a replay folder!
    public void LoadReplayButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        _loadSphereMaterial.color = loadSphere.color;
        _loadSphereCollider.isTrigger = true; // so we don't press it again.
        StartCoroutine(_replayer.CreateThumbnailAndLoadReplay());
        // _replayer.LoadReplay(_directories[_currentReplayIndex].FullName);
    }

    // this button starts or stops the recording
    public void RecordButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        if (!_isRecording)
        {
            StartCoroutine(StartRecordingWithCountdown());
        }
        else
        {
            StopRecording();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RecorderUI))]
public class RecorderUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        string text = "Start Recording";
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        
        RecorderUI recorderUI = (RecorderUI) target;
        
        
        if (!GUILayout.Button(text)) return;
         
         if (text == "Start Recording")
         {
             recorderUI.RecordButtonPressed(this, EventArgs.Empty);
             text = "Stop Recording";
         }
         else
         {
             recorderUI.RecordButtonPressed(this, EventArgs.Empty);
             text = "Start Recording";
         }
        
        
        if (GUILayout.Button("Record"))
        {
            recorderUI.RecordButtonPressed(this, EventArgs.Empty);
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        if (GUILayout.Button("Start Replay"))
        {
            recorderUI.StartReplayButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Stop Replay"))
        {
            recorderUI.StopReplayButtonPressed(this, EventArgs.Empty);
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        if (GUILayout.Button("Next"))
        {
            recorderUI.ForwardButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Previous"))
        {
            recorderUI.BackwardButtonPressed(this, EventArgs.Empty);
        }
        
        if (GUILayout.Button("Load Replay"))
        {
            recorderUI.LoadReplayButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Delete Replay"))
        {
            recorderUI.DeleteReplayButtonPressed(this, EventArgs.Empty);
        }
    }
}
#endif
