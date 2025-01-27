using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ubiq.Avatars;
using Ubiq.Rooms;
using Ubiq.Voip;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

public class AudioRecordable : MonoBehaviour
{
    // private Avatar m_Avatar;
    // private VoipAvatar m_VoipAvatar;
    // private VoipPeerConnectionManager m_PeerConnectionManager; // for multi-user recordings
    // private RoomClient m_RoomClient;
    // private PeerConnectionMicrophone m_Microphone;
    // private LonelyMicrophone m_LonelyMicrophone; // mic we use for single-user recordings
    // private RecorderAudioFilter m_RecorderAudioFilter;
    //
    // private Recorder _mRecorder;
    // private StreamWriter m_AudioWriter;
    //
    // private bool m_SkipSamples;
    // private AudioSource m_AudioSource;
    // private float m_StartPosition; // where to start recording from in the audio data
    // private float m_SkippedSamples;
    // private int m_Multiplier = 2; // how many times to skip m_StartPosition samples
    //
    // // Start is called before the first frame update
    // void Start()
    // {
    //     _mRecorder = GameObject.FindWithTag("Recorder").GetComponent<Recorder>();
    //     _mRecorder.onRecordingStart += OnRecordingStart;
    //     // _mRecorder.onRecordingStop += OnRecordingStop;
    //     
    //     m_Avatar = GetComponent<Avatar>();
    //     m_VoipAvatar = GetComponent<VoipAvatar>();
    //     m_PeerConnectionManager = VoipPeerConnectionManager.Find(this);
    //     m_RoomClient = GetComponentInParent<RoomClient>();
    //     
    //     // a recorded avatar should not have a peer uuid
    //     Debug.Log("peer uuid:" + m_Avatar.Peer.uuid);
    //
    //     if (m_Avatar.IsLocal)
    //     {
    //         m_LonelyMicrophone = gameObject.AddComponent<LonelyMicrophone>();
    //         m_LonelyMicrophone.MicrophoneSetUp += (sender, e) =>
    //         {
    //             m_RecorderAudioFilter = m_LonelyMicrophone.gameObject.GetComponent<RecorderAudioFilter>();
    //             m_RecorderAudioFilter.OnAudioData += OnAudioData;
    //             m_AudioSource =  m_LonelyMicrophone.gameObject.GetComponent<AudioSource>();
    //             m_StartPosition = (m_AudioSource.clip.frequency / 8.0f) * m_Multiplier;
    //         };
    //
    //     }
    //     // if avatar is not local it is either another peer's avatar or a recorded one
    //     else
    //     {
    //         // TODO do recorded one first so we can test if it works
    //         // TODO still need to handle remote peers separtely!
    //         // when this game object is a recorded avatar,
    //         // it should have a RecorderAudioFilter component attached to the game object referenced by the VoipAvatar
    //         m_RecorderAudioFilter = m_VoipAvatar.audioSourcePosition.gameObject.GetComponent<RecorderAudioFilter>();
    //         m_RecorderAudioFilter.OnAudioData += OnAudioData;
    //         m_AudioSource = m_VoipAvatar.audioSourcePosition.gameObject.GetComponent<AudioSource>();
    //         m_StartPosition = (m_AudioSource.clip.frequency / 8.0f);
    //     }
    //     
    // }
    //
    // private void OnDestroy()
    // {
    //     if (m_AudioWriter != null)
    //     {
    //         m_AudioWriter.Dispose();
    //     }
    //     
    //     _mRecorder.onRecordingStart -= OnRecordingStart;
    //     // _mRecorder.onRecordingStop -= OnRecordingStop;
    // }
    //
    // private void OnRecordingStart(object o, EventArgs e)
    // {
    //     m_SkipSamples = true;
    //     m_SkippedSamples = 0;
    //     // m_AudioWriter = new StreamWriter(Path.Combine(folder, "audio_" + m_Avatar.NetworkId + ".txt"));
    // }
    //
    // private void OnRecordingStop(object o, EventArgs e)
    // {
    //     if (m_AudioWriter != null)
    //     {
    //         m_AudioWriter.Dispose();
    //     }
    // }
    //
    // // this is only called when we are recording
    // private void OnAudioData(object sender, RecorderAudioFilter.AudioData e)
    // {
    //     if (m_SkipSamples)
    //     {
    //         // remove some samples at the beginning to reduce latency
    //         
    //         // calculate how many samples to skip
    //         m_SkippedSamples += e.data.Length;
    //         // Debug.Log(m_SkippedSamples + " " + m_StartPosition);
    //         // if we have skipped enough samples, we can start recording
    //         if (m_SkippedSamples >= m_StartPosition)
    //         {
    //             m_SkipSamples = false;
    //             var s = e.data.Length - (m_SkippedSamples - m_StartPosition);
    //             Debug.Log(s);
    //             Debug.Log(m_StartPosition);
    //             // Debug.Log(e.data.Length);
    //             // copy data to new shorter data array
    //             var shorterData = new float[e.data.Length - (int)s];
    //             for (int i = (int)s; i < e.data.Length; i++)
    //             {
    //                 shorterData[i - (int)s] = e.data[i];
    //             }
    //             e.data = shorterData;
    //         }
    //         else
    //         {
    //             // Debug.Log("return");
    //             return;
    //         }
    //     }
    //     // Debug.Log("AudioRecordable - Writing audio data");
    //     m_AudioWriter.WriteLine(JsonUtility.ToJson(e));
    // }
}
