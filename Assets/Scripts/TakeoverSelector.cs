using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    // private Dictionary<Guid, Collider> takeoverColliders = new();
    private Dictionary<Guid, GameObject> interactableSpheres = new();
    private Replayer replayer;
    private RecordingManager recordingManager;
    private NetworkSpawnManager spawnManager;
    
    private Camera playerCamera;
    private TunnelingVignetteController vignetteController;
    private float currentFeathering = 1.0f; // the current vignetting effect
    private float previousDistance = 0.2f; // the current distance between the player's camera and the takeoverObject
    public GameObject takeoverInteractable;
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
        recordingManager = GetComponent<RecordingManager>();
        recordingManager.onRecordingUndo += OnRecordingUndo;
        recordingManager.onRecordingRedo += OnRecordingRedo;
        
        spawnManager = NetworkSpawnManager.Find(this);
    }
    
    // for testing in the editor, we don't have a takeoverObject, but we simply take over the first replayable avatar from the collider list
    public void TakeoverTestEditor()
    {
        // Debug.Log("TakeoverTesteEditor: colliders count:" + takeoverColliders.Count);
        //
        // // count is 0 when no replay is loaded. colliders only get added when replay data is loaded but are not automatically on a thumbnail replayable
        // if (takeoverColliders.Count > 0)
        // {
        //     selectedReplayableObject = takeoverColliders.First().Value.gameObject;
        //     onTakeoverSelected?.Invoke(this, selectedReplayableObject);
        //     selectedReplayableObject = null;
        // }
    }

    public void TakeoverLastReplay()
    {
        if (interactableSpheres.Count == 0) return;

        var lastInteractable = interactableSpheres.Last();
        selectedReplayableObject = lastInteractable.Value.transform.parent.gameObject;
        onTakeoverSelected?.Invoke(this, selectedReplayableObject);
        selectedReplayableObject = null;
        
        Debug.Log("Takeover last replay: " + lastInteractable.Key);
    }

    public void TakeoverNthReplay(int n)
    {
        if (interactableSpheres.Count == 0 || n >= interactableSpheres.Count) return;
        
        var nthInteractable = interactableSpheres.ElementAt(n);
        selectedReplayableObject = nthInteractable.Value.transform.parent.gameObject;
        onTakeoverSelected?.Invoke(this, selectedReplayableObject);
        selectedReplayableObject = null;
        Debug.Log("Takeover " + n + ". replay: " + nthInteractable.Key);
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
        // add sphere interactables above the head of the avatar
        foreach (var replayable in replayables)
        {
            // Debug.Log("TakeoverSelector: OnReplayCreated(): replayable:" + replayable.Key);
            // skip if we already have an interactable sphere on this replayable
            if (interactableSpheres.ContainsKey(replayable.Key))
            {
                // Debug.Log("TakeoverSelector: Interactable Sphere already exists, skipping");
                continue;
            }
            
            AddTakeoverInteractable(replayable);
        }
    }
    
    private void AddTakeoverInteractable(KeyValuePair<Guid, Replayable> replayable)
    {
        // let's keep it simple here, just add it to the replayable object
        var sphere = Instantiate(takeoverInteractable, replayable.Value.transform);
        
        sphere.SetActive(false);
        sphere.GetComponent<InteractableSphere>().onSphereSelected += TakeoverSelected;
        // sphere.transform.localPosition = new Vector3(0.4f, 0, 0);
        // sphere.transform.localRotation = Quaternion.identity;
        interactableSpheres.Add(replayable.Key, sphere);
        Debug.Log("Add interactable to replayable: " + replayable.Key);
    }

    private void OnRecordingUndo(object o, (UndoManager.UndoType, List<Guid>) args)
    {
        if (args.Item1 == UndoManager.UndoType.New) // remove the interactable from the replayable we undo
        {
            foreach (var id in args.Item2)
            {
                if (interactableSpheres.ContainsKey(id))
                {
                    Debug.Log("UndoNew: Remove interactable sphere from replayable: " + id);
                    interactableSpheres[id].GetComponent<InteractableSphere>().onSphereSelected -= TakeoverSelected;
                    Destroy(interactableSpheres[id]);
                    interactableSpheres.Remove(id);
                }
            }
        }
    }
    
    private void OnRecordingRedo(object o, (UndoManager.UndoType, List<Replayable>) args)
    {
        if (args.Item1 == UndoManager.UndoType.New) // add the interactable to the replayable we redo
        {
            foreach (var replayable in args.Item2)
            {
                Debug.Log("Redo New");
                AddTakeoverInteractable(new KeyValuePair<Guid, Replayable>(replayable.replayableId, replayable));
            }
        }
    }

    public void EnableTakeover()
    {
        foreach(var sphere in interactableSpheres)
        {
            sphere.Value.SetActive(true);
        }
    }

    public void DisableTakeover()
    {
        foreach (var sphere in interactableSpheres)
        {
            sphere.Value.SetActive(false);
        }
    }
    
    private void TakeoverSelected(object o, EventArgs e)
    {
        onTakeoverSelected?.Invoke(this, ((InteractableSphere) o).transform.parent.gameObject);
    }
    private void OnReplayUnspawned(object o, EventArgs e)
    {
        // unsubscribe from events
        foreach (var sphere in interactableSpheres)
        {
            sphere.Value.GetComponent<InteractableSphere>().onSphereSelected -= TakeoverSelected;
            Destroy(sphere.Value);
        }
        
        interactableSpheres.Clear();
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
