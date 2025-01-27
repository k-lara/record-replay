using System;
using System.Collections.Generic;
using Ubiq.Spawning;
using UnityEngine;

public class UndoManager
{

   private int maxUndos;
   private RecordableListPool pool;
   private NetworkSpawnManager spawnManager;
   private Dictionary<string, GameObject> prefabCatalogue;

   // The last element in the undoStack is always the current state of the recording.
   // This means the undoStack also functions as a redo stack where we can go back to the most current recording state.
   private List<RecordableState> undoStack;
   private int undoIndex;

   // the change that has happened after a recording is done
   public enum UndoType
   {
      None,
      New, // the change is a new recordable (undoing it means to "delete" this recordable)
      Edit // the change is an edit to an existing recordable (undoing it means to revert the edit but not delete the recordable)
   }
   
   private class RecordableState
   {
      public UndoType type;
      public Guid id;
      public int numFrames;
      public int fps;
      public string prefabName;
      public List<Recording.RecordableDataFrame> dataFrames;

      public override string ToString()
      {
         return "RecordableState: \n" +
                "type: " + type + "\n" +
                "id: " + id + "\n" +
                "numFrames: " + numFrames + "\n" +
                "prefabName: " + prefabName + "\n";
      }
   }
   
   public UndoManager(NetworkSpawnManager spawnManager, Dictionary<string, GameObject> prefabCatalogue, RecordableListPool pool, int maxUndos)
   {
      this.spawnManager = spawnManager;
      this.prefabCatalogue = prefabCatalogue;
      this.pool = pool;
      this.maxUndos = maxUndos;
      undoStack = new List<RecordableState>(this.maxUndos);
   }

   public (UndoType, List<Guid>) Undo(Recording recording, List<GameObject> spawnedObjects)
   {
      List<Guid> undoIds = null;
      var type = UndoType.None;
      if (undoIndex > 0)
      {
         // check what undo type we need to perform
         type = undoStack[undoIndex].type;
         switch (type)
         {
            case UndoType.New: // this was a new recordable, undoing it means to remove the recordable
               // this means that we need to unspawn the prefab and remove it from existing lists
               undoIds = UndoNew(recording, spawnedObjects);
               break;
            
            case UndoType.Edit: // this was an existing recordable, only undo the changes
               // this means that we need to go back to the previous state and load it into the recordable
               undoIds = Edit(recording, spawnedObjects, -1);
                
               break;
         }
         undoIndex--;
      }

      return (type, undoIds);
   }

   private List<Guid> UndoNew(Recording recording, List<GameObject> spawnedObjects)
   { 
      List<Guid> removedIds = new List<Guid>(1); // maybe in the future we might undo more than one...
      // the last spawned object should be the one we need to unspawn
      var state = undoStack[undoIndex];
      Debug.Log("UndoNew: undoIndex: " + undoIndex + " state: " + state);
      removedIds.Add(state.id);
      // as we undo the recorded avatar we return list from recording to the pool
      pool.ReturnList(recording.recordableDataDict[state.id].dataFrames);
      recording.recordableDataDict.Remove(state.id);
      
      spawnManager.Despawn(spawnedObjects[^1]);
      spawnedObjects.RemoveAt(spawnedObjects.Count - 1);
      return removedIds;
   }
   
   private List<Replayable> RedoNew(Recording recording, List<GameObject> spawnedObjects)
   {
      List<Replayable> newReplayables = new List<Replayable>(3);
      var state = undoStack[undoIndex + 1];
      Debug.Log("RedoNew: undoIndex: " + (undoIndex + 1) + " state: " + state);
      
      // add new recordable data to recording
      recording.CreateNewRecordableData(state.id);
      recording.UpdateMetaData(state.id, state.numFrames, state.fps, state.prefabName);
      recording.recordableDataDict[state.id].dataFrames.AddRange(state.dataFrames);
      
      // spawn new object
      var go = spawnManager.SpawnWithPeerScope(prefabCatalogue[state.prefabName]);
      var replayable = go.AddComponent<Replayable>();
      replayable.replayableId = state.id;
      replayable.SetReplayablePose(state.dataFrames.Count - 1);
      replayable.SetIsLocal(true);
      
      spawnedObjects.Add(go);
      newReplayables.Add(replayable);
      return newReplayables;
   }
   
