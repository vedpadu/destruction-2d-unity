using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Mirror;

public class PrototypeHeroDemo : NetworkBehaviour {

    [Header("Variables")]
    [SerializeField] float m_maxSpeed = 4.5f;
    [SerializeField] float m_jumpForce = 7.5f;
    [SerializeField] float m_slowDownRatio = 0.2f;
    [SerializeField] bool m_hideSword = false;
    [Header("Effects")]
    [SerializeField] GameObject m_RunStopDust;
    [SerializeField] GameObject m_JumpDust;
    [SerializeField] GameObject m_LandingDust;
    [SerializeField] GameObject gunObject;
    [SerializeField] GameObject gunRotationPoint;
    private SpriteRenderer sR;
    private float gun_dist;


    private Animator m_animator;
    private Rigidbody2D m_body2d;
    private Sensor_Prototype m_groundSensor;
    private AudioSource m_audioSource;
    private AudioManager_PrototypeHero m_audioManager;
    private bool m_grounded = false;
    private bool m_moving = false;
    private int m_facingDirection = 1;
    private float m_disableMovementTimer = 0.0f;

    public GameObject objectToMove;
    public GameObject cam;

    // Use this for initialization
    void Start()
    {
        if (isLocalPlayer)
        {
            cam.SetActive(true);
        }
        objectToMove = GameObject.Find("arch (84)");
        sR = gunObject.GetComponent<SpriteRenderer>();
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_audioSource = GetComponent<AudioSource>();
        m_audioManager = AudioManager_PrototypeHero.instance;
        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_Prototype>();
        gun_dist = (gunObject.transform.position - gunRotationPoint.transform.position).magnitude;
    }

    // Update is called once per frame
    void Update()
    {
        //if (NetworkClient.active && NetworkClient.connection != null)
        //{
        //    var stats = NetworkClient.connection.transport.GetStats();
        //    Debug.Log($"Client Sent: {stats.bytesSent} bytes, Received: {stats.bytesReceived} bytes");
        //}

        //if (NetworkServer.active)
        //{
        //    foreach (var conn in NetworkServer.connections.Values)
        //    {
        //        var stats = conn.transport.GetStats();
        //        Debug.Log($"Conn {conn.connectionId} → Sent {stats.bytesSent}, Recv {stats.bytesReceived}");
        //    }
        //}
        //Transport transport = Transport.activeTransport;

        //if (transport is TelepathyTransport telepathy)
        //{
        //    Debug.Log($"Telepathy: Sent {telepathy.Client.GetTotalBytesOut()} bytes, Recv {telepathy.Client.GetTotalBytesIn()} bytes");
        //}
        //else if (transport is KcpTransport kcp)
        //{
        //    Debug.Log($"KCP: Sent {kcp.BytesOut}, Recv {kcp.BytesIn}, RTT {kcp.GetAverageRTT()} ms");
        //}
        if (!isLocalPlayer)
            return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            objectToMove.transform.position = new Vector3(objectToMove.transform.position.x - 1.0f, objectToMove.transform.position.y, objectToMove.transform.position.z);
        }
        var mouse_pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var diff = mouse_pos - gunRotationPoint.transform.position;
        diff = new Vector3(diff.x, diff.y, -5.0f);
        var angle = Mathf.Atan2(diff.y, diff.x);
        var pos_gun = diff.normalized * gun_dist;
        gunObject.transform.localPosition = gunRotationPoint.transform.localPosition + pos_gun;
        if (diff.x < 0)
        {
            sR.flipY = true;
        }
        else
        {
            sR.flipY = false;
        }


