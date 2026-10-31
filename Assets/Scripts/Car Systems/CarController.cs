using Possession;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(-100)]
public class CarController : MonoBehaviour, IPossessable
{
    public enum DriveType { FWD, RWD, AWD }

    private enum ShiftCommand { Up, Down }

    private const int ShiftCommandCapacity = 8;

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

    [Header("Optional Driving Style")]
    [Tooltip("Resolved once in Awake. A null profile or a profile with steering override disabled preserves the serialized steering settings above.")]
    [SerializeField] private DrivingStyleProfile drivingStyleProfile;
    [Tooltip("Editor/development builds only: logs authored baselines and the effective initialization snapshot once in Awake.")]
    [SerializeField] private bool logResolvedStyleOnAwake;

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
    private ResolvedSteeringSettings resolvedSteeringSettings;
    private ResolvedVehicleTireStyleSettings resolvedTireStyleSettings;
    private ResolvedVehicleSuspensionSettings resolvedSuspensionSettings;
    private ResolvedVehicleAntiRollSettings resolvedAntiRollSettings;
    private bool steeringOverrideApplied;
    private bool tireResponseOverrideApplied;
    private bool handbrakeOverrideApplied;
    private bool suspensionOverrideApplied;
    private bool antiRollOverrideApplied;

    private readonly ShiftCommand[] pendingShiftCommands = new ShiftCommand[ShiftCommandCapacity];
    private int pendingShiftCommandHead;
    private int pendingShiftCommandCount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        resolvedSteeringSettings = SteeringSettingsResolver.Resolve(
            lowSpeedSteerAngle,
            highSpeedSteerAngle,
            highSpeedThreshold,
            steerSmoothTime,
            drivingStyleProfile,
            out string steeringDiagnostic,
            out steeringOverrideApplied);

        if (!string.IsNullOrEmpty(steeringDiagnostic))
        {
            Debug.LogWarning($"{name}: {steeringDiagnostic}", this);
        }

        string tireDiagnostic = null;
        ResolvedVehicleTireStyleSettings authoredTireBaselines = new ResolvedVehicleTireStyleSettings(
            CaptureTireStyleBaseline(tireFL, "front-left", ref tireDiagnostic),
            CaptureTireStyleBaseline(tireFR, "front-right", ref tireDiagnostic),
            CaptureTireStyleBaseline(tireRL, "rear-left", ref tireDiagnostic),
            CaptureTireStyleBaseline(tireRR, "rear-right", ref tireDiagnostic));

        resolvedTireStyleSettings = TireStyleSettingsResolver.Resolve(
            authoredTireBaselines,
            drivingStyleProfile,
            out string styleTireDiagnostic,
            out tireResponseOverrideApplied,
            out handbrakeOverrideApplied);
        tireDiagnostic = AppendDiagnostic(tireDiagnostic, styleTireDiagnostic);

        ApplyTireStyleSettings(tireFL, resolvedTireStyleSettings.FrontLeft);
        ApplyTireStyleSettings(tireFR, resolvedTireStyleSettings.FrontRight);
        ApplyTireStyleSettings(tireRL, resolvedTireStyleSettings.RearLeft);
        ApplyTireStyleSettings(tireRR, resolvedTireStyleSettings.RearRight);

        if (!string.IsNullOrEmpty(tireDiagnostic))
        {
            Debug.LogWarning($"{name}: {tireDiagnostic}", this);
        }

        string suspensionDiagnostic = null;
        ResolvedVehicleSuspensionSettings authoredSuspensionBaselines = new ResolvedVehicleSuspensionSettings(
            CaptureSuspensionBaseline(tireFL, "front-left", ref suspensionDiagnostic),
            CaptureSuspensionBaseline(tireFR, "front-right", ref suspensionDiagnostic),
            CaptureSuspensionBaseline(tireRL, "rear-left", ref suspensionDiagnostic),
            CaptureSuspensionBaseline(tireRR, "rear-right", ref suspensionDiagnostic));

