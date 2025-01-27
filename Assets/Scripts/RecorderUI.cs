
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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/*
 * This is also the UI for the Replayer.
 */
public class RecorderUI : MonoBehaviour
{
    public float recordingCountdown = 5.0f; // countdown before recording starts
    public float currentFrameNormalized;
    private float _previousFrameNormalized;
    
    public InteractableSphere recordSphere;
    
    public InteractableSphere loadSphere;
    private SphereCollider _loadSphereCollider; // when a replay is loaded we prevent loading again
    
    public GameObject uiGameObject;
    public GameObject audioGameObject;
    public InteractableSphere startSphere;
    public InteractableSphere stopSphere;
    public InteractableSphere clearSphere;
    public InteractableSphere forwardSphere;
    public InteractableSphere backwardSphere;
    public InteractableSphere saveSphere;
    public InteractableSphere undoSphere;
    public InteractableSphere redoSphere;
    public InteractableSphere takeoverSphere;
    public CurveSlider frameSlider;
    
    public TextMeshProUGUI recordSphereText;
    public TextMeshProUGUI recordCountdownText;
    public TextMeshProUGUI replayNumberText;
    public TextMeshProUGUI thumbnailInfoText;

    public ActionBasedController leftController;
    public ActionBasedController rightController;
    
    private AudioSource _audioSourceLowBeep;
    private AudioSource _audioSourceHighBeep;
    private AudioSource _audioSourceButtonPress;
    
    private Recorder _recorder;
    private Replayer _replayer;
    private TakeoverSelector _takeoverSelector;
    private RecordingManager _recordingManager;
    private bool _isRecording;
    private bool _isReplaying;
    
    private bool _uiVisible;
    
    // Start is called before the first frame update
    void Start()
    {
        recordCountdownText.color = Color.clear; // countdown invisible at first

        _loadSphereCollider = loadSphere.gameObject.GetComponent<SphereCollider>();
        
        var audioSources = audioGameObject.GetComponents<AudioSource>();
        _audioSourceLowBeep = audioSources[0];
        _audioSourceHighBeep = audioSources[1];
        _audioSourceButtonPress = audioSources[2];
        
        var recorderGameObject = GameObject.FindWithTag("Recorder");
        _recorder = recorderGameObject.GetComponent<Recorder>();
        _replayer = recorderGameObject.GetComponent<Replayer>();
        _takeoverSelector = recorderGameObject.GetComponent<TakeoverSelector>();
        
        _recordingManager = recorderGameObject.GetComponent<RecordingManager>();
        _recordingManager.onRecordingLoaded += OnRecordingLoaded;
        _recordingManager.onRecordingUnloaded += OnRecordingUnloaded;
        _recordingManager.onRecordingSaved += OnRecordingSaved;
        
        recordSphere.onSphereSelected += RecordButtonPressed;
        loadSphere.onSphereSelected += LoadButtonPressed;
        startSphere.onSphereSelected += StartButtonPressed;
        stopSphere.onSphereSelected += StopButtonPressed;
        clearSphere.onSphereSelected += ClearButtonPressed;
        forwardSphere.onSphereSelected += ForwardButtonPressed;
        backwardSphere.onSphereSelected += BackwardButtonPressed;
        saveSphere.onSphereSelected += SaveButtonPressed;
        undoSphere.onSphereSelected += UndoButtonPressed;
        redoSphere.onSphereSelected += RedoButtonPressed;
        takeoverSphere.onSphereSelected += SelectTakeoverInEditor;
        
        frameSlider.onTChanged += SetFrameManually;
        _replayer.onFrameUpdate += SetSliderFromFrame;
        _replayer.onReplayStart += OnReplayStart;
        _replayer.onReplayStop += OnReplayStop;
        _recorder.onRecordingStart += OnRecordingStart;
        _recorder.onRecordingStop += OnRecordingStop;
        
        // get input action from primary button press
        InputActionManager manager;
        manager = leftController.gameObject.GetComponent<InputActionManager>();
        
        // listen to primary button press event
        
        
        // TODO maybe make script execute later than RecordingManager!
        SetRecordingNumberText();
    }

    private void SetSliderFromFrame(object o, bool e)
    {
        if (_uiVisible) 
            frameSlider.CalculateCubicBezierPointFromFrame(_replayer.GetCurrentFrameNormalized());
    }

    enum ActionType
    {
        Record, Load, Start, Stop, Clear, Forward, Backward, Save, Undo, Redo
    }

