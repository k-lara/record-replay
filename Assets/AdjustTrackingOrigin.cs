using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

public class AdjustTrackingOrigin : MonoBehaviour
{
    private XROrigin _xrOrigin;
    
    // Start is called before the first frame update
    void Start()
    {
        _xrOrigin = GetComponent<XROrigin>();
        StartCoroutine(SetTrackingOrigin());
    }
    
    private IEnumerator SetTrackingOrigin()
    {
        _xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        yield return new WaitForSeconds(0.1f);
        _xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
    }
}
