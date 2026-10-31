using UnityEngine;

public class TirePhysics : MonoBehaviour
{
    // ==========================================
    // DATA BACAAN (INSPECTOR VARIABLES)
    // ==========================================
    
    [Header("1. Suspension Settings")]
    public float suspensionRestDist = 0.45f;
    public float springStrength = 250f; 
    public float springDamper = 45f;

    [Header("2. Grip & Arcade Friction Circle")]
    [Tooltip("Kurva yang menentukan Grip hilang saat Slip membesar")]
    public AnimationCurve gripCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f);
    public float maxSlipSpeed = 8f; 
    public float tireMass = 2.5f; 
    [Range(0f, 2f)] public float gripModifier = 1f;
    
    [Header("3. Surface Settings")]
    public LayerMask drivableSurfaceLayer = ~0; 
    public LayerMask slipperySurfaceLayer;
    [Range(0f, 1f)] public float slipperySurfaceGripPenalty = 0.4f;
    
    [Header("4. Handbrake Settings")]
    [Range(0f, 1f)] public float handbrakeGripMultiplier = 0.2f;
    [Tooltip("Per-handbraked-rear-tire longitudinal drag cap in force-like Unity units. This is not a vehicle-wide budget.")]
    public float handbrakeDragForce = 15f;

    [Header("Visuals & References")]
    public Transform tireVisualMesh;
    public float tireRadius = 0.3f;
    public Rigidbody carRigidBody;
    public Transform carTransform;
    public Vector3 visualRotationSetting;


    // ==========================================
    // INJECTED DATA (Dikirim oleh CarController)
    // ==========================================
    [HideInInspector] public float appliedMotorForce; 
    [HideInInspector] public float appliedBrakeForce; 
    [HideInInspector] public bool isHandbraking; 


    // ==========================================
    // STATE VARIABLES
    // ==========================================
    public bool IsGrounded { get; private set; }
    public float CurrentSuspensionDistance { get; private set; }
    
    private RaycastHit groundHit;
    private Vector3 axleWorldVel;       
    private Vector3 contactPatchVel;    
    private float currentVisualRotation;
    private ResolvedTireStyleSettings resolvedStyleSettings;
    private bool hasResolvedStyleSettings;
    private ResolvedSuspensionSettings resolvedSuspensionSettings;
    private bool hasResolvedSuspensionSettings;

    private void Awake()
    {
        bool tireUsedSafeFallback = false;
        bool suspensionUsedSafeFallback = false;

        if (!hasResolvedStyleSettings)
        {
            resolvedStyleSettings = CaptureAuthoredStyleSettings(out tireUsedSafeFallback);
            hasResolvedStyleSettings = true;
        }

        if (!hasResolvedSuspensionSettings)
        {
            resolvedSuspensionSettings = CaptureAuthoredSuspensionSettings(out suspensionUsedSafeFallback);
            hasResolvedSuspensionSettings = true;
        }

        if (tireUsedSafeFallback || suspensionUsedSafeFallback)
        {
            Debug.LogWarning(
                $"{name}: Authored tire, handbrake, or suspension settings are invalid. Using a safe runtime fallback; correct the TirePhysics Inspector values.",
                this);
        }
    }

    internal ResolvedTireStyleSettings CaptureAuthoredStyleSettings(out bool usedSafeFallback)
    {
        return TireStyleSettingsResolver.CaptureBaseline(
            gripCurve,
            maxSlipSpeed,
            gripModifier,
            handbrakeGripMultiplier,
            handbrakeDragForce,
            out usedSafeFallback);
    }

    internal void ApplyResolvedStyleSettings(ResolvedTireStyleSettings settings)
    {
        resolvedStyleSettings = settings;
        hasResolvedStyleSettings = true;
    }

    internal ResolvedSuspensionSettings CaptureAuthoredSuspensionSettings(out bool usedSafeFallback)
    {
        return SuspensionSettingsResolver.CaptureBaseline(
            springStrength,
            springDamper,
            out usedSafeFallback);
    }

    internal void ApplyResolvedSuspensionSettings(ResolvedSuspensionSettings settings)
    {
        resolvedSuspensionSettings = settings;
        hasResolvedSuspensionSettings = true;
    }


    private void FixedUpdate()
    {
        UpdatePhysicsReference();

        if (IsGrounded)
        {
            ApplySuspension(); 
            float activeGrip = CalculateTireGrip();
            
            // Logika baru Arcade Friction Circle!
            ApplyArcadeFrictionCircle(activeGrip); 
        }
        else
        {
            HandleAirborneState();
        }
    }

    private void Update()
    {
        if (tireVisualMesh != null)
        {
            UpdateVisualPosition();
            UpdateVisualRotation();
        }
    }


    // ==========================================
    // FITUR 1: SISTEM SUSPENSI
    // ==========================================
    private void ApplySuspension()
    {
        Vector3 springDir = transform.up;
        float offset = suspensionRestDist - CurrentSuspensionDistance;
        float vel = Vector3.Dot(springDir, axleWorldVel);
        
        float suspensionForce = (offset * resolvedSuspensionSettings.SpringStrength)
            - (vel * resolvedSuspensionSettings.DamperStrength);
        carRigidBody.AddForceAtPosition(springDir * suspensionForce, transform.position);
    }


    // ==========================================
    // FITUR 2: KALKULASI TRAKSI DASAR
    // ==========================================
    private float CalculateTireGrip()
    {
        Vector3 steeringDir = transform.right;
        float steeringVel = Vector3.Dot(steeringDir, contactPatchVel);
        
        float currentSlipSpeed = Mathf.Abs(steeringVel);
        float normalizedSlip = Mathf.Clamp01(currentSlipSpeed / resolvedStyleSettings.MaxSlipSpeed);
        float baseCurveGrip = resolvedStyleSettings.EvaluateGrip(normalizedSlip);

        float surfaceMultiplier = 1f;
        if (((1 << groundHit.collider.gameObject.layer) & slipperySurfaceLayer) != 0)
        {
            surfaceMultiplier = slipperySurfaceGripPenalty;
        }

        float finalGrip = baseCurveGrip * resolvedStyleSettings.GripModifier * surfaceMultiplier;
        if (isHandbraking) finalGrip *= resolvedStyleSettings.HandbrakeGripMultiplier;
        
        return finalGrip;
    }


    // ==========================================
    // FITUR 3: ARCADE FRICTION CIRCLE (The Cozy Logic)
    // ==========================================
    private void ApplyArcadeFrictionCircle(float activeGrip)
    {
        // --- A. KEBUTUHAN LONGITUDINAL (GAS / REM) ---
        Vector3 accelDir = transform.forward;
        float desiredLngForceMag = 0f;
        float carSpeed = Vector3.Dot(carTransform.forward, carRigidBody.linearVelocity);

        if (isHandbraking)
        {
            float forwardVel = Vector3.Dot(accelDir, contactPatchVel);
            desiredLngForceMag = tireMass * (-forwardVel / Time.fixedDeltaTime);
            desiredLngForceMag = Mathf.Clamp(
                desiredLngForceMag,
                -resolvedStyleSettings.HandbrakeDragForce,
                resolvedStyleSettings.HandbrakeDragForce);
        }
        else 
        {
            // [PERBAIKAN BUG MUNDUR]: Gunakan Mathf.Abs(carSpeed) agar rem berfungsi saat maju & mundur
            if (appliedBrakeForce > 0.1f && Mathf.Abs(carSpeed) > 0.5f)
            {
                float forwardVel = Vector3.Dot(accelDir, contactPatchVel);
                desiredLngForceMag = tireMass * (-forwardVel / Time.fixedDeltaTime);
                desiredLngForceMag = Mathf.Clamp(desiredLngForceMag, -appliedBrakeForce, appliedBrakeForce);
            }
            else
            {
                desiredLngForceMag = appliedMotorForce;
            }
        }
        
        Vector3 longitudinalForce = accelDir * desiredLngForceMag;


        // --- B. ARCADE FRICTION BLENDING ---
        // Seberapa keras kita menginjak pedal (Asumsi rem paling keras di angka ~25)
        float longitudinalEffort = Mathf.Clamp01(Mathf.Abs(desiredLngForceMag) / 25f);
        
        // Semakin keras di-gas/rem, grip belok didiskon maksimal 40% (0.4f)
        float frictionCirclePenalty = 1f - (longitudinalEffort * 0.4f); 
        
        // Grip final untuk belok
        float dynamicLateralGrip = activeGrip * frictionCirclePenalty;


        // --- C. KEBUTUHAN LATERAL (BELOK - Sesuai Skrip Orisinalmu!) ---
        Vector3 steeringDir = transform.right;
        float steeringVel = Vector3.Dot(steeringDir, contactPatchVel);
        
        // Menghitung gaya murni untuk membatalkan kecepatan samping dengan mulus
        float desiredLatForceMag = -steeringVel * dynamicLateralGrip * (tireMass / Time.fixedDeltaTime);
        Vector3 lateralForce = steeringDir * desiredLatForceMag;


        // --- D. APLIKASI GAYA DI ASPAL ---
        carRigidBody.AddForceAtPosition(lateralForce + longitudinalForce, groundHit.point);
    }


    // ==========================================
    // UTILITIES & VISUALS
    // ==========================================
    private void UpdatePhysicsReference()
    {
        float castOffset = 0.5f; 
        Vector3 startPos = transform.position + (transform.up * castOffset);
        Vector3 downDir = -transform.up;
        float maxTravelDist = Mathf.Max(0f, (suspensionRestDist + 0.2f) - tireRadius) + castOffset;

        IsGrounded = Physics.SphereCast(startPos, tireRadius, downDir, out groundHit, maxTravelDist, drivableSurfaceLayer);
        
        if (IsGrounded)
        {
            if (groundHit.transform.IsChildOf(carTransform.root))
            {
                IsGrounded = false; 
                return; 
            }
            
            CurrentSuspensionDistance = (groundHit.distance - castOffset) + tireRadius;
            axleWorldVel = carRigidBody.GetPointVelocity(transform.position);
            contactPatchVel = carRigidBody.GetPointVelocity(groundHit.point);
        }
    }

    private void HandleAirborneState()
    {
        CurrentSuspensionDistance = Mathf.Lerp(CurrentSuspensionDistance, suspensionRestDist + 0.2f, Time.fixedDeltaTime * 10f);
    }

    private void UpdateVisualPosition()
    {
        tireVisualMesh.localPosition = new Vector3(0f, -CurrentSuspensionDistance + tireRadius, 0f);
    }

    private void UpdateVisualRotation()
    {
        float forwardSpeed = Vector3.Dot(carTransform.forward, axleWorldVel);
        float distanceTravelled = forwardSpeed * Time.deltaTime;
        float rotationAngle = (distanceTravelled / (2f * Mathf.PI * tireRadius)) * 360f;
        
        currentVisualRotation = Mathf.Repeat(currentVisualRotation + rotationAngle, 360f);
        tireVisualMesh.localRotation = Quaternion.Euler(currentVisualRotation, 0f, visualRotationSetting.z);
    }
    
    // ==========================================
    // DEBUGGING VISUAL (GIZMOS)
    // ==========================================
    private void OnDrawGizmos()
    {
        if (transform == null) return;
        
        float castOffset = 0.5f; 
        Vector3 startPos = transform.position + (transform.up * castOffset); // Titik tembak dari atas kap mesin
        Vector3 downDir = -transform.up;
        float maxTravelDist = Mathf.Max(0f, (suspensionRestDist + 0.2f) - tireRadius) + castOffset;

        // Garis Hijau: Menunjukkan target posisi ideal suspensi saat rileks (dihitung dari as roda asli)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + (downDir * suspensionRestDist));

        // SphereCast Debugging
        if (Physics.SphereCast(startPos, tireRadius, downDir, out RaycastHit hit, maxTravelDist, drivableSurfaceLayer))
        {
            // Bola Kuning: Posisi bola SphereCast saat menabrak sesuatu
            Vector3 sphereCenter = startPos + (downDir * hit.distance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(sphereCenter, tireRadius);
            
            // Titik Merah: Titik kontak pasti antara ban dan aspal (Sangat penting untuk titik aplikasi gaya)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(hit.point, 0.05f); 
        }
    }
}
