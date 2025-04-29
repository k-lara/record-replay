using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

/*
 * 1) Introduction to the study (consent form will be outside the VR environment)
 * 1.1) (Optional test run)
 * 2) Load the thumbnails for the scenarios and create new folder for participant
 *      and make a copy of the base recordings
 * 
 */
public class StudyProcedureSteps : MonoBehaviour
{
    private StudyProcedure studyProcedure;
    private StudyRecorderUI studyUI;
    private Camera cameraMain;

    private int currentScenarioIndex = 0; // [0-2] for the three scenarios
    
    private static string headerIntroduction = "Introduction";
    private static string introduction = "You will experience 3 different scenarios in this room. \n" +
                                  "In each scenario, you will have to react to something that is happening in the scene. \n" +
                                  "Your head and hand movements as well as your facial expressions will be recorded. \n";
    
    private static string headerTutorial = "Tutorial";
    private static string recordingInstructions = "Tutorial: How to do a recording? \n" +
                                           "\n" +
                                           "\"Pinch\" the record button to start a recording. It will always stop automatically.\n" +
                                           "There is a 5 second countdown before the recording starts and a 2 second countdown before it stops. \n" +
                                           "When tracking is lost temporarily, recording will stop immediately and you can try again. \n" +
                                           "To reposition the menu, do the menu gesture with your left hand!";
    
    private static string headerCharacterDescription = "Character Description";
    
    private static string mirrorInstructions = "Between recording sessions, you will always see your current avatar in the mirror. \n" +
                                        "It can be quite satisfying to see your avatar's face move when yours does! :) \n" +
                                        "You might need to exaggerate your facial expressions to get the best results on the avatar. \n" +
                                        "You can also check if the tracking works as expected! \n" +
                                        "Before a recording, you will also get the character description for your current character and what your character should do during the recording! \n" +
                                        "Example: You are very happy about something and wave your hands!";
    
    private static string replayInstructions = "If you take a step to the side, you will see you recorded character. Each character you record during the scenarios will be added to the scenario and replayed automatically whenever you record another character. \n" +
                                        "You won't have this option later, but just to see how your recording looks like, you can press the REPLAY button now!";
    
    private static string takeoverInstructions = "Press record again to overwrite your previous recording!";
    
    private static string finishHowTo = "You are now ready to start the study! \n" +
                                 "Whenever instructions aren't clear, you can ask the experimenter for help! \n" +
                                 "It occasionally happens that tracking is lost and unrecoverable and we need to restart the app or the Quest.";

    private static string headerScenarioSelection = "Scenario Selection";
    private static string scenarioSelection = "You will experience 3 different incidents that are taking place at a Christmas party. \n" +
                                       "In each scenario, you will successively play the role of 5 different guests who are reacting to the incident." +
                                       "The character descriptions are meant to give you an entry point into the character's role. Feel free to improvise as the scenario progresses.\n" +
                                       "No audio will be recorded.";
    
    private static string trackingLost = "Tracking was lost temporarily during the recording. Please rerecord the character! \n " +
                                   "You can check in the mirror if the tracking works as expected! \n" +
                                   "Press the RERECORD button when you are ready!";
    private static string trackingLostTutorial = "Tracking was lost temporarily during the recording. Please rerecord the character! \n " +
                                   "You can check in the mirror if the tracking works as expected! \n" +
                                   "Press the RECORD button when you are ready!";
    
    // private static string nextRecording = "Recording successful! :) \n \n" +
    //                                "Press the NEXT button and then go to the red highlighted location on the floor! \n \n" +
    //                                "Remeber to face the direction of the arrow on the floor before you start recording! \n" +
    //                                "During the recording you can move and look around freely!";
    
    private static string run1Finished = "You finished 1/3 runs! :) \n " +
                                  "In the second run, you will rerecord the same characters one by one, but this time the characters you recorded before are already in the room and you can interact with them directly.";
    private static string run2Finished = "You finished 2/3 runs! :) \n " +
                                  "This is the last scenario run. You will do exactly the same as in the previous run. This is to see if the recordings can be improved even further.";

    private string scenarioRun1Header => "Scenario " + (currentScenarioIndex + 1) + " / Run 1";
    private string scenarioRun2Header => "Scenario " + (currentScenarioIndex + 1) + " / Run 2";
    private string scenarioRun3Header => "Scenario " + (currentScenarioIndex + 1) + " / Run 3";

    private static string savingInProgress = "Saving recording...";
    private static string savingComplete = "<color=green> Recording saved! </color>";
    
    private static string takeoverAvatarRun1 = "Once you press the NEXT CHARACTER button, you will embody a previously recorded character. \n" +
                                    "You will get the same character description for this character.\n \n" +
                                    "This time, all other characters are already in the scene and you can react to them directly.";

    private static string takeoverAvatarRun2 = "Like before, press the NEXT CHARACTER button and you will embody an already recorded character. \n" +
                                        "The acting instructions for the character will be the same.";
    
    private static string redoIfWantedInstructions = "Recording Succesful! :) \n \n" + 
                                              "If you want to record this character again, press the RERECORD button! \n \n" +
                                              "Otherwise, press the NEXT CHARACTER button to embody the next character!";
    private static string redoIfWantedLastCharacter ="Recording Succesful! :) \n \n" + 
                                              "If you want to record this character again, press the RERECORD button! \n \n" +
                                                "Otherwise, press the NEXT button to finish the scenario!";
    
    private static string scenarioFinished = "You successfully finished the scenario! :) \n" +
                                      "Press NEXT to select another scenario!";
    
