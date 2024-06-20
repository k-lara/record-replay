// #if UNITY_WEBRTC || UNITY_WEBRTC_UBIQ_FORK

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// so this is tricky... the microphone for the local avatar does not get created until
// the local avatar joins a room and has other people to talk to
// because we want to record things alone, we need a microphone that is ready
// even when no other peers are around

// this is a modified version of the PeerConnectionMicrophone class
public class LonelyMicrophone : MonoBehaviour
{
    public enum State
    {
        Idle,
        Starting,
        Running
    }

    public State state
    {
        get
        {
            if (!m_AudioSource || m_AudioSource.clip == null)
            {
                return State.Idle;
            }
            if (m_AudioSource.isPlaying)
            {
                return State.Running;
            }
            return State.Starting;
        }
    }
    
    public AudioStreamTrack audioStreamTrack { get; private set; }
    private RecorderAudioFilter recorderAudioFilter;
    private AudioSource m_AudioSource;
    private bool m_MicrophoneAuthorized;

    public event EventHandler MicrophoneSetUp;
    
    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
            }
#endif
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void Update()
    {
        // unity's first few updates are insanely slow which makes the microphone insanely slow too
        // so we have to wait until the normal update rate is reached before we add the mic...
        if (Time.realtimeSinceStartup < 20.0f)
        {
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
            // Wait for microphone permissions before processing any audio
            if (!m_MicrophoneAuthorized)
            {
                m_MicrophoneAuthorized = Permission.HasUserAuthorizedPermission(Permission.Microphone);

                if (!m_MicrophoneAuthorized)
                {
                    return;
                }
            }
#endif

        if (state == State.Idle)
        {
            RequireAudioSource();
            // foreach (var device in Microphone.devices)
            // {
            //     Debug.Log(device);
            // }
            m_AudioSource.clip = Microphone.Start("",true,1,AudioSettings.outputSampleRate);
            MicrophoneSetUp?.Invoke(this, EventArgs.Empty);

        }

        if (state == State.Starting)
        {
            if (Microphone.GetPosition("") > m_AudioSource.clip.frequency / 24.0f)
            {
                Debug.Log("Microphone started");
                m_AudioSource.loop = true;
                m_AudioSource.Play();
                audioStreamTrack = new AudioStreamTrack(m_AudioSource);
            }
        }
        
        // TODO do something similar when we join a room with others
        // TODO and use the VoipPeerConnection Microphone instead
        // if (state == State.Running && users.Count == 0)
        // {
        //     m_AudioSource.Stop();
        //     Microphone.End("");
        //     Destroy(m_AudioSource.clip);
        //     m_AudioSource.clip = null;
        //     audioStreamTrack.Dispose();
        //     audioStreamTrack = null;
        // }
    }
    
    private void RequireAudioSource()
    {
        if(!m_AudioSource)
        {
            m_AudioSource = GetComponent<AudioSource>();

            if (!m_AudioSource)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                recorderAudioFilter = gameObject.AddComponent<RecorderAudioFilter>();
            }
        }
    }
}
// #endif
