using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/* this class comprises a recording that is stored in memory for extending and editing it
the recordingId is unique to the recording
the recording consists of 
 */
public class Recording
{
    public Guid recordingId { get; private set; } // guid
    // recordingSavePath + recordingId make up the path to the recording folder
    public string recordingSavePath { get; private set; } // path to the directory that has all the recording folders
    
    // contains the data of the recording which is the data from all recordables
    public Dictionary<Guid, RecordableData> recordableDataDict { get; private set; }
    
    // public List<RecordableDataFrame> currentTakoverOverwrite = new();
    // public Guid currentTakeoverId;
    
    [System.Serializable]    
    public class RecordableData
    {
        public int numFrames;
        public int fps;
        public string prefabName;
        
        // don't really need dataLabels because the RecordableDataFrame has the variables
        
        public List<RecordableDataFrame> dataFrames = new();
    }
    
    
    // a RecordableDataFrame stores all the data that is recorded for a single frame
    // this can be positional data, quaternions, or single float values
    // the order in which the data is saved in the array is important
    [System.Serializable]
    public class RecordableDataFrame
    {
        public int frameNr;

        public float xPosHead;
        public float yPosHead;
        public float zPosHead;
        public float xPosLeftHand;
        public float yPosLeftHand;
        public float zPosLeftHand;
        public float xPosRightHand;
        public float yPosRightHand;
        public float zPosRightHand;
        
        public float xRotHead;
        public float yRotHead;
        public float zRotHead;
        public float wRotHead;
        public float xRotLeftHand;
        public float yRotLeftHand;
        public float zRotLeftHand;
        public float wRotLeftHand;
        public float xRotRightHand;
        public float yRotRightHand;
        public float zRotRightHand;
        public float wRotRightHand;
    }

    [System.Serializable]
    public class ThumbnailData
    {
        public string recordingId; // guid
        
        // ids, prefabNames and firstPoses must be in same order
        public List<string> recordableIds;
        public List<string> prefabNames;
        public List<RecordableDataFrame> firstPoses = new();
    }

    public void Init()
    {
        recordingId = Guid.NewGuid();
        recordableDataDict = new Dictionary<Guid, RecordableData>();
        recordingSavePath = Application.persistentDataPath;
    }
    
    // when a new recording is happening we need to add the recordable data to the dict
    public Guid CreateNewRecordableData()
    {
        var recData = new RecordableData();
        var recId = Guid.NewGuid();
        recordableDataDict.Add(recId, recData);
        return recId;
    }

    public void TryAddEmptyDataFrame(Guid id, int frame)
    {
        var dataFrames = recordableDataDict[id].dataFrames;
        if (frame < dataFrames.Count)
        {
            return;
        }
        
        var dummyFrame = 0;
        while (dataFrames.Count < frame)
        {
            dataFrames.Add(new RecordableDataFrame());
            dataFrames[^1].frameNr = dummyFrame;
            dummyFrame++;
        }

        dataFrames.Add(new RecordableDataFrame());
        dataFrames[^1].frameNr = frame;
    }
    
    public void UpdateMetaData(Guid id, int numFrames, int fps, string prefabName)
    {
        var metaData = recordableDataDict[id];
        metaData.numFrames = numFrames;
        metaData.fps = fps;
        metaData.prefabName = prefabName;
    }
    
    public void ClearRecording()
    {
        recordingId = Guid.Empty;
        recordableDataDict.Clear();
    }
    
    public void AddHeadTransform(Guid id, int frame, Vector3 pos, Quaternion rot)
    {
        recordableDataDict[id].dataFrames[frame].xPosHead = pos.x;
        recordableDataDict[id].dataFrames[frame].yPosHead = pos.y;
        recordableDataDict[id].dataFrames[frame].zPosHead = pos.z;
        
        recordableDataDict[id].dataFrames[frame].xRotHead = rot.x;
        recordableDataDict[id].dataFrames[frame].yRotHead = rot.y;
        recordableDataDict[id].dataFrames[frame].zRotHead = rot.z;
        recordableDataDict[id].dataFrames[frame].wRotHead = rot.w;
    }

    public void AddLeftHandTransform(Guid id, int frame, Vector3 pos, Quaternion rot)
    {
        recordableDataDict[id].dataFrames[frame].xPosLeftHand = pos.x;
        recordableDataDict[id].dataFrames[frame].yPosLeftHand = pos.y;
        recordableDataDict[id].dataFrames[frame].zPosLeftHand = pos.z;
        
        recordableDataDict[id].dataFrames[frame].xRotLeftHand = rot.x;
        recordableDataDict[id].dataFrames[frame].yRotLeftHand = rot.y;
        recordableDataDict[id].dataFrames[frame].zRotLeftHand = rot.z;
        recordableDataDict[id].dataFrames[frame].wRotLeftHand = rot.w;
    }
    
