using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class CarControl : Agent
{
    [Header("Truck")]
    public float enginePower = 2000.0f;
    public float turnSpeed = 25.0f;
    public float turnSmoothness = 5.0f;

    public Transform[] wheels;
    public Transform[] wheelMeshes;
    public Transform centerOfMass;
    public GameObject steeringWheel;

    [Header("Goal")]
    public GameObject Meetingpoint;

    [SerializeField] private float requiredStopTime = 2f;
    [SerializeField] private float stopSpeedThreshold = 5f;
    [Header("Randomization")]
    [SerializeField] private float meetingPointRandomRange = 20f;

private Vector3 meetingPointStartPosition;

    private float timeInsideMeetingpoint = 0f;
    private bool isInsideMeetingPoint = false;

    [SerializeField] private float maxEpisodeTime = 40f;
    private float episodeTimer = 0f;


    [Header("Rewards")]
    public float goalReward = 10f;
    public float wallPenalty = -1f;
    public float timePenalty = -0.001f;

    private Rigidbody rb;
    private float currentTurnAngle = 0.0f;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private float previousDistance;


protected override void Awake()
{
    base.Awake();

    startingPosition = transform.position;
    startingRotation = transform.rotation;
    meetingPointStartPosition = Meetingpoint.transform.position;
}


    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null && centerOfMass != null)
        {
            rb.centerOfMass = centerOfMass.localPosition;
        }

        Debug.Log("CleanerController Initialize()");
    }

    public override void OnEpisodeBegin()
{
    ResetTruck();

    Meetingpoint.transform.position =
    meetingPointStartPosition +
    new Vector3(
        Random.Range(
            -meetingPointRandomRange,
             meetingPointRandomRange),
        0f,
        Random.Range(
            -meetingPointRandomRange,
             meetingPointRandomRange)
    );

    previousDistance =
        Vector3.Distance(
            transform.position,
            Meetingpoint.transform.position
        );

    timeInsideMeetingpoint = 0f;
    episodeTimer = 0f;
    isInsideMeetingPoint = false;

    transform.rotation =
    Quaternion.Euler(
        0f,
        Random.Range(0f, 0f),
        0f
    );
}


    public void ResetTruck()
    {
        transform.SetPositionAndRotation(
            startingPosition,
            startingRotation
        );

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentTurnAngle = 0f;

        // Reset wheel physics
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i].GetComponent<WheelCollider>();

            if (wheel != null)
            {
                wheel.motorTorque = 0f;
                wheel.steerAngle = 0f;
            }
        }
    }

    private void FixedUpdate()
{

    float currentDistance = Vector3.Distance(
    transform.position,
    Meetingpoint.transform.position
);

    episodeTimer += Time.fixedDeltaTime;
    
    if (episodeTimer >= maxEpisodeTime)
    {
        AddReward(-0.5f);
    
        Debug.Log(
            $"Truck timed out after {maxEpisodeTime} seconds"
        );
    
        EndEpisode();
        return;
    }

    float improvement =
        previousDistance - currentDistance;

    AddReward(improvement * 0.01f);

    previousDistance = currentDistance;

        // Small time penalty
    AddReward(timePenalty * Time.fixedDeltaTime);

    // Check if truck is inside MeetingPoint
    Collider meetingCollider = Meetingpoint.GetComponent<Collider>();

    Vector3 dirToGoal =
    (Meetingpoint.transform.position -
     transform.position).normalized;

    float alignment =
        Vector3.Dot(transform.forward, dirToGoal);

    AddReward(alignment * 0.001f);


    bool currentlyInside =
    meetingCollider != null &&
    meetingCollider.bounds.Contains(transform.position);


    if (currentlyInside)
    {
        // Small reward for being inside goal area
        AddReward(0.01f * Time.fixedDeltaTime);

        float speed = rb.linearVelocity.magnitude;

        if (speed <= stopSpeedThreshold)
        {
            timeInsideMeetingpoint += Time.fixedDeltaTime;

            // Reward being stationary
            AddReward(0.1f);

            if (timeInsideMeetingpoint >= requiredStopTime)
            {
                AddReward(goalReward);

                Debug.Log(
                    "Truck successfully stopped at MeetingPoint!"
                );

                EndEpisode();
                return;
            }
        }
        else
        {
            timeInsideMeetingpoint = 0f;
        }
    }
    else
    {
        timeInsideMeetingpoint = 0f;
    }
}


    public override void OnActionReceived(ActionBuffers actions)
    {
        int movementAction = actions.DiscreteActions[0];
        int rotationAction = actions.DiscreteActions[1];

        float throttle = 0f;
        float horizontalInput = 0f;

        // Movement
        if (movementAction == 1)
        {
            throttle = 1f;       // Forward
        }
        else if (movementAction == 2)
        {
            throttle = -1f;      // Reverse
        }

        // Steering
        if (rotationAction == 1)
        {
            horizontalInput = -1f; // Left
        }
        else if (rotationAction == 2)
        {
            horizontalInput = 1f;  // Right
        }

        DriveTruck(throttle, horizontalInput);
    }

    private void DriveTruck(float throttle, float horizontalInput)
    {
        // Calculate steering angle
        float targetTurnAngle = horizontalInput * turnSpeed;

        currentTurnAngle = Mathf.Lerp(
            currentTurnAngle,
            targetTurnAngle,
            Time.fixedDeltaTime * turnSmoothness
        );

        // Steering wheel visual
        if (steeringWheel != null)
        {
            steeringWheel.transform.localEulerAngles =
                new Vector3(
                    -64,
                    0,
                    currentTurnAngle * 3
                );
        }

        // Control wheels
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheelCollider =
                wheels[i].GetComponent<WheelCollider>();

            if (wheelCollider == null)
                continue;

            // First two wheels steer
            if (i < 2)
            {
                wheelCollider.steerAngle = currentTurnAngle;
            }
            else
            {
                wheelCollider.steerAngle = 0f;
            }

            // All wheels receive motor torque
            wheelCollider.motorTorque =
                throttle * enginePower;

            // Update visual wheel meshes
            if (i < wheelMeshes.Length &&
                wheelMeshes[i] != null)
            {
                Vector3 wheelPosition;
                Quaternion wheelRotation;

                wheelCollider.GetWorldPose(
                    out wheelPosition,
                    out wheelRotation
                );

                wheelMeshes[i].position = wheelPosition;
                wheelMeshes[i].rotation = wheelRotation;
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        // Default = nothing
        discreteActions[0] = 0;
        discreteActions[1] = 0;

        if (Keyboard.current == null)
            return;

        // Movement
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActions[0] = 1; // Forward
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActions[0] = 2; // Reverse
        }

        // Steering
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActions[1] = 1; // Left
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActions[1] = 2; // Right
        }
    }

