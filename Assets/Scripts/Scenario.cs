using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq.Avatars;
using UnityEngine;
using UnityEngine.Serialization;
using Avatar = Ubiq.Avatars.Avatar;

public class Scenario : MonoBehaviour
{
    public int ScenarioIndex; // used as index for the thumbnail list via GotoThumbnail(int value)
    
    public int NumberBaseAvatars; // the number of base avatars that are used in this scenario
    
    public string ScenarioDescription;

    public string PathBaseRecording; // the path to the base recording that the users have to react to (if any)

    public AudioClip ScenarioAudio; // TODO!!!
    
    public List<GameObject> UserSpawnPoints; // where the user and his avatars are spawned
    // should be the same length as UserSpawnPoints
    public List<GameObject> AvatarPrefabs; // the avatars that are used in this scenario
    public AvatarManager AvatarManager;
    public bool avatarCreated;
    
    private RecordingManager recordingManager;
    private int currentSpawnPoint;
    private int currentAvatarIndex;
    
    void Awake()
    {
        var recorderGo = GameObject.Find("Recorder");
        recordingManager = recorderGo.GetComponent<RecordingManager>();
    }
        
    // Start is called before the first frame update
    void Start()
    {
        AvatarManager.OnAvatarCreated.AddListener(OnAvatarCreated);
    }

    public void OnAvatarCreated(Avatar avatar)
    {
        avatarCreated = true;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
