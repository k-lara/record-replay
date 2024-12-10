using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq;
using Ubiq.Avatars;
using Ubiq.Messaging;
using Ubiq.Spawning;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Avatar = Ubiq.Avatars.Avatar;

// this class handles the takeover process and the visuals and interactions attached to it

// visual interaction:
// player can grab the head of the avatar to take over and will get a mask that they can put on their face
public class TakeoverSelector : MonoBehaviour
{
    // fade in and out of player camera when taking over (vignetting)

    private Dictionary<Guid, Collider> takeoverColliders = new();
    private Replayer replayer;
    private NetworkSpawnManager spawnManager;
    
    private Camera playerCamera;
    private TunnelingVignetteController vignetteController;
    private float currentFeathering = 1.0f; // the current vignetting effect
    private float previousDistance = 0.2f; // the current distance between the player's camera and the takeoverObject
    public GameObject takeoverObject; // the object used to visualise the takeover
    private GameObject spawnedObject; // the spawned object when the player selects the takeover avatar
    private GameObject selectedReplayableObject; // the replayable avatar that the player has selected to take over
    
    private float minDistanceCamera = 0.05f; // the minimum distance between the player's camera and the takeoverObject
    private float maxDistanceCamera = 0.2f; // the maximum distance between the player's camera and the takeoverObject
    private float maxAngle = 30.0f;
    private float currentDistance;
    private float currentAngle;

    public event EventHandler<GameObject> onTakeoverSelected;
    public void Start()
    {
        playerCamera = Camera.main;
        vignetteController = playerCamera?.GetComponent<TunnelingVignetteController>();

        replayer = GetComponent<Replayer>();
        replayer.onReplaySpawned += OnReplayCreated;
        replayer.onReplayUnspawned += OnReplayUnspawned;
        
        spawnManager = NetworkSpawnManager.Find(this);
    }
    
    // for testing in the editor, we don't have a takeoverObject, but we simple take over the first replayable avatar from the collider list
    public void TakeoverTestEditor()
    {
        Debug.Log("TakeoverTesteEditor: ");
        
        if (takeoverColliders.Count > 0)
        {
            selectedReplayableObject = takeoverColliders.First().Value.gameObject;
            onTakeoverSelected?.Invoke(this, selectedReplayableObject);
            selectedReplayableObject = null;
        }
    }
    
    
    // called when selectEntered on replayable avatar
    // we do this to make the takeover look cooler by spawning an object the player has to interact with
    private void SpawnTakeoverObject(SelectEnterEventArgs args)
    {
        if (takeoverObject != null)
        {
            spawnedObject = spawnManager.SpawnWithPeerScope(takeoverObject);
            spawnedObject.transform.position = args.interactableObject.transform.position;
            spawnedObject.transform.rotation = args.interactableObject.transform.rotation;

            selectedReplayableObject = args.interactableObject.transform.gameObject;
            
            // make the spawnedObject be grabbed by the player
            var grabInteractable = spawnedObject.GetComponent<XRGrabInteractable>();
            grabInteractable.selectExited.AddListener(UnspawnTakeoverObject);
            grabInteractable.matchAttachPosition = true;
            args.manager.SelectEnter(args.interactorObject, grabInteractable);
        }
    }
    
    // called when selectExited on the SPAWNED OBJECT!
    private void UnspawnTakeoverObject(SelectExitEventArgs args)
    {
        // check if the mask is close enough and aligned correctly in order to initiate takeover
        if (currentDistance <= minDistanceCamera && currentAngle <= maxAngle)
        {
            onTakeoverSelected?.Invoke(this, selectedReplayableObject);
        }
        
        spawnManager.Despawn(spawnedObject);
        spawnedObject = null;
        // TODO this might happen too soon, if it happens before the next update, we haven't actually changed position yet...
        vignetteController.defaultParameters.featheringEffect = 1.0f;
        selectedReplayableObject = null;
    }
    
    // Update the takeover colliders when a new replayable is created
    // this is currently only called when recording data is loaded 
    // and not when only a thumbnail is loaded as I cannot see why it would be necessary already
    private void OnReplayCreated(object o, Dictionary<Guid, Replayable> replayables)
    {
        // remove the old colliders
        takeoverColliders.Clear();
        foreach (var replayable in replayables)
        {
            Debug.Log("TakeoverSelector: Get Colliders/ Add Interactables to replayable: " + replayable.Key);
            if (replayable.Value.gameObject.TryGetComponent(out Collider coll))
            {
                takeoverColliders.Add(replayable.Key, coll);
                // check if it has the component already since this could be a reused object from a previous thumbnail
                if (!replayable.Value.gameObject.TryGetComponent(out XRSimpleInteractable interactable))
                {
                    interactable = replayable.Value.gameObject.AddComponent<XRSimpleInteractable>();
                }
                interactable.selectEntered.AddListener(SpawnTakeoverObject);
            }
        }
    }
    private void OnReplayUnspawned(object o, EventArgs e)
    {
        takeoverColliders.Clear();
    }

    public void TakeoverVignetting()
    {
        // when the takeoverObject moves closer to the player's camera we increase the vignetting
        // when the takeoverObject moves further away from the player's camera we decrease the vignetting
        
        // whether vignetting happens depends on the camera's look at direction and the takeoverObjects alignment 
        // takeoverObject (mask) should be aligned with the player's face (camera look at direction)
        
        
        // check for vignetting once takeoverObject is within 20cm of the player's camera
        // vignetting should be at its maximum when the takeoverObject is 0.05m away from the player's camera
        currentDistance = Vector3.Distance(spawnedObject.transform.position, playerCamera.transform.position);
        currentAngle = Vector3.Angle(playerCamera.transform.forward, spawnedObject.transform.forward);
        Debug.Log("dist/angle: " + currentDistance + "/" + currentAngle);
        if (currentDistance < maxDistanceCamera && currentAngle < maxAngle)
        {
            // compute feathering amount
            var f = (currentDistance - minDistanceCamera) / (maxDistanceCamera - minDistanceCamera);
            Debug.Log("f: " + f);
            if (currentDistance < previousDistance) // increase vignetting (= decrease feathering value)
            {
                if (f < 0.0f)
                {
                    currentFeathering = 0.0f;
                }
                else
                {
                    currentFeathering = f;
                }
                vignetteController.defaultParameters.featheringEffect  = currentFeathering;
                
            }
            else
            {
                if (f > 1.0f)
                {
                    currentFeathering = 1.0f;
                }
                else
                {
                    currentFeathering = f;
                }
                vignetteController.defaultParameters.featheringEffect  = currentFeathering;
            }

            previousDistance = currentDistance;
            
            // if the angle is small enough, increase the vignetting
            // if the angle is large enough, decrease the vignetting
            // if the angle is too large, stop the vignetting
        }
        else
        {
            vignetteController.defaultParameters.featheringEffect = 1.0f;
        }
    }

    public void Update()
    {
        // while takeoverObject is held by the player
        if (spawnedObject != null)
        {
            TakeoverVignetting();
        }
    }
}