    private void EnableSpheres()
    {
        saveSphere.EnableInteractable(true);
        takeoverSphere.EnableInteractable(true);
        recordSphere.EnableInteractable(true);
        loadSphere.EnableInteractable(true);
        clearSphere.EnableInteractable(true);
        startSphere.EnableInteractable(true);
        stopSphere.EnableInteractable(true);
        undoSphere.EnableInteractable(true);
        redoSphere.EnableInteractable(true);
    }

    private void DisableSpheres(ActionType action)
    {
        switch (action)
        {
            case ActionType.Record:
                saveSphere.EnableInteractable(false);
                takeoverSphere.EnableInteractable(false);
                break;
            case ActionType.Load:
                break;
            case ActionType.Start:
                break;
            case ActionType.Stop:
                break;
            case ActionType.Clear:
                break;
            case ActionType.Forward:
                break;
            case ActionType.Backward:
                break;
            case ActionType.Save:
                break;
            case ActionType.Undo:
                break;
            case ActionType.Redo:
                break;
        }
    }

    public void UIToggle()
    {
        _uiVisible = !_uiVisible;
        
        // place the ui in front of the player whenever it is toggled
        Camera cam = Camera.main;
        if (cam && _uiVisible)
        {
            uiGameObject.transform.position = cam.transform.position + new Vector3(0.0f, -0.5f, 0.5f);
        }
        uiGameObject.SetActive(_uiVisible);
    }
    
    private void SetRecordingNumberText()
    {
        replayNumberText.text = $"{_recordingManager.GetCurrentRecordingNumber():00}/" +
                                $"{_recordingManager.GetRecordingCount():00}";
    }

    private void SetRecordingInfoText()
    {
        thumbnailInfoText.text = _recordingManager.GetRecordingInfo();
    }

    public void ForwardButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        _recordingManager.GotoAdjacentThumbnail(1);
        SetRecordingNumberText();
    }
   
    public void BackwardButtonPressed(object o, EventArgs e)
    {
        // _audioSourceButtonPress.Play();
        _recordingManager.GotoAdjacentThumbnail(-1);
        SetRecordingNumberText();
    }

    public void LoadButtonPressed(object o, EventArgs e)
    {
        _recordingManager.LoadRecording();
        // when collider is a trigger it does not respond to the Interactable apparently...
    }
    
    private void OnRecordingLoaded(object o, EventArgs e)
    {
        SetRecordingInfoText();
        SetRecordingNumberText();
        loadSphere.EnableHighlight();
        _loadSphereCollider.isTrigger = true; // prevent sphere from being interacted with while recording is loaded
    }

    public void ClearButtonPressed(object o, EventArgs e)
    {
        _recordingManager.UnloadRecording(); // just clears recording data not the objects
        _recordingManager.ClearThumbnail(); // clears objects from the thumbnail
        // remove the thumbnail info too
    }
    
    public void OnRecordingUnloaded(object o, EventArgs e)
    {
        loadSphere.DisableHighlight();
        stopSphere.DisableHighlight();
        _loadSphereCollider.isTrigger = false;
        SetRecordingInfoText();
        SetRecordingNumberText();
    }
    
    // the highlight stuff is not that well thought through... 
    // maybe make a sound when things have successfully completed!
    public void SaveButtonPressed(object o, EventArgs e)
    {
        _recordingManager.SaveRecording();
        saveSphere.EnableHighlight();
    }

    private void OnRecordingSaved(object o, EventArgs e)
    {
        saveSphere.DisableHighlight();
    }
    
    public void RecordButtonPressed(object o, EventArgs e)
    {
        if (!_isRecording)
        {
            // If there is a thumbnail with no loaded data, we remove the spawned character
            if (!_recordingManager.Recording.flags.DataLoaded)
            {
                _recordingManager.ClearThumbnail();
            }
            StartCoroutine(RecordingWithCountdown());
        }
        else
        {
            _recorder.StopRecording();
        }
    }
    
    private void OnRecordingStart(object o, EventArgs e)
    {
        recordSphereText.text = "Stop";
        _isRecording = true;
        recordSphere.EnableHighlight();
        recordCountdownText.color = Color.clear;
    }
    
    private void OnRecordingStop(object o, Recording.Flags flags)
    {
        recordSphereText.text = "Record";
        _isRecording = false;
        recordSphere.DisableHighlight();
        recordCountdownText.color = Color.clear;
    }

    public void StartButtonPressed(object o, EventArgs e)
    {
        _replayer.StartReplay();
    }
    
    private void OnReplayStart(object o, EventArgs e)
    {
        _isReplaying = true;
        startSphere.EnableHighlight();
        stopSphere.DisableHighlight();
    }
    
    public void StopButtonPressed(object o, EventArgs e)
    {
        _replayer.StopReplay();
    }
    
    private void OnReplayStop(object o, EventArgs e)
    {
        _isReplaying = false;
        startSphere.DisableHighlight();
        stopSphere.EnableHighlight();
    }
    
    public void UndoButtonPressed(object o, EventArgs e)
    {
        _recordingManager.Undo();
    }
    public void RedoButtonPressed(object o, EventArgs e)
    {
        _recordingManager.Redo();
    }
    
    public void SelectTakeoverInEditor(object o, EventArgs e)
    {
        _takeoverSelector.TakeoverTestEditor();
    }
    
    // TODO slider for frames or something like that!
    public void SetFrameManually(object o, float t)
    {
        if (Mathf.Abs(currentFrameNormalized - _previousFrameNormalized) > 0.01f)
        {
            _replayer.SetCurrentFrameManually(currentFrameNormalized);
        }
        currentFrameNormalized = _previousFrameNormalized;
    }

    public void SetSliderFromFrame()
    {
        
    }
    
    private void RecordingWithoutCountdown()
    {
        if (_uiVisible)
        {
            recordSphereText.text = "Stop";
        }
        _audioSourceButtonPress.Play();
        _recorder.StartRecording();
        _isRecording = true;
    }
    private IEnumerator RecordingWithCountdown()
    {
        recordCountdownText.color = Color.white;
        // print a recording countdown that changes the text from white to red gradually
        for (int i = 0; i < recordingCountdown; i++)
        {
            recordCountdownText.text = (recordingCountdown - i).ToString();
            recordCountdownText.color = Color.Lerp(Color.white, recordSphere.color, (float)i / recordingCountdown);
            // _audioSourceLowBeep.Play();
            yield return new WaitForSeconds(1.0f);
            Debug.Log("Countdown: " + (recordingCountdown - i));
        }

        // _audioSourceHighBeep.Play();
        
        _recorder.StartRecording();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RecorderUI))]
