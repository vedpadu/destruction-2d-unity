using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using DigitalRuby.AdvancedPolygonCollider;
using Unity.Mathematics;
using System.Linq;
using Mirror;

public class DestructableSyncer : NetworkBehaviour
{
    public List<GameObject> syncList;
    private List<SpriteRenderer> syncListSRs = new List<SpriteRenderer>();
    private List<Rigidbody2D> syncListRBs = new List<Rigidbody2D>();
    private List<destructableClientSyncer> syncListScripts = new List<destructableClientSyncer>();
    private int consideredWindowSize = 50;
    public int numberSyncedPerTick = 20;
    private int currentIndex = 0;
    public float[] priorities;
    private Rigidbody2D[] currentWindow;
    private SpriteRenderer[] currentWindowSRs;
    public Color highPrioColor;
    public Color lowPrioColor;
    bool initialized = false;
    public float refreshRateHz;
    private float timeStart;
    private int numberSynced = 0;
    private float sampleInterval;
    private float nextSampleTime;

    private float clientStartTime;
    private List<float> serverAssociatedTime = new List<float>();

    bool updateDestructables = false;
    bool clientReady = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized && isServer)
        {
            syncList = new List<GameObject>(GameObject.FindGameObjectsWithTag("syncDestructable"));
            if (syncList.Count > 0)
            {
                priorities = new float[consideredWindowSize];
                currentWindow = new Rigidbody2D[consideredWindowSize];
                currentWindowSRs = new SpriteRenderer[consideredWindowSize];
                foreach (GameObject obj in syncList)
                {
                    try
                    {
                        syncListRBs.Add(obj.GetComponent<Rigidbody2D>());
                        syncListSRs.Add(obj.GetComponent<SpriteRenderer>());
                        syncListScripts.Add(obj.GetComponent<destructableClientSyncer>());
                        
                    }
                    catch(Exception e)
                    {
                        print(obj);
                    }
                }
                initialized = true;
                int totalObjects = syncList.Count;
                float fixedUpdateRefreshRate = 1.0f / Time.fixedDeltaTime;
                float objectsPerTick = ((float)refreshRateHz / (float)fixedUpdateRefreshRate) * totalObjects;
                consideredWindowSize = (int)objectsPerTick;
                print("Window Size: " + consideredWindowSize);
                timeStart = Time.realtimeSinceStartup;
                sampleInterval = 1f / refreshRateHz;
                nextSampleTime = Time.time; // start right away

            }
        }
        else if(!initialized && isClient)
        {
            syncList = new List<GameObject>(GameObject.FindGameObjectsWithTag("syncDestructable"));
            if (syncList.Count > 0)
            {
                foreach (GameObject obj in syncList)
                {
                    try
                    {
                        syncListScripts.Add(obj.GetComponent<destructableClientSyncer>());
                    }
                    catch (Exception e)
                    {
                        print(obj);
                    }
                }
                initialized = true;
                sampleInterval = 1f / refreshRateHz;
                nextSampleTime = Time.time; // start right away
                timeStart = Time.realtimeSinceStartup;
                
            }
        }
        else if(initialized && isClientOnly)
        {
            //int intervalCount = 0;
            int badCount = 0;
            if (updateDestructables)
            {
                numberSynced += 1;
                //foreach (destructableClientSyncer s in syncListScripts)
                //{
                //    s.updatePosition();
                //    if (s.inBadState)
                //    {
                //        badCount++;
                //    }
                //}
                //nextSampleTime += sampleInterval;
                //intervalCount++;
                updateDestructables = false;
                print(badCount);
                print(numberSynced / (Time.realtimeSinceStartup - timeStart));
            }
        }
    }

    void FixedUpdate()
    {
        if (!initialized)
            return;
        if (isServer)
        {
            //int intervalCount = 0;
            if (currentIndex == 0)
            {
                float timePacket = Time.realtimeSinceStartup;
                foreach (destructableClientSyncer s in syncListScripts)
                {
                    s.savePosition(timePacket);
                }
                getStartTime(timePacket);
                //print(numberSynced / (Time.realtimeSinceStartup - timeStart));
                //nextSampleTime += sampleInterval;
                //intervalCount++;
            }
            foreach (SpriteRenderer s in syncListSRs)
            {
                s.color = lowPrioColor;
            }
            //int windSize = consideredWindowSize;
            //if (currentIndex + windSize > syncList.Count)
            //{
            //    windSize = syncList.Count - currentIndex;
            //}
            //syncListRBs.GetRange(currentIndex, windSize).CopyTo(currentWindow, 0);
            //syncListSRs.GetRange(currentIndex, windSize).CopyTo(currentWindowSRs, 0);
            //for (int i = 0; i < currentWindow.Length; i++)
            //{
            //    if (i < windSize)
            //    {
            //        priorities[i] = Mathf.Abs(currentWindow[i].linearVelocity.magnitude);
            //    }
            //    else
            //    {
            //        priorities[i] = 0.0f;
            //    }
            //}
            //int[] indices = Enumerable.Range(0, priorities.Length).ToArray();

            //// Sort indices based on keys
            //Array.Sort(indices, (i, j) => priorities[i].CompareTo(priorities[j]));

            //// Apply permutation
            //priorities = indices.Select(i => priorities[i]).ToArray();
            //currentWindowSRs = indices.Select(i => currentWindowSRs[i]).ToArray();
            //currentWindow = indices.Select(i => currentWindow[i]).ToArray();

            int windSize = consideredWindowSize;
            if (currentIndex + windSize >= syncList.Count)
            {
                windSize = syncList.Count - currentIndex;
            }

            bool doTimeLog = false;
            if (currentIndex == 0)
            {
                doTimeLog = true;
            }
            for (int j = 0; j < windSize; j++)
            {
                
                int ind = currentIndex + j;
                if (ind >= syncList.Count)
                {
                    ind = ind - syncList.Count;
                }
                GameObject toSync = syncList[ind].gameObject;
                
                //if (j == 0)
                //    doprint = true;
                
                if (Mathf.Abs(syncListScripts[ind].lastServerVelocity.magnitude) > 0.01f)
                {
                    syncListSRs[ind].color = highPrioColor;
                    syncObjectIntervalled(toSync, syncListScripts[ind].lastServerPosition, syncListScripts[ind].lastServerRotation, syncListScripts[ind].lastServerVelocity, syncListScripts[ind].lastServerTime, doTimeLog);
                    doTimeLog = false;
                }
                //syncObject(toSync, (Vector2)toSync.transform.position, toSync.transform.rotation.eulerAngles.z, syncListRBs[ind].linearVelocity, syncListRBs[ind].angularVelocity);

                //GameObject toSync = syncList[ind].gameObject;
                //currentWindowSRs[ind].color = highPrioColor;
                //syncObject(toSync, (Vector2)toSync.transform.position, toSync.transform.rotation.eulerAngles.z, currentWindow[ind].linearVelocity, currentWindow[ind].angularVelocity);
            }
            //print(numberSynced / (Time.realtimeSinceStartup - timeStart));
            currentIndex += consideredWindowSize;
            if (currentIndex >= syncList.Count)
            {
                currentIndex = 0;
                setFinished();
            }
        }
        
    }

    [ClientRpc]
    void setFinished()
    {
        updateDestructables = true;
    }

    [ClientRpc]
    void getStartTime(float serverTime)
    {
        //clientStartTime.Add(Time.realtimeSinceStartup);
        //serverAssociatedTime.Add(serverTime);
        //if (clientStartTime.Count > 4)
        //{
        //    clientStartTime.RemoveAt(0);
        //    serverAssociatedTime.RemoveAt(0);
        //}
        ////print("Start Time Done");
        //clientReady = true;
    }

    [ClientRpc]
    void syncObjectIntervalled(GameObject obj, Vector2 pos, float rot, Vector2 vel, float t, bool doTimeLog)
    {
        
        if (isClientOnly)
        {
            if (doTimeLog)
            {
                clientStartTime = Time.realtimeSinceStartup;
                clientReady = true;
                //print("syncStarted");
            }
            if (clientReady)
            {
                float ping = (float)NetworkTime.rtt;
                destructableClientSyncer objScript;
                if (obj.TryGetComponent(out objScript))
                {
                    if (objScript.lastEvents.Count == 0 && objScript.initSync)
                    {
                        objScript.startSyncTime = t;
                        //syncTimer = Time.realtimeSinceStartup;
                        //print("starting thing");
                    }
                    if (objScript.lastEvents.Count == 1 && objScript.initSync)
                    {
                        
                        objScript.syncTimer = clientStartTime;
                        objScript.initSync = false;


                    }
                    objScript.lastEvents.Add(new destructableClientSyncer.SyncEvent(pos, rot, vel, t));
                    //objScript.lastServerPosition = pos;
                    //objScript.lastServerRotation = rot;
                    //objScript.lastServerVelocity = vel;
                    //objScript.lastServerTime = t;
                    //Rigidbody2D objRB = obj.GetComponent<Rigidbody2D>();
                    //objScript.ApplyNetworkState(pos, orientation, velocity, angVel);
                    //StartCoroutine(objScript.syncToTarget(pos, orientation, velocity));
                }
            }
            
        }
    }

    [ClientRpc]
    void syncObject(GameObject obj, Vector2 pos, float orientation, Vector2 velocity, float angVel)
    {
        if (isClientOnly)
        {
            destructableClientSyncer objScript;
            if (obj.TryGetComponent(out objScript))
            {
                //Rigidbody2D objRB = obj.GetComponent<Rigidbody2D>();
                objScript.ApplyNetworkState(pos, orientation, velocity, angVel);
                //StartCoroutine(objScript.syncToTarget(pos, orientation, velocity));
            }
            
            //objRB.position = pos;
            //objRB.rotation = orientation;
            //objRB.linearVelocity = velocity;
            //objScript.targetPosition = pos;
            //objScript.targetRotation = orientation;
            //objScript.targetVelocity = velocity;
            //objScript.targetAngularVelocity = angVel;
            //objScript.updateReceived = true;
        }
        
    }
}
