using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class VRButtonTrigger : MonoBehaviour
{
    // let's have the possibility to have a non physics button, just to see if that works better
    
    public float deadTime = 0.8f;

    private bool _deadTimeActive = false;

    public UnityEvent onPressed, onReleased;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Button") && !_deadTimeActive)
        {
            onPressed.Invoke();
            // Debug.Log("Button pressed");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Button") && !_deadTimeActive)
        {
            onReleased.Invoke();
            Debug.Log("Button released");
            StartCoroutine(WaitForDeadTime());
        }
    }

    IEnumerator WaitForDeadTime()
    {
        _deadTimeActive = true;
        yield return new WaitForSeconds(deadTime);
        _deadTimeActive = false;
    }
}
