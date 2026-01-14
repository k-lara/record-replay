using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class RecorderExample : MonoBehaviour
{
    [SerializeField] private bool auto;
    [SerializeField] private int streamWidth = 1024;
    [SerializeField] private int streamHeight = 1024;
    [SerializeField] private int framesPerSecond = -1;
    [SerializeField] private VideoRecorder recorder;
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Transform rotateObject;
    [SerializeField] private Camera cam;
    [SerializeField] private AudioListener listener;
    [SerializeField] private AudioSource syncTestAudioSource;
    [SerializeField] private RawImage sourceImage;
    [SerializeField] private string filenameNoSuffix;
    
    private float time;
    private float autoRecordingStartTime = -1;
    
    private void Awake()
    {
        startButton.onClick.AddListener(StartRecording);
        stopButton.onClick.AddListener(StopRecording);
    }
    
    private void Start()
    {
        startButton.interactable = false;
        stopButton.interactable = false;
    }
    
    private void Update()
    {
        if (rotateObject)
        {
            float t = Time.deltaTime;
            rotateObject.Rotate(100 * t, 200 * t, 300 * t);
        }
        
        if (syncTestAudioSource)
        {
            // The synctest wav file has silence in this interval.
            if (syncTestAudioSource.time > 0.4f && syncTestAudioSource.time < 0.6f)
            {
                cam.backgroundColor = Color.green;
            }
            else
            {
                cam.backgroundColor = Color.magenta;
            }
        }
        
        if (!recorder)
        {
            startButton.interactable = false;
            stopButton.interactable = false;
            return;
        }
        
        if (recorder.state == VideoRecorder.State.Idle)
        {
            startButton.interactable = true;
            stopButton.interactable = false;
            
            
        }
        
        if (recorder.state == VideoRecorder.State.Starting 
            || recorder.state == VideoRecorder.State.Recording)
        {
            startButton.interactable = false;
            stopButton.interactable = true;
        }
        
        if (auto && recorder.state == VideoRecorder.State.Idle 
                 && autoRecordingStartTime < 0 && Time.time > 3.0f)
        {
            autoRecordingStartTime = Time.time;
            StartRecording();
        }
        
        if (auto && recorder.state == VideoRecorder.State.Recording
                 && Time.time > autoRecordingStartTime + 5.0f)
        {
            StopRecording();
        }
    }
    
    private void StartRecording()
    {
        if (!recorder)
        {
            return;
        }
        
        recorder.StartRecording(streamWidth,streamHeight,cam,listener,GetPath(),
            framesPerSecond);
        
        sourceImage.texture = cam.targetTexture;
        sourceImage.color = Color.white;
    }
    
    private void StopRecording()
    {
        if (!recorder)
        {
            return;
        }
        
        recorder.StopRecording();
    }
    
    private string GetPath()
    {
        var filename = filenameNoSuffix+".mp4";
        return Path.Combine(Application.persistentDataPath,filename);
    }
}
