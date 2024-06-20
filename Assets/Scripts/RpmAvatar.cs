using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq.Avatars;
using UnityEngine;
using UnityEngine.Serialization;

public class RpmAvatar : MonoBehaviour
{
    // the target transforms are used for VRIK
    // these targets are set from the tracked avatar which gets them from the hints
    public Transform headTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public float yHeadOffset;
    
    private ThreePointTrackedAvatar _trackedAvatar;
    
    private void OnEnable()
    {
        _trackedAvatar = GetComponentInParent<ThreePointTrackedAvatar>();

        if (_trackedAvatar)
        {
            _trackedAvatar.OnHeadUpdate.AddListener(TrackedAvatarOnHeadUpdate);
            _trackedAvatar.OnLeftHandUpdate.AddListener(TrackedAvatarOnLeftHandUpdate);
            _trackedAvatar.OnRightHandUpdate.AddListener(TrackedAvatarOnRightHandUpdate);
        }
    }

    private void OnDisable()
    {
        if (_trackedAvatar && _trackedAvatar != null)
        {
            _trackedAvatar.OnHeadUpdate.RemoveListener(TrackedAvatarOnHeadUpdate);
            _trackedAvatar.OnLeftHandUpdate.RemoveListener(TrackedAvatarOnLeftHandUpdate);
            _trackedAvatar.OnRightHandUpdate.RemoveListener(TrackedAvatarOnRightHandUpdate);
        }
    }
    
    private void TrackedAvatarOnHeadUpdate(Vector3 position, Quaternion rotation)
    {
        headTarget.position = position;
        headTarget.rotation = rotation;
        
    }
    
    // hand rotation is wrong for VRIK when coming correctly from the tracker
    private void TrackedAvatarOnLeftHandUpdate(Vector3 position, Quaternion rotation)
    {
        leftHandTarget.position = position;
        leftHandTarget.rotation = rotation;
    }
    
    // hand rotation is wrong for VRIK
    private void TrackedAvatarOnRightHandUpdate(Vector3 position, Quaternion rotation)
    {
        rightHandTarget.position = position;
        rightHandTarget.rotation = rotation;

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
