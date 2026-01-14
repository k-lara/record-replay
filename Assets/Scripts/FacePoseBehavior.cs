using Meta.XR.Movement.FaceTracking.Samples;
using Oculus.Avatar2;
using UnityEngine;

public class FacePoseBehavior : OvrAvatarFacePoseBehavior
{
    public OVRWeightsProvider WeightsProvider { get; private set; }
    public OVRFaceExpressions FaceExpressions { get; private set; }
    
    // these weights are set during a replay
    public float[] recordedWeights = new float[63];
    public bool recordedValid = false;
    
    [HideInInspector]
    public UbiqInputManager inputManager;
    
    public override OvrAvatarFacePoseProviderBase FacePoseProvider 
    {
        get
        {
            InitializeFacePoseProvider();

            return _facePoseProvider;
        }
    }
    
    private OvrAvatarFacePoseProviderBase _facePoseProvider;

    private void InitializeFacePoseProvider()
    {
        if (_facePoseProvider != null) return;

        _facePoseProvider = new FaceTrackingProvider(this);
    }

    public void Start()
    {
        inputManager = GetComponent<UbiqInputManager>();
        // if we are a replayable, we don't want to use face tracking
        // but we want to use the provided data from the current recording
        if (!gameObject.TryGetComponent(out Replayable _))
        {
            Debug.Log("(Real User) Get WeightsProvider and FaceExpressions");
            WeightsProvider = GetComponent<OVRWeightsProvider>();
            FaceExpressions = GetComponent<OVRFaceExpressions>();
        }
        
    }
}

public class FaceTrackingProvider : OvrAvatarFacePoseProviderBase
{
    private FacePoseBehavior _facePoseBehavior;
    
    public FaceTrackingProvider(FacePoseBehavior facePoseBehavior)
    {
        _facePoseBehavior = facePoseBehavior;
    }
    
    protected override bool GetFacePose(OvrAvatarFacePose facePose)
    {
        if (_facePoseBehavior.WeightsProvider)
        {
            // size: 70
            // Debug.Log(_facePoseBehavior);
            // Debug.Log(_facePoseBehavior.WeightsProvider);
            // Debug.Log(_facePoseBehavior.FaceExpressions.enabled);
            // Debug.Log(_facePoseBehavior.FaceExpressions.FaceTrackingEnabled);
            // Debug.Log(_facePoseBehavior.FaceExpressions.ValidExpressions);
            if (_facePoseBehavior.WeightsProvider.IsValid)
            {
                var weights = _facePoseBehavior.WeightsProvider.GetWeights();
                
                // the order of the weights is mostly the same for the OVRWeights provider and the ovrAvatar2FaceExpressions
                // the avatar expressions have a few extra weights which we have to exclude
                // weights are the same until index 49
                for (int i = 0; i < 49; i++)
                {
                    facePose.expressionWeights[i] = weights[i];
                    facePose.expressionConfidence[i] = 1;
                }
                // incoming weights only have LipsTowards
                // avatar weights have LipsTowardLB/LT/RT/RB 
                facePose.expressionWeights[50] = weights[50];
                facePose.expressionWeights[51] = 0;
                facePose.expressionWeights[52] = 0;
                facePose.expressionWeights[53] = 0;
                
                facePose.expressionWeights[54] = weights[51];
                facePose.expressionWeights[55] = weights[52];
                facePose.expressionWeights[56] = weights[53];
                facePose.expressionWeights[57] = weights[54];
                
                // incoming weights don't have NasiolabialFurrowL/R
                facePose.expressionWeights[58] = 0;
                facePose.expressionWeights[59] = 0;
                
                facePose.expressionWeights[60] = weights[55];
                facePose.expressionWeights[61] = weights[56];
                
                // incoming weights don't have NostrilCompressorL/R and NostrilDilatorL/R
                facePose.expressionWeights[62] = 0;
                facePose.expressionWeights[63] = 0;
                facePose.expressionWeights[64] = 0;
                facePose.expressionWeights[65] = 0;
                
                facePose.expressionWeights[66] = weights[57];
                facePose.expressionWeights[67] = weights[58];
                facePose.expressionWeights[68] = weights[59];
                facePose.expressionWeights[69] = weights[60];
                facePose.expressionWeights[70] = weights[61];
                facePose.expressionWeights[71] = weights[62];
                
                // incoming weights have tongue movements but avatars don't
                
                for (int i = 49; i < facePose.expressionWeights.Length; i++)
                {
                    facePose.expressionConfidence[i] = 1;
                }
                return true;
            }
        }
        else
        {
            // if we don't have a weights provider, we must be a replayable or we don't have face tracking
            if (!_facePoseBehavior.recordedValid)
            {
                // Debug.Log(_facePoseBehavior);
                // Debug.Log(_facePoseBehavior.inputManager);
                // Debug.Log(_facePoseBehavior.inputManager.faceTrackingValid);
                _facePoseBehavior.inputManager.faceTrackingValid = false;
                return false;
            }
            
            // see comments above for the weights...
            // I duplicated everything down here because I didn't want to call ToArray() on the ReadOnlyList above
            for (int i = 0; i < 49; i++)
            {
                facePose.expressionWeights[i] = _facePoseBehavior.recordedWeights[i];
                facePose.expressionConfidence[i] = 1;
            }
            facePose.expressionWeights[50] = _facePoseBehavior.recordedWeights[50];
            facePose.expressionWeights[51] = 0;
            facePose.expressionWeights[52] = 0;
            facePose.expressionWeights[53] = 0;
            facePose.expressionWeights[54] = _facePoseBehavior.recordedWeights[51];
            facePose.expressionWeights[55] = _facePoseBehavior.recordedWeights[52];
            facePose.expressionWeights[56] = _facePoseBehavior.recordedWeights[53];
            facePose.expressionWeights[57] = _facePoseBehavior.recordedWeights[54];
            facePose.expressionWeights[58] = 0;
            facePose.expressionWeights[59] = 0;
            facePose.expressionWeights[60] = _facePoseBehavior.recordedWeights[55];
            facePose.expressionWeights[61] = _facePoseBehavior.recordedWeights[56];
            facePose.expressionWeights[62] = 0;
            facePose.expressionWeights[63] = 0;
            facePose.expressionWeights[64] = 0;
            facePose.expressionWeights[65] = 0;
                
            facePose.expressionWeights[66] = _facePoseBehavior.recordedWeights[57];
            facePose.expressionWeights[67] = _facePoseBehavior.recordedWeights[58];
            facePose.expressionWeights[68] = _facePoseBehavior.recordedWeights[59];
            facePose.expressionWeights[69] = _facePoseBehavior.recordedWeights[60];
            facePose.expressionWeights[70] = _facePoseBehavior.recordedWeights[61];
            facePose.expressionWeights[71] = _facePoseBehavior.recordedWeights[62];
            
            for (int i = 49; i < facePose.expressionWeights.Length; i++)
            {
                facePose.expressionConfidence[i] = 1;
            }
            return true;
        }
        _facePoseBehavior.inputManager.faceTrackingValid = false;
        return false;
    }
}
