using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class destructableClientSyncer : NetworkBehaviour
{
    public struct SyncEvent
    {
        public Vector2 pos;
        public float rot;
        public Vector2 vel;
        public float time;

        public SyncEvent(Vector2 p, float r, Vector2 v, float t)
        {
            pos = p;
            rot = r;
            vel = v;
            time = t;
        }
    }

    public Vector2 targetPosition;
    public float targetRotation;
    public Vector2 targetVelocity;
    public float targetAngularVelocity;
    private Rigidbody2D rb;

    //[Range(0f, 1f)] public float positionLerpRate = 0.2f;
    //[Range(0f, 1f)] public float rotationLerpRate = 0.2f;

    public float syncTime = 0.05f;
    public float syncDistanceEpsilon = 0.02f;
    public float maxVelocity = 20f;     // optional cap to avoid explosive impulses

    private Coroutine currentCoroutine;
    public bool inBadState = false;
    //[Range(0f, 1f)] public float positionLerpFactor = 0.2f;   // how fast to correct towards target
    //[Range(0f, 1f)] public float rotationLerpFactor = 0.2f;
    //[Range(0f, 1f)] public float velocityLerpFactor = 0.1f;   // gentler on velocity
    //[Range(0f, 1f)] public float angularVelLerpFactor = 0.1f;
    public bool updateReceived = false;


    public float alpha = 0.2f;      // LPF smoothing factor (0–1)

    private Vector2 prevVel;
    private Vector2 filteredAccel;
    private bool hasPrev = false;

    [HideInInspector]
    public Vector2 lastServerPosition;
    [HideInInspector]
    public float lastServerRotation;
    [HideInInspector]
    public Vector2 lastServerVelocity;
    [HideInInspector]
    public float lastServerTime;

    //public List<SyncEvent> lastEventsPublic = new List<SyncEvent>();
    public List<SyncEvent> lastEvents = new List<SyncEvent>();
    public float startSyncTime;
    public float syncTimer;
    public bool initSync = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        targetPosition = rb.position;
        targetVelocity = rb.linearVelocity;
        targetRotation = rb.rotation;
        if (isClientOnly)
        {
            rb.isKinematic = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void savePosition(float t)
    {
        lastServerPosition = rb.position;
        lastServerRotation = rb.rotation;
        lastServerVelocity = rb.linearVelocity;
        lastServerTime = t;
    }

    public void updatePosition()
    {
        //lastEvents = new List<SyncEvent>(lastEventsPublic);
        //if (currentCoroutine == null)
        //{
        //    currentCoroutine = StartCoroutine(syncToTarget(lastServerPosition, lastServerRotation, lastServerVelocity, rb.angularVelocity));
        //}
        //if (lastEvents.Count == 0 && initSync)
        //{
        //    startSyncTime = lastServerTime;
        //    //syncTimer = Time.realtimeSinceStartup;
        //    //print("starting thing");
        //}
        //if (lastEvents.Count == 1 && initSync)
        //{
        //    syncTimer = Time.realtimeSinceStartup;
        //    initSync = false;
        //}
        //lastEvents.Add(new SyncEvent(lastServerPosition, lastServerRotation, lastServerVelocity, lastServerTime));
        //if (lastEvents.Count == 1)
        //{
        //    print(lastEvents[0].time - startSyncTime);
        //}
        //rb.position = lastServerPosition;
        //rb.rotation = lastServerRotation;
        //rb.linearVelocity = lastServerVelocity;
        //lastServerPosition = rb.position;
        //lastServerRotation = rb.rotation;
    }

    void FixedUpdate()
    {
        if (isClientOnly)
        {
            
            if (lastEvents.Count >= 2)
            {
                float ping = (float)NetworkTime.rtt;
                float currentClientTime = Time.realtimeSinceStartup - syncTimer;
                int currentEventIndex = lastEvents.Count - 1;
                for (int i = 0; i < lastEvents.Count - 1; i++)
                {
                    float eventTime = lastEvents[i].time - startSyncTime;
                    float nextEventTime = lastEvents[i + 1].time - startSyncTime;
                    if (currentClientTime >= eventTime && currentClientTime <= nextEventTime)
                    {
                        currentEventIndex = i;
                        break;
                    }
                }
                
                if (currentEventIndex != lastEvents.Count - 1)
                {
                    if (currentEventIndex > 0)
                    {
                        for (int i = 0; i < currentEventIndex; i++)
                        {
                            lastEvents.RemoveAt(0);
                        }
                    }
                    float t = (currentClientTime - (lastEvents[0].time - startSyncTime)) / (lastEvents[1].time - lastEvents[0].time);
                    rb.position = Vector2.Lerp(lastEvents[0].pos, lastEvents[1].pos, t);
                    //rb.position = lastEvents[1].pos;
                    //rb.rotation = lastEvents[1].rot; 
                    rb.rotation = Mathf.LerpAngle(lastEvents[0].rot, lastEvents[1].rot, t);
                    inBadState = false;
                }
                else
                {
                    inBadState = true;
                    //print("BAD!!!");
                    //print(lastEvents[0].time - startSyncTime);
                    //print(lastEvents[1].time - startSyncTime);
                    //print(syncTimer);

                    //print(lastEvents[lastEvents.Count - 1].time - startSyncTime);
                    //print(lastEvents[lastEvents.Count - 2].time - startSyncTime);
                    //print(currentClientTime);
                    //print(syncTimer);
                }
                

            }
            // get current velocity from Rigidbody2D
            Vector2 vel = rb.linearVelocity;

            if (hasPrev)
            {
                // finite difference acceleration
                Vector2 rawAccel = (vel - prevVel) / Time.fixedDeltaTime;

                // exponential smoothing (low-pass filter)
                filteredAccel = alpha * rawAccel + (1f - alpha) * filteredAccel;
            }
            else
            {
                // initialize first frame
                filteredAccel = Vector2.zero;
                hasPrev = true;
            }

            prevVel = vel;
            // Position correction  adjust velocity instead of teleporting
            //Vector2 posError = targetPosition - rb.position;
            //Vector2 correctionVel = posError / Time.fixedDeltaTime;

            //rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, targetVelocity + correctionVel, velocityLerpFactor);

            //// Rotation correction  also via angular velocity
            //float rotError = Mathf.DeltaAngle(rb.rotation, targetRotation);
            //float correctionAngVel = rotError / Time.fixedDeltaTime;

            //rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, targetAngularVelocity + correctionAngVel, angularVelLerpFactor);
            //rb.position = targetPosition;
            //rb.rotation = targetRotation;
            //rb.linearVelocity = targetVelocity;
            //rb.angularVelocity = targetAngularVelocity;
            //rb.MovePosition(Vector2.Lerp(rb.position, targetPosition, positionLerpRate));

            ////// Smoothly rotate towards the target rotation
            //rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetRotation, rotationLerpRate));
            //rb.linearVelocity = targetVelocity;
        }
    }

    public void ApplyNetworkState(Vector2 targetPos, float targetRot, Vector2 targetVelocity, float targetAngularVel)
    {
        // Stop any ongoing correction
        if (currentCoroutine == null)
        {
            currentCoroutine = StartCoroutine(CorrectRigidbody(targetPos, targetRot, targetVelocity, targetAngularVel));
        }
            //StopCoroutine(currentCoroutine);

           
    }

    private IEnumerator CorrectRigidbody(Vector2 targetPos, float targetRot, Vector2 targetVelocity, float targetAngularVel)
    {
        float timer = 0f;
        Vector2 startPos = rb.position;
        float startRot = rb.rotation;
        float ping = (float)NetworkTime.rtt;
        Vector2 initVel = rb.linearVelocity;
        float initAngularVel = rb.angularVelocity;
        //float ping = ((float)rtt / 1000.0f);
        //pos = pos + vel * (syncTime + ping);
        //syncTime = ping;
        float t_forward = ping;
        targetPos = targetPos + (targetVelocity * (t_forward));
        //targetPos = targetPos + (targetVelocity * (t_forward) + 0.5f * filteredAccel * (t_forward * t_forward));
        //targetVelocity = targetVelocity + filteredAccel * t_forward;
        // Given x0, v0, a0, xf, vf, af, T, and time t in [0,T]
        //Vector2 a0 = filteredAccel;
        //Vector2 af = filteredAccel;
        //Vector2 v0 = rb.linearVelocity;
        //Vector2 S1 = targetPos - (startPos + rb.linearVelocity * syncTime + 0.5f * a0 * syncTime * syncTime);
        //Vector2 S2 = targetVelocity - (rb.linearVelocity + a0 * syncTime);
        //Vector2 S3 = af - a0;

        //Vector2 c0 = startPos;
        //Vector2 c1 = rb.linearVelocity;
        //Vector2 c2 = 0.5f * a0;
        //Vector2 c3 = 10f * S1 / (syncTime * syncTime * syncTime) - 4f * S2 / (syncTime * syncTime) + 0.5f * S3 / syncTime;
        //Vector2 c4 = -15f * S1 / (syncTime * syncTime * syncTime * syncTime) + 7f * S2 / (syncTime * syncTime * syncTime) - S3 / (syncTime * syncTime);
        //Vector2 c5 = 6f * S1 / (syncTime * syncTime * syncTime * syncTime * syncTime) - 3f * S2 / (syncTime * syncTime * syncTime * syncTime) + 0.5f * S3 / (syncTime * syncTime * syncTime);

        // position / velocity / acceleration at time t
        //Vector2 x = c0 + c1 * t + c2 * t * t + c3 * Math.Pow(t, 3) + c4 * Math.Pow(t, 4) + c5 * Math.Pow(t, 5);
        //double v = c1 + 2 * c2 * t + 3 * c3 * t * t + 4 * c4 * Math.Pow(t, 3) + 5 * c5 * Math.Pow(t, 4);

        // THIS IS A WHOLE LOAD OF BULLWSHIT from chat it doesn't work..
        if ((targetPos - startPos).magnitude > syncDistanceEpsilon)
        {
            //while (timer < syncTime)
            //{
            //    timer += Time.fixedDeltaTime;
            //    //rb.position = Vector2.
            //    //rb.MovePosition(Vector2.Lerp(startPos, targetPos, Mathf.Min(timer / syncTime, 1.0f)));
            //    //rb.linearVelocity = Vector2.Lerp(initVel,targetVelocity, Mathf.Min(timer / syncTime, 1.0f));
            //    //rb.MoveRotation(Mathf.LerpAngle(startRot, targetRot, Mathf.Min(timer / syncTime, 1.0f)));
            //    //rb.angularVelocity = Mathf.Lerp(initAngularVel, targetAngularVel, Mathf.Min(timer / syncTime, 1.0f));
            //    rb.MovePosition(Vector2.Lerp(startPos, targetPos, 0.2f));
            //    rb.linearVelocity = Vector2.Lerp(initVel, targetVelocity, 0.2f);
            //    rb.MoveRotation(Mathf.LerpAngle(startRot, targetRot, 0.2f));
            //    rb.angularVelocity = Mathf.Lerp(initAngularVel, targetAngularVel, 0.2f);
            //    //Vector2 a = 2f * c2 + 6f * c3 * timer + 12f * c4 * timer * timer + 20f * c5 * Mathf.Pow(timer, 3f);
            //    //Vector2 v = c1 + 2f * c2 * timer + 3f * c3 * timer * timer + 4f * c4 * Mathf.Pow(timer, 3f) + 5f * c5 * Mathf.Pow(timer, 4f);
            //    ////float t = Mathf.Clamp01(timer / syncTime);

            //    //// --- Velocity correction toward target position ---


            //    //rb.linearVelocity = v;
            //    //print("Actual vel: " + v + " Init Vel: " + v0 + " Final Vel: " + targetVelocity);


            //    // --- Angular velocity correction ---
            //    //float rotDiff = Mathf.DeltaAngle(rb.rotation, targetRot);
            //    //rb.angularVelocity = rotDiff / Mathf.Max(Time.fixedDeltaTime, 0.0001f) + targetAngularVel;

            //    yield return new WaitForFixedUpdate();
            //}

            // Optional: snap exactly at the end
            rb.position = targetPos;
            rb.rotation = targetRot;
            //rb.linearVelocity = targetVelocity;
            //rb.angularVelocity = targetAngularVel;
            currentCoroutine = null;
            yield return null;
        }
        currentCoroutine = null;
    }

    // DOESNT WORK WITH POLYGON COLLIDERS
    public IEnumerator syncToTarget(Vector2 pos, float rot, Vector2 vel, float ang_vel)
    {
        if ((pos - rb.position).magnitude > syncDistanceEpsilon)
        {
            float timer = 0f;
            Vector2 origPos = rb.position;
            float origRot = rb.rotation;
            Vector2 origVel = rb.linearVelocity;
            float ping = (float)NetworkTime.rtt;
            //float ping = ((float)rtt / 1000.0f);
            //pos = pos + vel * (syncTime);
            //rb.linearVelocity = vel;
            //while (timer <= syncTime)
            //{

            //    timer += Time.deltaTime; // Increment timer by the time passed since last frame
            //                             //rb.position = Vector2.lerp(origPos, pos, rot);
            //    rb.MovePosition(Vector2.Lerp(origPos, pos, Mathf.Min(timer / syncTime, 1.0f)));
            //    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, vel, Mathf.Min(timer / syncTime, 1.0f));
            //    rb.MoveRotation(Mathf.LerpAngle(origRot, targetRotation, Mathf.Min(timer / syncTime, 1.0f)));
            //    rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, ang_vel, Mathf.Min(timer / syncTime, 1.0f));
            //    yield return null; // Pause execution and resume in the next frame
            //}
            rb.position = pos;
            rb.rotation = rot;
            rb.linearVelocity = vel;
            rb.angularVelocity = ang_vel;
            currentCoroutine = null;
            yield return null;
        }
       

    }


}
