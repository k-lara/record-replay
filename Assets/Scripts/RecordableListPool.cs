using System;
using System.Collections.Generic;
using UnityEngine;

/*
    Pool of lists that have a predefined capacity and from which we take the lists to use in the recording.
    When we save aside a list to the undo manager we take another list from that pool and copy the data across.
    It should be reasonably fast because our list elements are structs and should be in order in memory.
    When we unload a recording we give the lists back to that pool!
    This way all the lists are handled in one place, even the ones from the  undo manager, and we always can easily compute how much memory we are already using!

    size of recordable data frame (motion only):
    1 int (4 bytes) + 21 floats (4 bytes each) = 88 bytes / frame
    10 fps = 880 bytes / second
    52,800 bytes / minute
    264,000 bytes / 5 minutes
    
    10 fps
    frames per minute: 10 * 60 = 600
    2 minutes: 1200 frames
    5 minutes: 3000 frames
    */

public class RecordableListPool
{
    private int poolCapacity; // number of lists in the pool
    private int listCapacity; // number of elements in each list

    private Queue<List<Recording.RecordableDataFrame>> freeLists;
    private List<List<Recording.RecordableDataFrame>> listPool;

    public RecordableListPool(int poolCapacity, int listCapacity)
    {
       this.poolCapacity = poolCapacity;
       this.listCapacity = listCapacity;
       
       listPool = new List<List<Recording.RecordableDataFrame>>(poolCapacity);
       freeLists = new Queue<List<Recording.RecordableDataFrame>>(poolCapacity);
       
       // fill pool with lists
       for (int i = 0; i < poolCapacity; i++)
       {
           var l = new List<Recording.RecordableDataFrame>(listCapacity);
           listPool.Add(l);
           freeLists.Enqueue(l);
       }
    }

    public List<Recording.RecordableDataFrame> GetList()
    {
        if (freeLists.Count > 0)
        {
            return freeLists.Dequeue();
        }
        // if we run out of lists, we create a new one and return it
        // we also add it to the list pool
        var newList = new List<Recording.RecordableDataFrame>(listCapacity);
        listPool.Add(newList);
        Debug.Log("Add new list to pool: # lists: " + listPool.Count);
        return newList;
    }
    
    // when a list is not used anymore we return it to the pool
    public void ReturnList(List<Recording.RecordableDataFrame> list)
    {
        list.Clear();
        freeLists.Enqueue(list);
        Debug.Log("Return list to pool: free lists: " + freeLists.Count);
    }
    
    // clears the lists in the pool, but not the pool itself!
    public void Clear()
    {
        freeLists.Clear();
        foreach (var list in listPool)
        {
            list.Clear();
            freeLists.Enqueue(list);
        }
        Debug.Log("Clear list pool: free lists: " + freeLists.Count);
    }
    // clears the list pool and fills it with new lists with initial capacity
    public void ResetPool()
    {
        freeLists.Clear();
        listPool.Clear();
        
        for (int i = 0; i < poolCapacity; i++)
        {
            var l = new List<Recording.RecordableDataFrame>(listCapacity);
            listPool.Add(l);
            freeLists.Enqueue(l);
        }
    }
}
