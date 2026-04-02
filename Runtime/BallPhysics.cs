using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Data structure for aerodynamic coefficients lookup
/// </summary>
[System.Serializable]
public class BallAerodynamics
{
    public float ballSpeed;      // m/s
    public float spinRate;       // rpm
    public float launchAngle;    // degrees
    public float magnusCoeff;      // dimensionless
    public float dragCoeff;      // dimensionless
    public float spinDecayRate;

    public BallAerodynamics(float speed, float spin, float angle, float cl, float cd, float sd)
    {
        ballSpeed = speed;
        spinRate = spin;
        launchAngle = angle;
        magnusCoeff = cl;
        dragCoeff = cd;
        spinDecayRate = sd;
    }
}

/// <summary>
/// Simulates a golf shot in Unity physics using launch monitor data
/// </summary>
public class BallPhysics : MonoBehaviour
{
    // How much the air density impacts the drag on the golf ball
    private float dragCoefficient = 0.28f;
    // How much the Magnus effect is applied (https://en.wikipedia.org/wiki/Magnus_effect)
    private float magnusCoefficient = 0.00002f;

    // When the ball is considered "stopped"
    private float stopThreshold = 0.25f;

    // As of now we just apply simple spin and velocity damping as a starting point for standard fairway/range grass,
    // but we'll need to build on it for other surface types (green, rough, sand, etc.)
    private float grassDeceleration = 2.0f;

    // Initial airDensity (configurable based on user preference or course elevation)
    private float airDensity = 1.225f; // kg/m^3
    private float airDensityMin = 1.225f; // sea-level
    private float airDensityMax = 1f; // -20% for highest elevation
    
    // Initial grip / friction 
    private float gripStrength = 0.01f;    
    
    // Average radius of a golf ball in meters
    float ballRadius = 0.021335f;
    // Average mass of a golf ball (~45.93 grams)
    float ballMass = 0.04593f;

    // Fixed coefficients for this flight
    private float initialMagnusCoeff = 0.00015f;
    private float initialDragCoeff = 0.25f;
    private float initialSpinDecayRate = 0.987f;
    private float bounceVelocityThreshold = 7.0f;
    private float endShotThresholdAngular = 6.0f;

    private Rigidbody rb;
    // Aerodynamics lookup table
    public List<BallAerodynamics> aeroTable = new List<BallAerodynamics>();
    public bool isPutt = false;
    public bool isGrounded = false;
    public bool isLanded = false;
    public bool isEnded = false;
    public Action shotEndedCallback;

    

    void Awake()
    {
      rb = gameObject.GetComponent<Rigidbody>();
      rb.mass = ballMass; 
      rb.linearDamping = 0.00f;
      rb.angularDamping = 0.00f;
      rb.maxAngularVelocity = 1300f;
      rb.interpolation = RigidbodyInterpolation.Interpolate;
      rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
      // Set a lower maxDepenetrationVelocity on the Rigidbody (e.g., between 2 and 10 m/s) to prevent the "pop-up" effect when it hits a seam
      rb.maxDepenetrationVelocity = 10;
      // rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

      
      rb.useGravity = true;
      rb.freezeRotation = false;

      // I wasn't able to find a single set of lift/drag coefficient values that worked for
      // all shots, so we use a lookup table of values modeled from real-world ball flight data
      // Currently we lookup based on ball speed.

      // 67 m/s ~= 150 mph
      aeroTable.Add(new BallAerodynamics(67, 2500, 10, 0.0004f, 0.27f, 0.986f));
      // 64 m/s ~= 143 mph
      aeroTable.Add(new BallAerodynamics(64, 3000, 9, 0.00038f, 0.27f, 0.985f));
      // 60 m/s ~= 134 mph
      aeroTable.Add(new BallAerodynamics(60, 3200, 12, 0.00035f, 0.30f, 0.985f));
      // 54 m/s ~= 120 mph
      aeroTable.Add(new BallAerodynamics(54, 4700, 14, 0.0001f, 0.31f, 0.985f));
      // 48 m/s ~= 107 mph
      aeroTable.Add(new BallAerodynamics(48, 6700, 16, 0.00005f, 0.32f, 0.98f));
      // 42 m/s ~= 94 mph
      aeroTable.Add(new BallAerodynamics(42, 9000, 22, 0.00005f, 0.34f, 0.98f));
      // 40 m/s ~= 89 mph
      aeroTable.Add(new BallAerodynamics(40, 10500, 25, 0.00005f, 0.34f, 0.98f));

      UpdatePhysics(0);

      ToggleBallFrozen(true);
    }

    public void UpdatePhysics(float elevation)
    {
      if (elevation == 0) {
        airDensity = airDensityMin;
        return;
      }
      float densityScale = Mathf.Clamp(elevation, 0f, 10000f) / 10000f;
      airDensity = Mathf.Lerp(airDensityMin, airDensityMax, densityScale);
      Debug.Log($"Setting airDensity {airDensity} based on elevation {elevation}");
    }