    private static string ARROW_EXPLANATION = "\n \n The arrows on the floor indicate the positions of the characters you will embody. \n" + 
                                              "Face in the direction of the arrows when starting the recording. During the recording you can move around freely.";

    private static string scenario1Introduction = "Scenario duration: 35 seconds \n \n" +
                                           "Two friends are starting a heated argument." +
                                           "The room goes uncomfortably quiet as the other guests watch." + ARROW_EXPLANATION;
                                           
    private static string scenario2Introduction = "Scenario duration: 30 seconds \n" +
                                            "A fire breaks out during the party. The guests are scared. Everyone is urged to leave the room." + ARROW_EXPLANATION;
    private static string scenario3Introduction = "Scenario duration: 30 seconds \n" +
                                           "Two coaches start fighting about who has the better team. Riled up by the coaches, the teams start fighting too." + ARROW_EXPLANATION;
    
    private static string STAND_ON_ARROW = "Stand on the <color=red> red arrow</color> and face the right direction before you start recording!\n \n";

    private static string IMPROVISE = "\n \n Feel free to improvise as the scenario unfolds!";
    // TODO: face in the direction of the arrows on the floor
    private static string s1c0 = STAND_ON_ARROW + "<b>Jules: Your friends Quinn and Ray really love dogs. You are rather bored by their conversation. You find the argument that has broken out pretty unnecessary.</b>" + IMPROVISE;
    private static string s1c1 = STAND_ON_ARROW + "<b>Quinn: You are so excited about your new dog. You feel embarrassed about the argument.</b>" + IMPROVISE;
    private static string s1c2 = STAND_ON_ARROW + "<b>Ray: You love dogs and don't understand Jule's lack of interest. You would never have such an argument in public.</b>" + IMPROVISE;
    private static string s1c3 = STAND_ON_ARROW + "<b>Parker: You are trying to cheer you friend up. You find the argument justified.</b>" + IMPROVISE;
    private static string s1c4 = STAND_ON_ARROW + "<b>Toni: You are talking about your worries. You are following the argument with concern.</b>" + IMPROVISE;
    
    private static string s2c0 = STAND_ON_ARROW + "<b>Sky: You are offering drinks to your friends. When you hear about the fire, you leave with them.</b>" + IMPROVISE;
    private static string s2c1 = STAND_ON_ARROW + "<b>Eden: You are talking to your friend Jesse. When you hear about the fire, you leave with Jesse, who started coughing from the smoke and needs your help.</b>" + IMPROVISE;
    private static string s2c2 = STAND_ON_ARROW + "<b>Jesse: You are talking to your friend Eden. When you hear about the fire, you leave with Eden. But you start coughing and need help from Eden.</b>" + IMPROVISE;
    private static string s2c3 = STAND_ON_ARROW + "<b>Dana: Sky is offering you drinks. You only like coke. When you hear about the fire you leave with your friends.</b>" + IMPROVISE;
    private static string s2c4 = STAND_ON_ARROW + "<b>Alex: Sky is offering you drinks. You can't decide and take what Dana is having. When you hear about the fire you leave with your friends.</b>" + IMPROVISE;
    
    private static string s3c0 = STAND_ON_ARROW + "<b>Ash: Your teammate tells you that they don't think much about the other team. You agree. When your teammate starts provoking the other team you try to deescalate. It is not helping.</b>" + IMPROVISE;
    private static string s3c1 = STAND_ON_ARROW + "<b>Charlie: When your coach starts ranting about the other team, you agree. You start provoking/intimidating the other team at the buffet.</b>" + IMPROVISE;
    private static string s3c2 = STAND_ON_ARROW + "<b>Robin: You and your team are at the buffet as you hear the argument. You are not going to put up with the insults from the other team. You join the fight." + IMPROVISE;
    private static string s3c3 = STAND_ON_ARROW + "<b>Blake: You are at the buffet as you hear the argument. You are furious and join the fight.</b>" + IMPROVISE;
    private static string s3c4 = STAND_ON_ARROW + "<b>Billie: You are at the buffet when the argument starts. You hate fighting. When everyone starts fighting you step between the two groups and force them to stop.</b>" + IMPROVISE;
    
    private static string charactersToEmbodyHeader = "The characters you will embody are: \n";

    private static string s1Characters =
        "Jules: Your friends really love dogs. You are rather bored by their conversation. You find the argument that has broken out pretty unnecessary. \n" +
        "Quinn: You are so excited about your new dog. You feel embarrassed about the argument.\n" +
        "Ray: You love dogs and don't understand Jule's lack of interest. You would never have such an argument in public.\n" +
        "Parker: You are trying to cheer you friend up. You find the argument justified.\n" +
        "Toni: You are talking about your worries. You are following the argument with concern.";
    
    private static string s2Characters =
        "Sky: You are offering drinks to your friends. When you hear about the fire, you leave with them.\n" +
        "Eden: You are talking to your friend Jesse. When you hear about the fire, you leave with Jesse, who started coughing from the smoke and needs your help.\n" +
        "Jesse: You are talking to your friend Eden. When you hear about the fire, you leave with Eden. But you start coughing and need help from Eden.\n" +
        "Dana: Sky is offering you drinks. You only like coke. When you hear about the fire you leave with your friends.\n" +
        "Alex: Sky is offering you drinks. You can't decide and take what Dana is having. When you hear about the fire you leave with your friends.";
    
