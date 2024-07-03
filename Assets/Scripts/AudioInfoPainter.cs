using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// Uses the audio data to paint a visual representation of the audio
// in a moving window above the avatar's head

public class AudioInfoPainter : MonoBehaviour
{
    private ReplayInfo m_ReplayInfo;
    private AudioReplayable.AudioInfoData m_AudioInfoData;
    private LineRenderer m_LineRenderer;
    private SpriteMask m_SpriteMask;
    private Sprite m_Sprite;
    private GameObject m_LineRendererObject;
    private bool m_isPlaying;
    private float m_deltaTime;

    private float m_Offset = 0.25f;
    private float intervalInSeconds = 5;
    private int pointsPerInterval; // depends on length of intervalInSeconds
    private int pointsPerSecond;
    private int currentPointIndex;
    
    // y-coordinates of the points on the line renderer
    // x coordinates are the time
    // (z is fixed)
    private List<Vector3> m_Points = new List<Vector3>();
    
    void Awake()
    {
        // m_LineRenderer = GetComponent<LineRenderer>();
        m_ReplayInfo = GetComponent<ReplayInfo>();
        
        m_ReplayInfo.OnReplayInfoAudioDataLoaded += OnReplayInfoAudioDataLoaded;
        m_ReplayInfo.OnReplayInfoReplayStart += OnReplayInfoReplayStart;
        m_ReplayInfo.OnReplayInfoReplayStop += OnReplayInfoReplayStop;
    }

    void Start()
    {
    }

    private void OnReplayInfoReplayStart(object o, EventArgs e)
    {
        if (m_isPlaying)
            m_deltaTime = 0.0f;
        
        m_isPlaying = true;
    }
    
    private void OnReplayInfoReplayStop(object o, EventArgs e)
    {
        m_isPlaying = false;
    }

    private void OnReplayInfoAudioDataLoaded(object o, AudioReplayable.AudioInfoData audioInfoData)
    {
        m_AudioInfoData = audioInfoData;
        Debug.Log("OnReplayInfoAudioDataLoaded " + audioInfoData.data.Length);
        
        // add a line renderer to a game object at the head position of the avatar
        m_LineRendererObject = new GameObject("AudioInfo");
        m_LineRendererObject.transform.parent = m_ReplayInfo.replayInfoGameObject.transform;
        m_LineRendererObject.transform.localPosition = Vector3.zero;
        m_LineRendererObject.transform.localRotation = Quaternion.identity;
        
        m_LineRenderer = m_LineRendererObject.AddComponent<LineRenderer>();
        m_LineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        m_LineRenderer.alignment = LineAlignment.View;
        m_LineRenderer.useWorldSpace = false;
        m_LineRenderer.receiveShadows = false;
        
        // draw audio data
        var sampleCount = audioInfoData.data.Length / audioInfoData.channels;
        
        var lengthSeconds = (float)sampleCount / audioInfoData.frequency;
        var roundedSeconds = Mathf.FloorToInt(lengthSeconds);
        var fraction = lengthSeconds - roundedSeconds;

        Debug.Log("sample count " + sampleCount);
        Debug.Log("frequency " + audioInfoData.frequency);
        Debug.Log("seconds " + lengthSeconds);

        var volumeSum = 0.0f;
        var numSamples = 0;
        var spacing = 0.005f;
        var x = 0.0f;
        var z = 0.0f;
        for (var i = 0; i < audioInfoData.data.Length; i += audioInfoData.channels)
        {
            if (numSamples == audioInfoData.frequency/30)
            {
                // average volume of the last second
                var volume = volumeSum / numSamples;
                // var volume = volumeSum;
                x += spacing;
                m_Points.Add(new Vector3(x, volume, z));
                // Debug.Log("Add sample: " + volume);
                volumeSum = 0.0f;
                numSamples = 0;
            }
            volumeSum += Mathf.Abs(audioInfoData.data[i]);
            numSamples++;
        }
        // add one last point for whatever fraction of a second is left
        m_Points.Add(new Vector3(x, volumeSum / numSamples, z));
        Debug.Log("Num points: " + m_Points.Count);
        // m_LineRenderer.widthCurve.AddKey(0, 0.01f);
        // m_LineRenderer.widthCurve.AddKey(1, 0.01f);
        m_LineRenderer.startWidth = 0.01f;
        m_LineRenderer.endWidth = 0.01f;
        m_LineRenderer.positionCount = m_Points.Count;
        m_LineRenderer.SetPositions(m_Points.ToArray());
        
        // compute number of points per interval
        pointsPerInterval = Mathf.FloorToInt(intervalInSeconds * (m_Points.Count - 1) / roundedSeconds);
        pointsPerSecond = Mathf.FloorToInt(pointsPerInterval / intervalInSeconds);
        
        // set the first pointsPerInterval
        m_LineRenderer.positionCount = pointsPerInterval;
        m_LineRenderer.SetPositions(m_Points.GetRange(0, pointsPerInterval).ToArray());
        currentPointIndex++;

        // define how many samples per second we want to draw
        // maybe we always show 10 seconds at once
        // draw interval of 10 seconds or less
        // how many points in the array are 10 seconds





    }
    
    

    // Update is called once per frame
    void Update()
    {
        if (m_isPlaying)
        {
            m_deltaTime += Time.deltaTime;
            // TODO SET THE X COORDINATE WITHIN THE INTERVAL CORRECTLY
            // OTHERWISE THE LINE KEEPS MOVING AWAY
            // we draw the next interval if enough frames have passed, so we can remove the current first point and add another at the end
            if (m_deltaTime > 1.0f / pointsPerSecond)
            {
                if (currentPointIndex < m_Points.Count)
                {
                    m_deltaTime = 0.0f;
                    if (currentPointIndex < m_Points.Count - pointsPerInterval)
                    {
                        m_LineRenderer.SetPositions(m_Points.GetRange(currentPointIndex, pointsPerInterval).ToArray());
                    }
                    else
                    {
                        m_LineRenderer.SetPositions(m_Points.GetRange(currentPointIndex, m_Points.Count-currentPointIndex).ToArray());
                    }
                    currentPointIndex++;
                }
                else
                {   
                    // end of replay reached, start again
                    m_deltaTime = 0.0f;
                    currentPointIndex = 0;
                }
               
               
               
            }

        }
    }

    private void OnDestroy()
    {
        m_ReplayInfo.OnReplayInfoAudioDataLoaded -= OnReplayInfoAudioDataLoaded;
        
        // make sure to also delete the ReplayInfo game object where the LineRenderer is attached to
        Destroy(m_LineRendererObject);
        
    }
}
