using Unity.WebRTC;
using UnityEngine;

/// <summary>
/// Starts the WebRTC video update coroutine. Separated out from the Recorder
/// into its own class because you likely already do this from within your own
/// code, and you do not want to start the coroutine multiple times.
/// </summary>
public class StartWebRTCVideoUpdate : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WebRTC.Update());
    }
}