    private static string s3Characters =
        "Ash: Your teammate tells you that they don't think much about the other team. You agree. When your teammate starts provoking the other team you try to deescalate. It is not helping.\n" +
        "Charlie: When your coach starts ranting about the other team, you agree. The other team is a joke. You start provoking the other team at the buffet.\n" +
        "Robin: You and your team are at the buffet as you hear the argument. You are not going to put up with the insults from the other team. You join the fight.\n" +
        "Blake: You are at the buffet as you hear the argument. You are furious and join the fight.\n" +
        "Billie: You are at the buffet when the argument starts. You hate fighting. When everyone starts fighting you step between the two groups and force them to stop.\n";
    
    private static string headerS1 = "Scenario 1 ";
    private static string headerS2 = "Scenario 2 ";
    private static string headerS3 = "Scenario 3 ";
    private static string headerC0 = "Character 1 ";
    private static string headerC1 = "Character 2 ";
    private static string headerC2 = "Character 3 ";
    private static string headerC3 = "Character 4 ";
    private static string headerC4 = "Character 5 ";

    private static string newOrResumeInstructions =
        "Are you NEW or do you want to RESUME your last session? \n \n Stand still when you press either of the buttons so your height can be adjusted to the avatars.";

    private static float defaultFontSize = 0.03f;
    private static float smallerFontSize = 0.025f;
        
    private string GetScenarioHeader(int scenarioIndex)
    {
        if (scenarioIndex == 0)
            return headerS1;
        if (scenarioIndex == 1)
            return headerS2;
        if (scenarioIndex == 2)
            return headerS3;
        return "";
    }
    
    private float maxRecordingTime = 10.0f; // 10 seconds for the test recording
    private float finishCountdown = 2.0f; // 2 seconds to finish the recording
    private float currentRecordingTime = 0.0f;
    
    private Color inactiveUserPosition = Color.gray;
    private Color activeUserPosition = Color.red;
    private float particeStartTime = 5.0f;
    private bool particlesOn = false;
    
    void Start()
    {
        studyProcedure = GetComponent<StudyProcedure>();
        studyUI = GetComponentInChildren<StudyRecorderUI>();
        studyUI.countdownCanvas.gameObject.SetActive(false);
        studyUI.recordSphere.gameObject.SetActive(false);
        studyUI.previousSphere.gameObject.SetActive(false);
        studyUI.redoSphere.gameObject.SetActive(false);
        studyUI.replaySphere.gameObject.SetActive(false);
        studyUI.scenario1Sphere.gameObject.SetActive(false);
        studyUI.scenario2Sphere.gameObject.SetActive(false);
        studyUI.scenario3Sphere.gameObject.SetActive(false);
        studyUI.skipSphere.gameObject.SetActive(false);
        studyUI.mirror.gameObject.SetActive(false);
        studyUI.nextSphere.gameObject.SetActive(false);
        studyUI.nextAvatarSphere.gameObject.SetActive(false);
        
        cameraMain = Camera.main;
        
        StartCoroutine(StudyCoroutine());
    }
    
    IEnumerator StudyCoroutine()
    {
        yield return NewOrResumeUser();
    }
    
    public IEnumerator Scenario(Scenario scenario, string scenarioIntro, string allCharacterDescriptions, string c0, string c1, string c2, string c3, string c4)
    {   
        // set the correct background audio for the current scenario
        scenario.backgroundAudioSource.clip = scenario.backgroundAudio;
        // get the max recording time for this scenario
        maxRecordingTime = scenario.backgroundAudio.length;
        RenderSettings.fog = false;
        
        currentScenarioIndex = scenario.ScenarioIndex;
        Debug.Log("Starting Scenario " + (scenario.ScenarioIndex + 1));
        studyUI.scenario1Sphere.gameObject.SetActive(false);
        studyUI.scenario2Sphere.gameObject.SetActive(false);
        studyUI.scenario3Sphere.gameObject.SetActive(false);

        if (!studyUI.recordingManager.CheckIfSaveExists("Run1", scenario))
        {
            studyUI.instructionsText.text = scenarioIntro;
            studyUI.headerText.text = scenarioRun1Header;

            yield return ShowAllCharacterDescriptions(allCharacterDescriptions);
            
            yield return Run1(scenario, c0, c1, c2, c3, c4);
            yield return RunFinished(run1Finished, "Run1"); // save recording by default
            
            studyUI.headerText.text = scenarioRun2Header; // 2nd run overall but first takeover run
            yield return TakeoverRun(scenario, takeoverAvatarRun1, c0, c1, c2, c3, c4);
        
            // does a save of each run after the run is finished with a custom run name
            // this way we can use our initial saving structure and don't need to do additional file handling
            yield return RunFinished(run2Finished, "Run2");
        
            studyUI.headerText.text = scenarioRun3Header;
            yield return TakeoverRun(scenario, takeoverAvatarRun2, c0, c1, c2, c3, c4);

            yield return ReadyForNextScenario("Run3");

            yield return ScenarioSelection();
            
        }
        else
        {
            if (!studyUI.recordingManager.CheckIfSaveExists("Run2", scenario))
            {
                yield return LoadRecording(scenario);

                studyUI.headerText.text = scenarioRun2Header; // 2nd run overall but first takeover run
                yield return TakeoverRun(scenario, takeoverAvatarRun1, c0, c1, c2, c3, c4);
                
                yield return RunFinished(run2Finished, "Run2");
        
                studyUI.headerText.text = scenarioRun3Header;
                yield return TakeoverRun(scenario, takeoverAvatarRun2, c0, c1, c2, c3, c4);

                yield return ReadyForNextScenario("Run3");

                yield return ScenarioSelection();
            }
            else
            {
                if (!studyUI.recordingManager.CheckIfSaveExists("Run3", scenario))
                {
                    yield return LoadRecording(scenario);
                    
                    studyUI.headerText.text = scenarioRun3Header;
                    yield return TakeoverRun(scenario, takeoverAvatarRun2, c0, c1, c2, c3, c4);

                    yield return ReadyForNextScenario("Run3");

                    yield return ScenarioSelection();
                }
                else
                {
                    yield return ScenarioSelection();
                }
            }
        }
    }

