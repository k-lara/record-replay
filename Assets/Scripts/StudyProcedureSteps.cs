using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    public bool introductionDone = false;
    
    public TextMeshProUGUI instructionText;

    private string introduction = "Introduction here!";
    private string recordingInstructions = "How to do a recording?";
    private string replayInstructions = "How to replay a recording?";
    private string redoInstructions = "Redo the recording, if tracking gets lost! (Avatar will be blue)";
    private string takeoverInstructions = "Press record again and you previous recording will be overwritten!";
    private string rewatchRedoInstructions = "If you press replay again, you will see the new recording!";
    private string finishHowTo = "You are now ready to start the study!";
    private string scenarioSelection = "You will experience different scenarios. Select a scenario!";

    private string scenario1Introduction = "In this scenario something will happen... you will be teleported to your position";
    private string s1c1 = "Character 1 (name?): ...";
    private string s1c2 = "Character 2 (name?): ...";
    private string s1c3 = "Character 3 (name?): ...";
    private string s1c4 = "Character 4 (name?): ...";
    private string s1c5 = "Character 5 (name?): ...";
    private string ready = "When you are ready, press record!";
    
    private float maxRecordingTime = 10.0f; // 10 seconds for the test recording
    private float finishCountdown = 5.0f; // 5 seconds to finish the recording
    private float currentRecordingTime = 0.0f;
    
    void Awake()
    {
        studyProcedure = GetComponent<StudyProcedure>();
        studyUI.recordSphere.gameObject.SetActive(false);
        studyUI.previousSphere.gameObject.SetActive(false);
        studyUI.redoRecordingSphere.gameObject.SetActive(false);
        studyUI.replaySphere.gameObject.SetActive(false);
        studyUI.scenario1Sphere.gameObject.SetActive(false);
        studyUI.scenario2Sphere.gameObject.SetActive(false);
        studyUI.scenario3Sphere.gameObject.SetActive(false);
    }
    
    // Scenario 1 (low interactivity) reacting to an argument between two people
    public IEnumerator Scenario1()
    {
        instructionText.text = scenario1Introduction;
        studyUI.scenario1Sphere.gameObject.SetActive(false);
        studyUI.scenario2Sphere.gameObject.SetActive(false);
        studyUI.scenario3Sphere.gameObject.SetActive(false);
        studyUI.nextSphere.gameObject.SetActive(true);
        
        // load the characters and the recorded data for the scenario
        yield return studyUI.recordingManager.GotoThumbnail(studyProcedure.scenario1.ScenarioNumber);
        while (!studyUI.thumbnailAvatarsSpawned) yield return null;
        studyUI.thumbnailAvatarsSpawned = false; // reset
        studyUI.recordingManager.LoadRecording();
        while (!studyUI.recordingDataLoaded) yield return null;
        studyUI.recordingDataLoaded = false; // reset
        
        while(!studyUI.nextPressed) yield return null;
        studyUI.nextPressed = false;
        studyUI.nextSphere.gameObject.SetActive(false);
        
        // show the avatar for the user in the current position
        yield return studyProcedure.SetUserPosition(studyProcedure.scenario1.UserSpawnPoints[0]);
        yield return UIToggle(true); // show the UI in front of the user in new position
        studyUI.mirror.SetActive(true);
        studyUI.instructionsText.text = s1c1;
        
        // recording (make this into a separate function bc it is always the same)
        studyUI.recordSphere.gameObject.SetActive(true);
        while(!studyUI.recordPressed) yield return null;
        yield return UIToggle(false); // hide the UI during recording
        
        yield return new WaitForSeconds(maxRecordingTime - finishCountdown);
        
        // play countdown beep
        yield return Countdown(finishCountdown);
        
        
        studyUI.recordPressed = false;
        

    }

    public IEnumerator Scenario2()
    {
        yield return null;
    }
    
    public IEnumerator Scenario3()
    {
        yield return null;
    }

    public IEnumerator Introduction()
    {
        instructionText.text = introduction;
        studyUI.gameObject.SetActive(true);
        studyUI.nextSphere.gameObject.SetActive(true);
        while (studyUI.recordingManager.GetThumbnailCount() < 2) yield return null;
        
        while (!studyUI.nextPressed) yield return null;
        studyUI.nextPressed = false;
    }

    public IEnumerator TutorialYesNo()
    {
        instructionText.text = recordingInstructions;
        studyUI.gameObject.SetActive(true);
        studyUI.nextSphere.gameObject.SetActive(false);
        studyUI.recordSphere.gameObject.SetActive(true);
        studyUI.skipSphere.gameObject.SetActive(true);
        
        while (!studyUI.recordPressed && !studyUI.skipPressed) yield return null;
        
        if (studyUI.recordPressed)
        {
            studyUI.nextPressed = studyUI.skipPressed = false;
            yield return HowToRecordReplay();
        }
        else if (studyUI.skipPressed)
        {
            studyUI.nextPressed = studyUI.skipPressed = false;
            studyUI.recordSphere.gameObject.SetActive(false);
            studyUI.skipSphere.gameObject.SetActive(false);
            yield return ScenarioSelection();
        }
    }
    
    public IEnumerator HowToRecordReplay()
    {
        while (!studyUI.recordPressed) yield return null;
        yield return UIToggle(false);
        // wait for current recording time to reach maxRecordingTime - end countdown beep
        yield return new WaitForSeconds(maxRecordingTime - finishCountdown);
        
        // play countdown beep
        yield return Countdown(finishCountdown);
        
        // stop recording
        studyUI.recordPressed = false;
        currentRecordingTime = 0.0f;
        studyUI.recorder.StopRecording();
        // enable UI
        studyUI.recordSphere.gameObject.SetActive(false);
        yield return UIToggle(true); // show UI after recording in front of user
        
        studyUI.instructionsText.text = replayInstructions;
        studyUI.replaySphere.gameObject.SetActive(true); // show replay sphere
        while (!studyUI.replayPressed) yield return null; // replay the recording
        
        // wait for replay to finish (can in theory be stopped by user when pressing the replay sphere again)
        while(studyUI.replaying) yield return null;
        
        // redo instructions
        studyUI.instructionsText.text = recordingInstructions;
        studyUI.redoRecordingSphere.gameObject.SetActive(true); // show redo recording sphere
        while (!studyUI.redoPressed) yield return null; // takeover avatar
        yield return UIToggle(true); // just for adjusting the UI position after takeover
        studyUI.instructionsText.text = takeoverInstructions;
        studyUI.recordSphere.gameObject.SetActive(true);
        
        // do the whole recording again!
        while (!studyUI.recordPressed) yield return null;
        yield return UIToggle(false);
        studyUI.replayPressed = false; // reset replay button
        studyUI.gameObject.SetActive(false); // hide UI during recording
        // wait for current recording time to reach maxRecordingTime - end countdown beep
        yield return new WaitForSeconds(maxRecordingTime - finishCountdown);
        
        // play countdown beep
        yield return Countdown(finishCountdown);
        
        // stop recording
        studyUI.recordPressed = false;
        currentRecordingTime = 0.0f;
        studyUI.recorder.StopRecording();
        // enable UI
        studyUI.recordSphere.gameObject.SetActive(false);
        yield return UIToggle(true); // show UI after recording in front of user
        
        // replay the redo again
        studyUI.instructionsText.text = replayInstructions;
        studyUI.replaySphere.gameObject.SetActive(true); // show replay sphere
        while (!studyUI.replayPressed) yield return null; // replay the recording
        
        // wait for replay to finish (can in theory be stopped by user when pressing the replay sphere again)
        while(studyUI.replaying) yield return null;
        
        // enable next button
        studyUI.instructionsText.text = finishHowTo;
        studyUI.nextSphere.gameObject.SetActive(true);
        while (!studyUI.nextPressed) yield return null;
        studyUI.nextPressed = false;
        
        // cleanup the current recording
        studyUI.recordingManager.UnloadRecording();
        studyUI.recordingManager.ClearThumbnail();
        
        studyUI.redoPressed = false;
        studyUI.replayPressed = false;
    }

    public IEnumerator ScenarioSelection()
    {
        instructionText.text = scenarioSelection;
        studyUI.scenario1Sphere.gameObject.SetActive(true);
        studyUI.scenario2Sphere.gameObject.SetActive(true);
        studyUI.scenario3Sphere.gameObject.SetActive(true);
        
        while (!studyUI.scenario1Pressed && !studyUI.scenario2Pressed && !studyUI.scenario3Pressed) yield return null;
        
        if (studyUI.scenario1Pressed)
        {
            studyUI.scenario1Pressed = false;
            yield return Scenario1();
        }
        else if (studyUI.scenario2Pressed)
        {
            studyUI.scenario2Pressed = false;
            yield return Scenario2();
        }
        else if (studyUI.scenario3Pressed)
        {
            studyUI.scenario3Pressed = false;
            yield return Scenario3();
        }
    }
    
    // TODO after a replay remember to set the replay back to frame 0!!!

    private IEnumerator Countdown(float countdownTime)
    {
        for (int i = 0; i < countdownTime; i++)
        {
            studyUI.PlayHighBeep();
            yield return new WaitForSeconds(1.0f);
        }
        studyUI.PlayLowBeep();
    }
    
    public IEnumerator UIToggle(bool uiVisible)
    {
        // place the ui in front of the player whenever it is toggled
        Camera cam = Camera.main;
        if (cam && uiVisible)
        {
            // align it with the direction the camera is looking at
            studyUI.gameObject.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
            studyUI.gameObject.transform.position = 
                new Vector3(cam.transform.position.x, cam.transform.position.y - 0.3f, cam.transform.position.z) + 
                cam.transform.forward * 0.45f;
        }
        studyUI.gameObject.SetActive(uiVisible);
        yield return null;
    }

    private void Update()
    {
        if (studyUI.recordPressed)
        {
            if (currentRecordingTime <= maxRecordingTime)
            {
                currentRecordingTime += Time.deltaTime;
            }
        }
    }
}
