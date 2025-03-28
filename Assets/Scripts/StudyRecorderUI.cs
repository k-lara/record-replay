using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class StudyRecorderUI : MonoBehaviour
{
    public InteractableSphere recordSphere;
    public InteractableSphere replaySphere;
    public InteractableSphere redoRecordingSphere;
    public InteractableSphere nextSphere;
    public InteractableSphere previousSphere;
    public InteractableSphere skipSphere;
    public InteractableSphere scenario1Sphere;
    public InteractableSphere scenario2Sphere;
    public InteractableSphere scenario3Sphere;

    public GameObject mirror;
    
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
    
    public TextMeshProUGUI instructionsText;
    public TextMeshProUGUI recordCountdownText;

    public GameObject audioGameObject; 
    private AudioSource audioSourceLowBeep;
    private AudioSource audioSourceHighBeep;
    private AudioSource audioSourceButtonPress;

    private int recordingCountdown = 5;

    public Recorder recorder;
    public Replayer replayer;
    public RecordingManager recordingManager;
    public TakeoverSelector takeoverSelector;
    
    
    public int maxFrameNrReplay;
    public float maxReplayTime;
    public bool maxFrameNrSet;

    public bool replaying;
    public bool recording;
    
    // Start is called before the first frame update
    void Start()
    {
        recordSphere.onSphereSelected += StartRecording;
        replaySphere.onSphereSelected += StartReplay;
        redoRecordingSphere.onSphereSelected += RedoRecordingButtonPressed;
        nextSphere.onSphereSelected += NextButtonPressed;
        previousSphere.onSphereSelected += PreviousButtonPressed;
        skipSphere.onSphereSelected += SkipButtonPressed;
        scenario1Sphere.onSphereSelected += Scenario1ButtonPressed;
        scenario2Sphere.onSphereSelected += Scenario2ButtonPressed;
        scenario3Sphere.onSphereSelected += Scenario3ButtonPressed;
        
        var audioSources = audioGameObject.GetComponents<AudioSource>();
        audioSourceLowBeep = audioSources[0];
        audioSourceHighBeep = audioSources[1];
        audioSourceButtonPress = audioSources[2];
        
        var recorderGameObject = GameObject.FindWithTag("Recorder");
        recorder = recorderGameObject.GetComponent<Recorder>();
        replayer = recorderGameObject.GetComponent<Replayer>();
        recordingManager = recorderGameObject.GetComponent<RecordingManager>();
        takeoverSelector = recorderGameObject.GetComponent<TakeoverSelector>();
        
        replayer.onReplaySpawned += GetMaxFrameNr;
        replayer.onReplayStop += OnReplayStop;
        recorder.onRecordingStop += OnRecordingStop;
        
        recordingManager.onThumbnailSpawned += OnThumbnailSpawned;
        recordingManager.onRecordingLoaded += OnRecordingLoaded;
    }

    public void StartRecording(object o, EventArgs e)
    {
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
        recordCountdownText.color = Color.white;
        // print a recording countdown that changes the text from white to red gradually
        for (int i = 0; i < recordingCountdown; i++)
        {
            recordCountdownText.text = (recordingCountdown - i).ToString();
            recordCountdownText.color = Color.Lerp(Color.white, recordSphere.color, (float)i / recordingCountdown);
            audioSourceLowBeep.Play();
            yield return new WaitForSeconds(1.0f);
            Debug.Log("Countdown: " + (recordingCountdown - i));
        }

        audioSourceHighBeep.Play();
        
        recordPressed = recording = true;
        
        recorder.StartRecording();
    }
    public void StartReplay(object o, EventArgs e)
    {
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
        if (redoPressed) return;
        audioSourceButtonPress.Play();
        takeoverSelector.TakeoverLastReplay();
        redoPressed = true;
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
        thumbnailAvatarsSpawned = false;
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