    public IEnumerator ShowAllCharacterDescriptions(string characterTexts)
    {
        yield return WaitNextPressed();
        studyUI.instructionsText.fontSize = smallerFontSize;
        studyUI.headerText.text = charactersToEmbodyHeader;
        studyUI.instructionsText.text = characterTexts;
        yield return null;
    }
    
    public IEnumerator ResizeUser()
    {
     
        var userCamHeight = cameraMain.transform.position.y;
        var userCamHeightOffset = studyProcedure.MetaAvatarDefaultCamHeight - cameraMain.transform.position.y;
        Debug.Log($"Resize user height from camera height: {userCamHeight:F} to {studyProcedure.MetaAvatarDefaultCamHeight:F} (diff: {userCamHeightOffset:F})");
        var newCamHeight = new Vector3(studyProcedure.XrOrigin.transform.position.x, userCamHeightOffset, studyProcedure.XrOrigin.transform.position.z);
        studyProcedure.XrOrigin.transform.position = newCamHeight;
        
        yield return null;
    }

    public IEnumerator NewOrResumeUser()
    {
        studyUI.newUserSphere.gameObject.SetActive(true);
        studyUI.resumeUserSphere.gameObject.SetActive(true);
        yield return UIToggle(true);
        studyUI.instructionsText.text = newOrResumeInstructions;
        studyUI.headerText.text = "";
        while (!studyUI.newUserPressed && !studyUI.resumeUserPressed) yield return null;
        
        yield return ResizeUser();
        
        if (studyUI.newUserPressed)
        {
            studyUI.newUserPressed = false;
            studyUI.newUserSphere.gameObject.SetActive(false);
            studyUI.resumeUserSphere.gameObject.SetActive(false);
            
            studyUI.recordingManager.NewUserFolderWithBaseRecordings();
            
            yield return Introduction();
            yield return TutorialYesNo();
            yield return ScenarioSelection();

        }
        else if (studyUI.resumeUserPressed)
        {
            studyUI.resumeUserPressed = false;
            
            if (studyUI.recordingManager.PreparePreviousUserFolder())
            {
                studyUI.newUserSphere.gameObject.SetActive(false);
                studyUI.resumeUserSphere.gameObject.SetActive(false);
                
                yield return ScenarioSelection();
            }
            else
            {
                yield return NewOrResumeUser();
            }
            
        }
    }

    public IEnumerator Run1(Scenario scenario, string c0, string c1, string c2, string c3, string c4)
    {
        Debug.Log("Starting Run 1");
        // load characters and recorded data for scenario 1
        yield return LoadRecording(scenario); // exclude saves from existing runs

        yield return WaitNextPressed();

        yield return PositionMirrorRecordNext(scenario, 0, c0, GetScenarioHeader(scenario.ScenarioIndex) + headerC0);
        
        yield return PositionMirrorRecordNext(scenario, 1, c1, GetScenarioHeader(scenario.ScenarioIndex) + headerC1);
        
        yield return PositionMirrorRecordNext(scenario, 2, c2, GetScenarioHeader(scenario.ScenarioIndex) + headerC2);
        
        yield return PositionMirrorRecordNext(scenario, 3, c3, GetScenarioHeader(scenario.ScenarioIndex) + headerC3);
        
        yield return PositionMirrorRecordNext(scenario, 4, c4, GetScenarioHeader(scenario.ScenarioIndex) + headerC4, false);
    }
    
    /*
     * Positions the avatar at the designated spawn point, shows the mirror with the character description
     * and starts the recording. if this is not the last recording, it will show text for the next recording
     */
    public IEnumerator PositionMirrorRecordNext(Scenario scenario, int spawnPoint, string characterDescription, string headerCharacterScenario,
        bool hasNext = true)
    {
        yield return ActivateNextUserPosition(scenario, spawnPoint);
        yield return SpawnNextAvatar(scenario, spawnPoint);
        yield return ShowMirrorAndCharacterDescription(characterDescription, headerCharacterScenario);

        yield return Recording(scenario, studyProcedure.scenario1.AvatarManager.avatarPrefab, false, -1, hasNext); // does the recording and redo if necessary
    }

    public IEnumerator LoadRecording(Scenario scenario, string exclude = null)
    {
        // load the characters and the recorded data for the scenario
        yield return studyUI.recordingManager.GotoThumbnail(scenario.ScenarioIndex);
        while (!studyUI.thumbnailAvatarsSpawned) yield return null;
        studyUI.thumbnailAvatarsSpawned = false; // reset
        // Debug.Log("Coroutine: thumbnail avatars spawned");
        studyUI.recordingManager.LoadRecording(exclude);
        while (!studyUI.recordingDataLoaded) yield return null;
        studyUI.recordingDataLoaded = false; // reset
        // Debug.Log("Coroutine: recording data loaded");
    }

