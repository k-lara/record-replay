using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// grabs the audio data during a recording
// probably should not do anything if not recording
public class RecorderAudioFilter : MonoBehaviour
{

    private ConcurrentQueue<float[]> m_dataArrayQueue = new ConcurrentQueue<float[]>();
    private float[] copy;
    
    [Serializable]
    public struct AudioData
    {
        [SerializeField] public float[] data;
        [SerializeField] public int channels;

        public AudioData(float[] data, int channels)
        {
            this.data = data;
            this.channels = channels;
        }
        
        public override string ToString()
        {
            string s = "";
            for (int i = 0; i < data.Length; i++)
            {
                s += data[i] + " ";
            }
            return s;
        }
    }
    
    public event EventHandler<AudioData> OnAudioData;
    private ConcurrentQueue<AudioData> _audioDataQueue = new ConcurrentQueue<AudioData>();
    private int m_queueSize = 48000;
    
    private Recorder _recorder;
    private bool _isRecording;
    
    private void OnEnable()
    {
        _recorder = GameObject.FindWithTag("Recorder").GetComponent<Recorder>();
        _recorder.onRecordingStart += OnRecordingStart;
        // _recorder.onRecordingStop += OnRecordingStop;
        
        // fill queue with float arrays of size 512
        for (var i = 0; i < m_queueSize; i++)
        {
            m_dataArrayQueue.Enqueue(new float[512]);
        }
        
    }

    private void OnDisable()
    {
        // remove event listeners
        _recorder.onRecordingStart -= OnRecordingStart;
    }

    private void OnRecordingStart(object o, EventArgs e)
    {
        _isRecording = true;
    }

    private void OnRecordingStop(object o, EventArgs e)
    {
        _isRecording = false;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }
    
    // during recording dequeue audio data and write it to file
    void Update()
    {

        while (_audioDataQueue.TryDequeue(out var audioData))
        {   
            // Debug.Log(audioData.data[14]);
            // Debug.Log(audioData.ToString());
            if (_isRecording)
                OnAudioData?.Invoke(this, audioData);
            
            // keep the queue size constant
            m_dataArrayQueue.Enqueue(new float[512]);
        }
        
    }

    // private float[] copy = new float[512];
    
    // gather audio data in queue during a recording
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_isRecording) return;

        if (m_dataArrayQueue.TryDequeue(out copy))
        {
            for (var i = 0; i < data.Length; i++)
            {
                copy[i] = data[i];
            }
            
            _audioDataQueue.Enqueue(new AudioData(copy, channels));
        }
        
    }
}
