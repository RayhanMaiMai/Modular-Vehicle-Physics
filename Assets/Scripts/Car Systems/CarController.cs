using Possession;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour, IPossessable
{
    public enum DriveType { FWD, RWD, AWD }

    // ==========================================
    // DATA & REFERENSI
    // ==========================================
    [Header("Global Car Profile")]
    public CarProfile activeCarProfile;

    [Header("Audio System")]
    public CarAudioManager audioManager;

    [Header("Input References")]
    public InputActionReference moveAction;
    public InputActionReference handbrakeAction;
    public InputActionReference hornAction;
    public InputActionReference shiftUpAction;
    public InputActionReference shiftDownAction;
    public InputActionReference interactAction; // Tombol Keluar Mobil
    
    [Header("Camera Profile")]
    [Tooltip("Offset yang lebih jauh untuk mencakup seluruh bodi mobil")]
    public Vector3 carCameraOffset = new Vector3(0, 15f, -15f);

    public Transform CameraTarget => transform; 
    public Vector3 CameraOffset => carCameraOffset;

    [Header("Vehicle Interaction")]
    public VehicleInteract exitDoor; // Referensi pintu keluar utama
    private DriverController storedDriver; // Menyimpan referensi driver yang merasuki

    [Header("Steering Settings (Weighted & Speed-Sensitive)")]
    public Transform frontLeftTire;
    public Transform frontRightTire;
    public float wheelbase = 2.5f; 
    public float trackWidth = 1.6f;
    public float lowSpeedSteerAngle = 35f;
    public float highSpeedSteerAngle = 10f;
    public float highSpeedThreshold = 20f;
    public float steerSmoothTime = 0.15f; 

    [Header("Drivetrain Settings")]
    public CarGearbox gearbox; 
    public DriveType driveType = DriveType.RWD;
    [Tooltip("Fraction of the vehicle-wide motor and engine-brake budgets sent to the front axle when using AWD.")]
    [Range(0f, 1f)] public float awdFrontBias = 0.5f;
    
    [Header("Driving Assists")]
    public bool enableABS = true;
    public float absPulseFrequency = 25f; 
    [Range(0.1f, 1f)] public float absMinBrakeFactor = 0.5f;

    [Header("Tire Physics References")]
    public TirePhysics tireFL; 
    public TirePhysics tireFR; 
    public TirePhysics tireRL; 
    public TirePhysics tireRR;
    
    // ==========================================
    // STATE VARIABLES
    // ==========================================
    private bool isControlled;
    private float steerInput;
    private float accelInput;
    private float currentSteerAngle;
    private float currentSteerVelocity; 
    private bool isHandbrakePressed;
    
    private bool wasHandbrakePressedLastFrame;
    private bool wasAccelPressedLastFrame;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetStoredDriver(DriverController driver)
    {
        storedDriver = driver;
    }

    // ==========================================
    // IMPLEMENTASI IPOSSESSABLE
    // ==========================================
    public void OnPossess()
    {
        isControlled = true;
        EnableAllInputs();
        
        isHandbrakePressed = false; 
        if (audioManager != null) audioManager.TriggerEngineStart();
    }

    public void OnUnpossess()
    {
        isControlled = false;
        DisableAllInputs();
        
        // Netralisir input seketika
        accelInput = 0f;
        steerInput = 0f;
        isHandbrakePressed = true; // Rem tangan otomatis ditarik saat parkir
        
        if (audioManager != null) audioManager.StopEngine();
    }

    public void ProcessInput()
    {
        GatherInputReference();       
            
        if (gearbox != null && gearbox.transmissionType == CarGearbox.TransmissionType.Manual)
        {
            if (shiftUpAction != null && shiftUpAction.action.WasPressedThisFrame()) gearbox.ShiftUp();
            if (shiftDownAction != null && shiftDownAction.action.WasPressedThisFrame()) gearbox.ShiftDown();
        }

        HandleVehicleExit();
    }

    private void HandleVehicleExit()
    {
        if (interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            // Syarat mutlak: Kendaraan harus berhenti agar aman untuk keluar
            if (rb.linearVelocity.magnitude < 1f && storedDriver != null && exitDoor != null)
            {
                storedDriver.transform.position = exitDoor.getOutPosition.position;
                PossessionManager.Instance.Possess(storedDriver);
            }
        }
    }

    // ==========================================
    // ORCHESTRATOR & LOGIC
    // ==========================================
    private void Update()
    {
        // Fungsi mekanis tetap berjalan memproses fisika ban & mesin 
        // meskipun sedang tidak dikendalikan oleh pemain.
        UpdateSteeringVisuals();
        
        if (gearbox != null)
        {
            float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
            gearbox.ProcessGears(forwardSpeed, accelInput); 
        }

        DistributeDrivetrainPower();  
        HandleAudioOrchestration();  
    }

    private void GatherInputReference()
    {
        if (moveAction != null)
        {
            Vector2 input = moveAction.action.ReadValue<Vector2>();
            steerInput = input.x; 
            accelInput = input.y; 
        }
        else
        {
            steerInput = 0f;
            accelInput = 0f;
        }

        if (handbrakeAction != null && handbrakeAction.action.WasPressedThisFrame())
        {
            isHandbrakePressed = !isHandbrakePressed;
        }
    }

    private void EnableAllInputs()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (handbrakeAction != null) handbrakeAction.action.Enable();
        if (hornAction != null) hornAction.action.Enable();
        if (shiftUpAction != null) shiftUpAction.action.Enable();
        if (shiftDownAction != null) shiftDownAction.action.Enable();
        if (interactAction != null) interactAction.action.Enable();
    }

    private void DisableAllInputs()
    {
        // Matikan Input Actions jika diperlukan untuk optimasi,
        // namun netralisasi variabel (accelInput = 0) di OnUnpossess sudah cukup mengamankan fisika.
    }

    private void UpdateSteeringVisuals()
    {
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = Mathf.Clamp01(currentSpeed / highSpeedThreshold);
        float dynamicMaxSteer = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedFactor);
        float targetAngle = steerInput * dynamicMaxSteer;

        currentSteerAngle = Mathf.SmoothDamp(currentSteerAngle, targetAngle, ref currentSteerVelocity, steerSmoothTime);

        float leftSteerAngle = currentSteerAngle;
        float rightSteerAngle = currentSteerAngle;

        if (Mathf.Abs(currentSteerAngle) > 0.1f)
        {
            float steerRad = currentSteerAngle * Mathf.Deg2Rad;
            float turnRadius = wheelbase / Mathf.Tan(steerRad);

            leftSteerAngle = Mathf.Atan(wheelbase / (turnRadius + (trackWidth / 2f))) * Mathf.Rad2Deg;
            rightSteerAngle = Mathf.Atan(wheelbase / (turnRadius - (trackWidth / 2f))) * Mathf.Rad2Deg;
        }

        if (frontLeftTire != null) frontLeftTire.localRotation = Quaternion.Euler(0f, leftSteerAngle, 0f);
        if (frontRightTire != null) frontRightTire.localRotation = Quaternion.Euler(0f, rightSteerAngle, 0f);
    }

    private void DistributeDrivetrainPower()
    {
        if (activeCarProfile == null) return;

        float carSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        float finalMotorForce = 0f;
        float pedalBrakeForce = 0f; 
        float engineBrakeForce = 0f; 

        float normalizedOverallSpeed = Mathf.Clamp01(Mathf.Abs(carSpeed) / activeCarProfile.topSpeed);
        float enginePowerCurveLimit = activeCarProfile.powerCurve.Evaluate(normalizedOverallSpeed);

        int gear = gearbox != null ? gearbox.CurrentGear : 1;
        float outputTorque = gearbox != null ? gearbox.OutputTorqueMultiplier : Mathf.Abs(accelInput);

        if (accelInput > 0f) 
        {
            if (carSpeed < -0.5f && gear <= 0) 
            {
                pedalBrakeForce = accelInput * activeCarProfile.maxBrakingForce;
            }
            else 
            {
                finalMotorForce = outputTorque * activeCarProfile.maxAccelerationForce * enginePowerCurveLimit;
            }
        }
        else if (accelInput < 0f) 
        {
            if (carSpeed > 0.5f && gear >= 0) 
            {
                pedalBrakeForce = Mathf.Abs(accelInput) * activeCarProfile.maxBrakingForce;

                if (enableABS && Mathf.Abs(accelInput) > 0.5f)
                {
                    float pulse = (Mathf.Sin(Time.time * absPulseFrequency) + 1f) / 2f;
                    float absModifier = Mathf.Lerp(absMinBrakeFactor, 1f, pulse);
                    pedalBrakeForce *= absModifier;
                }
            }
            else 
            {
                finalMotorForce = -outputTorque * activeCarProfile.maxAccelerationForce * enginePowerCurveLimit; 
            }
        }
        else 
        {
            if (gearbox != null && carSpeed > 1f)
            {
                engineBrakeForce = gearbox.EngineBrakeMultiplier * activeCarProfile.maxBrakingForce;
            }
        }

        float frontBias = driveType == DriveType.FWD ? 1f : (driveType == DriveType.AWD ? awdFrontBias : 0f);
        float rearBias = driveType == DriveType.RWD ? 1f : (driveType == DriveType.AWD ? 1f - awdFrontBias : 0f);

        // Profile motor and brake values are whole-vehicle, force-like command budgets.
        // Allocate fixed shares to the four configured tire positions. A missing or
        // ungrounded tire does not redistribute its share to another tire.
        const float tiresPerAxle = 2f;
        const float configuredTireCount = 4f;

        float frontMotorForcePerTire = finalMotorForce * frontBias / tiresPerAxle;
        float rearMotorForcePerTire = finalMotorForce * rearBias / tiresPerAxle;
        float serviceBrakeForcePerTire = pedalBrakeForce / configuredTireCount;
        float frontEngineBrakeForcePerTire = engineBrakeForce * frontBias / tiresPerAxle;
        float rearEngineBrakeForcePerTire = engineBrakeForce * rearBias / tiresPerAxle;

        float frontTotalBrake = serviceBrakeForcePerTire + frontEngineBrakeForcePerTire;
        float rearTotalBrake = serviceBrakeForcePerTire + rearEngineBrakeForcePerTire;

        ApplyPowerToTire(tireFL, frontMotorForcePerTire, frontTotalBrake, false);
        ApplyPowerToTire(tireFR, frontMotorForcePerTire, frontTotalBrake, false);
        ApplyPowerToTire(tireRL, rearMotorForcePerTire, rearTotalBrake, isHandbrakePressed);
        ApplyPowerToTire(tireRR, rearMotorForcePerTire, rearTotalBrake, isHandbrakePressed);
    }

    private void ApplyPowerToTire(TirePhysics tire, float motor, float brake, bool applyHandbrake)
    {
        if (tire != null)
        {
            tire.appliedMotorForce = motor;
            tire.appliedBrakeForce = brake;
            tire.isHandbraking = applyHandbrake;
        }
    }

    private void HandleAudioOrchestration()
    {
        if (audioManager == null) return;

        bool isHonking = hornAction != null && hornAction.action.IsPressed();
        audioManager.SetHorn(isHonking);

        if (isHandbrakePressed && !wasHandbrakePressedLastFrame)
        {
            audioManager.TriggerHandbrake();
        }
        wasHandbrakePressedLastFrame = isHandbrakePressed;

        bool isPressingGas = accelInput > 0.1f; 
        if (isPressingGas && !wasAccelPressedLastFrame)
        {
            audioManager.TriggerAccelRev();
        }
        wasAccelPressedLastFrame = isPressingGas;

        if (activeCarProfile != null)
        {
            float currentSpeed = rb.linearVelocity.magnitude;
            float normalizedSpeed = Mathf.Clamp01(currentSpeed / activeCarProfile.topSpeed);
            
            audioManager.UpdateEngineParameters(normalizedSpeed, accelInput);
        }
    }
}
