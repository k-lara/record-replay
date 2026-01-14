using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using Ubiq.Avatars;
using UnityEngine;
using Avatar = Ubiq.Avatars.Avatar;

// an AudioReplayable gets added automatically to a newly spawned recorded avatar.
// the local player avatar never has this component because it doesn't need it.
public class AudioReplayable : MonoBehaviour
{
    private Replayer _mReplayer;
    private string m_Folder;
    private string m_AudioFile;
    private bool m_IsLoaded;
    private bool m_IsPlaying;
    private int m_channels;
    
    private AudioSource m_AudioSource;
    private VoipAvatar m_VoipAvatar;
    private Avatar m_Avatar;

    public event EventHandler<AudioInfoData> OnAudioReplayLoaded;

    public struct AudioInfoData
    {
        public float[] data;
        public int channels;
        public int frequency;
        public Transform transform;
    }
    
    private void Awake()
    {
        // _mReplayer = GameObject.FindWithTag("Recorder").GetComponent<ReplayerOld>();
        // _mReplayer.onThumbnailCreated += OnThumbnailCreated;
        // _mReplayer.onReplayCreated += OnReplayCreated;
        _mReplayer.onReplayStart += OnReplayStart;
        _mReplayer.onReplayStop += OnReplayStop;
        
        m_VoipAvatar = GetComponent<VoipAvatar>();
        m_Avatar = GetComponent<Avatar>();
        
        // we add an audio source to the game object that is the position of the head of the avatar
        // we also add a filter which is used for audio recording
        m_AudioSource = m_VoipAvatar.audioSourcePosition.gameObject.AddComponent<AudioSource>();
        m_VoipAvatar.audioSourcePosition.gameObject.AddComponent<RecorderAudioFilter>();
    }

    private void OnReplayStart(object o, EventArgs e)
    {
        if (!m_IsLoaded) return;
        if (m_AudioSource.isPlaying)
        {
            // if already playing Play() starts from the beginning again
            m_AudioSource.Play();
        }
        else
        {
            if (m_AudioSource.time > 0.0f)
            {
                m_AudioSource.UnPause();
            }
            else
            {
                m_AudioSource.Play();
            }
        }
        m_IsPlaying = true;
    }

    private void OnReplayStop(object o, EventArgs e)
    {
        if (!m_IsPlaying) return;
        m_IsPlaying = false;
        m_AudioSource.Pause();
    }

    private void OnDestroy()
    {
        _mReplayer.onReplayStart -= OnReplayStart;
        _mReplayer.onReplayStop -= OnReplayStop;
        // _mReplayer.onThumbnailCreated -= OnThumbnailCreated;
        // _mReplayer.onReplayCreated -= OnReplayCreated;
        
    }

    public void ReplayAudioFromStart()
    {
        if (!m_IsLoaded) return;
        m_AudioSource.Play();
        // Debug.Log("Replay audio from start");
    }

    // on replay is called when we already have the spawned objects and the data is loaded from the files
    // we then start loading the audio data
    // is called from coroutine in Replayer
    private void OnReplayCreated(object o, string folder)
    {
        Task<float[]> task = Task.Run(() => ReadAudioData(folder));
        var audioData = task.Result;
        AddAudioDataToClip(audioData, m_channels);
        var audioInfoData = new AudioInfoData()
        {
            data = audioData,
            channels = m_channels,
            frequency = AudioSettings.outputSampleRate,
            transform = m_VoipAvatar.audioSourcePosition
        };
        OnAudioReplayLoaded?.Invoke(this, audioInfoData);
    }
    
    // we create an audio source where the head of the character is
    // and load the audio data into the audio clip
    // the VoipAvatar has a reference to the position where the audio source should be
    private async Task<float[]> ReadAudioData(string folder)
    {
        using (StreamReader sr = new StreamReader(Path.Combine(folder, m_AudioFile)))
        {
            string line;
            var data = new List<float[]>();
            while ((line = await sr.ReadLineAsync()) != null)
            {
                var audioData = JsonUtility.FromJson<RecorderAudioFilter.AudioData>(line);
                data.Add(audioData.data);
                m_channels = audioData.channels;
            }
            // convert to float array
            return data.SelectMany(x => x).ToArray();
        }
    }
    private void AddAudioDataToClip(float[] data, int channels)
    {
        // m_AudioSource.loop = true;
        m_AudioSource.clip = AudioClip.Create("replayedAudio", data.Length, channels,
            AudioSettings.outputSampleRate, false);
        m_AudioSource.clip.SetData(data, 0);
        m_IsLoaded = true;
        // Debug.Log("Audio data loaded");
    }
    
    // we check which game object this AudioReplayable is attached to
    // by comparing it to the game objects in the spawned list
    // if we find it in the list, we try and get the ID from the thumbnail data
    // which should be in the same order as the spawned game objects list
    // once we have the ID, we can fetch an audio file with that ID
    // we do not load the audio here, we just get the path to the file
    // because we do not want to load the audio until we actually load the full replay
    // and this is at that point just a thumbnail (preview)
    // private void OnThumbnailCreated(object o, RecorderOld.ThumbnailData data)
    // {
    //     var spawnedObjects = _mReplayer.GetSpawnedObjectsList();
    //     for (int i = 0; i < spawnedObjects.Count; i++)
    //     {
    //         if (spawnedObjects[i] == gameObject)
    //         {
    //             var id = data.recordableIds[i];
    //             m_AudioFile = "audio_" + id + ".txt";
    //             Debug.Log("Found gameobject for audio data: " + id);
    //         }
    //     }
    // }
}
