using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq.Avatars;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;


/*
 * The ReplayInfo displays replay information that is helpful for the player when recording over previous replays.
 * We want to display the audio information of each avatar above their head in a moving window.
 * We also want to display the gaze direction of the avatar as they speak to make it easier for the player
 * to understand where the avatar is looking.
 */
public class ReplayInfo : MonoBehaviour
{
    public GameObject replayInfoGameObject;
    
    private Replayable m_Replayable;
    private AudioReplayable m_AudioReplayable;
    private Replayer m_Replayer;

    private Recordable.RecordableData m_replayableData;
    private AudioReplayable.AudioInfoData m_audioReplay;
    
    public event EventHandler<Recordable.RecordableData> OnReplayInfoDataLoaded;
    public event EventHandler<AudioReplayable.AudioInfoData> OnReplayInfoAudioDataLoaded;
    public event EventHandler OnReplayInfoReplayStart;
    public event EventHandler OnReplayInfoReplayStop;
    
    private LineRenderer lineRenderer;
    // the cursor indicates where the current replay is in time
    // we get the cursor location from the voip avatar who has the head position 
    // and we add an offset to that position
    private Transform cursorLocation;
    private float offset = 0.25f;

    private Avatar avatar;
    private Camera camera;
    
    void Awake()
    {
        camera = Camera.main;
        
        cursorLocation = GetComponent<VoipAvatar>().audioSourcePosition;
        avatar = GetComponent<Avatar>();
        
        m_Replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();
        m_Replayer.onReplayStart += OnReplayStart;
        m_Replayer.onReplayStop += OnReplayStop;

        replayInfoGameObject = new GameObject("ReplayInfo-" + avatar.NetworkId);
        replayInfoGameObject.transform.position = cursorLocation.position;
        // draw the cursor with the lineRenderer
        DrawCursor();
        
        m_Replayable = GetComponent<Replayable>();
        m_Replayable.OnReplayableDataLoaded += OnReplayableDataLoaded;
        gameObject.AddComponent<GazeInfoPainter>();
        
        // audio recordings might not always be available or needed
        m_AudioReplayable = GetComponent<AudioReplayable>();
        if (m_AudioReplayable)
        {
            Debug.Log("Add AudioInfoPainter");
            m_AudioReplayable.OnAudioReplayLoaded += OnAudioReplayLoaded;
            gameObject.AddComponent<AudioInfoPainter>();

        }
        
    }

    private void DrawCursor()
    {
        lineRenderer = replayInfoGameObject.AddComponent<LineRenderer>();
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.useWorldSpace = false;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.black;
        lineRenderer.endColor = Color.black;
        // draw a vertical line as cursor
        var positions = new Vector3[2];
        positions[0] = new Vector3(0.0f, 0.0f, 0.0f);
        positions[1] = new Vector3(0.0f, 0.1f, 0.0f);
        lineRenderer.SetPositions(positions);
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.positionCount = positions.Length;
    }

    private void OnReplayStart(object o, EventArgs e)
    {
        OnReplayInfoReplayStart?.Invoke(this, EventArgs.Empty);
    }

    private void OnReplayStop(object o, EventArgs e)
    {
        OnReplayInfoReplayStop?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnReplayableDataLoaded(object sender, Recordable.RecordableData data)
    {
        m_replayableData = data;
        OnReplayInfoDataLoaded?.Invoke(this, m_replayableData);
        
    }
    
    private void OnAudioReplayLoaded(object sender, AudioReplayable.AudioInfoData audioInfoData)
    {
        m_audioReplay = audioInfoData;
        OnReplayInfoAudioDataLoaded?.Invoke(this, audioInfoData);
        
    }

    // Update is called once per frame
    void Update()
    {
        replayInfoGameObject.transform.position = new Vector3(cursorLocation.transform.position.x, cursorLocation.transform.position.y + offset, cursorLocation.transform.position.z);
        replayInfoGameObject.transform.LookAt(camera.transform);

    }

    private void OnDestroy()
    {
        if (m_AudioReplayable)
        {
            m_AudioReplayable.OnAudioReplayLoaded -= OnAudioReplayLoaded;
        }

        m_Replayable.OnReplayableDataLoaded -= OnReplayableDataLoaded;
        m_Replayer.onReplayStart -= OnReplayStart;
        m_Replayer.onReplayStop -= OnReplayStop;
    }
}