    public BallAerodynamics InterpolateAero(float ballSpeed, float launchAngle)
    {
        // Extract and sort unique speeds and angles
        var speeds = aeroTable.Select(x => x.ballSpeed).Distinct().OrderBy(x => x).ToList();
        var angles = aeroTable.Select(x => x.launchAngle).Distinct().OrderBy(x => x).ToList();

        // Clamp to edge values if outside the table
        if (ballSpeed <= speeds[0] && launchAngle <= angles[0])
            return aeroTable.First(x => x.ballSpeed == speeds[0] && x.launchAngle == angles[0]);
        if (ballSpeed >= speeds[^1] && launchAngle >= angles[^1])
            return aeroTable.First(x => x.ballSpeed == speeds[^1] && x.launchAngle == angles[^1]);

        // Find indices bracketing ballSpeed
        int s0 = 0, s1 = 0;
        for (int i = 0; i < speeds.Count - 1; i++)
        {
            if (ballSpeed >= speeds[i] && ballSpeed <= speeds[i + 1])
            {
                s0 = i;
                s1 = i + 1;
                break;
            }
        }

        // Find indices bracketing launchAngle
        int a0 = 0, a1 = 0;
        for (int j = 0; j < angles.Count - 1; j++)
        {
            if (launchAngle >= angles[j] && launchAngle <= angles[j + 1])
            {
                a0 = j;
                a1 = j + 1;
                break;
            }
        }

        // Get the four grid points
        BallAerodynamics Q11 = aeroTable.FirstOrDefault(x => x.ballSpeed == speeds[s0] && x.launchAngle == angles[a0]);
        BallAerodynamics Q12 = aeroTable.FirstOrDefault(x => x.ballSpeed == speeds[s0] && x.launchAngle == angles[a1]);
        BallAerodynamics Q21 = aeroTable.FirstOrDefault(x => x.ballSpeed == speeds[s1] && x.launchAngle == angles[a0]);
        BallAerodynamics Q22 = aeroTable.FirstOrDefault(x => x.ballSpeed == speeds[s1] && x.launchAngle == angles[a1]);

        // If any corners missing, fallback: nearest neighbor
        if (Q11 == null || Q12 == null || Q21 == null || Q22 == null)
        {
            // Could choose the closest sample instead
            return aeroTable.OrderBy(x =>
                Mathf.Abs(x.ballSpeed - ballSpeed) + Mathf.Abs(x.launchAngle - launchAngle)).First();
        }

        float tx = (ballSpeed - speeds[s0]) / (speeds[s1] - speeds[s0]);
        float ty = (launchAngle - angles[a0]) / (angles[a1] - angles[a0]);

        // Bilinear interpolation
        float magnusCoeff =
            Mathf.Lerp(
                Mathf.Lerp(Q11.magnusCoeff, Q21.magnusCoeff, tx),
                Mathf.Lerp(Q12.magnusCoeff, Q22.magnusCoeff, tx),
                ty);

        float dragCoeff =
            Mathf.Lerp(
                Mathf.Lerp(Q11.dragCoeff, Q21.dragCoeff, tx),
                Mathf.Lerp(Q12.dragCoeff, Q22.dragCoeff, tx),
                ty);

        float spinRate =
            Mathf.Lerp(
                Mathf.Lerp(Q11.spinRate, Q21.spinRate, tx),
                Mathf.Lerp(Q12.spinRate, Q22.spinRate, tx),
                ty);

        float decay =
            Mathf.Lerp(
                Mathf.Lerp(Q11.spinDecayRate, Q21.spinDecayRate, tx),
                Mathf.Lerp(Q12.spinDecayRate, Q22.spinDecayRate, tx),
                ty);

        return new BallAerodynamics(ballSpeed, spinRate, launchAngle, magnusCoeff, dragCoeff, decay);
    }

    BallAerodynamics InterpolateBySpeed(float ballSpeed)
    {
        aeroTable.Sort((a, b) => a.ballSpeed.CompareTo(b.ballSpeed));

        // Table must be sorted by ballSpeed ascending!
        if (ballSpeed <= aeroTable[0].ballSpeed)
            return aeroTable[0];
        if (ballSpeed >= aeroTable[aeroTable.Count - 1].ballSpeed)
            return aeroTable[aeroTable.Count - 1];

        // Find interval for interpolation
        for (int i = 0; i < aeroTable.Count - 1; i++)
        {
            var a = aeroTable[i];
            var b = aeroTable[i + 1];
            if (ballSpeed >= a.ballSpeed && ballSpeed < b.ballSpeed)
            {
                float t = (ballSpeed - a.ballSpeed) / (b.ballSpeed - a.ballSpeed);
                float magCoeff = Mathf.Lerp(a.magnusCoeff, b.magnusCoeff, t);
                float dragCoeff = Mathf.Lerp(a.dragCoeff, b.dragCoeff, t);
                float spinRate = Mathf.Lerp(a.spinRate, b.spinRate, t);
                float launchAngle = Mathf.Lerp(a.launchAngle, b.launchAngle, t);
                float spinDecayRate = Mathf.Lerp(a.spinDecayRate, b.spinDecayRate, t);
                return new BallAerodynamics(ballSpeed, spinRate, launchAngle, magCoeff, dragCoeff, spinDecayRate);
            }
        }
        return aeroTable[aeroTable.Count - 1];
    }