        resolvedSuspensionSettings = SuspensionSettingsResolver.Resolve(
            authoredSuspensionBaselines,
            drivingStyleProfile,
            out string styleSuspensionDiagnostic,
            out suspensionOverrideApplied);
        suspensionDiagnostic = AppendDiagnostic(suspensionDiagnostic, styleSuspensionDiagnostic);

        ApplySuspensionSettings(tireFL, resolvedSuspensionSettings.FrontLeft);
        ApplySuspensionSettings(tireFR, resolvedSuspensionSettings.FrontRight);
        ApplySuspensionSettings(tireRL, resolvedSuspensionSettings.RearLeft);
        ApplySuspensionSettings(tireRR, resolvedSuspensionSettings.RearRight);

        if (!string.IsNullOrEmpty(suspensionDiagnostic))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"{name}: {suspensionDiagnostic}", this);
#endif
        }

        AntiRollBar[] antiRollCandidates = GetComponentsInChildren<AntiRollBar>(true);
        AntiRollBar frontAntiRollBar = FindUniqueAntiRollBar(
            antiRollCandidates,
            tireFL,
            tireFR);
        AntiRollBar rearAntiRollBar = FindUniqueAntiRollBar(
            antiRollCandidates,
            tireRL,
            tireRR);
        bool hasUnambiguousAntiRollLinkage = frontAntiRollBar != null
            && rearAntiRollBar != null
            && frontAntiRollBar != rearAntiRollBar;

        ResolvedVehicleAntiRollSettings authoredAntiRollBaselines = new ResolvedVehicleAntiRollSettings(
            frontAntiRollBar != null
                ? frontAntiRollBar.CaptureAuthoredStyleSettings()
                : AntiRollSettingsResolver.CreateUnavailableBaseline(),
            rearAntiRollBar != null
                ? rearAntiRollBar.CaptureAuthoredStyleSettings()
                : AntiRollSettingsResolver.CreateUnavailableBaseline());

        resolvedAntiRollSettings = AntiRollSettingsResolver.Resolve(
            authoredAntiRollBaselines,
            hasUnambiguousAntiRollLinkage,
            drivingStyleProfile,
            out string antiRollDiagnostic,
            out antiRollOverrideApplied);

        if (hasUnambiguousAntiRollLinkage)
        {
            frontAntiRollBar.ApplyResolvedStyleSettings(resolvedAntiRollSettings.Front);
            rearAntiRollBar.ApplyResolvedStyleSettings(resolvedAntiRollSettings.Rear);
        }

        if (!string.IsNullOrEmpty(antiRollDiagnostic))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"{name}: {antiRollDiagnostic}", this);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logResolvedStyleOnAwake)
        {
            LogResolvedStyleSnapshot(
                authoredTireBaselines,
                authoredSuspensionBaselines,
                authoredAntiRollBaselines,
                hasUnambiguousAntiRollLinkage);
        }
