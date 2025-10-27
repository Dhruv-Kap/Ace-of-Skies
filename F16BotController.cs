using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class F16BotController : MonoBehaviour
{
    public enum BotState
    {
        Patrol,
        Engage,
        Evade,
        Return
    }

    public enum EngagementBehavior
    {
        Aggressive,
        Defensive,
        Escort
    }

    [Header("AI Settings")]
    public BotState currentState = BotState.Patrol;
    public EngagementBehavior engagementBehavior = EngagementBehavior.Aggressive;
    [Range(0f, 1f)] public float skillLevel = 0.7f;
    public float reactionDelay = 0.3f;

    [Header("Bounding Box Corners (Drag & Drop)")]
    public Transform corner1; // Bottom-left
    public Transform corner2; // Bottom-right
    public Transform corner3; // Top-right
    public Transform corner4; // Top-left


    [Header("Terrain Avoidance Settings")]
    public float minAltitude = 300f;
    public float terrainCheckDistance = 1500f;
    public LayerMask terrainMask;

    [Header("Detection Settings")]
    public float detectionRange = 3500f;
    public float detectionCheckInterval = 0.5f;
    public LayerMask detectionLayerMask = -1;
    private List<GameObject> potentialTargets = new List<GameObject>();
    private GameObject currentTarget;
    private Rigidbody targetRb;
    private float lastDetectionCheck = 0f;

    [Header("Patrol Settings")]
    public Vector3 patrolAreaCenter = Vector3.zero;
    public Vector2 patrolAreaSize = new Vector2(5000f, 5000f);
    public float patrolAltitudeMin = 500f;
    public float patrolAltitudeMax = 2000f;
    public int waypointCount = 5;
    public bool randomWaypoints = false;
    private List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private float waypointReachedDistance = 100f;

    [Header("Engagement Settings")]
    public float engagementDistanceMin = 200f;
    public float engagementDistanceMax = 1500f;
    public float engagementAltitudeAdvantage = 200f;
    public float leadPredictionTime = 1.5f;
    private Vector3 navTarget;

    [Header("Throttle Settings")]
    public float maxThrottle = 200f;
    public float throttleAdjustRate = 50f;
    public float cruiseSpeed = 930f;
    public float combatSpeed = 1200f;
    private float currentThrottle = 500f;
    private float targetSpeed;

    [Header("Speed & Stall Settings")]
    public float stallSpeedThreshold = 235f;
    public float stallDescentRate = 5f;
    public float maxSpeed = 2125f;
    private bool isStalling = false;

    [Header("Flight Control Settings")]
    public float pitchSpeed = 80f;
    public float rollSpeed = 110f;
    public float turnForce = 25f;
    public float loopLift = 20f;
    public float turnPenaltyFactor = 0.05f;
    public float maxPitchAngle = 85f;
    public float maxRollAngle = 120f;

    [Header("Input Smoothing")]
    public float inputSmoothTime = 0.15f;
    public float inputDeadZone = 0.05f;
    private float pitchInput = 0f;
    private float rollInput = 0f;
    private float throttleInput = 0f;
    private float pitchInputVelocity = 0f;
    private float rollInputVelocity = 0f;
    private float throttleInputVelocity = 0f;

    private Rigidbody rb;

    [Header("Debug Visualization")]
    public bool showDebugInfo = true;
    public bool showPatrolRoute = true;
    public bool showDetectionRange = false;

    private float lastDebugPrint = 0f;
    private const float debugPrintInterval = 5f;

    [Header("Mach Control")]
    [Range(0.5f, 1.5f)] public float machMin = 0.7f;
    [Range(0.5f, 1.5f)] public float machMax = 1.1f;
    [Range(0.5f, 1.5f)] public float machCruise = 0.9f;
    [Range(0.5f, 1.5f)] public float machCombat = 1.0f;
    public float machP = 100f;  // Reduced from 600
    public float machI = 5f;    // Added integral
    public float machD = 20f;   // Reduced from 0
    private float machInt = 0f;
    private float lastMachErr = 0f;
    private const float machIntMax = 0.5f;  // Anti-windup

    [Header("Predictive Terrain Avoidance")]
    public float hardDeckAGL = 150f;
    public float lookaheadTime = 3.0f;
    public float lookaheadTimeMax = 6.0f;
    public float climbCmd = 0.85f;
    public float sidestepCmd = 0.8f;

    // Terrain avoidance state
    private bool isAvoidingTerrain = false;
    private float terrainAvoidancePriority = 0f;

    void Start()
    {
        InitializeBot();
        GeneratePatrolWaypoints();
        StartCoroutine(DetectionRoutine());
        StartCoroutine(StateManagementRoutine());
    }

    void InitializeBot()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1.5f;

        currentThrottle = cruiseSpeed * 0.5f;
        rb.linearVelocity = transform.forward * stallSpeedThreshold * 1.2f;

        targetSpeed = cruiseSpeed;
        navTarget = transform.position + transform.forward * 1000f;

        // Warn if terrain mask not set
        if (terrainMask == 0)
        {
            Debug.LogError($"[F16Bot] {gameObject.name}: Terrain Mask is NOT SET! Bot will crash into ground. Set it in Inspector!");
        }
    }

    void GeneratePatrolWaypoints()
    {
        waypoints.Clear();

        for (int i = 0; i < waypointCount; i++)
        {
            float x = Random.Range(-patrolAreaSize.x / 2, patrolAreaSize.x / 2);
            float z = Random.Range(-patrolAreaSize.y / 2, patrolAreaSize.y / 2);
            float y = Random.Range(patrolAltitudeMin, patrolAltitudeMax);

            Vector3 waypoint = patrolAreaCenter + new Vector3(x, y, z);
            waypoints.Add(waypoint);
        }

        if (waypoints.Count > 0)
        {
            navTarget = waypoints[0];
        }
    }

    void Update()
    {
        // Debug output only
        if (showDebugInfo && Time.time - lastDebugPrint >= debugPrintInterval)
        {
            PrintDebugInfo();
            lastDebugPrint = Time.time;
        }
    }

    void FixedUpdate()
    {
        // All navigation and control in FixedUpdate to prevent wobbling
        UpdateNavigation();
        UpdateMachController();
        ApplyTerrainAvoidance();
        CalculateFlightInputs();
        UpdateFlightControls();

        // Physics
        ApplyThrust();
        ApplyStallBehavior();
        EnforceMaxSpeed();
        ApplyPitch();
        ApplyRoll();

        EnforceMinimumAltitude();
        EnforceBoundingBox();
    }

    IEnumerator DetectionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(detectionCheckInterval);
            DetectTargets();
        }
    }

    IEnumerator StateManagementRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            UpdateBotState();
        }
    }

    void DetectTargets()
    {
        potentialTargets.Clear();

        // Find all potential targets (Players and other bots)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject[] bots = GameObject.FindGameObjectsWithTag("Enemy");

        List<GameObject> allTargets = new List<GameObject>();
        allTargets.AddRange(players);
        allTargets.AddRange(bots);

        foreach (var target in allTargets)
        {
            if (target == gameObject) continue; // Don't target self
            if (target.GetComponent<Rigidbody>() == null) continue;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance <= detectionRange)
            {
                potentialTargets.Add(target);
            }
        }

        // Select best target based on angle-off (least work required)
        SelectBestTarget();
    }

    void SelectBestTarget()
    {
        if (potentialTargets.Count == 0)
        {
            if (currentTarget != null)
            {
                Debug.Log($"[F16Bot] {gameObject.name}: Lost all targets");
            }
            currentTarget = null;
            targetRb = null;
            return;
        }

        GameObject bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (var target in potentialTargets)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            float distance = toTarget.magnitude;

            // Calculate angle off (how much we need to turn)
            float angleOff = Vector3.Angle(transform.forward, toTarget.normalized);

            // Score = weighted combination of angle and distance
            // Lower score = better target (less work to engage)
            float angleWeight = 2.0f; // Prioritize targets we're already pointing at
            float distanceWeight = 0.001f; // Slight preference for closer targets
            float score = (angleOff * angleWeight) + (distance * distanceWeight);

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        if (bestTarget != currentTarget)
        {
            currentTarget = bestTarget;
            targetRb = bestTarget.GetComponent<Rigidbody>();
            Debug.Log($"[F16Bot] {gameObject.name}: New target selected - {bestTarget.name} (angle: {Vector3.Angle(transform.forward, (bestTarget.transform.position - transform.position).normalized):F1}°)");
        }
    }

    void UpdateBotState()
    {
        BotState previousState = currentState;

        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);

            switch (engagementBehavior)
            {
                case EngagementBehavior.Aggressive:
                    currentState = BotState.Engage;
                    targetSpeed = combatSpeed;
                    break;

                case EngagementBehavior.Defensive:
                    if (distance < engagementDistanceMin)
                        currentState = BotState.Evade;
                    else
                        currentState = BotState.Engage;
                    targetSpeed = combatSpeed * 0.9f;
                    break;

                case EngagementBehavior.Escort:
                    currentState = BotState.Engage;
                    targetSpeed = cruiseSpeed;
                    break;
            }
        }
        else
        {
            currentState = BotState.Patrol;
            targetSpeed = cruiseSpeed;
        }

        if (previousState != currentState)
        {
            Debug.Log($"[F16Bot] {gameObject.name}: State changed: {previousState} -> {currentState}");
        }
    }

    void UpdateNavigation()
    {
        switch (currentState)
        {
            case BotState.Patrol:
                UpdatePatrolNavigation();
                break;

            case BotState.Engage:
                UpdateEngagementNavigation();
                break;

            case BotState.Evade:
                UpdateEvasionNavigation();
                break;
        }
    }

    void UpdatePatrolNavigation()
    {
        if (waypoints.Count == 0)
            return;

        float distanceToWaypoint = Vector3.Distance(transform.position, waypoints[currentWaypointIndex]);

        if (distanceToWaypoint < waypointReachedDistance)
        {
            if (randomWaypoints)
                currentWaypointIndex = Random.Range(0, waypoints.Count);
            else
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }

        navTarget = waypoints[currentWaypointIndex];
    }

    void UpdateEngagementNavigation()
    {
        if (currentTarget == null) return;

        // Predict target position
        Vector3 targetVelocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        float predictionTime = leadPredictionTime * skillLevel;
        Vector3 predictedPosition = currentTarget.transform.position + targetVelocity * predictionTime;

        float idealDistance = (engagementDistanceMin + engagementDistanceMax) / 2f;
        Vector3 toTarget = (transform.position - currentTarget.transform.position).normalized;

        switch (engagementBehavior)
        {
            case EngagementBehavior.Aggressive:
                // Get behind target
                Vector3 behindTarget = currentTarget.transform.position - currentTarget.transform.forward * idealDistance;
                behindTarget.y += engagementAltitudeAdvantage;
                navTarget = Vector3.Lerp(predictedPosition, behindTarget, skillLevel * 0.7f);
                break;

            case EngagementBehavior.Defensive:
                // Maintain distance and altitude
                navTarget = currentTarget.transform.position + toTarget * idealDistance;
                navTarget.y += engagementAltitudeAdvantage * 1.5f;
                break;

            case EngagementBehavior.Escort:
                // Stay alongside
                Vector3 escortOffset = currentTarget.transform.right * idealDistance * 0.7f;
                navTarget = currentTarget.transform.position + escortOffset;
                navTarget.y = currentTarget.transform.position.y + 50f;
                break;
        }
    }

    void UpdateEvasionNavigation()
    {
        if (currentTarget == null)
        {
            currentState = BotState.Patrol;
            return;
        }

        Vector3 awayFromTarget = (transform.position - currentTarget.transform.position).normalized;
        Vector3 perpendicular = Vector3.Cross(awayFromTarget, Vector3.up).normalized;

        if (Time.time % 4f < 2f)
            perpendicular *= -1f;

        navTarget = transform.position + (awayFromTarget + perpendicular) * 500f;
        navTarget.y += Random.Range(-200f, 400f);
    }

    float GetSpeedOfSound() => 343.0f;

    float GetMach()
    {
        float vForward = Vector3.Dot(rb.linearVelocity, transform.forward);
        return Mathf.Max(0f, vForward) / Mathf.Max(1e-2f, GetSpeedOfSound());
    }

    void UpdateMachController()
    {
        float desired = (currentState == BotState.Engage || currentState == BotState.Evade) ? machCombat : machCruise;
        desired = Mathf.Clamp(desired, machMin, machMax);

        float mach = GetMach();
        float err = desired - mach;

        // Integral with anti-windup
        machInt += err * Time.fixedDeltaTime;
        machInt = Mathf.Clamp(machInt, -machIntMax, machIntMax);

        float dErr = (err - lastMachErr) / Mathf.Max(Time.fixedDeltaTime, 0.01f);
        lastMachErr = err;

        float accelCmd = machP * err + machI * machInt + machD * dErr;

        // Update target speed based on Mach
        targetSpeed = Mathf.Clamp(desired * GetSpeedOfSound(), stallSpeedThreshold * 1.2f, maxSpeed);

        // Hard limits outside band, smooth control inside
        if (mach < machMin - 0.05f)
            throttleInput = 1f;
        else if (mach > machMax + 0.05f)
            throttleInput = -1f;
        else
            throttleInput = Mathf.Clamp(accelCmd / 500f, -1f, 1f);
    }

    void CalculateFlightInputs()
    {
        // If avoiding terrain, that takes absolute priority
        if (isAvoidingTerrain && terrainAvoidancePriority > 0.5f)
        {
            // Terrain avoidance already set inputs, just smooth them
            return;
        }

        Vector3 toTarget = navTarget - transform.position;
        Vector3 targetDirection = toTarget.normalized;

        // Calculate desired pitch
        float pitchAngle = Vector3.SignedAngle(transform.forward, targetDirection, transform.right);
        float desiredPitch = Mathf.Clamp(pitchAngle / 45f, -1f, 1f);

        // Calculate desired roll for turning
        Vector3 localTarget = transform.InverseTransformPoint(navTarget);
        float rollAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float desiredRoll = Mathf.Clamp(rollAngle / 90f, -1f, 1f);

        // Apply skill level smoothing
        desiredPitch *= (0.5f + skillLevel * 0.5f);
        desiredRoll *= (0.5f + skillLevel * 0.5f);

        // Smooth inputs with SmoothDamp (eliminates wobbling)
        pitchInput = Mathf.SmoothDamp(pitchInput, desiredPitch, ref pitchInputVelocity, inputSmoothTime);
        rollInput = Mathf.SmoothDamp(rollInput, desiredRoll, ref rollInputVelocity, inputSmoothTime);

        // Apply dead zone
        if (Mathf.Abs(pitchInput) < inputDeadZone) pitchInput = 0f;
        if (Mathf.Abs(rollInput) < inputDeadZone) rollInput = 0f;

        // Emergency stall recovery
        float currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (currentSpeed < stallSpeedThreshold * 1.1f)
        {
            pitchInput = Mathf.Min(pitchInput, -0.3f);
            throttleInput = 1f;
        }
    }

    void UpdateFlightControls()
    {
        // Smooth throttle adjustments
        float desiredThrottle = currentThrottle;

        if (throttleInput > 0.1f)
        {
            desiredThrottle += throttleAdjustRate * Time.fixedDeltaTime;
        }
        else if (throttleInput < -0.1f)
        {
            desiredThrottle -= throttleAdjustRate * Time.fixedDeltaTime;
        }

        currentThrottle = Mathf.Clamp(desiredThrottle, 0f, maxThrottle);
    }

    void ApplyThrust()
    {
        rb.AddForce(transform.forward * currentThrottle, ForceMode.Acceleration);

        if (throttleInput < -0.5f)
        {
            Vector3 vel = rb.linearVelocity;
            if (vel.sqrMagnitude > 0.0001f)
            {
                Vector3 brakeDir = -vel.normalized;
                rb.AddForce(brakeDir * 75f, ForceMode.Acceleration);
            }
        }
    }

    void ApplyStallBehavior()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        isStalling = forwardSpeed <= stallSpeedThreshold;

        if (isStalling)
        {
            Vector3 targetDir = Vector3.Lerp(transform.forward, Vector3.down, 0.5f);
            Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 0.5f));

            rb.AddForce(Vector3.down * stallDescentRate, ForceMode.Acceleration);
            rb.AddForce(transform.forward * stallDescentRate, ForceMode.Acceleration);
        }
    }

    void EnforceMaxSpeed()
    {
        float mag = rb.linearVelocity.magnitude;
        if (mag > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    void ApplyPitch()
    {
        if (Mathf.Abs(pitchInput) > 0.01f)
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float speedFactor = Mathf.Clamp01(forwardSpeed / cruiseSpeed);
            float dynamicPitchSpeed = pitchSpeed * (0.5f + speedFactor) * skillLevel;

            Quaternion deltaRot = Quaternion.Euler(pitchInput * dynamicPitchSpeed * Time.fixedDeltaTime, 0f, 0f);
            rb.MoveRotation(rb.rotation * deltaRot);

            if (pitchInput > 0)
            {
                forwardSpeed -= 5f * Time.fixedDeltaTime;
                rb.AddForce(transform.up * loopLift, ForceMode.Acceleration);
            }
            else
            {
                forwardSpeed += 3f * Time.fixedDeltaTime;
            }

            forwardSpeed = Mathf.Max(forwardSpeed, stallSpeedThreshold * 0.8f);
            rb.linearVelocity = transform.forward * forwardSpeed +
                              (rb.linearVelocity - Vector3.Project(rb.linearVelocity, transform.forward));
        }
    }

    void ApplyRoll()
    {
        if (Mathf.Abs(rollInput) > 0.01f)
        {
            float dynamicRollSpeed = rollSpeed * skillLevel;
            Quaternion deltaRot = Quaternion.Euler(0f, 0f, -rollInput * dynamicRollSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * deltaRot);
        }

        float bankAngle = Vector3.SignedAngle(Vector3.up, transform.up, transform.forward);

        if (Mathf.Abs(bankAngle) > 15f && Mathf.Abs(bankAngle) < 165f)
        {
            float turnStrength = Mathf.InverseLerp(15f, 165f, Mathf.Abs(bankAngle));
            float yawDir = -Mathf.Sign(bankAngle);

            float yawSpeed = turnStrength * turnForce * Time.fixedDeltaTime;
            Quaternion yawRot = Quaternion.AngleAxis(yawDir * yawSpeed, Vector3.up);
            rb.MoveRotation(yawRot * rb.rotation);

            rb.AddForce(transform.right * yawDir * turnStrength * 2f, ForceMode.Acceleration);

            float penalty = turnStrength * turnPenaltyFactor;
            rb.linearVelocity *= (1f - penalty * Time.fixedDeltaTime);
        }
        else
        {
            Vector3 currentVelocity = rb.linearVelocity;
            float forwardSpeed = Vector3.Dot(currentVelocity, transform.forward);
            Vector3 targetVelocity = transform.forward * forwardSpeed;
            rb.linearVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * 2f);
        }
    }

    void ApplyTerrainAvoidance()
    {
        isAvoidingTerrain = false;
        terrainAvoidancePriority = 0f;

        if (terrainMask == 0)
        {
            // Fallback: simple altitude check if mask not set
            if (transform.position.y < minAltitude)
            {
                isAvoidingTerrain = true;
                terrainAvoidancePriority = 1f;
                pitchInput = Mathf.Lerp(pitchInput, 1f, Time.fixedDeltaTime * 5f);
                throttleInput = 1f;
            }
            return;
        }

        float currentAltitude = transform.position.y;

        // AGL hard-deck check
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, Mathf.Infinity, terrainMask))
        {
            float groundHeight = groundHit.point.y;
            float agl = currentAltitude - groundHeight;

            if (agl < Mathf.Max(minAltitude, hardDeckAGL))
            {
                isAvoidingTerrain = true;
                terrainAvoidancePriority = 1f;

                // Smooth emergency climb
                float emergencyPitch = Mathf.InverseLerp(hardDeckAGL, hardDeckAGL * 0.5f, agl);
                pitchInput = Mathf.Lerp(pitchInput, emergencyPitch, Time.fixedDeltaTime * 5f);
                throttleInput = 1f;
            }
        }

        // Forward look-ahead with speed scaling
        float speed = rb.linearVelocity.magnitude;
        float lookDist = Mathf.Clamp(speed * lookaheadTime, 500f, Mathf.Min(terrainCheckDistance, speed * lookaheadTimeMax));
        Vector3 origin = transform.position + transform.forward * 5f;

        if (Physics.Raycast(origin, transform.forward, out RaycastHit forwardHit, lookDist, terrainMask))
        {
            isAvoidingTerrain = true;
            float distToObstacle = forwardHit.distance;
            terrainAvoidancePriority = Mathf.InverseLerp(lookDist, lookDist * 0.3f, distToObstacle);

            // Smooth terrain avoidance maneuver
            float climbAmount = climbCmd * terrainAvoidancePriority;
            pitchInput = Mathf.Lerp(pitchInput, climbAmount, Time.fixedDeltaTime * 4f);
            throttleInput = Mathf.Max(throttleInput, 0.8f);

            // Lateral avoidance
            float side = Vector3.Dot(transform.right, forwardHit.normal);
            float desiredRoll = (side > 0f) ? -sidestepCmd : sidestepCmd;
            desiredRoll *= terrainAvoidancePriority;
            rollInput = Mathf.Lerp(rollInput, desiredRoll, Time.fixedDeltaTime * 3f);
        }
    }

    void PrintDebugInfo()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float altitude = transform.position.y;
        string targetStatus = currentTarget != null ?
            $"{currentTarget.name} ({Vector3.Distance(transform.position, currentTarget.transform.position):F0}m)" :
            "No Target";
        float mach = GetMach();
        string terrainStatus = isAvoidingTerrain ? $"AVOIDING (Priority: {terrainAvoidancePriority:F2})" : "Clear";

        Debug.Log($"[F16Bot] {gameObject.name} | State: {currentState} | Speed: {forwardSpeed:F0} | Mach: {mach:F2} | Alt: {altitude:F0} | Throttle: {currentThrottle:F0} | Target: {targetStatus} | Terrain: {terrainStatus}");
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        if (showPatrolRoute && waypoints != null && waypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.DrawWireSphere(waypoints[i], 50f);
                if (i < waypoints.Count - 1)
                    Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
                else if (!randomWaypoints)
                    Gizmos.DrawLine(waypoints[i], waypoints[0]);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(navTarget, 75f);
            Gizmos.DrawLine(transform.position, navTarget);
        }

        if (showDetectionRange)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }

        // State indicator
        Color stateColor = currentState == BotState.Engage ? Color.red :
                          (isAvoidingTerrain ? Color.magenta : Color.green);
        Gizmos.color = stateColor;
        Gizmos.DrawWireSphere(transform.position, 25f);

        // Forward look-ahead
        if (rb != null)
        {
            Gizmos.color = isAvoidingTerrain ? Color.red : Color.magenta;
            float speed = rb.linearVelocity.magnitude;
            float look = Mathf.Clamp(speed * lookaheadTime, 500f, Mathf.Min(terrainCheckDistance, speed * lookaheadTimeMax));
            Gizmos.DrawRay(transform.position + transform.forward * 5f, transform.forward * look);
        }

        // Target line
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }

    void EnforceMinimumAltitude()
    {
        if (transform.position.y < 1400f)
        {
            Vector3 pos = transform.position;
            pos.y = 1400f;
            transform.position = pos;

            // Zero any downward velocity to prevent sinking again
            Vector3 vel = rb.linearVelocity;
            if (vel.y < 0f)
            {
                vel.y = 0f;
                rb.linearVelocity = vel;
            }
        }
    }

    void EnforceBoundingBox()
    {
        if (corner1 == null || corner2 == null || corner3 == null || corner4 == null)
            return;

        float minX = Mathf.Min(corner1.position.x, corner2.position.x, corner3.position.x, corner4.position.x);
        float maxX = Mathf.Max(corner1.position.x, corner2.position.x, corner3.position.x, corner4.position.x);

        float minZ = Mathf.Min(corner1.position.z, corner2.position.z, corner3.position.z, corner4.position.z);
        float maxZ = Mathf.Max(corner1.position.z, corner2.position.z, corner3.position.z, corner4.position.z);

        float minY = Mathf.Min(corner1.position.y, corner2.position.y, corner3.position.y, corner4.position.y);
        float maxY = Mathf.Max(corner1.position.y, corner2.position.y, corner3.position.y, corner4.position.y);

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        transform.position = pos;

        // Optional: slow velocity when hitting edges
        Vector3 vel = rb.linearVelocity;
        if (pos.x <= minX || pos.x >= maxX) vel.x = Mathf.Clamp(vel.x, -10f, 10f);
        if (pos.y <= minY || pos.y >= maxY) vel.y = Mathf.Clamp(vel.y, -10f, 10f);
        if (pos.z <= minZ || pos.z >= maxZ) vel.z = Mathf.Clamp(vel.z, -10f, 10f);
        rb.linearVelocity = vel;
    }


}