    public void AddRightHandTransform(Guid id, int frame, Vector3 pos, Quaternion rot)
    {
        recordableDataDict[id].dataFrames[frame].xPosRightHand = pos.x;
        recordableDataDict[id].dataFrames[frame].yPosRightHand = pos.y;
        recordableDataDict[id].dataFrames[frame].zPosRightHand = pos.z;
        
        recordableDataDict[id].dataFrames[frame].xRotRightHand = rot.x;
        recordableDataDict[id].dataFrames[frame].yRotRightHand = rot.y;
        recordableDataDict[id].dataFrames[frame].zRotRightHand = rot.z;
        recordableDataDict[id].dataFrames[frame].wRotRightHand = rot.w;
    }
    
    public void AddFloatValue(Guid id, int frame, DataLabel label, float value)
    {
        switch (label)
        {
            case DataLabel.xPosHead:
                recordableDataDict[id].dataFrames[^1].xPosHead = value;
                break;
            case DataLabel.yPosHead:
                recordableDataDict[id].dataFrames[^1].yPosHead = value;
                break;
            case DataLabel.zPosHead:
                recordableDataDict[id].dataFrames[^1].zPosHead = value;
                break;
            case DataLabel.xPosLeftHand:
                recordableDataDict[id].dataFrames[^1].xPosLeftHand = value;
                break;
            case DataLabel.yPosLeftHand:
                recordableDataDict[id].dataFrames[^1].yPosLeftHand = value;
                break;
            case DataLabel.zPosLeftHand:
                recordableDataDict[id].dataFrames[^1].zPosLeftHand = value;
                break;
            case DataLabel.xPosRightHand:
                recordableDataDict[id].dataFrames[^1].xPosRightHand = value;
                break;
            case DataLabel.yPosRightHand:
                recordableDataDict[id].dataFrames[^1].yPosRightHand = value;
                break;
            case DataLabel.zPosRightHand:
                recordableDataDict[id].dataFrames[^1].zPosRightHand = value;
                break;
            case DataLabel.xRotHead:
                recordableDataDict[id].dataFrames[^1].xRotHead = value;
                break;
            case DataLabel.yRotHead:
                recordableDataDict[id].dataFrames[^1].yRotHead = value;
                break;
            case DataLabel.zRotHead:
                recordableDataDict[id].dataFrames[^1].zRotHead = value;
                break;
            case DataLabel.wRotHead:
                recordableDataDict[id].dataFrames[^1].wRotHead = value;
                break;
            case DataLabel.xRotLeftHand:
                recordableDataDict[id].dataFrames[^1].xRotLeftHand = value;
                break;
            case DataLabel.yRotLeftHand:
                recordableDataDict[id].dataFrames[^1].yRotLeftHand = value;
                break;
            case DataLabel.zRotLeftHand:
                recordableDataDict[id].dataFrames[^1].zRotLeftHand = value;
                break;
            case DataLabel.wRotLeftHand:
                recordableDataDict[id].dataFrames[^1].wRotLeftHand = value;
                break;
            case DataLabel.xRotRightHand:
                recordableDataDict[id].dataFrames[^1].xRotRightHand = value;
                break;
            case DataLabel.yRotRightHand:
                recordableDataDict[id].dataFrames[^1].yRotRightHand = value;
                break;
            case DataLabel.zRotRightHand:
                recordableDataDict[id].dataFrames[^1].zRotRightHand = value;
                break;
            case DataLabel.wRotRightHand:
                recordableDataDict[id].dataFrames[^1].wRotRightHand = value;
                break;
        }
    }
    
    public enum DataLabel
    {
        // position
        xPosHead, 
        yPosHead, 
        zPosHead,
        xPosLeftHand,
        yPosLeftHand,
        zPosLeftHand,
        xPosRightHand,
        yPosRightHand, 
        zPosRightHand,
        // rotation
        xRotHead, 
        yRotHead, 
        zRotHead, 
        wRotHead,
        xRotLeftHand,
        yRotLeftHand,
        zRotLeftHand,
        wRotLeftHand,
        xRotRightHand, 
        yRotRightHand, 
        zRotRightHand, 
        wRotRightHand
    };
        
}
