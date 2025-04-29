using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(StudyProcedureSteps))]
public class StudyProcedure : MonoBehaviour
{ 
    public XROrigin XrOrigin;
    public float MetaAvatarDefaultCamHeight = 1.65f;
    
    public float MaxFogValue = 0.35f;

    public Scenario scenario1;
    public Scenario scenario2;
    public Scenario scenario3;
}
