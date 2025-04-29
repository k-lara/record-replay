using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

public class BaseRecordingAdditions : MonoBehaviour
{
    private RecorderUI recorderUI;
    
    private float MaxFogValue = 0.35f;

    private Recorder recorder; 
    private Replayer replayer;
    // private RecordingManager recordingManager;

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

    private float recordingLength;
    private float currentTime;
    private bool particlesOn;
    private float particleStartTime = 5.0f;
    
    public XROrigin XrOrigin;
    private Camera cameraMain;
    private const float MetaAvatarDefaultCamHeight = 1.65f;

    
    // Start is called before the first frame update
    void Start()
    {
        cameraMain = Camera.main;
        var recorderGameObject = GameObject.FindWithTag("Recorder");
        recorder = recorderGameObject.GetComponent<Recorder>();
        replayer = recorderGameObject.GetComponent<Replayer>();
        // recordingManager = recorderGameObject.GetComponent<RecordingManager>();
        recorderUI = recorderGameObject.GetComponent<RecorderUI>();
        recorder.onRecordingStart += OnRecordingStart;
        recorder.onRecordingStop += OnRecordingStop;
        replayer.onReplayStart += OnReplayStart;
        replayer.onReplayStop += OnReplayStop;
        
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
            else
            {
                // stop the recording
                recorderUI.MakeButtonSound();
                recorder.StopRecording();
            }
            currentTime += Time.deltaTime;
        }

        if (isReplaying)
        {
            // Debug.Log(replayer.currentFrame);
            // Debug.Log(backgroundAudioSource.time);
            
        }
    }
    
    
    public IEnumerator ResizeUser()
    {
        var userCamHeight = cameraMain.transform.position.y;
        var userCamHeightOffset = MetaAvatarDefaultCamHeight - cameraMain.transform.position.y;
        Debug.Log($"Resize user height from camera height: {userCamHeight:F} to {MetaAvatarDefaultCamHeight:F} (diff: {userCamHeightOffset:F})");
        var newCamHeight = new Vector3(XrOrigin.transform.position.x, userCamHeightOffset, XrOrigin.transform.position.z);
        XrOrigin.transform.position = newCamHeight;
        yield return null;
    }

    private void OnS1SphereSelected(object o, EventArgs e)
    {
        StartCoroutine(ResizeUser());
        backgroundAudioSource.clip = s1.backgroundAudio;
        s1.person1AudioSource.clip = s1.person1Audio;
        s1.person2AudioSource.clip = s1.person2Audio;
        s1Selected = true;
        s2Selected = s3Selected = false;
        Debug.Log("AudioClips: " + s1.person1Audio.name + " " + s1.person2Audio.name + " " + s1.backgroundAudio.name);
        Debug.Log("MonoStereo: " + s1.person1AudioSource.clip.channels);
        Debug.Log("MonoStereo: " + s1.person2AudioSource.clip.channels);
        Debug.Log("MonoStereo: " + backgroundAudioSource.clip.channels);
    }
    
    private void OnS2SphereSelected(object o, EventArgs e)
    {
        StartCoroutine(ResizeUser());
        backgroundAudioSource.clip = s2.backgroundAudio;
        s2.person1AudioSource.clip = s2.person1Audio;
        s2Selected = true;
        s1Selected = s3Selected = false;
        Debug.Log("AudioClips: " + s2.person1Audio.name + " " + s2.backgroundAudio.name);
        Debug.Log("MonoStereo: " + s2.person1AudioSource.clip.channels);
        Debug.Log("MonoStereo: " + backgroundAudioSource.clip.channels);
    }
    private void OnS3SphereSelected(object o, EventArgs e)
    {
        StartCoroutine(ResizeUser());
        backgroundAudioSource.clip = s3.backgroundAudio;
        s3.person1AudioSource.clip = s3.person1Audio;
        s3.person2AudioSource.clip = s3.person2Audio;
        s3Selected = true;
        s1Selected = s2Selected = false;
        Debug.Log("AudioClips: " + s3.person1Audio.name + " " + s3.person2Audio.name + " " + s3.backgroundAudio.name);
        Debug.Log("MonoStereo: " + s3.person1AudioSource.clip.channels);
        Debug.Log("MonoStereo: " + s3.person2AudioSource.clip.channels);
        Debug.Log("MonoStereo: " + backgroundAudioSource.clip.channels);
    }
    private void OnRecordingStart(object o, EventArgs e)
    {
        isRecording = true;
        StartAudioClips();
        RenderSettings.fogDensity = 0;
    }

    private void OnRecordingStop(object o, Recording.Flags flags)
    {
        isRecording = false;
        StopAudioClips();
        StopParticlesS2();
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
        // also stop recording!
        recorder.StopRecording();
        particlesOn = false;
    }

    private void StartAudioClips()
    {
        currentTime = 0;
        recordingLength = backgroundAudioSource.clip.length;

        if (s1Selected)
        {
            backgroundAudioSource.Play();
            s1.person1AudioSource.Play();
            s1.person2AudioSource.Play();
        }
        else if (s2Selected)
        {
            backgroundAudioSource.Play();
            s2.person1AudioSource.Play();
        }
        else if (s3Selected)
        {
            backgroundAudioSource.Play();
            s3.person1AudioSource.Play();
            s3.person2AudioSource.Play();
        }
    }
    
    private void StopAudioClips()
    {
        if (s1Selected)
        {
            backgroundAudioSource.Stop();
            s1.person1AudioSource.Stop();
            s1.person2AudioSource.Stop();
        }
        else if (s2Selected)
        {
            backgroundAudioSource.Stop();
            s2.person1AudioSource.Stop();
        }
        else if (s3Selected)
        {
            backgroundAudioSource.Stop();
            s3.person1AudioSource.Stop();
            s3.person2AudioSource.Stop();
        }
        
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
