using UnityEngine;

[System.Serializable]
public struct GearSettings
{
    public float maxSpeed; 
    public float torqueMultiplier; 
}

public class CarGearbox : MonoBehaviour
{
    public enum TransmissionType { Automatic, Manual }

    [Header("Gearbox Settings")]
    public TransmissionType transmissionType = TransmissionType.Automatic;
    public float shiftDelay = 0.3f; 
    
    [Header("Reverse Settings")]
    public float reverseMaxSpeed = 10f;
    public float reverseTorqueMultiplier = 1.0f;

    [Header("Engine Power Band (Rev to Power)")]
    [Tooltip("Sumbu X = RPM (0-1). Sumbu Y = Persentase Torsi (0-1).")]
    public AnimationCurve engineTorqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f),   // RPM 0 (Idle): Torsi lemah (30%)
        new Keyframe(0.6f, 1f),   // RPM 60% (Mid): Puncak Tenaga (100%)
        new Keyframe(0.95f, 0.5f) // RPM 95% (Redline): Tenaga ngempos/turun (50%)
    );

    [Header("Engine Braking")]
    [Tooltip("Dimensionless factor used with the profile brake budget to produce a vehicle-wide engine-brake request.")]
    public float engineBrakeStrength = 0.075f;

    [Header("Forward Gear Ratios")]
    public GearSettings[] gears;

    // ==========================================
    // OUTPUT DATA 
    // ==========================================
    public int CurrentGear { get; private set; } 
    public float CurrentRPM { get; private set; } 
    public float OutputTorqueMultiplier { get; private set; }
    public float EngineBrakeMultiplier { get; private set; } 
    public bool IsShifting { get; private set; }

    [Header("Debug Info")]
    public float currentSpeedKmH; 

    private float shiftTimer;

    private void Start()
    {
        CurrentGear = 0; 
        IsShifting = false;
    }

    public void ProcessGears(float forwardSpeed, float accelInput, float deltaTime)
    {
        if (gears == null || gears.Length == 0) return;

        currentSpeedKmH = Mathf.Abs(forwardSpeed) * 3.6f;

        // 1. TANGANI LOGIKA OTOMATIS (R, N, D)
        if (transmissionType == TransmissionType.Automatic)
        {
            if (forwardSpeed > 1f) {
                if (CurrentGear < 1) CurrentGear = 1; 
            } else if (forwardSpeed < -1f) {
                CurrentGear = -1; 
            } else {
                if (accelInput > 0.1f) CurrentGear = 1;
                else if (accelInput < -0.1f) CurrentGear = -1;
                else CurrentGear = 0; 
            }
        }

        // 2. KALKULASI TENAGA & RPM
        if (CurrentGear == -1) 
        {
            // === GIGI MUNDUR (R) ===
            float revSpeed = Mathf.Abs(forwardSpeed);
            float baseRPM = Mathf.Clamp01(revSpeed / reverseMaxSpeed);
            
            if (accelInput < -0.1f) {
                CurrentRPM = Mathf.Lerp(CurrentRPM, baseRPM + 0.15f, deltaTime * 6f);
                
                // EVALUASI KURVA TENAGA DI GIGI MUNDUR
                float rpmTorqueFactor = engineTorqueCurve.Evaluate(CurrentRPM);
                OutputTorqueMultiplier = Mathf.Abs(accelInput) * reverseTorqueMultiplier * rpmTorqueFactor;
            } else {
                CurrentRPM = Mathf.Lerp(CurrentRPM, baseRPM * 0.3f, deltaTime * 4f);
                OutputTorqueMultiplier = 0f;
            }
            IsShifting = false;
        }
        else if (CurrentGear == 0) 
        {
            // === GIGI NETRAL (N) ===
            float gasAmt = Mathf.Abs(accelInput);
            CurrentRPM = Mathf.Lerp(CurrentRPM, gasAmt * 0.9f, deltaTime * 8f);
            OutputTorqueMultiplier = 0f; 
            IsShifting = false;
        }
        else 
        {
            // === GIGI MAJU (D1, D2, dst) ===
            int gearArrayIndex = CurrentGear - 1; 

            if (IsShifting)
            {
                shiftTimer -= deltaTime;
                CurrentRPM = Mathf.Lerp(CurrentRPM, 0.4f, deltaTime * 10f);
                OutputTorqueMultiplier = 0f; 
                EngineBrakeMultiplier = 0f; 
                if (shiftTimer <= 0f) IsShifting = false;
                return;
            }

            float maxSpeedForGear = gears[gearArrayIndex].maxSpeed;
            float baseRPM = Mathf.Clamp01(forwardSpeed / maxSpeedForGear);
            
            if (accelInput > 0.1f) 
            {
                CurrentRPM = Mathf.Lerp(CurrentRPM, baseRPM + 0.15f, deltaTime * 6f);
                EngineBrakeMultiplier = 0f; 
            }
            else 
            {
                CurrentRPM = Mathf.Lerp(CurrentRPM, baseRPM * 0.3f, deltaTime * 4f);
                EngineBrakeMultiplier = engineBrakeStrength * gears[gearArrayIndex].torqueMultiplier * CurrentRPM;
            }

            // REV LIMITER (Redline Cut-off)
            float revLimiter = 1f;
            if (forwardSpeed > maxSpeedForGear * 0.95f) 
            {
                revLimiter = Mathf.Clamp01((maxSpeedForGear - forwardSpeed) / (maxSpeedForGear * 0.05f));
            }

            // ==========================================
            // [PERBAIKAN]: EVALUASI KURVA TENAGA!
            // ==========================================
            float rpmTorqueFactor = engineTorqueCurve.Evaluate(CurrentRPM);
            
            // Output Torsi Akhir = Input Gas x Rasio Gigi x Batas Redline x KURVA TENAGA MESIN
            OutputTorqueMultiplier = gears[gearArrayIndex].torqueMultiplier * accelInput * revLimiter * rpmTorqueFactor;

            // Handle Naik/Turun Gigi Otomatis
            if (transmissionType == TransmissionType.Automatic)
            {
                if (gearArrayIndex < gears.Length - 1 && forwardSpeed >= maxSpeedForGear * 0.95f) 
                    ShiftUp();
                else if (gearArrayIndex > 0 && forwardSpeed < gears[gearArrayIndex - 1].maxSpeed * 0.8f) 
                    ShiftDown();
            }
        }
    }

    public void ShiftUp()
    {
        if (CurrentGear < gears.Length) 
        {
            CurrentGear++;
            if (CurrentGear > 1) InitiateShift(); 
        }
    }

    public void ShiftDown()
    {
        if (CurrentGear > -1) 
        {
            CurrentGear--;
            if (CurrentGear > 0) InitiateShift(); 
        }
    }

    private void InitiateShift()
    {
        IsShifting = true;
        shiftTimer = shiftDelay;
    }
}
