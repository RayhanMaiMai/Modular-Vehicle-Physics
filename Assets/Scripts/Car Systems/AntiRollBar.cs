using UnityEngine;

[DefaultExecutionOrder(100)]
public class AntiRollBar : MonoBehaviour
{
    [Header("Axle Wheels")]
    public TirePhysics wheelLeft;
    public TirePhysics wheelRight;

    [Header("Force Ownership")]
    [SerializeField] private Rigidbody chassisRigidbody;

    [Tooltip("Opt in only after assigning the root chassis Rigidbody and calibrating safe strengths. Existing 10000/8000 values are not chassis-calibrated.")]
    [SerializeField] private bool enableChassisAntiRoll = false;

    [Header("Anti-Roll Settings")]
    [Tooltip("Kekuatan penyeimbang. Biasanya suspensi depan (Front) butuh nilai lebih besar dari belakang (Rear).")]
    [Min(0f)]
    public float antiRollForce = 5000f;

    [Tooltip("Absolute force limit applied to each side of the axle. Keep at zero until a safe chassis tune is approved.")]
    [SerializeField, Min(0f)] private float maxForcePerSide = 0f;

    private bool hasLoggedMissingChassis;
    private bool hasLoggedInvalidConfiguration;
    private ResolvedAntiRollSettings resolvedStyleSettings;
    private bool hasResolvedStyleSettings;

    private void Awake()
    {
        if (hasResolvedStyleSettings) return;

        resolvedStyleSettings = CaptureAuthoredStyleSettings();
        hasResolvedStyleSettings = true;
    }

    internal ResolvedAntiRollSettings CaptureAuthoredStyleSettings()
    {
        return new ResolvedAntiRollSettings(
            enableChassisAntiRoll,
            antiRollForce,
            maxForcePerSide);
    }

    internal void ApplyResolvedStyleSettings(ResolvedAntiRollSettings settings)
    {
        resolvedStyleSettings = settings;
        hasResolvedStyleSettings = true;
    }

    internal bool HasExactLinkage(
        Rigidbody expectedChassis,
        TirePhysics expectedWheelA,
        TirePhysics expectedWheelB)
    {
        if (expectedChassis == null
            || expectedWheelA == null
            || expectedWheelB == null
            || expectedWheelA == expectedWheelB
            || chassisRigidbody != expectedChassis)
        {
            return false;
        }

        return (wheelLeft == expectedWheelA && wheelRight == expectedWheelB)
            || (wheelLeft == expectedWheelB && wheelRight == expectedWheelA);
    }

    private void FixedUpdate()
    {
        if (!resolvedStyleSettings.GateEnabled) return;

        if (chassisRigidbody == null)
        {
            LogMissingChassisOnce();
            return;
        }

        if (!IsFinite(resolvedStyleSettings.MaxForcePerSide)
            || resolvedStyleSettings.MaxForcePerSide < 0f)
        {
            LogInvalidConfigurationOnce("maxForcePerSide must be finite and nonnegative.");
            return;
        }

        // A zero cap is the intentionally inert default, even if the gate is enabled.
        if (resolvedStyleSettings.MaxForcePerSide == 0f) return;

        if (!wheelLeft || !wheelRight)
        {
            LogInvalidConfigurationOnce("assign both left and right TirePhysics references.");
            return;
        }

        if (wheelLeft == wheelRight)
        {
            LogInvalidConfigurationOnce("left and right tires must be different objects.");
            return;
        }

        // This safe first version never creates an unbalanced one-wheel lift force.
        if (!wheelLeft.IsGrounded || !wheelRight.IsGrounded) return;

        if (!IsFinite(resolvedStyleSettings.Strength) || resolvedStyleSettings.Strength < 0f)
        {
            LogInvalidConfigurationOnce("antiRollForce must be finite and nonnegative.");
            return;
        }

        if (!HasValidSuspensionMeasurement(wheelLeft) || !HasValidSuspensionMeasurement(wheelRight))
        {
            LogInvalidConfigurationOnce("grounded tires need finite suspension distances and a rest distance greater than 0.0001.");
            return;
        }

        Vector3 chassisUp = chassisRigidbody.transform.up;
        if (!IsFinite(chassisUp) || chassisUp.sqrMagnitude <= 0.0001f)
        {
            LogInvalidConfigurationOnce("the chassis up axis is invalid.");
            return;
        }

        chassisUp.Normalize();

        Vector3 leftForcePosition = wheelLeft.transform.position;
        Vector3 rightForcePosition = wheelRight.transform.position;
        if (!IsFinite(leftForcePosition) || !IsFinite(rightForcePosition))
        {
            LogInvalidConfigurationOnce("the tire force application positions are invalid.");
            return;
        }

        // 1. Normalize and clamp suspension travel (1.0 = extended, 0.0 = compressed).
        float travelL = Mathf.Clamp01(wheelLeft.CurrentSuspensionDistance / wheelLeft.suspensionRestDist);
        float travelR = Mathf.Clamp01(wheelRight.CurrentSuspensionDistance / wheelRight.suspensionRestDist);

        // 2. Cari selisih tekanan (Jika mobil miring, nilai ini tidak akan nol)
        float travelDifference = travelL - travelR;

        // 3. Convert the travel difference into a bounded, equal-and-opposite force pair.
        float requestedForce = travelDifference * resolvedStyleSettings.Strength;
        if (!IsFinite(requestedForce))
        {
            LogInvalidConfigurationOnce("the requested anti-roll force is non-finite.");
            return;
        }

        float rollForce = Mathf.Clamp(
            requestedForce,
            -resolvedStyleSettings.MaxForcePerSide,
            resolvedStyleSettings.MaxForcePerSide);

        // 4. Aplikasikan gaya ke sasis mobil
        // Jika kiri tertekan (travelL kecil), sasis kiri didorong KE ATAS (-rollForce)
        chassisRigidbody.AddForceAtPosition(chassisUp * -rollForce, leftForcePosition);

        // Dan sasis kanan ditarik KE BAWAH (rollForce) agar mobil kembali rata
        chassisRigidbody.AddForceAtPosition(chassisUp * rollForce, rightForcePosition);
    }

    private static bool HasValidSuspensionMeasurement(TirePhysics wheel)
    {
        float restDistance = wheel.suspensionRestDist;
        float currentDistance = wheel.CurrentSuspensionDistance;

        return IsFinite(restDistance)
            && restDistance > 0.0001f
            && IsFinite(currentDistance);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private void LogMissingChassisOnce()
    {
        if (hasLoggedMissingChassis) return;
        hasLoggedMissingChassis = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            $"{name}: chassis anti-roll is enabled without a chassis Rigidbody. Assign the vehicle root Rigidbody or disable the anti-roll gate.",
            this);
#endif
    }

    private void LogInvalidConfigurationOnce(string reason)
    {
        if (hasLoggedInvalidConfiguration) return;
        hasLoggedInvalidConfiguration = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            $"{name}: anti-roll skipped; {reason}",
            this);
#endif
    }
}