#endif
    }

    private static ResolvedTireStyleSettings CaptureTireStyleBaseline(
        TirePhysics tire,
        string tireLabel,
        ref string diagnostic)
    {
        if (tire == null)
        {
            return TireStyleSettingsResolver.CreateSafeFallback();
        }

        ResolvedTireStyleSettings baseline = tire.CaptureAuthoredStyleSettings(out bool usedSafeFallback);
        if (usedSafeFallback)
        {
            diagnostic = AppendDiagnostic(
                diagnostic,
                $"The {tireLabel} tire has invalid authored style values and is using a safe runtime fallback; correct its TirePhysics Inspector fields.");
        }

        return baseline;
    }

    private static void ApplyTireStyleSettings(TirePhysics tire, ResolvedTireStyleSettings settings)
    {
        if (tire != null)
        {
            tire.ApplyResolvedStyleSettings(settings);
        }
    }

    private static ResolvedSuspensionSettings CaptureSuspensionBaseline(
        TirePhysics tire,
        string tireLabel,
        ref string diagnostic)
    {
        if (tire == null)
        {
            return SuspensionSettingsResolver.CreateSafeFallback();
        }

        ResolvedSuspensionSettings baseline = tire.CaptureAuthoredSuspensionSettings(out bool usedSafeFallback);
        if (usedSafeFallback)
        {
            diagnostic = AppendDiagnostic(
                diagnostic,
                $"The {tireLabel} tire has invalid authored suspension values and is using a safe runtime fallback; correct its TirePhysics Inspector fields.");
        }

        return baseline;
    }

    private static void ApplySuspensionSettings(TirePhysics tire, ResolvedSuspensionSettings settings)
    {
        if (tire != null)
        {
            tire.ApplyResolvedSuspensionSettings(settings);
        }
    }

    private AntiRollBar FindUniqueAntiRollBar(
        AntiRollBar[] candidates,
        TirePhysics expectedWheelA,
        TirePhysics expectedWheelB)
    {
        AntiRollBar match = null;
        int matchCount = 0;

        for (int i = 0; i < candidates.Length; i++)
        {
            AntiRollBar candidate = candidates[i];
            if (candidate == null || !candidate.HasExactLinkage(rb, expectedWheelA, expectedWheelB))
            {
                continue;
            }

            match = candidate;
            matchCount++;
        }

        return matchCount == 1 ? match : null;
    }

    private static string AppendDiagnostic(string existing, string addition)
    {
        if (string.IsNullOrEmpty(addition)) return existing;
        return string.IsNullOrEmpty(existing) ? addition : existing + " " + addition;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LogResolvedStyleSnapshot(
        ResolvedVehicleTireStyleSettings authoredTireBaselines,
        ResolvedVehicleSuspensionSettings authoredSuspensionBaselines,
        ResolvedVehicleAntiRollSettings authoredAntiRollBaselines,
        bool hasUnambiguousAntiRollLinkage)
    {
        bool steeringRequested = drivingStyleProfile != null && drivingStyleProfile.OverrideSteering;
        bool tireRequested = drivingStyleProfile != null && drivingStyleProfile.OverrideTireResponse;
        bool handbrakeRequested = drivingStyleProfile != null && drivingStyleProfile.OverrideHandbrake;
        bool suspensionRequested = drivingStyleProfile != null && drivingStyleProfile.OverrideSuspension;
        bool antiRollRequested = drivingStyleProfile != null && drivingStyleProfile.OverrideAntiRoll;

        Debug.Log(
            $"{name} driving-style snapshot (read-only, resolved in Awake)\n"
            + $"Override flags: steering={steeringRequested}, tireResponse={tireRequested}, "
            + $"handbrake={handbrakeRequested}, suspension={suspensionRequested}, antiRoll={antiRollRequested}\n"
            + $"Resolution: steering={FormatResolutionStatus(steeringRequested, steeringOverrideApplied)}, "
            + $"tireResponse={FormatResolutionStatus(tireRequested, tireResponseOverrideApplied)}, "
            + $"handbrake={FormatResolutionStatus(handbrakeRequested, handbrakeOverrideApplied)}, "
            + $"suspension={FormatResolutionStatus(suspensionRequested, suspensionOverrideApplied)}, "
            + $"antiRoll={FormatResolutionStatus(antiRollRequested, antiRollOverrideApplied)}, "
            + $"antiRollLinkage={(hasUnambiguousAntiRollLinkage ? "exact" : "missing/ambiguous")}\n"
            + $"Authored steering baseline: low={lowSpeedSteerAngle:0.###}, high={highSpeedSteerAngle:0.###}, "
            + $"referenceSpeed={highSpeedThreshold:0.###}, smoothTime={steerSmoothTime:0.###}\n"
            + $"Runtime effective steering: low={resolvedSteeringSettings.LowSpeedSteerAngle:0.###}, "
            + $"high={resolvedSteeringSettings.HighSpeedSteerAngle:0.###}, "
            + $"referenceSpeed={resolvedSteeringSettings.HighSpeedThreshold:0.###}, "
            + $"smoothTime={resolvedSteeringSettings.SteerSmoothTime:0.###}\n"
            + $"Authored tires: FL {FormatTireStyle(authoredTireBaselines.FrontLeft)}; "
            + $"FR {FormatTireStyle(authoredTireBaselines.FrontRight)}; "
            + $"RL {FormatTireStyle(authoredTireBaselines.RearLeft)}; "
            + $"RR {FormatTireStyle(authoredTireBaselines.RearRight)}\n"
            + $"Runtime effective tires: FL {FormatTireStyle(resolvedTireStyleSettings.FrontLeft)}; "
            + $"FR {FormatTireStyle(resolvedTireStyleSettings.FrontRight)}; "
            + $"RL {FormatTireStyle(resolvedTireStyleSettings.RearLeft)}; "
            + $"RR {FormatTireStyle(resolvedTireStyleSettings.RearRight)}\n"
            + $"Authored suspension: FL {FormatSuspension(authoredSuspensionBaselines.FrontLeft)}; "
            + $"FR {FormatSuspension(authoredSuspensionBaselines.FrontRight)}; "
            + $"RL {FormatSuspension(authoredSuspensionBaselines.RearLeft)}; "
            + $"RR {FormatSuspension(authoredSuspensionBaselines.RearRight)}\n"
            + $"Runtime effective suspension: FL {FormatSuspension(resolvedSuspensionSettings.FrontLeft)}; "
            + $"FR {FormatSuspension(resolvedSuspensionSettings.FrontRight)}; "
            + $"RL {FormatSuspension(resolvedSuspensionSettings.RearLeft)}; "
            + $"RR {FormatSuspension(resolvedSuspensionSettings.RearRight)}\n"
            + $"Authored anti-roll: front {FormatAntiRoll(authoredAntiRollBaselines.Front)}; "
            + $"rear {FormatAntiRoll(authoredAntiRollBaselines.Rear)}\n"
            + $"Runtime effective anti-roll: front {FormatAntiRoll(resolvedAntiRollSettings.Front)}; "
            + $"rear {FormatAntiRoll(resolvedAntiRollSettings.Rear)}\n"
            + "An OFF prefab anti-roll gate remains OFF and disables that axle regardless of resolved strength/cap.",
            this);
    }

    private static string FormatTireStyle(ResolvedTireStyleSettings settings)
    {
        return $"[grip={settings.GripModifier:0.###}, maxSlip={settings.MaxSlipSpeed:0.###}, "
            + $"curveKeys={settings.GripCurveKeyCount}, curve(0)={settings.EvaluateGrip(0f):0.###}, "
            + $"curve(1)={settings.EvaluateGrip(1f):0.###}, handbrakeGrip={settings.HandbrakeGripMultiplier:0.###}, "
            + $"handbrakeDrag={settings.HandbrakeDragForce:0.###}]";
    }

    private static string FormatSuspension(ResolvedSuspensionSettings settings)
    {
        return $"[spring={settings.SpringStrength:0.###}, damper={settings.DamperStrength:0.###}]";
    }

    private static string FormatAntiRoll(ResolvedAntiRollSettings settings)
    {
        return $"[gate={settings.GateEnabled}, strength={settings.Strength:0.###}, cap={settings.MaxForcePerSide:0.###}]";
    }

    private static string FormatResolutionStatus(bool requested, bool applied)
    {
        if (!requested) return "authored baseline";
        return applied ? "style applied" : "fallback to authored baseline";
    }
#endif

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

        ClearPendingShiftCommands();
        isHandbrakePressed = false; 
        if (audioManager != null) audioManager.TriggerEngineStart();
    }

    public void OnUnpossess()
    {
        isControlled = false;
        DisableAllInputs();
        
        ResetControlStateForParking();
        
        if (audioManager != null) audioManager.StopEngine();
    }

    private void OnDisable()
    {
        isControlled = false;
        ResetControlStateForParking();
    }

    public void ProcessInput()
    {
        GatherInputReference();       

        if (gearbox != null && gearbox.transmissionType == CarGearbox.TransmissionType.Manual)
        {
            bool shiftUpPressed = shiftUpAction != null && shiftUpAction.action.WasPressedThisFrame();
            bool shiftDownPressed = shiftDownAction != null && shiftDownAction.action.WasPressedThisFrame();

            // Input System does not expose a reliable order for opposite edges captured
            // together, so cancel the pair instead of inventing a priority.
            if (shiftUpPressed != shiftDownPressed)
            {
                EnqueueShiftCommand(shiftUpPressed ? ShiftCommand.Up : ShiftCommand.Down);
            }
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
        HandleAudioOrchestration();
    }

    private void FixedUpdate()
    {
        ProcessPendingShiftCommand();
        UpdatePhysicsSteering(Time.fixedDeltaTime);

        if (gearbox != null)
        {
            float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
            gearbox.ProcessGears(forwardSpeed, accelInput, Time.fixedDeltaTime);
        }

        DistributeDrivetrainPower();
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

    private void UpdatePhysicsSteering(float deltaTime)
    {
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = Mathf.Clamp01(currentSpeed / resolvedSteeringSettings.HighSpeedThreshold);
        float dynamicMaxSteer = Mathf.Lerp(
            resolvedSteeringSettings.LowSpeedSteerAngle,
            resolvedSteeringSettings.HighSpeedSteerAngle,
            speedFactor);
        float targetAngle = steerInput * dynamicMaxSteer;

        currentSteerAngle = Mathf.SmoothDamp(
            currentSteerAngle,
            targetAngle,
            ref currentSteerVelocity,
            resolvedSteeringSettings.SteerSmoothTime,
            Mathf.Infinity,
            deltaTime);

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
                    float pulse = (Mathf.Sin(Time.fixedTime * absPulseFrequency) + 1f) / 2f;
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

    private void EnqueueShiftCommand(ShiftCommand command)
    {
        if (pendingShiftCommandCount >= ShiftCommandCapacity)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("Shift command buffer is full; discarding newest command.", this);
#endif
            return;
        }

        int tail = (pendingShiftCommandHead + pendingShiftCommandCount) % ShiftCommandCapacity;
        pendingShiftCommands[tail] = command;
        pendingShiftCommandCount++;
    }

    private void ProcessPendingShiftCommand()
    {
        if (pendingShiftCommandCount == 0) return;

        ShiftCommand command = pendingShiftCommands[pendingShiftCommandHead];
        pendingShiftCommandHead = (pendingShiftCommandHead + 1) % ShiftCommandCapacity;
        pendingShiftCommandCount--;

        if (gearbox == null) return;

        if (command == ShiftCommand.Up) gearbox.ShiftUp();
        else gearbox.ShiftDown();
    }

    private void ClearPendingShiftCommands()
    {
        pendingShiftCommandHead = 0;
        pendingShiftCommandCount = 0;
    }

    private void ResetControlStateForParking()
    {
        accelInput = 0f;
        steerInput = 0f;
        currentSteerAngle = 0f;
        currentSteerVelocity = 0f;
        isHandbrakePressed = true;
        ClearPendingShiftCommands();

        if (frontLeftTire != null) frontLeftTire.localRotation = Quaternion.identity;
        if (frontRightTire != null) frontRightTire.localRotation = Quaternion.identity;

        ApplyPowerToTire(tireFL, 0f, 0f, false);
        ApplyPowerToTire(tireFR, 0f, 0f, false);
        ApplyPowerToTire(tireRL, 0f, 0f, true);
        ApplyPowerToTire(tireRR, 0f, 0f, true);
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