        gunObject.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);


        

        // -- Handle input and movement --
        float inputX = 0.0f;

        inputX = Input.GetAxis("Horizontal");

        // GetAxisRaw returns either -1, 0 or 1
        float inputRaw = Input.GetAxisRaw("Horizontal");

        bool jumpPressed = Input.GetButtonDown("Jump");

        // Decrease timer that disables input movement. Used when attacking
        m_disableMovementTimer -= Time.deltaTime;

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        if (Mathf.Abs(inputRaw) > Mathf.Epsilon && Mathf.Sign(inputRaw) == m_facingDirection)
            m_moving = true;

        else
            m_moving = false;

        // Swap direction of sprite depending on move direction
        if (inputRaw > 0)
        {
            GetComponent<SpriteRenderer>().flipX = false;
            m_facingDirection = 1;
        }

        else if (inputRaw < 0)
        {
            GetComponent<SpriteRenderer>().flipX = true;
            m_facingDirection = -1;
        }

        // SlowDownSpeed helps decelerate the characters when stopping
        float SlowDownSpeed = m_moving ? 1.0f : m_slowDownRatio;
        // Set movement
        m_body2d.linearVelocity = new Vector2(inputX * m_maxSpeed * SlowDownSpeed, m_body2d.linearVelocity.y);

        // Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);

        // Set Animation layer for hiding sword
        int boolInt = m_hideSword ? 1 : 0;
        m_animator.SetLayerWeight(1, boolInt);

        // -- Handle Animations --
        //Jump
        if (jumpPressed && m_grounded)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }

        //Run
        else if (m_moving)
            m_animator.SetInteger("AnimState", 1);

        //Idle
        else
            m_animator.SetInteger("AnimState", 0);
        MovePlayer(inputRaw, jumpPressed, inputX);

        // Check if current move input is larger than 0 and the move direction is equal to the characters facing direction
        //if (Mathf.Abs(inputRaw) > Mathf.Epsilon && Mathf.Sign(inputRaw) == m_facingDirection)
        //    m_moving = true;

        //else
        //    m_moving = false;

        //// Swap direction of sprite depending on move direction
        //if (inputRaw > 0)
        //{
        //    GetComponent<SpriteRenderer>().flipX = false;
        //    m_facingDirection = 1;
        //}

        //else if (inputRaw < 0)
        //{
        //    GetComponent<SpriteRenderer>().flipX = true;
        //    m_facingDirection = -1;
        //}

        //// SlowDownSpeed helps decelerate the characters when stopping
        //float SlowDownSpeed = m_moving ? 1.0f : m_slowDownRatio;
        //// Set movement
        //m_body2d.linearVelocity = new Vector2(inputX * m_maxSpeed * SlowDownSpeed, m_body2d.linearVelocity.y);

        //// Set AirSpeed in animator
        //m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);

        //// Set Animation layer for hiding sword
        //int boolInt = m_hideSword ? 1 : 0;
        //m_animator.SetLayerWeight(1, boolInt);

        //// -- Handle Animations --
        ////Jump
        //if (Input.GetButtonDown("Jump") && m_grounded && m_disableMovementTimer < 0.0f)
        //{
        //    m_animator.SetTrigger("Jump");
        //    m_grounded = false;
        //    m_animator.SetBool("Grounded", m_grounded);
        //    m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
        //    m_groundSensor.Disable(0.2f);
        //}

        ////Run
        //else if (m_moving)
        //    m_animator.SetInteger("AnimState", 1);

        ////Idle
        //else
        //    m_animator.SetInteger("AnimState", 0);
    }

    [Command]
    public void setBreakableObjectClientFinished(breakableObject b)
    {
        b.numFinishedClients += 1;
    }

    [Command]
    void MovePlayer(float inputRaw, bool jumpPressed, float inputX)
    {
        // Decrease timer that disables input movement. Used when attacking
        m_disableMovementTimer -= Time.deltaTime;

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        if (Mathf.Abs(inputRaw) > Mathf.Epsilon && Mathf.Sign(inputRaw) == m_facingDirection)
            m_moving = true;

        else
            m_moving = false;

        // Swap direction of sprite depending on move direction
        if (inputRaw > 0)
        {
            GetComponent<SpriteRenderer>().flipX = false;
            m_facingDirection = 1;
        }

        else if (inputRaw < 0)
        {
            GetComponent<SpriteRenderer>().flipX = true;
            m_facingDirection = -1;
        }

        // SlowDownSpeed helps decelerate the characters when stopping
        float SlowDownSpeed = m_moving ? 1.0f : m_slowDownRatio;
        // Set movement
        m_body2d.linearVelocity = new Vector2(inputX * m_maxSpeed * SlowDownSpeed, m_body2d.linearVelocity.y);

        // Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);

        // Set Animation layer for hiding sword
        int boolInt = m_hideSword ? 1 : 0;
        m_animator.SetLayerWeight(1, boolInt);

        // -- Handle Animations --
        //Jump
        if (jumpPressed && m_grounded)
        {
            m_animator.SetTrigger("Jump");
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
            m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
            m_groundSensor.Disable(0.2f);
        }

        //Run
        else if (m_moving)
            m_animator.SetInteger("AnimState", 1);

        //Idle
        else
            m_animator.SetInteger("AnimState", 0);
    }

    [ClientRpc]
    void sendVelocity(Vector2 vel)
    {
        if (!isLocalPlayer)
        {
            if (m_body2d == null)
            {
                m_body2d = GetComponent<Rigidbody2D>();
            }
            m_body2d.linearVelocity = vel;
        }
        
    }

    // Function used to spawn a dust effect
    // All dust effects spawns on the floor
    // dustXoffset controls how far from the player the effects spawns.
    // Default dustXoffset is zero
    void SpawnDustEffect(GameObject dust, float dustXOffset = 0)
    {
        if (dust != null)
        {
            // Set dust spawn position
            Vector3 dustSpawnPosition = transform.position + new Vector3(dustXOffset * m_facingDirection, 0.0f, 0.0f);
            GameObject newDust = Instantiate(dust, dustSpawnPosition, Quaternion.identity) as GameObject;
            // Turn dust in correct X direction
            newDust.transform.localScale = newDust.transform.localScale.x * new Vector3(m_facingDirection, 1, 1);
        }
    }

    // Animation Events
    // These functions are called inside the animation files
    void AE_runStop()
    {
        return;
        m_audioManager.PlaySound("RunStop");
        // Spawn Dust
        float dustXOffset = 0.6f;
        SpawnDustEffect(m_RunStopDust, dustXOffset);
    }

    void AE_footstep()
    {
        return;
        m_audioManager.PlaySound("Footstep");
    }

    void AE_Jump()
    {
        return;
        m_audioManager.PlaySound("Jump");
        // Spawn Dust
        SpawnDustEffect(m_JumpDust);
    }

    void AE_Landing()
    {
        return;
        m_audioManager.PlaySound("Landing");
        // Spawn Dust
        SpawnDustEffect(m_LandingDust);
    }
}
