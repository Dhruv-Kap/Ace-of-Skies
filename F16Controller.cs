using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class F16Controller : MonoBehaviour
{
    [Header("Throttle Settings")]
    public float maxThrottle = 200f;
    public float throttleIncreaseRate = 100f;
    public float airBrakeForce = 75f;
    public float cruiseSpeed = 930f;
    public float cruiseAdjustSpeed = 75f;

    [Header("Speed & Stall Settings")]
    public float stallSpeedThreshold = 235f;
    public float stallDescentRate = 5f;
    public float maxSpeed = 2125f;

    [HideInInspector] public float currentThrottle = 500f;

    private Rigidbody rb;

    // Print interval
    float lastPrintTime = 0f;
    const float printInterval = 5f;

    [Header("Pitch Settings")]
    public float pitchSpeed = 80f;   // base pitch rate
    public float loopLift = 20f;     // extra lift force during pitch

    [Header("Roll Settings")]
    public float rollSpeed = 110f;    // degrees per second
    public float turnForce = 25f;    // lateral acceleration when banked
    public float turnPenaltyFactor = 0.05f; // how much speed penalty per degree of bank

    float pitchInput = 0f;
    float rollInput = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1.5f;

        // Start with some throttle and speed to prevent immediate stall
        currentThrottle = cruiseSpeed * 0.5f;
        rb.linearVelocity = transform.forward * stallSpeedThreshold * 1.2f;
    }

    void Update()
    {
        HandleThrottle(Time.deltaTime);
        HandlePitchInput();
        HandleRollInput();

        // Print stats every 5s
        if (Time.time - lastPrintTime >= printInterval)
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float altitude = transform.position.y;

            //Debug.Log($"Speed: {forwardSpeed:F1} | Altitude: {altitude:F1} | Throttle: {currentThrottle:F1}");
            lastPrintTime = Time.time;
        }
    }

    void FixedUpdate()
    {
        ApplyThrust();
        ApplyStallBehavior();
        EnforceMaxSpeed();
        ApplyPitch();
        ApplyRoll();
    }


    // ----------------------
    // Throttle Control
    // ----------------------
    void HandleThrottle(float dt)
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentThrottle = Mathf.Clamp(currentThrottle + throttleIncreaseRate * dt, 0f, maxThrottle);
        }
        else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            currentThrottle = Mathf.Clamp(currentThrottle - airBrakeForce * dt, 0f, maxThrottle);
        }
        else
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (forwardSpeed < cruiseSpeed)
                currentThrottle = Mathf.Clamp(currentThrottle + cruiseAdjustSpeed * dt, 0f, maxThrottle);
            else if (forwardSpeed > cruiseSpeed)
                currentThrottle = Mathf.Clamp(currentThrottle - cruiseAdjustSpeed * dt, 0f, maxThrottle);
        }
    }

    // ----------------------
    // Flight Physics
    // ----------------------
    void ApplyThrust()
    {
        rb.AddForce(transform.forward * currentThrottle, ForceMode.Acceleration);

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        bool isStalling = forwardSpeed <= stallSpeedThreshold;

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !isStalling)
        {
            Vector3 vel = rb.linearVelocity;
            if (vel.sqrMagnitude > 0.0001f)
            {
                Vector3 brakeDir = -vel.normalized;
                rb.AddForce(brakeDir * airBrakeForce * 1.5f, ForceMode.Acceleration);
            }
        }
    }

    void ApplyStallBehavior()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (forwardSpeed <= stallSpeedThreshold)
        {
            Vector3 targetDir = Vector3.Lerp(transform.forward, Vector3.down, 0.5f);
            Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);

            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 0.5f));

            rb.AddForce(Vector3.down * stallDescentRate, ForceMode.Acceleration);
            rb.AddForce(transform.forward * stallDescentRate, ForceMode.Acceleration);

            Debug.LogWarning($"STALL: Diving gently... Speed={forwardSpeed:F1}");
        }
    }

    void EnforceMaxSpeed()
    {
        float mag = rb.linearVelocity.magnitude;
        if (mag > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    void HandlePitchInput()
    {
        pitchInput = (Input.GetKey(KeyCode.W) ? 1f : 0f) +
                     (Input.GetKey(KeyCode.S) ? -1f : 0f);
    }

    void ApplyPitch()
    {
        if (Mathf.Abs(pitchInput) > 0.01f)
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            float speedFactor = Mathf.Clamp01(forwardSpeed / cruiseSpeed);
            float dynamicPitchSpeed = pitchSpeed * (0.5f + speedFactor);

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                dynamicPitchSpeed *= 0.7f;
                forwardSpeed += 5f * Time.fixedDeltaTime;
            }
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                dynamicPitchSpeed *= 1.3f;
                forwardSpeed -= 10f * Time.fixedDeltaTime;
            }
            else
            {
                forwardSpeed -= 5f * Time.fixedDeltaTime;
            }

            forwardSpeed = Mathf.Max(forwardSpeed, stallSpeedThreshold * 0.8f);

            Quaternion deltaRot = Quaternion.Euler(pitchInput * dynamicPitchSpeed * Time.fixedDeltaTime, 0f, 0f);
            rb.MoveRotation(rb.rotation * deltaRot);

            rb.linearVelocity = transform.forward * forwardSpeed;
            rb.AddForce(transform.up * loopLift, ForceMode.Acceleration);
        }
    }

    // ----------------------
    // Roll Control
    // ----------------------
    void HandleRollInput()
    {
        rollInput = (Input.GetKey(KeyCode.A) ? -1f : 0f) +
                    (Input.GetKey(KeyCode.D) ? 1f : 0f);
    }

    void ApplyRoll()
    {
        if (Mathf.Abs(rollInput) > 0.01f)
        {
            // Rotate around local Z for rolling
            Quaternion deltaRot = Quaternion.Euler(0f, 0f, -rollInput * rollSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * deltaRot);
        }

        // --- Auto Turn (yaw when banked) ---
        float bankAngle = Vector3.SignedAngle(Vector3.up, transform.up, transform.forward);

        if (Mathf.Abs(bankAngle) > 15f && Mathf.Abs(bankAngle) < 165f)
        {
            float turnStrength = Mathf.InverseLerp(15f, 165f, Mathf.Abs(bankAngle));

            // Yaw direction (depends on bank)
            float yawDir = -Mathf.Sign(bankAngle);

            // Rotate nose into the turn (yaw around world Y)
            float yawSpeed = turnStrength * turnForce * Time.fixedDeltaTime;
            Quaternion yawRot = Quaternion.AngleAxis(yawDir * yawSpeed, Vector3.up);
            rb.MoveRotation(yawRot * rb.rotation);

            // Optional: add small sideways force for realism
            rb.AddForce(transform.right * yawDir * turnStrength * 2f, ForceMode.Acceleration);

            // Apply drag penalty
            float penalty = turnStrength * turnPenaltyFactor;
            rb.linearVelocity *= (1f - penalty * Time.fixedDeltaTime);
        }
        else
        {
            // When wings are level (not banking), straighten flight path
            Vector3 currentVelocity = rb.linearVelocity;
            float forwardSpeed = Vector3.Dot(currentVelocity, transform.forward);

            // Gradually align velocity with forward direction when wings are level
            Vector3 targetVelocity = transform.forward * forwardSpeed;
            rb.linearVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * 2f);
        }
    }
}