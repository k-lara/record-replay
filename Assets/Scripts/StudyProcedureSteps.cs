using System;
using System.Collections;
using System.Collections.Generic;
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
    
    private string headerIntroduction = "Introduction";
    private string introduction = "You will experience 3 different scenarios in this room. \n" +
                                  "In each scenario, you will have to react to something that is happening in the scene. \n" +
                                  "Your head and hand movements as well as your facial expressions will be recorded. \n";
    
    private string headerTutorial = "Tutorial";
    private string recordingInstructions = "Tutorial: How to do a recording? \n" +
                                           "\n" +
                                           "\"Pinch\" the record button to start a recording. It will always stop automatically.\n" +
                                           "There is a 5 second countdown before the recording starts and a 2 second countdown before it stops. \n" +
                                           "When tracking is lost temporarily, recording will stop immediately and you can try again. \n" +
                                           "To reposition the menu, do the menu gesture with your left hand!";
    
    private string headerCharacterDescription = "Character Description";
    
    private string mirrorInstructions = "Between recording sessions, you will always see your current avatar in the mirror. \n" +
                                        "It can be quite satisfying to see your avatar's face move when yours does! :) \n" +
                                        "You might need to exaggerate your facial expressions to get the best results on the avatar. \n" +
                                        "You can also check if the tracking works as expected! \n" +
                                        "Before a recording, you will also get the character description for your current character and what your character should do during the recording! \n" +
                                        "Example: You are very happy about something and wave your hands!";
    
    private string replayInstructions = "Each character you record during the scenarios will be added to the scenario and replayed automatically whenever you record another character. \n" +
                                        "You won't have this option later, but just to see how your recording looks like, you can press the REPLAY button now!";
    
    // private string redoInstructions = "Redo the recording, if tracking gets lost! Should you have moved from your initial starting position you will be teleported back.";
    
    private string takeoverInstructions = "Press record again to overwrite your previous recording!";
    
    // private string rewatchRedoInstructions = "If you press replay again, you will see the new recording!";
    
    private string finishHowTo = "You are now ready to start the study! \n" +
                                 "Whenever instructions aren't clear, you can ask the experimenter for help! \n" +
                                 "It occasionally happens that tracking is lost and unrecoverable and we need to restart the app or the Quest.";

    private string headerScenarioSelection = "Scenario Selection";
    private string scenarioSelection = "You will experience 3 different incidents that are taking place at a Christmas party. \n" +
                                       "In each scenario, you will successively play the role of 5 different guests who are reacting to the incident." +
                                       "The character descriptions are meant to give you an entry point into the character's role. \n" +
                                       "No audio will be recorded.";
    
    private string trackingLost = "Tracking was lost temporarily during the recording. Please redo the recording! \n " +
                                   "You can check in the mirror if the tracking works as expected! \n" +
                                   "Press the REDO button when you are ready!";
    
    private string nextRecording = "Recording successful! :) \n" +
                                   "Press the NEXT button and then go to the red highlighted location on the floor!" +
                                   "Remeber to face the direction of the arrow on the floor before you start recording! \n" +
                                   "During the recording you can move and look around freely!";
    
    private string run1Finished = "You finished 1/3 runs! :) \n " +
                                  "In the second run, you will rerecord the same characters one by one, but this time the characters you recorded before are already in the room and you can interact with them directly.";
    private string run2Finished = "You finished 2/3 runs! :) \n " +
                                  "This is the last scenario run. You will do exactly the same as in the previous run. This is to see if the recordings can be improved even further.";

    private string scenarioRun1Header => "Scenario " + (currentScenarioIndex + 1) + " / Run 1";
    private string scenarioRun2Header => "Scenario " + (currentScenarioIndex + 1) + " / Run 2";
    private string scenarioRun3Header => "Scenario " + (currentScenarioIndex + 1) + " / Run 3";

    private string savingInProgress = "Saving recording...";
    private string savingComplete = "<color=green> Recording saved! </color>";
    
    private string takeoverAvatarRun1 = "Once you press the REDO button, you will embody a previously recorded character. \n" +
                                    "You will get the same character description for this character.\n" +
                                    "This time, all other characters are already in the scene and you can react to them directly.";

    private string takeoverAvatarRun2 = "Like before, press the REDO button and you will embody an already recorded character. \n" +
                                        "The acting instructions for the character will be the same.";
    
    private string nextTakeover = "Recording successful! :) \n" +
                                  "Press the REDO button to embody the next character!";
    
    private string scenarioFinished = "You successfully finished the scenario! \n" +
                                      "Press NEXT to select another scenario!";
    
    private static string ARROW_EXPLANATION = "You will see arrows on the floor that indicate the positions of the characters you will embody. \n" + 
                                              "The direction of the arrows shows which way to face when starting the recording.";

    private string scenario1Introduction = "Scenario duration: 35 seconds \n" +
                                           "Two friends are having a heated discussion.\n" + ARROW_EXPLANATION;
                                           
    private string scenario2Introduction = "Scenario duration: 30 seconds \n" +
                                            "A fire breaks out during the party.";
    private string scenario3Introduction = "Scenario duration: 30 seconds \n" +
                                           "Two coaches start fighting about who has the better team.";
    private static string STAND_ON_ARROW = "(Make sure you are standing on the red arrow and facing the right direction!)\n";
    // TODO: face in the direction of the arrows on the floor
    private string s1c0 = STAND_ON_ARROW + "S1 Character 1 (name?): ...";
    private string s1c1 = STAND_ON_ARROW + "S1 Character 2 (name?): ...";
    private string s1c2 = STAND_ON_ARROW + "S1 Character 3 (name?): ...";
    private string s1c3 = STAND_ON_ARROW + "S1 Character 4 (name?): ...";
    private string s1c4 = STAND_ON_ARROW + "S1 Character 5 (name?): ...";
    
    private string s2c0 = STAND_ON_ARROW + "S2 Character 1 (name?): ...";
    private string s2c1 = STAND_ON_ARROW + "S2 Character 2 (name?): ...";
    private string s2c2 = STAND_ON_ARROW + "S2 Character 3 (name?): ...";
    private string s2c3 = STAND_ON_ARROW + "S2 Character 4 (name?): ...";
    private string s2c4 = STAND_ON_ARROW + "S2 Character 5 (name?): ...";
    
    private string s3c0 = STAND_ON_ARROW + "S3 Character 1 (name?): ...";
    private string s3c1 = STAND_ON_ARROW + "S3 Character 2 (name?): ...";
    private string s3c2 = STAND_ON_ARROW + "S3 Character 3 (name?): ...";
    private string s3c3 = STAND_ON_ARROW + "S3 Character 4 (name?): ...";
    private string s3c4 = STAND_ON_ARROW + "S3 Character 5 (name?): ...";
    
    private float maxRecordingTime = 10.0f; // 10 seconds for the test recording
    private float finishCountdown = 2.0f; // 2 seconds to finish the recording
    private float currentRecordingTime = 0.0f;
    
    private Color inactiveUserPosition = Color.gray;
    private Color activeUserPosition = Color.red;
    private float particeStartTime = 5.0f;
    private bool particlesOn = false;
    
    void Awake()
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
        
        cameraMain = Camera.main;
    }
    public IEnumerator Scenario(Scenario scenario, string scenarioIntro, string c0, string c1, string c2, string c3, string c4)
    {   
        // set the correct background audio for the current scenario
        scenario.backgroundAudioSource.clip = scenario.backgroundAudio;
        // get the max recording time for this scenario
        maxRecordingTime = scenario.backgroundAudio.length;
        RenderSettings.fog = false;
        
        currentScenarioIndex = scenario.ScenarioIndex;
        Debug.Log("Starting Scenario " + scenario.ScenarioIndex + 1);
        studyUI.instructionsText.text = scenarioIntro;
        studyUI.headerText.text = scenarioRun1Header;
        studyUI.scenario1Sphere.gameObject.SetActive(false);
        studyUI.scenario2Sphere.gameObject.SetActive(false);
        studyUI.scenario3Sphere.gameObject.SetActive(false);

        if (!studyUI.recordingManager.CheckIfSaveExists("Run1", scenario))
        {
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

    public IEnumerator NewOrResumeUser()
    {
        studyUI.newUserSphere.gameObject.SetActive(true);
        studyUI.resumeUserSphere.gameObject.SetActive(true);
        yield return UIToggle(true);
        studyUI.instructionsText.text = "Are you NEW or do you want to RESUME your last session?";
        studyUI.headerText.text = "";
        while (!studyUI.newUserPressed && !studyUI.resumeUserPressed) yield return null;
        
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

        yield return PositionMirrorRecordNext(scenario, 0, c0);
        
        yield return PositionMirrorRecordNext(scenario, 1, c1);
        
        yield return PositionMirrorRecordNext(scenario, 2, c2);
        
        yield return PositionMirrorRecordNext(scenario, 3, c3);
        
        yield return PositionMirrorRecordNext(scenario, 4, c4, false);
    }
    
    /*
     * Positions the avatar at the designated spawn point, shows the mirror with the character description
     * and starts the recording. if this is not the last recording, it will show text for the next recording
     */
    public IEnumerator PositionMirrorRecordNext(Scenario scenario, int spawnPoint, string characterDescription,
        bool hasNext = true)
    {
        yield return ActivateNextUserPositionAndAvatar(scenario, spawnPoint);
        yield return ShowMirrorAndCharacterDescription(characterDescription);

        yield return Recording(scenario, false, -1); // does the recording and redo if necessary
        
        if (hasNext)
            yield return ReadyForNextRecording();
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

    public IEnumerator ActivateNextUserPositionAndAvatar(Scenario scenario, int index)
    {
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
        
        // cannot teleport the user since we are using hand tracking and real world room scale!
        // yield return studyProcedure.SetUserPosition(scenario.UserSpawnPoints[index]);
        
        scenario.AvatarManager.avatarPrefab = scenario.AvatarPrefabs[index];
        while (!scenario.avatarCreated) yield return null;
        scenario.avatarCreated = false;
    }

    public IEnumerator ShowMirrorAndCharacterDescription(string text)
    {
        yield return UIToggle(true, true); // show the UI in front of the user in new position
        yield return MirrorToggle(true);
        studyUI.instructionsText.text = text;
        studyUI.headerText.text = headerCharacterDescription;
    }

    public IEnumerator ReadyForNextRecording()
    {
        // enable UI but disable the mirror because we only want to show it when next is pressed again
        yield return UIToggle(true);
        studyUI.instructionsText.text = nextRecording;
        yield return WaitNextPressed();
    }

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

    public IEnumerator ReadyForNextTakeover()
    {
        yield return UIToggle(true);
        studyUI.instructionsText.text = nextTakeover;
    }

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
    

    public IEnumerator TakeoverMirrorRecordNext(Scenario scenario, GameObject takeoverPrefab, int takeoverIndex, 
        string characterDescription, bool hasNext = true)
    {
        yield return WaitRedoPressed(takeoverPrefab, true, takeoverIndex);
        yield return ShowMirrorAndCharacterDescription(characterDescription);
        yield return Recording(scenario, true, takeoverIndex); // does the recording and redo if necessary
        if (hasNext)
            yield return ReadyForNextTakeover();
    }
    
    public IEnumerator TakeoverRun(Scenario scenario, string takeoverRun, string c0, string c1, string c2, string c3, string c4)
    {
        Debug.Log("Starting Takeover Run");
        studyUI.instructionsText.text = takeoverRun;
        
        // get the correct takeover avatar prefab for the current takeover
        // also make sure to takeover the correct avatar
        // depending on the scenario, the first/first two avatar(s) are the base recording
        // this means we start from either the second/third avatar for takeover
        // e.g. 5 recorded + 2 base avatars = 7 avatars in total -> start at index 2 (excluding base avatars)

        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[0], scenario.NumberBaseAvatars, c0);
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[1], scenario.NumberBaseAvatars + 1, c1);
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[2], scenario.NumberBaseAvatars + 2, c2);
        
        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[3], scenario.NumberBaseAvatars + 3, c3);

        yield return TakeoverMirrorRecordNext(scenario, scenario.AvatarPrefabs[4], scenario.NumberBaseAvatars + 4, c4, false);
    }

    public IEnumerator Recording(Scenario scenario, bool shouldTakeover, int takeoverIndex)
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
            yield return RedoIfWanted(scenario, takeoverIndex);

        }
        else // tracking was lost, recording is already stopped, attempt redo!
        {
            // shouldTakeover is true when we redo an existing replay.
            // otherwise when only tracking was lost, and there isn't a replay yet
            // all we want is to redo the normal recording
            yield return RedoRecording(scenario, shouldTakeover, takeoverIndex);
        }
        yield return null;
    }
    
    /*
     * Allows the user to redo a recording, even if it was fine(!), just in case they have
     * been doing something unintentional during the recording
     */
    public IEnumerator RedoIfWanted(Scenario scenario, int takeoverIndex)
    {
        yield return UIToggle(true);
        yield return MirrorToggle(true);
        studyUI.instructionsText.text = "If you want to redo the recording, press the REDO button! \n" +
                                        "Otherwise, press the NEXT button to continue!";
        yield return WaitRedoRecordingOrNext(scenario, studyProcedure.scenario1.AvatarManager.avatarPrefab, takeoverIndex);
    }

    // used when tracking failed!
    // when we are taking over and want to redo that we need to takeover the nth avatar again, not the last!
    public IEnumerator RedoRecording(Scenario scenario, bool shouldTakeover, int takeoverIndex)
    {
        studyUI.instructionsText.text = trackingLost;
        yield return UIToggle(true);
        yield return MirrorToggle(true);

        yield return WaitRedoPressed(studyProcedure.scenario1.AvatarManager.avatarPrefab, shouldTakeover, takeoverIndex);
        
        studyUI.instructionsText.text = takeoverInstructions;
        // yield return UIToggle(true); // just for adjusting the UI position after takeover

        yield return Recording(scenario, shouldTakeover, takeoverIndex);
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
        }
        else // tracking was lost, recording is already stopped, attempt redo!
        {
            // shouldTakeover is true when we redo an existing replay.
            // otherwise when only tracking was lost, and there isn't a replay yet
            // all we want is to redo the normal recording
            yield return HowToRecordReplay(trackingLost, true);
        }
        
        studyUI.instructionsText.text = replayInstructions;
        yield return UIToggle(true); // show UI after recording in front of user

        yield return WaitReplayPressed();
        
        // wait for replay to finish (can in theory be stopped by user when pressing the replay sphere again)
        while(studyUI.replaying) yield return null;
        
        // redo instructions
        // studyUI.instructionsText.text = redoInstructions;
        
        // yield return WaitRedoPressed(studyProcedure.scenario1.AvatarManager.avatarPrefab, TODO);
        //
        // studyUI.instructionsText.text = takeoverInstructions;
        // yield return UIToggle(true); // just for adjusting the UI position after takeover
        //
        // do the whole recording again!
        // yield return WaitRecordPressed(null);
        //
        // yield return UIToggle(false);
        // // wait for current recording time to reach maxRecordingTime - end countdown beep
        // yield return new WaitForSeconds(maxRecordingTime - finishCountdown);
        //
        // // play countdown beep
        // yield return Countdown(finishCountdown);
        //
        // // stop recording
        // currentRecordingTime = 0.0f;
        // studyUI.recorder.StopRecording();
        // // enable UI
        // yield return UIToggle(true); // show UI after recording in front of user
        // studyUI.instructionsText.text = rewatchRedoInstructions;
        //
        // // replay the redo again
        // yield return WaitReplayPressed();
        //
        // // wait for replay to finish (can in theory be stopped by user when pressing the replay sphere again)
        // while(studyUI.replaying) yield return null;
        
        // enable next button
        studyUI.instructionsText.text = finishHowTo;
        yield return WaitNextPressed();
        
        // cleanup the current recording
        studyUI.recordingManager.UnloadRecording();
        studyUI.recordingManager.ClearThumbnail();
        
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
            yield return Scenario(studyProcedure.scenario1, scenario1Introduction, s1c0, s1c1, s1c2, s1c3, s1c4);
        }
        else if (studyUI.scenario2Pressed)
        {
            studyUI.scenario2Pressed = false;
            yield return EnableUserSpawnPoints(studyProcedure.scenario2);
            yield return DisableUserSpawnPoints(studyProcedure.scenario1);
            yield return DisableUserSpawnPoints(studyProcedure.scenario3);
            yield return Scenario(studyProcedure.scenario2, scenario2Introduction, s2c0, s2c1, s2c2, s2c3, s2c4);
        }
        else if (studyUI.scenario3Pressed)
        {
            studyUI.scenario3Pressed = false;
            yield return EnableUserSpawnPoints(studyProcedure.scenario3);
            yield return DisableUserSpawnPoints(studyProcedure.scenario1);
            yield return DisableUserSpawnPoints(studyProcedure.scenario2);
            yield return Scenario(studyProcedure.scenario3, scenario3Introduction, s3c0, s3c1, s3c2, s3c3, s3c4);
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
    public IEnumerator WaitRedoRecordingOrNext(Scenario scenario, GameObject takeoverAvatar, int takeoverIndex)
    {
        studyUI.redoSphere.gameObject.SetActive(true);
        studyUI.nextSphere.gameObject.SetActive(true);
        
        while (!studyUI.redoPressed && !studyUI.nextPressed) yield return null;

        if (studyUI.redoPressed)
        {
            studyUI.redoPressed = false;
            studyUI.nextSphere.gameObject.SetActive(false);
            studyUI.redoSphere.gameObject.SetActive(false);
            
            studyUI.SetTakeoverAvatar(takeoverAvatar);
            if (takeoverIndex != -1)
            {
                studyUI.takeoverSelector.TakeoverLastReplay();
            }
            else
            {
                studyUI.takeoverSelector.TakeoverNthReplay(takeoverIndex);
            }
            
            studyUI.instructionsText.text = takeoverInstructions;
            // yield return UIToggle(true); // don't need it as we aren't teleporting!

            yield return Recording(scenario, true, takeoverIndex);
        }
        else if (studyUI.nextPressed)
        {
            studyUI.nextPressed = false;
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
