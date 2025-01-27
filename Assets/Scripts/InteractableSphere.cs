using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;

// instead of using physicsy buttons that don't work properly and are a pain
// we use spheres that the user can interact with
// when the user touches the sphere it pulsates, showing that it is selected
// when grip button is pressed (or whatever gesture for hand tracking) the sphere shrinks
// and becomes more opaque
// the sphere can be pressed again once it has returned to its original shape and transparency
public class InteractableSphere : MonoBehaviour
{
    private XRSimpleInteractable _sphereInteractable;
    
    private bool _pulsing;
    private bool _reset;
    private bool _shrinking;
    private bool _expanding;

    private float _currentScale;
    private float _previousSin;

    private float _defaultScale;
    private float _minScale = 0.01f;
    
    private float _delta;

    public float speed;
    public float multiplier;
    public Color color;
    private bool highlight;
    private Material _mat;

    public event EventHandler onSphereSelected;
    
    public void EnableInteractable(bool enable)
    {
        _sphereInteractable.enabled = enable;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        _mat = GetComponent<MeshRenderer>().material;
        _defaultScale = transform.localScale.x;
        _defaultScale = 0.1f;
        _currentScale = _defaultScale;
        _sphereInteractable = GetComponent<XRSimpleInteractable>();
        _sphereInteractable.hoverEntered.AddListener(HoverEntered);
        _sphereInteractable.hoverExited.AddListener(HoverExited);
        _sphereInteractable.selectEntered.AddListener(SelectEntered);
        // _sphereInteractable.selectExited.AddListener(SelectExited);
            
    }
    
    // we set highlight to true so we know when the animations happen that it should stay highlighted
    // and not go back to default color
    public void EnableHighlight()
    {
        _mat.color = color;
        highlight = true;
    }

    public void DisableHighlight()
    { 
        _mat.color = new Color(1f, 1f, 1f, 0.4f);
        highlight = false;
    }
    
    // when the user hovers close to the sphere it starts pulsing
    // if sphere is currently shrinking or expanding don't do anything
    private void HoverEntered(HoverEnterEventArgs args)
    {
        Debug.Log("Hover entered");
        if (_shrinking || _expanding) return;
        _pulsing = true;
    }
    
    // when the user removes the hand hovering stops and the sphere goes back to its original size
    // if the sphere is currently shrinking or expanding due to a select actions
    // we let the current process finish and do not reset as we would if the sphere is only pulsing
    private void HoverExited(HoverExitEventArgs args)
    {
        Debug.Log("Hover exited");
        if (_shrinking || _expanding) return;
        _pulsing = false;
        if (Mathf.Approximately(_currentScale, _defaultScale)) return;
        _reset = true;
    }
    // when the user selects the sphere, hovering stops and the sphere shrinks
    private void SelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log("Select entered");
        if (_shrinking || _expanding) return;
        _pulsing = false;
        _shrinking = true;
        onSphereSelected?.Invoke(this, EventArgs.Empty);
    }
    
    // nothing happens on select exit so far
    // private void SelectExited(SelectExitEventArgs args)
    // {
    //     Debug.Log("Select exited");
    // }

    // when we exit hovering, we want the sphere to go back to its original size
    // and NOT stay in whatever scale it was when the hover was exited
    private void ReturnToOriginalScale(float speed)
    {
        var sin = Mathf.Sin(_delta * speed);
        _currentScale = _defaultScale + sin * 0.01f;
        _delta += Time.deltaTime;

        if (!Mathf.Approximately(Mathf.Sign(sin), Mathf.Sign(_previousSin)))
        {
            transform.localScale = new Vector3(_defaultScale, _defaultScale, _defaultScale);
            _reset = false;
        }
        else
        {
            transform.localScale = new Vector3(_currentScale, _currentScale, _currentScale);
            _previousSin = sin;
        }
        
    }

    private void Pulsing(float speed)
    {
        // make the sphere pulse
        var sin = Mathf.Sin(_delta * speed);
        _currentScale = _defaultScale + sin * 0.01f;
        _delta += Time.deltaTime;
        transform.localScale = new Vector3(_currentScale, _currentScale, _currentScale);
        _previousSin = sin;
    }

    private void Shrinking(float speed)
    {
        _currentScale -= Time.deltaTime * 0.02f * speed;
        if (highlight)
            EnableHighlight();
        else
        {  
        }
        
        if (_currentScale < _minScale)
        {
            transform.localScale = new Vector3(_minScale, _minScale, _minScale);
            _currentScale = _minScale;
            _shrinking = false;
            _expanding = true;
        }
        else
        {
            transform.localScale = new Vector3(_currentScale, _currentScale, _currentScale);
        }
    }

    private void Expanding(float speed)
    {
        _currentScale += Time.deltaTime * 0.02f * speed;

        if (_currentScale > _defaultScale)
        {
            transform.localScale = new Vector3(_defaultScale, _defaultScale, _defaultScale);
            _currentScale = _defaultScale;
            _expanding = false;
            
            if (!highlight)
                DisableHighlight();
        }
        else
        {
            transform.localScale = new Vector3(_currentScale, _currentScale, _currentScale);
        }
    }
  
    
    // Update is called once per frame
    void Update()
    {
        if (_pulsing)
        {
            Pulsing(speed);
        }

        if (_reset)
        {
            ReturnToOriginalScale(speed);
        }

        if (_shrinking)
        {
            Shrinking(speed * multiplier);
        }

        if (_expanding)
        {
            Expanding(speed * multiplier);
        }
    }
}
