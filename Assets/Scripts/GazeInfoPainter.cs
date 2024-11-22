using System;
using System.Collections;
using System.Collections.Generic;
using Ubiq.Avatars;
using UnityEngine;

public class GazeInfoPainter : MonoBehaviour
{
    private ReplayInfo replayInfo;
    // private RecordableOld.RecordableData replayableData;
    
    private Gradient gradient = new Gradient();
    

    void Awake()
    {
        
        replayInfo = GetComponent<ReplayInfo>();
        // replayInfo.OnReplayInfoDataLoaded += OnReplayInfoDataLoaded;
        replayInfo.OnReplayInfoReplayStart += OnReplayInfoReplayStart;
        replayInfo.OnReplayInfoReplayStop += OnReplayInfoReplayStop;
        
        var colors = new GradientColorKey[3];
        colors[0] = new GradientColorKey(Color.blue, 0.0f);
        colors[1] = new GradientColorKey(Color.white, 0.5f);
        colors[2] = new GradientColorKey(Color.magenta, 1.0f);
        
        // no alpha values in this case
        var alphas = new GradientAlphaKey[2];
        alphas[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphas[1] = new GradientAlphaKey(1.0f, 1.0f);
        
        gradient.SetKeys(colors, alphas);
    }

    private void OnReplayInfoReplayStop(object sender, EventArgs e)
    {
    }

    private void OnReplayInfoReplayStart(object sender, EventArgs e)
    {
    }

    // maps the gradient colors to the orientation of the head movements
    // private void OnReplayInfoDataLoaded(object sender, RecordableOld.RecordableData data)
    // {
    //     replayableData = data;
    //     
    //     // draw the cursor with the lineRenderer
    //     
    //     
    // }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
