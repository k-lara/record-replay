using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

public class StudyRecorderUI : MonoBehaviour
{
    // won't do recording logic but tests the flow through the coroutine
    public bool debugTest = true;

    public GameObject countdownCanvas;
    public InteractableSphere recordSphere;
    public InteractableSphere replaySphere;
    [FormerlySerializedAs("redoRecordingSphere")] public InteractableSphere redoSphere;
    public InteractableSphere nextSphere;
    public InteractableSphere previousSphere;
    public InteractableSphere skipSphere;
    public InteractableSphere scenario1Sphere;
    public InteractableSphere scenario2Sphere;
    public InteractableSphere scenario3Sphere;
    public InteractableSphere newUserSphere;
    public InteractableSphere resumeUserSphere;

    public GameObject mainPanel;
    private Vector3 mainPanelPosition;
    private Quaternion mainPanelRotation;

    public GameObject mirror;
    public Transform mirrorPanelTransform; // position panel to the side of the mirror when mirror is active
    
    // button press checks for coroutine advancement
    public bool recordPressed;
    public bool replayPressed;
    public bool redoPressed;
    public bool nextPressed;
    public bool previousPressed;
    public bool skipPressed;
    public bool scenario1Pressed;
    public bool scenario2Pressed;
    public bool scenario3Pressed;
    public bool recordingDataLoaded;
    public bool thumbnailAvatarsSpawned;
    public bool recordingSaved;
    public bool newUserPressed;
    public bool resumeUserPressed;
    
    public TextMeshProUGUI instructionsText;
    public TextMeshProUGUI recordCountdownText;
    public TextMeshProUGUI headerText;

    public GameObject audioGameObject; 
    private AudioSource audioSourceLowBeep;
    private AudioSource audioSourceHighBeep;
    private AudioSource audioSourceButtonPress;

    private int recordingCountdown = 5;

    [HideInInspector] public Recorder recorder;
    [HideInInspector] public Replayer replayer;
    [HideInInspector] public RecordingManager recordingManager;
    [HideInInspector] public TakeoverSelector takeoverSelector;
    [HideInInspector] public AvatarTakeover avatarTakeover;
    
    
    private int maxFrameNrReplay;
    public float maxReplayTime;
    public bool maxFrameNrSet;

    public bool replaying;
    public bool recording;
    
    // Start is called before the first frame update
    void Start()
    {
        recordSphere.onSphereSelected += StartRecording;
        replaySphere.onSphereSelected += StartReplay;
        redoSphere.onSphereSelected += RedoRecordingButtonPressed;
        nextSphere.onSphereSelected += NextButtonPressed;
        previousSphere.onSphereSelected += PreviousButtonPressed;
        skipSphere.onSphereSelected += SkipButtonPressed;
        scenario1Sphere.onSphereSelected += Scenario1ButtonPressed;
        scenario2Sphere.onSphereSelected += Scenario2ButtonPressed;
        scenario3Sphere.onSphereSelected += Scenario3ButtonPressed;
        newUserSphere.onSphereSelected += NewUserButtonPressed;
        resumeUserSphere.onSphereSelected += ResumeUserButtonPressed;
        
        var audioSources = audioGameObject.GetComponents<AudioSource>();
        audioSourceLowBeep = audioSources[0];
        audioSourceHighBeep = audioSources[1];
        audioSourceButtonPress = audioSources[2];
        
        var recorderGameObject = GameObject.FindWithTag("Recorder");
        recorder = recorderGameObject.GetComponent<Recorder>();
        replayer = recorderGameObject.GetComponent<Replayer>();
        recordingManager = recorderGameObject.GetComponent<RecordingManager>();
        takeoverSelector = recorderGameObject.GetComponent<TakeoverSelector>();
        avatarTakeover = recorderGameObject.GetComponent<AvatarTakeover>();
        
        replayer.onReplaySpawned += GetMaxFrameNr;
        replayer.onReplayStop += OnReplayStop;
        recorder.onRecordingStop += OnRecordingStop;
        // recorder.onSaveReady += OnSaveReady; // gets invoked when data is ready to be saved to disk
        
        recordingManager.onThumbnailSpawned += OnThumbnailSpawned;
        recordingManager.onRecordingLoaded += OnRecordingLoaded;
        recordingManager.onRecordingSaved += OnRecordingSaved;
        
        mainPanelPosition = mainPanel.transform.localPosition;
        mainPanelRotation = mainPanel.transform.localRotation;
    }
    // the recording.flags.saveReady could also be checked instead of getting the event here

    public void OnRecordingSaved(object o, EventArgs e)
    {
        recordingSaved = true;
    }
    
    public void PanelToMirrorPosition()
    {
        mainPanel.transform.localPosition = mirrorPanelTransform.localPosition;
        mainPanel.transform.localRotation = mirrorPanelTransform.localRotation;
    }

    public void PanelToMainPosition()
    {
        mainPanel.transform.localPosition = mainPanelPosition;
        mainPanel.transform.localRotation = mainPanelRotation;
    }