    public IEnumerator ActivateNextUserPosition(Scenario scenario, int index)
    {
        // cannot teleport the user since we are using hand tracking and real world room scale!
        // yield return studyProcedure.SetUserPosition(scenario.UserSpawnPoints[index]);
        for (var p = 0; p < scenario.UserSpawnPoints.Count; p++)
        {
            if (p == index)
            {
                scenario.UserSpawnPoints[p].GetComponent<SpriteRenderer>().color = activeUserPosition;
            }
            else
            {
                scenario.UserSpawnPoints[p].GetComponent<SpriteRenderer>().color = inactiveUserPosition;
            }
        }

        yield return null;
    }

    public IEnumerator SpawnNextAvatar(Scenario scenario, int index)
    {
        scenario.AvatarManager.avatarPrefab = scenario.AvatarPrefabs[index];
        while (!scenario.avatarCreated) yield return null;
        scenario.avatarCreated = false;
    }

    public IEnumerator ShowMirrorAndCharacterDescription(string text, string headerCharacterScenario)
    {
        yield return UIToggle(true, true); // show the UI in front of the user in new position
        yield return MirrorToggle(true);
        studyUI.instructionsText.fontSize = defaultFontSize;
        studyUI.instructionsText.text = text;
        studyUI.headerText.text = headerCharacterDescription + " " + headerCharacterScenario;
    }

    // public IEnumerator ReadyForNextRecording()
    // {
    //     // enable UI but disable the mirror because we only want to show it when next is pressed again
    //     yield return UIToggle(true);
    //     studyUI.instructionsText.text = nextRecording;
    //     yield return WaitNextPressed();
    // }

    public IEnumerator ReadyForNextScenario(string fileName, bool save = true)
    {
        yield return MirrorToggle(false);
        yield return UIToggle(true);
        studyUI.instructionsText.text = scenarioFinished;

        if (save)
        {
            yield return SaveCurrentRun(fileName);
        }
        
        // cleanup the current recording
        studyUI.recordingManager.UnloadRecording();
        studyUI.recordingManager.ClearThumbnail();

        yield return WaitNextPressed();
    }

    // public IEnumerator ReadyForNextTakeover()
    // {
    //     yield return UIToggle(true);
    //     studyUI.instructionsText.text = nextTakeover;
    // }

    public IEnumerator RunFinished(string runNFinished, string fileName, bool save = true)
    {
        yield return UIToggle(true);
        studyUI.instructionsText.text = runNFinished;

        if (save)
        {
            yield return SaveCurrentRun(fileName);
        }
        
        yield return WaitNextPressed();
    }

    public IEnumerator SaveCurrentRun(string fileName)
    {
        studyUI.headerText.text = savingInProgress;
        while (!studyUI.recorder.recording.flags.SaveReady) yield return null;

        studyUI.recordingManager.SaveRecording(fileName);
        while (!studyUI.recordingSaved) yield return null;
        studyUI.recordingSaved = false; // reset
        studyUI.headerText.text = savingComplete;
    }
    

    public IEnumerator TakeoverMirrorRecordNext(Scenario scenario, GameObject takeoverPrefab, int takeoverIndex, int spawnPoint, 
        string characterDescription, string headerCharacterScenario, bool hasNext = true)
    {
        yield return ActivateNextUserPosition(scenario, spawnPoint);
        yield return TakeoverNextAvatar(takeoverPrefab, true, takeoverIndex);
        yield return ShowMirrorAndCharacterDescription(characterDescription, headerCharacterScenario);
        yield return Recording(scenario, takeoverPrefab, true, takeoverIndex, hasNext); // does the recording and redo if necessary
    }
    
