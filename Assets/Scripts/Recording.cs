using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Crypto.Tls;
using UnityEngine;

/* this class comprises a recording that is stored in memory for extending and editing it
 */
public class Recording
{
    public Guid recordingId { get; private set; } // guid
    // recordingSavePath + recordingId make up the path to the recording folder
    // public string recordingSavePath { get; private set; } // path to the directory that has all the recording folders
    
    // contains the data of the recording which is the data from all recordables
    public Dictionary<Guid, RecordableData> recordableDataDict { get; private set; }

    public Flags flags { get; set; } = new();

    private RecordableListPool listPool;
    
    public Recording(RecordableListPool listPool)
    {
        this.listPool = listPool;
    }
    
    public class Flags
    {
        public bool SaveReady; // is true when recording is finished and ready to be saved
        public bool DataLoaded; // is true when motion(etc..) data of recording is loaded
        public bool NewDataAvailable; // is true when new data is added to the recording
        public bool IsRecording; // is true when recording is happening

        public void Clear()
        {
            SaveReady = DataLoaded  = NewDataAvailable = false;
        }
        
    }

    public string RecordingFlagsToString()
    {
        return "Flags: \n"
               + "SaveReady: " + flags.SaveReady + "\n"
               + "DataLoaded: " + flags.DataLoaded + "\n"
               + "NewDataAvailable: " + flags.NewDataAvailable + "\n"
               + "IsRecording: " + flags.IsRecording + "\n";
    }

    public override string ToString()
    {
        string s = "";

        s += "Recording: " + recordingId + "\n"
             + "dict size: " + recordableDataDict.Count + "\n";
        foreach (var recordableData in recordableDataDict)
        {
            s += "--------------" + "\n";
            s += "Recordable: " + recordableData.Key + "\n"
                 + "prefabName: " + recordableData.Value.prefabName + "\n"
                 + "numFrames == dataFrames size?: " + recordableData.Value.numFrames + " == " +  recordableData.Value.dataFrames.Count + "\n";
        }
        
        return s;
    }
    
    [System.Serializable]    
    public class RecordableData
    {
        public int numFrames;
        public int fps;
        public string prefabName;
        public List<RecordableDataFrame> dataFrames;
    }
    
    
    // a RecordableDataFrame stores all the data that is recorded for a single frame
    // this can be positional data, quaternions, or single float values
    // the order in which the data is saved in the array is important
    [System.Serializable]
    public struct RecordableDataFrame
    {
        public int frameNr;
        public bool valid; // when adding empty data frames we shouldn't use the data in there bc it is not valid
        
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
    
    // gathers data from recording such as id and information from the recordableDataDict and creates a thumbnail
    public ThumbnailData AssembleThumbnailData()
    {
        var thumbnail = new ThumbnailData
        {
            recordingId = recordingId.ToString(),
            recordableIds = new List<string>(),
            prefabNames = new List<string>(),
            firstPoses = new List<RecordableDataFrame>()
        };

        foreach (var recordableData in recordableDataDict)
        {
            thumbnail.recordableIds.Add(recordableData.Key.ToString());
            thumbnail.prefabNames.Add(recordableData.Value.prefabName);
            // thumbnail.firstPoses.Add(recordableData.Value.dataFrames[0]);
            
            // add first valid pose of each recordable
            for (var i = 0; i < recordableData.Value.dataFrames.Count; i++)
            {
                if (recordableData.Value.dataFrames[i].valid)
                {
                    thumbnail.firstPoses.Add(recordableData.Value.dataFrames[i]);
                    break;
                }
            }
        }

        return thumbnail;
    }

    public void SetRecordingId(Guid id)
    {
        recordingId = id;
    }

    public void SetRecordableDataDict(Dictionary<Guid, RecordableData> dict)
    {
        recordableDataDict = dict;
    }
    
    // creates a new recording with new guid and empty recordableDataDict
    public void InitNew()
    {
        recordingId = Guid.NewGuid();
        recordableDataDict = new Dictionary<Guid, RecordableData>();
        // recordingSavePath = Application.persistentDataPath;
        Debug.Log("Init new recording: " + recordingId);
    }
    
    // when a new recording is happening we need to add the recordable data to the dict
    public Guid CreateNewRecordableData(Guid guid)
    {
        var recData = new RecordableData();
        recData.dataFrames = listPool.GetList();
        Guid recId;
        if (guid == Guid.Empty)
        {
            recId = Guid.NewGuid();
        }
        else
        {
            recId = guid;
        }
        recordableDataDict.Add(recId, recData);
        Debug.Log("Create recordable data: " + recId);
        return recId;
    }
    
    public void UpdateMetaData(Guid id, int numFrames, int fps, string prefabName)
    {
        Debug.Log("Update meta data: " + id + " " + numFrames + " " + fps + " " + prefabName);
        
        var metaData = recordableDataDict[id];
        metaData.numFrames = numFrames;
        metaData.fps = fps;
        metaData.prefabName = prefabName;
    }
    
    public void Clear()
    {
        listPool.Clear();
        recordingId = Guid.Empty;
        recordableDataDict.Clear();
        flags.Clear();
    }
    
    /*
     * Adds empty data frames until frame (excluded) is reached
     */
    public void TryAddEmptyDataFrames(Guid id, int frame)
    {
        var dataFrames = recordableDataDict[id].dataFrames;
        if (frame < dataFrames.Count)
        {
            return;
        }
        var dummyFrame = 0;
        while (dataFrames.Count < frame)
        {
            dataFrames.Add(new RecordableDataFrame(){frameNr = dummyFrame});
            dummyFrame++;
        }
        Debug.Log("Recording: TryAddEmptyDataFrames(): " + recordableDataDict[id].dataFrames.Count);
    }
    
    public void AddDataFrame(Guid id, int frame, Recordable.RecordablePose pose)
    {
        flags.NewDataAvailable = true;
        var df = new RecordableDataFrame()
        {
            frameNr = frame,
            valid = true,
            xPosHead = pose.head.position.x,
            yPosHead = pose.head.position.y,
            zPosHead = pose.head.position.z,
            xRotHead = pose.head.rotation.x,
            yRotHead = pose.head.rotation.y,
            zRotHead = pose.head.rotation.z,
            wRotHead = pose.head.rotation.w,
            xPosLeftHand = pose.leftHand.position.x,
            yPosLeftHand = pose.leftHand.position.y,
            zPosLeftHand = pose.leftHand.position.z,
            xRotLeftHand = pose.leftHand.rotation.x,
            yRotLeftHand = pose.leftHand.rotation.y,
            zRotLeftHand = pose.leftHand.rotation.z,
            wRotLeftHand = pose.leftHand.rotation.w,
            xPosRightHand = pose.rightHand.position.x,
            yPosRightHand = pose.rightHand.position.y,
            zPosRightHand = pose.rightHand.position.z,
            xRotRightHand = pose.rightHand.rotation.x,
            yRotRightHand = pose.rightHand.rotation.y,
            zRotRightHand = pose.rightHand.rotation.z,
            wRotRightHand = pose.rightHand.rotation.w
        };

        if (recordableDataDict[id].dataFrames.Count <= frame)
        {
            recordableDataDict[id].dataFrames.Add(df);
        }
        else
        {
            recordableDataDict[id].dataFrames[frame] = df;
        }
    }

    public void RemoveDataFrames(Guid id, int frame, int count)
    {
        flags.NewDataAvailable = true;
        // make sure we don't remove more frames than we have!!!
        recordableDataDict[id].dataFrames.RemoveRange(frame, count);
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
