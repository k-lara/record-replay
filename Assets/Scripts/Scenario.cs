using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq.Avatars;
using UnityEngine;

public class Scenario : MonoBehaviour
{
    public int ScenarioNumber; // used as index for the thumbnail list via GotoThumbnail(int value)
    
    public string ScenarioDescription;

    public string PathBaseRecording; // the path to the base recording that the users have to react to (if any)

    public AudioClip ScenarioAudio; // TODO!!!
    
    public List<GameObject> UserSpawnPoints; // where the user and his avatars are spawned
    // should be the same length as UserSpawnPoints
    public List<GameObject> AvatarPrefabs; // the avatars that are used in this scenario
    public AvatarManager AvatarManager;
    
    private RecordingManager recordingManager;
    private int currentSpawnPoint;
    private int currentAvatarIndex;

    public void LoadBaseRecording()
    {
        recordingManager.GotoThumbnail(ScenarioNumber); // spawns the avatars required for the base recording
        recordingManager.LoadRecording();
    }
    
    public GameObject NextSpawnPoint()
    {
        if (currentSpawnPoint < UserSpawnPoints.Count - 1)
            currentSpawnPoint += 1;
        return UserSpawnPoints[currentSpawnPoint];
    }
    
    public GameObject PreviousSpawnPoint()
    {
        if (currentSpawnPoint > 0)
            currentSpawnPoint -= 1;
        return UserSpawnPoints[currentSpawnPoint];
    }
    
    public void NextAvatar()
    {
        currentAvatarIndex++;
        if (currentAvatarIndex < AvatarPrefabs.Count)
        {
            AvatarManager.avatarPrefab = AvatarPrefabs[currentAvatarIndex];
        }
        else
        {
            currentAvatarIndex = 0;
            AvatarManager.avatarPrefab = AvatarPrefabs[currentAvatarIndex];
        }
    }
    
    public void PreviousAvatar()
    {
        currentAvatarIndex--;
        if (currentAvatarIndex >= 0)
        {
            AvatarManager.avatarPrefab = AvatarPrefabs[currentAvatarIndex];
        }
        else
        {
            currentAvatarIndex = AvatarPrefabs.Count - 1;
            AvatarManager.avatarPrefab = AvatarPrefabs[currentAvatarIndex];
        }
    }
    
    void Awake()
    {
        var recorderGo = GameObject.Find("Recorder");
        recordingManager = recorderGo.GetComponent<RecordingManager>();
    }
        
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