private void OnTriggerEnter(Collider other)
{
    if (other.gameObject.CompareTag("Meetingpoint"))
    {
        Debug.Log("Truck entered MeetingPoint.");
    }

    if (other.CompareTag("OutOfBounds"))
    {
        AddReward(-1f);
        Debug.Log("Truck went out of bounds!");
        EndEpisode();
    }
}
private void OnTriggerExit(Collider other)
{
    if (other.gameObject.CompareTag("Meetingpoint"))
    {
        AddReward(-0.2f);

        timeInsideMeetingpoint = 0f;

        Debug.Log("Truck left MeetingPoint.");
    }
}



    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(wallPenalty);

            Debug.Log("Truck hit wall!");

            EndEpisode();
        }
    }
    public override void CollectObservations(VectorSensor sensor)
{
    // Direction and distance to the goal
    Vector3 directionToGoal =
        Meetingpoint.transform.position - transform.position;

    Vector3 localDirection =
        transform.InverseTransformDirection(directionToGoal);

    sensor.AddObservation(localDirection.normalized);

    // Distance to goal
    sensor.AddObservation(directionToGoal.magnitude);

    // Truck velocity in local coordinates
    Vector3 localVelocity =
        transform.InverseTransformDirection(rb.linearVelocity);

    sensor.AddObservation(localVelocity);

    // Current steering angle
    sensor.AddObservation(currentTurnAngle / turnSpeed);
}

}