    private void ToggleBallFrozen(bool isFrozen)
    {
      rb.isKinematic = isFrozen;
      rb.useGravity = !isFrozen;
    }

    public void LaunchShot(ShotData shot, bool isPutt)
    {
      StartCoroutine(LaunchAfterFrame(shot, isPutt));
    }
    
    private IEnumerator LaunchAfterFrame(ShotData shot, bool isPutt)
    {
      // wait a frame
      yield return null;
      // unfreeze and launch ball
      ToggleBallFrozen(false);

      float ballSpeedMeters = shot.ballSpeed * OGSUnitConversions.milesPerHourToMeters;
      // Clamp the launch angle values with a minimum of 2 degrees, so we don't shoot the ball right into the ground on 0 or negative VLA shots
      float vla = Mathf.Min(Mathf.Max(shot.verticalLaunchAngle, 1.0f), 45.0f); // min 1, max 45 degrees

      if (isPutt) {
        ApplyInitialPuttForce(ballSpeedMeters, shot.horizontalLaunchAngle);
      } else {
        ApplyInitialShotForce(
            ballSpeedMeters,
            vla,
            shot.horizontalLaunchAngle,
            shot.spinSpeed,
            shot.spinAxis
        );
      }
    }

    private void ApplyInitialPuttForce(float ballSpeed, float horizontalLaunchAngle)
    {
      isGrounded = false;
      isLanded = false;    
      isPutt = true;
      Vector3 launchDir = transform.forward;
      launchDir = Quaternion.AngleAxis(horizontalLaunchAngle, transform.up) * launchDir;
      launchDir = Quaternion.AngleAxis(0, transform.right) * launchDir;

      Vector3 initialVelocity = launchDir.normalized * ballSpeed;

      rb.linearVelocity = initialVelocity;
    }

    private void ApplyInitialShotForce(
        float ballSpeed, // meters per second
        float verticalLaunchAngle, // degrees (-45 - 45)
        float horizontalLaunchAngle, // degrees (0 - 45)
        float spinSpeed, // rotations per minute (RPM)
        float spinAxis // degrees -45 to 45
      )
    {

      isGrounded = false;
      isLanded = false;
      isPutt = false;
      isEnded = false;

      float spinRadPerSec = spinSpeed * 2f * Mathf.PI / 60f;
      float axisRad = spinAxis * Mathf.Deg2Rad;

      // Use the ball's local axes for spin calculation
      Vector3 localLeft = -transform.right; // backspin axis
      Vector3 localUp = transform.up; // sidespin axis
      
      // Apply spin to shape shot
      Vector3 spinVector = (localLeft * Mathf.Cos(axisRad) + localUp * Mathf.Sin(axisRad)) * spinRadPerSec;

      rb.angularVelocity = spinVector;

      // Calculate initial ball speed and launch angle
      Vector3 launchDir = transform.forward;
      launchDir = Quaternion.AngleAxis(horizontalLaunchAngle, transform.up) * launchDir;
      launchDir = Quaternion.AngleAxis(-verticalLaunchAngle, transform.right) * launchDir;
      Vector3 initialVelocity = launchDir.normalized * ballSpeed;
      rb.linearVelocity = initialVelocity;

      // Lookup initial coefficients and store for this shot
      BallAerodynamics coeffs = InterpolateBySpeed(ballSpeed);
      initialMagnusCoeff = coeffs.magnusCoeff;
      initialDragCoeff = coeffs.dragCoeff;
      initialSpinDecayRate = coeffs.spinDecayRate;
    }