public class RecorderUIEditor : Editor
{
    SerializedProperty _normalizedFrame;
    private float norm = 0.0f;
    private void OnEnable()
    {
        _normalizedFrame = serializedObject.FindProperty("currentFrameNormalized");
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        
        RecorderUI recorderUI = (RecorderUI) target;
        
        if (GUILayout.Button("TakeoverAvatar"))
        {
            recorderUI.SelectTakeoverInEditor(this, EventArgs.Empty);
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        if (GUILayout.Button("Start/Stop Recording"))
        {
             recorderUI.RecordButtonPressed(this, EventArgs.Empty);
             // Debug.Log("Start/Stop Recording");
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        serializedObject.Update();
        var prevNorm = norm;
        norm = EditorGUILayout.Slider("Replay Frame (normalized):", norm, 0, 1);
        
        if (!Mathf.Approximately(prevNorm, norm))
        {
            Debug.Log(prevNorm + " " + norm);
            _normalizedFrame.floatValue = norm;
            serializedObject.ApplyModifiedProperties();
            recorderUI.SetFrameManually(this, _normalizedFrame.floatValue);
        }
        
        if (GUILayout.Button("Start Replay"))
        {
            recorderUI.StartButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Stop Replay"))
        {
            recorderUI.StopButtonPressed(this, EventArgs.Empty);
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        if (GUILayout.Button("Next >"))
        {
            recorderUI.ForwardButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Previous <"))
        {
            recorderUI.BackwardButtonPressed(this, EventArgs.Empty);
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));

        if (GUILayout.Button("Save"))
        {
            recorderUI.SaveButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Load"))
        {
            recorderUI.LoadButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Clear"))
        {
            recorderUI.ClearButtonPressed(this, EventArgs.Empty);
        }
        
        // dashed line to separate the buttons
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        if (GUILayout.Button("Undo"))
        {
            recorderUI.UndoButtonPressed(this, EventArgs.Empty);
        }
        if (GUILayout.Button("Redo"))
        {
            recorderUI.RedoButtonPressed(this, EventArgs.Empty);
        }
        
    }
}
#endif