   // TODO later: instead of copying the whole thing we could get the 
   // frame range in which a change happened and only copy that...
   // this will save time, but because nothing is implemented yet that would allow this
   // we don't do it yet!
   private List<Guid> Edit(Recording recording, List<GameObject> spawnedObjects, int prevNext)
   {
      List<Guid> undoIds = new List<Guid>(1); // maybe in the future we might undo more than one...
      // need to go to previous state
      // if previous state is not available, because the recording might have been loaded from file
      // and the first change was an edit, then we have to undo the edit and need the previous state of the recordable
      Debug.Log("Edit: undoIndex: " + undoIndex + " prevNext: " + prevNext);
      var state = undoStack[undoIndex + prevNext]; // +1 or -1
      var recData = recording.recordableDataDict[state.id];
      recData.numFrames = state.numFrames;
      recData.fps = state.fps;
      recData.prefabName = state.prefabName;
      
      Debug.Log("Edit: recdata before" + recData.dataFrames.Count + " state (to be): " + state.dataFrames.Count);
      
      for (int i = 0; i < state.dataFrames.Count; i++)
      {
         if (i >= recData.dataFrames.Count)
         {
            // if redo: we need more frames than we have so we add them
            recData.dataFrames.Add(state.dataFrames[i]);
         }
         else
         {
            // undo: we have more frames than we need so we replace them
            recData.dataFrames[i] = state.dataFrames[i];
         }
      }
      if (prevNext == -1) // only remove extra frames when we are undoing an edit not when redoing
      {
         // remove list entries beyond the new numFrames
         recData.dataFrames.RemoveRange(state.numFrames, recData.dataFrames.Count - state.numFrames);
      }
      Debug.Log("Edit: recdata after removal/insertion: " + recording.recordableDataDict[state.id].dataFrames.Count);
      
      undoIds.Add(state.id);
      return undoIds;
   }

   public (UndoType, List<Replayable>) Redo(Recording recording, List<GameObject> spawnedObjects)
   {
      List<Replayable> redoReplayables = null;
      var type = UndoType.None;
      if (undoIndex < undoStack.Count - 1)
      {
         type = undoStack[undoIndex + 1].type;
         switch (undoStack[undoIndex + 1].type)
         {            
            case UndoType.New:
               redoReplayables = RedoNew(recording, spawnedObjects);
               break;
            case UndoType.Edit:
               Edit(recording, spawnedObjects, 1);
               break;
         }
         undoIndex++;
      }

      return (type, redoReplayables);
   }

   /*
    * When a recording is cleared/unloaded we also need to clear the undo stack and free all lists.
    */
   public void Clear()
   {
      for (int i = 0; i < undoStack.Count; i++)
      {
         // pool.ReturnList(undoStack[i].dataFrames); // not required as we clear the pool anyway and this only adds a new list otherwise
         undoStack.RemoveAt(i);
      }
   }
   
   
   public void InitUndoStack(UndoType type, Guid id, Recording.RecordableData data)
   {
      if (undoStack.Count == 0)
      {
         var state = new RecordableState
         {
            type = type,
            id = id
         };
         if (data != null)
         {
            state.numFrames = data.numFrames;
            state.fps = data.fps;
            state.prefabName = data.prefabName;
            state.dataFrames = pool.GetList();
            state.dataFrames.AddRange(data.dataFrames);
         }
         
         undoStack.Add(state);
         undoIndex = undoStack.Count - 1;
         Debug.Log("InitUndoStack: undoIndex: " + undoIndex + " " + state);
      }
   }

   /*
    * Adds a new undo state to the undo stack.
    * The undo state is a copy of the recordable data that is to be undone.
    * The data is copied to a new list from the pool.
    * The undo state is added to the undo stack and the undo index is set to the last element.
    * The undo index is used to keep track of the current undo state.
    */
   public void AddUndoState(UndoType type, Guid id, Recording.RecordableData data)
   {
      var state = new RecordableState
      {
         type = type,
         id = id,
         numFrames = data.numFrames,
         fps = data.fps,
         prefabName = data.prefabName,
         dataFrames = pool.GetList() // get list from pool
      };
      state.dataFrames.AddRange(data.dataFrames); // copy data over
      // here we need to check what our current undo index is
      // it is possible we create a new undo state from an older state.
      // so we need to get rid of all newer states and make this one the most recent one.
      UpdateStateStack();
      
      undoStack.Add(state);
      undoIndex = undoStack.Count - 1;
      Debug.Log("AddUndoState: undoIndex: " + undoIndex + " " + state);
   }
   
   /*
    * Updates the state stack when a new recording is done.
    * Makes sure that the last recording is the most recent on the state stack (being on the last index).
    */
   private void UpdateStateStack()
   {
      // make sure the current state is the newest and remove older states that have a higher index
      if (undoIndex < undoStack.Count - 1)
      {
         var end = undoStack.Count - 1;
         for (int i = end; i > undoIndex; i--)
         {
            Debug.Log("UpdateStateStack: i: " + i + "undoIndex: " + undoIndex + " " + undoStack[i].id);
            pool.ReturnList(undoStack[i].dataFrames);
            undoStack.RemoveAt(i);
         }
      }
      if (undoStack.Count == maxUndos)
      {
         pool.ReturnList(undoStack[0].dataFrames);
         undoStack.RemoveAt(0);
      }
   }
}
