using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(StudyProcedureSteps))]
public class StudyProcedure : MonoBehaviour
{ 
    public ParticleSystem ParticleSystem;
    
    public float MaxFogValue = 0.3f;

    public Scenario scenario1;
    public Scenario scenario2;
    
    // each participant has to act with the same scenarios using the same base recordings
    // so whenever a new participants starts, should we copy the base recordings to a new folder ? 
    
    private XROrigin xrOrigin;
    
    // Start is called before the first frame update
    void Start()
    {
        xrOrigin = FindObjectOfType<XROrigin>();
    }
    
    public void EnableFog(bool value)
    {
        RenderSettings.fog = value;
    }

    // x axis points where the arrow points on the floor
    public IEnumerator SetUserPosition(GameObject target)
    {
        // set player position to replayable position
        if (xrOrigin != null)
        {
            // TODO check if this works!
            var forward = Quaternion.Euler(0, target.transform.rotation.eulerAngles.y, 0) * Vector3.right;
            var success = xrOrigin.MoveCameraToWorldLocation(new Vector3(target.transform.position.x, xrOrigin.Camera.transform.position.y, target.transform.position.z));
            success = success && xrOrigin.MatchOriginUpCameraForward(xrOrigin.transform.up, forward);
            yield return success;
        }
        yield return false;
    }

    IEnumerator StudyCoroutine()
    {
        //TODO: maybe add a recording test run at the beginning to let the users see how the recording works?
        
        
        yield return null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