    public void ApplyAirPhysics()
    {
      // Basic air-resistance
      Vector3 v = rb.linearVelocity;
      float vMag = v.magnitude;
      Vector3 spinVec = rb.angularVelocity;
      float spinMag = spinVec.magnitude;

      float ballArea = Mathf.PI * ballRadius * ballRadius;
      float spinRatio = ballRadius * spinMag / vMag;

      // Use current values for interpolation
      float currentSpeed = vMag;
      float currentSpin = spinMag * 60f / (2f * Mathf.PI); // convert rad/s to RPM
      float currentLaunchAngle = Vector3.Angle(v, Vector3.ProjectOnPlane(v, Vector3.up));

      // Drag
      Vector3 drag = -0.5f * initialDragCoeff * airDensity * ballArea * vMag * v;
      rb.AddForce(drag);

      if (!isLanded) {
        // Magnus (Lift)
        Vector3 magnus = initialMagnusCoeff * Vector3.Cross(rb.angularVelocity, rb.linearVelocity);
        // Clamp Magnus to gravity * 0.85 for realism
        float gravityForce = ballMass * 9.81f;
        float maxLift = gravityForce * 0.83f;
        if (magnus.magnitude > maxLift) magnus = magnus.normalized * maxLift;
        rb.AddForce(magnus);
      }

      // Slows the ball spin over time
      // rb.angularVelocity *= spinDecayRate;
      // if the ball has landed and is bouncing, we skip magnus and apply a higher spin decay rate
      // this ensures the ball eventually comes to rest and doesn't roll forever
      // rb.angularVelocity *= isLanded ? groundSpinDamping : initialSpinDecayRate;
      rb.angularVelocity *= initialSpinDecayRate;

    }

    // Applied every frame the ball is in contact with the ground
    public void ApplyGroundPhysics()
    {
      // when to stop the ball movement
      float angularStopThreshold = 3.00f;
      float linearStopThreshold = 0.2f;

      // defaults / rough
      // lower = stops faster
      float grassSpinDampen = 4.5f;
      // higher = stops faster
      float grassDrag = 2.25f;
      float frictionCoefficient = 1.0f; // tune for realism; 0.13 is a typical golf green estimate

      // Only apply friction if actually moving
      if (rb.linearVelocity.magnitude > linearStopThreshold)
      {
          // Simulate rolling resistance (opposes motion)
          Vector3 horizontalVelocity = rb.linearVelocity;
          horizontalVelocity.y = 0;
          Vector3 frictionForce = -horizontalVelocity.normalized * frictionCoefficient * ballMass * Physics.gravity.magnitude;

          // Only apply if horizontalVelocity is significant (avoid div by zero)
          if(horizontalVelocity.sqrMagnitude > 0.01f) {
            rb.AddForce(frictionForce, ForceMode.Force);
          }
      } else {
        rb.linearVelocity = Vector3.zero;
      }


      if (rb.angularVelocity.magnitude > angularStopThreshold)
      {
        float dampingFactor = Mathf.Clamp01(1f - grassSpinDampen * Time.fixedDeltaTime);
        rb.angularVelocity *= dampingFactor;
      }
      else
      {
        rb.angularVelocity = Vector3.zero;
      }


    }
    
    // Applied when the ball first hits the ground surface
    public void ApplyBouncePhysics(Collision collision)
    {
      Vector3 contactNormal = collision.contacts[0].normal;
      Vector3 incomingVelocity = rb.linearVelocity;

      // Decompose velocity
      Vector3 velocityNormal = Vector3.Project(incomingVelocity, contactNormal);
      Vector3 velocityTangent = incomingVelocity - velocityNormal;

      // set bounce factor
      float restitution = 2.5f;
      Vector3 bouncedNormal = -velocityNormal * restitution;

      float friction = 0.4f;
      Vector3 bouncedTangent = velocityTangent * friction;

      // Spin-induced grip
      Vector3 spinAxis = rb.angularVelocity.normalized;
      float spinSpeed = rb.angularVelocity.magnitude;

      if (spinSpeed > 10.0f) {
        Vector3 gripDir = Vector3.Cross(spinAxis, contactNormal).normalized;
        float gripFactor = spinSpeed * gripStrength; // tune for realism
        bouncedTangent += gripDir * gripFactor;
      }

      // Combine
      rb.linearVelocity = bouncedNormal + bouncedTangent;
    }

    void FixedUpdate()
    {
      if (isGrounded) {
        
        ApplyGroundPhysics();

        bool basicallyStopped = rb.angularVelocity.magnitude < endShotThresholdAngular;
        if (!isEnded && basicallyStopped) {
          ToggleBallFrozen(true);
          isEnded = true;
          if (shotEndedCallback != null) {
            shotEndedCallback();
          }
        }
      } else {
        ApplyAirPhysics();
      }
    }
    
    void OnCollisionEnter(Collision collision)
    {
      if (!isPutt && !isGrounded && rb.linearVelocity.magnitude > bounceVelocityThreshold) {
        ApplyBouncePhysics(collision);
      }
    }
    void OnCollisionExit(Collision collision)
    {
      isGrounded = false;
    }
    void OnCollisionStay(Collision collision)
    {
      if (!isLanded)
      {
        isLanded = true;
      }
      isGrounded = true;
    }
}