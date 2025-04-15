using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseRecordingAdditions : MonoBehaviour
{
    private RecorderUI recorderUI;
    private StudyRecorderUI studyRecorderUI;
    StudyProcedure studyProcedure;
    
    private float MaxFogValue = 0.35f;

    private Recorder recorder; 
    private Replayer replayer;
    private RecordingManager recordingManager;

    public InteractableSphere s1Sphere;
    public InteractableSphere s2Sphere;
    public InteractableSphere s3Sphere;
    
    public Scenario s1;
    public Scenario s2;
    public Scenario s3;
    
    // TODO put the used avatars in the avatar switcher
    // TODO make buttons for the 3 scenarios which load the correct audio
    // TODO start particles for Scenario 2 at the right time:
    
    private bool isRecording;
    private bool isReplaying;
    private bool s1Selected = true;
    private bool s2Selected;
    private bool s3Selected;
    
    public AudioSource backgroundAudioSource;
    public AudioSource person1AudioSource;
    public AudioSource person2AudioSource;

    private float recordingLength;
    private float currentTime;
    private bool particlesOn;
    private float particleStartTime = 5.0f;
    
    // Start is called before the first frame update
    void Start()
    {
        recorder.onRecordingStart += OnRecordingStart;
        replayer.onReplayStart += OnReplayStart;
        replayer.onReplayStop += OnReplayStop;
        var recorderGameObject = GameObject.FindWithTag("Recorder");
        recorder = recorderGameObject.GetComponent<Recorder>();
        replayer = recorderGameObject.GetComponent<Replayer>();
        recordingManager = recorderGameObject.GetComponent<RecordingManager>();
        recorderUI = recorderGameObject.GetComponent<RecorderUI>();
        studyRecorderUI = GetComponentInChildren<StudyRecorderUI>();
        
        s1Sphere.onSphereSelected += OnS1SphereSelected;
        s2Sphere.onSphereSelected += OnS2SphereSelected;
        s3Sphere.onSphereSelected += OnS3SphereSelected;
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isRecording)
        {
            // check the time of the current audio clip based on the background audio
            if (backgroundAudioSource.isPlaying)
            {
                if (currentTime >= recordingLength)
                {
                    // stop the recording
                    recorder.StopRecording();
                    
                    if (s2Selected && particlesOn)
                    {
                        StopParticlesS2();
                    }
                    isRecording = false;
                    RenderSettings.fogDensity = 0;
                }

                if (!particlesOn)
                {
                    if (s2Selected && currentTime >= particleStartTime)
                    {
                        StartParticlesS2();
                    }
                }
                else
                {
                    // increase the fog until max fog
                    var fogValue = Mathf.Lerp(0, MaxFogValue, currentTime / (recordingLength-10.0f));
                    RenderSettings.fogDensity = fogValue;
                }
            }
        }
    }

    private void OnS1SphereSelected(object o, EventArgs e)
    {
        backgroundAudioSource.clip = s1.backgroundAudio;
        person1AudioSource.clip = s1.person1Audio;
        person2AudioSource.clip = s1.person2Audio;
        s1Selected = true;
        s2Selected = s3Selected = false;
    }
    
    private void OnS2SphereSelected(object o, EventArgs e)
    {
        backgroundAudioSource.clip = s2.backgroundAudio;
        person1AudioSource.clip = s2.person1Audio;
        person2AudioSource.clip = s2.person2Audio;
        s2Selected = true;
        s1Selected = s3Selected = false;
    }
    private void OnS3SphereSelected(object o, EventArgs e)
    {
        backgroundAudioSource.clip = s3.backgroundAudio;
        person1AudioSource.clip = s3.person1Audio;
        person2AudioSource.clip = s3.person2Audio;
        s3Selected = true;
        s1Selected = s2Selected = false;
    }
    private void OnRecordingStart(object o, EventArgs e)
    {
        isRecording = true;
        StartAudioClips();
    }

    private void OnReplayStart(object o, EventArgs e)
    {
        isReplaying = true;

        if (!isRecording) // otherwise they are started in OnRecordingStart
        {
            StartAudioClips();
        }
    }

    private void OnReplayStop(object o, EventArgs e)
    {
        isReplaying = false;
        StopAudioClips();
        particlesOn = false;
    }

    private void StartAudioClips()
    {
        currentTime = 0;
        recordingLength = backgroundAudioSource.clip.length;
        // there is always a person1 audio clip,
        // but in scenario 2 there is no person2 audio clip
        if (person2AudioSource.clip)
        {
            person2AudioSource.Play();
        }
        person1AudioSource.Play();
        backgroundAudioSource.Play();
    }
    
    private void StopAudioClips()
    {
        backgroundAudioSource.Stop();
        person1AudioSource.Stop();
        person2AudioSource.Stop();
        currentTime = 0;
    }
    
    private void StartParticlesS2()
    {
            particlesOn = true;
            s2.particleSystem.Play();
    }

    private void StopParticlesS2()
    {
            particlesOn = false;
            s2.particleSystem.Stop();
    }
}