    public IEnumerator TakeoverRun(Scenario scenario, string takeoverRun, string c0, string c1, string c2, string c3, string c4)
    {
        
        Debug.Log("Starting Takeover Run");
        // yield return WaitNextPressed();
        studyUI.instructionsText.text = takeoverRun;
        
        // get the correct takeover avatar prefab for the current takeover
        // also make sure to takeover the correct avatar
        // depending on the scenario, the first/first two avatar(s) are the base recording
        // this means we start from either the second/third avatar for takeover
        // e.g. 5 recorded + 2 base avatars = 7 avatars in total -> start at index 2 (excluding base avatars)

        yield return WaitNextAvatarPressed();
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[0], scenario.NumberBaseAvatars, 0, c0, GetScenarioHeader(scenario.ScenarioIndex) + headerC0);
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[1], scenario.NumberBaseAvatars + 1, 1, c1, GetScenarioHeader(scenario.ScenarioIndex) + headerC1);
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[2], scenario.NumberBaseAvatars + 2, 2, c2, GetScenarioHeader(scenario.ScenarioIndex) + headerC2);
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[3], scenario.NumberBaseAvatars + 3, 3, c3, GetScenarioHeader(scenario.ScenarioIndex) + headerC3);

        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[4], scenario.NumberBaseAvatars + 4, 4, c4, GetScenarioHeader(scenario.ScenarioIndex) + headerC4, false);
    }

    public IEnumerator Recording(Scenario scenario, GameObject takeoverPrefab, bool shouldTakeover, int takeoverIndex, bool hasNextRecording)
    {
        currentRecordingTime = 0.0f;
        
        // set frame to 0 (not sure if necessary, but maybe then it won't have a jump at the beginning)
        studyUI.replayer.SetCurrentFrameManually(0.0f);
        
        yield return WaitRecordPressed(scenario); // also starts the audio clips!
        
        yield return UIToggle(false); // hide the UI during recording
        yield return MirrorToggle(false); // hide the mirror during recording
        
        // if the scenario has a particle system we start it here
        yield return StartParticleSystem(scenario);
        
        // either wait until time is up or stop when tracking is lost and it stops early
        while (studyUI.recording && currentRecordingTime < maxRecordingTime)
        {
            if (currentRecordingTime >= maxRecordingTime - finishCountdown)
            {
                yield return Countdown(finishCountdown);
            }
            yield return null;
        }
        
        // if the scenario has a particle system we stop it here
        StopParticleSystem(scenario);
        StopAudioClips(scenario);
        
        if (studyUI.recording) // all good, tracking was not lost, stop recording now
        {
            studyUI.recorder.StopRecording();
            yield return RedoIfWanted(scenario, takeoverPrefab, takeoverIndex, hasNextRecording);

        }
        else // tracking was lost, recording is already stopped, attempt redo!
        {
            // shouldTakeover is true when we redo an existing replay.
            // otherwise when only tracking was lost, and there isn't a replay yet
            // all we want is to redo the normal recording
            yield return RedoRecording(scenario, takeoverPrefab, shouldTakeover, takeoverIndex, hasNextRecording);
        }
        yield return null;
    }
    
    /*
     * Allows the user to redo a recording, even if it was fine(!), just in case they have
     * been doing something unintentional during the recording
     */
    public IEnumerator RedoIfWanted(Scenario scenario, GameObject takeoverPrefab, int takeoverIndex, bool hasNextRecording)
    {
        yield return UIToggle(true);
        yield return MirrorToggle(true);
        if (hasNextRecording)
        {
            studyUI.instructionsText.text = redoIfWantedInstructions;
        }
        else
        {
            studyUI.instructionsText.text = redoIfWantedLastCharacter;
        }
        yield return WaitRedoRecordingOrNextAvatar(scenario, takeoverPrefab, takeoverIndex, hasNextRecording);
    }

    // used when tracking failed!
    // when we are taking over and want to redo that we need to takeover the nth avatar again, not the last!
    public IEnumerator RedoRecording(Scenario scenario, GameObject takeoverPrefab, bool shouldTakeover, int takeoverIndex, bool hasNextRecording)
    {
        studyUI.instructionsText.text = trackingLost;
        yield return UIToggle(true);
        yield return MirrorToggle(true);

        yield return WaitRedoPressed(takeoverPrefab, shouldTakeover, takeoverIndex);
        
        studyUI.instructionsText.text = takeoverInstructions;
        // yield return UIToggle(true); // just for adjusting the UI position after takeover

        yield return Recording(scenario, takeoverPrefab, shouldTakeover, takeoverIndex, hasNextRecording);
    }
    
    public IEnumerator Introduction()
    {
        studyUI.headerText.text = headerIntroduction;
        studyUI.instructionsText.text = introduction;
        yield return UIToggle(true);
        yield return MirrorToggle(true);
        
        // TODO: disable this for testing
        while (studyUI.recordingManager.GetThumbnailCount() < 2) yield return null;

        yield return WaitNextPressed();
    }

    public IEnumerator TutorialYesNo()
    {
        studyUI.headerText.text = headerTutorial;
        studyUI.instructionsText.text = recordingInstructions;
        yield return UIToggle(true, false);
        yield return MirrorToggle(true);
        studyUI.nextSphere.gameObject.SetActive(true);
        studyUI.skipSphere.gameObject.SetActive(true);
        
        while (!studyUI.nextPressed && !studyUI.skipPressed) yield return null;
        
        if (studyUI.nextPressed)
        {
            studyUI.nextPressed = studyUI.skipPressed = false;
            studyUI.skipSphere.gameObject.SetActive(false);
            studyUI.nextSphere.gameObject.SetActive(false);
            yield return HowToRecordReplay(mirrorInstructions);
        }
        else if (studyUI.skipPressed)
        {
            studyUI.nextPressed = studyUI.skipPressed = false;
            studyUI.skipSphere.gameObject.SetActive(false);
            studyUI.nextSphere.gameObject.SetActive(false);
            yield return ScenarioSelection();
        }
    }
    
    public IEnumerator HowToRecordReplay(string instructions, bool trackingLostAttempt = false)
    {
        yield return MirrorToggle(true);
        yield return UIToggle(true);
        // mirror instructions first, at second attempt if tracking is lost we do tracking lost instructions
        studyUI.instructionsText.text = instructions; 
        currentRecordingTime = 0.0f;

        yield return WaitRecordPressed(null);
        
        yield return UIToggle(false);
        yield return MirrorToggle(false);
        
        // either wait until time is up or stop when tracking is lost and it stops early
        while (studyUI.recording && currentRecordingTime < maxRecordingTime)
        {
            if (currentRecordingTime >= maxRecordingTime - finishCountdown)
            {
                yield return Countdown(finishCountdown);
            }
            yield return null;
        }
        
        if (studyUI.recording) // all good, tracking was not lost, stop recording now
        {
            studyUI.recorder.StopRecording();
            
            studyUI.instructionsText.text = replayInstructions;
            yield return UIToggle(true); // show UI after recording in front of user

            yield return WaitReplayPressed();
        
            // wait for replay to finish (can in theory be stopped by user when pressing the replay sphere again)
            while(studyUI.replaying) yield return null;
        
            // enable next button
            studyUI.instructionsText.text = finishHowTo;
            yield return WaitNextPressed();
        
            // cleanup the current recording
            studyUI.recordingManager.UnloadRecording();
            studyUI.recordingManager.ClearThumbnail();
        }
        else // tracking was lost, recording is already stopped, attempt redo!
        {
            // shouldTakeover is true when we redo an existing replay.
            // otherwise when only tracking was lost, and there isn't a replay yet
            // all we want is to redo the normal recording
            yield return HowToRecordReplay(trackingLostTutorial, true);
        }
    }

    public IEnumerator ScenarioSelection()
    {
        studyUI.headerText.text = headerScenarioSelection;
        studyUI.instructionsText.text = scenarioSelection;
        studyUI.scenario1Sphere.gameObject.SetActive(true);
        studyUI.scenario2Sphere.gameObject.SetActive(true);
        studyUI.scenario3Sphere.gameObject.SetActive(true);
        
        while (!studyUI.scenario1Pressed && !studyUI.scenario2Pressed && !studyUI.scenario3Pressed) yield return null;
        
        if (studyUI.scenario1Pressed)
        {
            studyUI.scenario1Pressed = false;
            yield return EnableUserSpawnPoints(studyProcedure.scenario1);
            yield return DisableUserSpawnPoints(studyProcedure.scenario2);
            yield return DisableUserSpawnPoints(studyProcedure.scenario3);
            yield return Scenario(studyProcedure.scenario1, scenario1Introduction, s1Characters, s1c0, s1c1, s1c2, s1c3, s1c4);
        }
        else if (studyUI.scenario2Pressed)
        {
            studyUI.scenario2Pressed = false;
            yield return EnableUserSpawnPoints(studyProcedure.scenario2);
            yield return DisableUserSpawnPoints(studyProcedure.scenario1);
            yield return DisableUserSpawnPoints(studyProcedure.scenario3);
            yield return Scenario(studyProcedure.scenario2, scenario2Introduction, s2Characters, s2c0, s2c1, s2c2, s2c3, s2c4);
        }
        else if (studyUI.scenario3Pressed)
        {
            studyUI.scenario3Pressed = false;
            yield return EnableUserSpawnPoints(studyProcedure.scenario3);
            yield return DisableUserSpawnPoints(studyProcedure.scenario1);
            yield return DisableUserSpawnPoints(studyProcedure.scenario2);
            yield return Scenario(studyProcedure.scenario3, scenario3Introduction, s3Characters, s3c0, s3c1, s3c2, s3c3, s3c4);
        }
    }

    private IEnumerator EnableUserSpawnPoints(Scenario scenario)
    {
        scenario.UserSpawnPoints[0].transform.parent.gameObject.SetActive(true);
        yield return null;
    }

    private IEnumerator DisableUserSpawnPoints(Scenario scenario)
    {
        scenario.UserSpawnPoints[0].transform.parent.gameObject.SetActive(false);
        yield return null;
    }
    
    private IEnumerator Countdown(float countdownTime)
    {
        for (int i = 0; i < countdownTime; i++)
        {
            studyUI.PlayHighBeep();
            yield return new WaitForSeconds(1.0f);
        }
        studyUI.PlayLowBeep();
    }
    
    public IEnumerator UIToggle(bool uiVisible, bool reposition = true)
    {
        // place the ui in front of the player whenever it is toggled
        if (cameraMain && uiVisible && reposition)
        {
            // make the panel always show in front of the user at the same height
            var panelHeight = 1.4f;
            var distanceFromUser = 0.8f;
            
            var camForward = cameraMain.transform.forward;
            camForward.y = 0f;
            
            studyUI.gameObject.transform.position = new Vector3 (
                cameraMain.transform.position.x,
                panelHeight,
                cameraMain.transform.position.z) + camForward * distanceFromUser; 

            // Make the panel face the user
            studyUI.gameObject.transform.LookAt(
                new Vector3(
                    cameraMain.transform.position.x, 
                    panelHeight, 
                    cameraMain.transform.position.z));
            studyUI.gameObject.transform.Rotate(0, 180, 0); // Flip to face the user (since LookAt makes it face away)
        }
        studyUI.gameObject.SetActive(uiVisible);
        yield return null;
    }

    public IEnumerator MirrorToggle(bool visible)
    {
        studyUI.mirror.gameObject.SetActive(visible);
        // if (visible)
        // {
        //     studyUI.PanelToMirrorPosition();
        // }
        // else
        // {
        //     studyUI.PanelToMainPosition();
        // }
        yield return null;
    }

    public IEnumerator WaitRecordPressed(Scenario scenario)
    {
        studyUI.recordSphere.gameObject.SetActive(true);
        while (!studyUI.recordPressed) yield return null; // record
        studyUI.recordPressed = false;
        StartAudioClips(scenario);
        studyUI.recordSphere.gameObject.SetActive(false);
        studyUI.headerText.text = "";
    }

    private IEnumerator StartParticleSystem(Scenario scenario)
    {
        if (scenario.particleSystem)
        {
            while (studyUI.recording && currentRecordingTime <= particeStartTime) yield return null;
            scenario.particleSystem.Play();
            RenderSettings.fog = true;

            while (studyUI.recording && currentRecordingTime <= maxRecordingTime - 10.0f)
            {
                RenderSettings.fogDensity =
                Mathf.Lerp(0, studyProcedure.MaxFogValue, currentRecordingTime / (maxRecordingTime-10.0f));
                yield return null;
            }
        }
        yield return null;
    }

    private void StopParticleSystem(Scenario scenario)
    {
        if (scenario.particleSystem)
        {
            RenderSettings.fogDensity = 0;
            RenderSettings.fog = false;
            scenario.particleSystem.Stop();
        }
        // yield return null;
    }

    private void StartAudioClips(Scenario scenario)
    {
        if (scenario)
        {
            if (scenario.person2Audio != null)
            {
                scenario.person2AudioSource.Play();
            }
            scenario.backgroundAudioSource.Play();
            scenario.person1AudioSource.Play();
            // yield return null;
        }
    }

    private void StopAudioClips(Scenario scenario)
    {
        if (scenario)
        {
            if (scenario.person2Audio != null)
            {
                scenario.person2AudioSource.Stop();
            }
            scenario.backgroundAudioSource.Stop();
            scenario.person1AudioSource.Stop();
            // yield return null;
        }
    }

    public IEnumerator WaitReplayPressed()
    {
        studyUI.replaySphere.gameObject.SetActive(true);
        while (!studyUI.replayPressed) yield return null; // replay the recording
        studyUI.replayPressed = false;
        studyUI.replaySphere.gameObject.SetActive(false);
        
    }

    public IEnumerator WaitRedoPressed(GameObject takeoverAvatar, bool shouldTakeover, int n = -1)
    {
        // any scenario is fine here as they all point to the same avatar manager!
        studyUI.redoSphere.gameObject.SetActive(true);
        while (!studyUI.redoPressed) yield return null; // redo the recording
        studyUI.redoPressed = false;
        studyUI.redoSphere.gameObject.SetActive(false);

        if (shouldTakeover)
        {
            studyUI.SetTakeoverAvatar(takeoverAvatar);

            if (n != -1)
            {
                studyUI.takeoverSelector.TakeoverNthReplay(n);
            }
            else
            {
                studyUI.takeoverSelector.TakeoverLastReplay();
            }
        }
        yield return null;
    }

    public IEnumerator WaitNextAvatarPressed()
    {
        // any scenario is fine here as they all point to the same avatar manager!
        
        studyUI.nextAvatarSphere.gameObject.SetActive(true);
        while (!studyUI.nextAvatarPressed) yield return null; // redo the recording
        studyUI.nextAvatarPressed = false;
        studyUI.nextAvatarSphere.gameObject.SetActive(false);
        yield return null;
    }

    public IEnumerator TakeoverNextAvatar(GameObject takeoverAvatar, bool shouldTakeover, int n = -1)
    {
        if (shouldTakeover)
        {
            studyUI.SetTakeoverAvatar(takeoverAvatar);

            if (n != -1)
            {
                studyUI.takeoverSelector.TakeoverNthReplay(n);
            }
            else
            {
                studyUI.takeoverSelector.TakeoverLastReplay();
            }
        }
        yield return null;
    }

    public IEnumerator WaitNextPressed()
    {
        studyUI.nextSphere.gameObject.SetActive(true);
        while (!studyUI.nextPressed) yield return null; // next
        studyUI.nextPressed = false;
        studyUI.nextSphere.gameObject.SetActive(false);
        studyUI.headerText.text = "";
    }

    // TODO MAYBE here we can also add a replay if wanted (but it might unnecessarily lengthen the data collection)
    public IEnumerator WaitRedoRecordingOrNextAvatar(Scenario scenario, GameObject takeoverAvatar, int takeoverIndex, bool hasNextRecording)
    {
        studyUI.redoSphere.gameObject.SetActive(true);
        if (hasNextRecording)
        {
            studyUI.nextAvatarSphere.gameObject.SetActive(true);
        }
        else
        {
            studyUI.nextSphere.gameObject.SetActive(true);
        }
        
        while (!studyUI.redoPressed && !studyUI.nextAvatarPressed && !studyUI.nextPressed) yield return null;

        if (studyUI.redoPressed)
        {
            studyUI.redoPressed = false;
            studyUI.nextSphere.gameObject.SetActive(false);
            studyUI.nextAvatarSphere.gameObject.SetActive(false);
            studyUI.redoSphere.gameObject.SetActive(false);
            
            studyUI.SetTakeoverAvatar(takeoverAvatar);
            if (takeoverIndex != -1)
            {
                studyUI.takeoverSelector.TakeoverNthReplay(takeoverIndex);
            }
            else
            {
                studyUI.takeoverSelector.TakeoverLastReplay();
            }
            
            studyUI.instructionsText.text = takeoverInstructions;
            // yield return UIToggle(true); // don't need it as we aren't teleporting!

            yield return Recording(scenario, takeoverAvatar, true, takeoverIndex, hasNextRecording);
        }
        else if (studyUI.nextAvatarPressed)
        {
            
            studyUI.nextAvatarPressed = false;
            studyUI.nextAvatarSphere.gameObject.SetActive(false); 
            studyUI.nextSphere.gameObject.SetActive(false);
            studyUI.redoSphere.gameObject.SetActive(false);
        }
        else if (studyUI.nextPressed)
        {
            studyUI.nextPressed = false;
            studyUI.nextAvatarSphere.gameObject.SetActive(false); 
            studyUI.nextSphere.gameObject.SetActive(false);
            studyUI.redoSphere.gameObject.SetActive(false);
            studyUI.headerText.text = "";
        }
    }

    public void LeftMenuButtonGesturePerformed()
    {
        if (!studyUI.recording)
        {
            StartCoroutine(UIToggle(true));
        }
    }
    
    private void Update()
    {
        if (studyUI.recording)
        {
            if (currentRecordingTime <= maxRecordingTime)
            {
                currentRecordingTime += Time.deltaTime;
            }
        }
    }
}
