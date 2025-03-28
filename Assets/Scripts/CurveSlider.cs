using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.XR.Interaction.Toolkit;


// quadratic bezier curve
// P(t)=(1−t)^2*P0 + 2(1−t)tP1 + t*2*P2

// cubic bezier curve
// P(t)=(1−t)^3*P0 + 3(1−t)^2*t*P1 + 3(1−t)t^2*P2 + t^3*P3

// circle parametric curve
// x = r * cos(t)

public class CurveSlider : MonoBehaviour
{
    // Start is called before the first frame update
    
    [Range(0,1)]
    public float t;
    public GameObject slider;
    public EventHandler<float> onTChanged;

    private Replayer replayer;

    private float tPrev;
    private SplineAnimate sliderAnimate;
    private XRGrabInteractable grabInteractable;
    private Transform interactorTransform;
    private Vector3 interactorPosition;
    private bool isGrabbed;
    
    public AudioSource audioSource;
    
    void Start()
    {
        grabInteractable = slider.GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
        sliderAnimate = slider.GetComponent<SplineAnimate>();
        replayer = GameObject.FindWithTag("Recorder").GetComponent<Replayer>();

    }
    
    private void OnSelectExited(SelectExitEventArgs args)
    {
        // Debug.Log("Select exited");
        audioSource.Play();
        isGrabbed = false;
    }
    
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Debug.Log("Select entered");
        isGrabbed = true;
        interactorTransform = args.interactorObject.transform;
        interactorPosition = interactorTransform.position;
    }

    public void SetT(float t)
    {
        sliderAnimate.NormalizedTime = this.t = t;
        // Debug.Log("Set t: " + t + " " + sliderAnimate.NormalizedTime);
    }

    // Update is called once per frame
    void Update()
    {
        if (replayer.IsPlaying) return;
      
        // t is between 0 and 1
        if (isGrabbed)
        {
            // compute velocity of movement of the interactor
            var velocity = (interactorTransform.position - interactorPosition) / Time.deltaTime;
            
            // increase t depending on the velocity
            // only consider x and y components of the velocity
            // dot product of the velocity and the forward vector of the slider
            var dot = Vector3.Dot(velocity, slider.transform.forward);
            t += dot * Time.deltaTime * 2.0f;

            if (t >= 1.0f)
            {
                t = 1.0f;
            }
            else if (t <= 0.0f)
            {
                t = 0.0f;
            }
            
            sliderAnimate.NormalizedTime = t;
            // Debug.Log("dot: " + dot + " t: " + t + " velocity: " + velocity);
            
            if (!Mathf.Approximately(tPrev, t))
            {
                // Debug.Log("t changed: " + t);
                onTChanged?.Invoke(this, t);
            }
            
            tPrev = t;
            interactorPosition = interactorTransform.position;
        }
    }
}