    public void StartRecording(object o, EventArgs e)
    {
        if (debugTest)
        {
            recordPressed = recording = true;
            return;
        }
        
        audioSourceButtonPress.Play();
        // If there is a thumbnail with no loaded data, we remove the spawned character
        if (!recordingManager.Recording.flags.DataLoaded)
        {
            recordingManager.ClearThumbnail();
        }
        StartCoroutine(RecordingWithCountdown());
    }

    private IEnumerator RecordingWithCountdown()
    {
        countdownCanvas.SetActive(true);
        recordCountdownText.color = Color.white;
        // print a recording countdown that changes the text from white to red gradually
        for (int i = 0; i < recordingCountdown-1; i++) // -1 so we hear the high beep on 5th second and then it stops after that
        {
            recordCountdownText.text = (recordingCountdown - i).ToString();
            recordCountdownText.color = Color.Lerp(Color.white, recordSphere.color, (float)i / recordingCountdown);
            audioSourceLowBeep.Play();
            yield return new WaitForSeconds(1.0f);
            Debug.Log("Countdown: " + (recordingCountdown - i));
        }

        audioSourceHighBeep.Play();
        countdownCanvas.SetActive(false);
        
        recordPressed = recording = true;
        
        recorder.StartRecording();
    }
    public void StartReplay(object o, EventArgs e)
    {
        if (debugTest)
        {
            replayPressed = replaying = !replaying;

        }
        if (!replaying)
        {
            audioSourceButtonPress.Play();
            replayer.StartReplay();
            replayPressed = replaying = true;
        }
        else
        {
            audioSourceButtonPress.Play();
            replayer.StopReplay();
            replaying = false;
        }
    }
    
    // redo the last recording the user made
    public void RedoRecordingButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        redoPressed = true;
    }
    
    public void SetTakeoverAvatar(GameObject avatar)
    {
        avatarTakeover.takeoverPrefab = avatar;
    }

    public void NextButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        nextPressed = true;
    }
    
    public void SkipButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        skipPressed = true;
    }
    
    public void PreviousButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        previousPressed = true;
    }
    
    public void Scenario1ButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        scenario1Pressed = true;

    }
    
    public void Scenario2ButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        scenario2Pressed = true;
    }
    
    public void Scenario3ButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        scenario3Pressed = true;
    }
    
    public void NewUserButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        newUserPressed = true;
    }
    
    public void ResumeUserButtonPressed(object o, EventArgs e)
    {
        audioSourceButtonPress.Play();
        resumeUserPressed = true;
    }

    public void GetMaxFrameNr(object o, Dictionary<Guid, Replayable> replayables)
    {
        maxFrameNrReplay = replayer.GetFrameNr();
        maxReplayTime = maxFrameNrReplay * recorder.fps;
        maxFrameNrSet = true;
    }
    
    public void OnReplayStop(object o, EventArgs e)
    {
        replaying = false;
    }

    public void OnRecordingStop(object o, Recording.Flags flags)
    {
        recording = false;
    }

    public void OnRecordingLoaded(object o, EventArgs e)
    {
        recordingDataLoaded = true;
    }
    
    public void OnThumbnailSpawned(object o, List<GameObject> spawned)
    {
        thumbnailAvatarsSpawned = true;
    }
    
    public void PlayLowBeep()
    {
        audioSourceLowBeep.Play();
    }
    
    public void PlayHighBeep()
    {
        audioSourceHighBeep.Play();
    }
    
    public void PlayButtonPress()
    {
        audioSourceButtonPress.Play();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StudyRecorderUI))]
public class StudyRecorderUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        
        StudyRecorderUI recorderUI = (StudyRecorderUI) target;

        if (GUILayout.Button("Scenario 1"))
        {
            recorderUI.Scenario1ButtonPressed(null, EventArgs.Empty);
        }
        if (GUILayout.Button("Scenario 2"))
        {
            recorderUI.Scenario2ButtonPressed(null, EventArgs.Empty);
        }
        if (GUILayout.Button("Scenario 3"))
        {
            recorderUI.Scenario3ButtonPressed(null, EventArgs.Empty);
        }
        
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));

        if (GUILayout.Button("Record"))
        {
            recorderUI.StartRecording(null, EventArgs.Empty);
        }
        if (GUILayout.Button("Replay"))
        {
            recorderUI.StartReplay(null, EventArgs.Empty);
        }
        if (GUILayout.Button("Redo"))
        {
            recorderUI.RedoRecordingButtonPressed(null, EventArgs.Empty);
        }
        
        GUILayout.Box("-----", GUILayout.ExpandWidth(true), GUILayout.Height(1));

        if (GUILayout.Button("Next"))
        {
            recorderUI.NextButtonPressed(null, EventArgs.Empty);
        }
        
        if (GUILayout.Button("Skip"))
        {
            recorderUI.SkipButtonPressed(null, EventArgs.Empty);
        }
        
        if (GUILayout.Button("Previous"))
        {
            recorderUI.PreviousButtonPressed(null, EventArgs.Empty);
        }
    }
}
#endif