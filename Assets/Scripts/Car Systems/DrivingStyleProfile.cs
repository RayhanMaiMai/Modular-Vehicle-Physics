using UnityEngine;

[CreateAssetMenu(fileName = "NewDrivingStyleProfile", menuName = "DriveFeel/Driving Style Profile")]
public class DrivingStyleProfile : ScriptableObject
{
    [Header("Steering Override")]
    [Tooltip("When disabled, assigning this asset does not change the vehicle's serialized steering settings.")]
    [SerializeField] private bool overrideSteering;

    [Tooltip("Maximum steering angle at low speed, in degrees. Zero is allowed.")]
    [SerializeField, Min(0f)] private float lowSpeedSteerAngle = 35f;

    [Tooltip("Maximum steering angle at or above the steering reference speed, in degrees. Zero is allowed.")]
    [SerializeField, Min(0f)] private float highSpeedSteerAngle = 10f;

    [Tooltip("Vehicle speed at which the high-speed steering angle is fully applied. Must be greater than zero.")]
    [SerializeField, Min(0.01f)] private float highSpeedThreshold = 20f;

    [Tooltip("SmoothDamp response time for steering, in seconds. Zero is allowed for an immediate response.")]
    [SerializeField, Min(0f)] private float steerSmoothTime = 0.15f;

    [Header("Tire Response Override")]
    [Tooltip("When disabled, every tire keeps its own serialized grip curve, maximum slip speed, and grip modifier.")]
    [SerializeField] private bool overrideTireResponse;

    [Tooltip("Grip multiplier applied to both front tires when the tire-response override is enabled.")]
    [SerializeField, Range(0f, 2f)] private float frontGripModifier = 1f;

    [Tooltip("Lateral slip speed that maps to the end of the front grip curve. Must be greater than zero.")]
    [SerializeField, Min(0.01f)] private float frontMaxSlipSpeed = 8f;

    [Tooltip("Front lateral-grip response over normalized slip. Copied per vehicle tire during initialization.")]
    [SerializeField] private AnimationCurve frontGripCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f);

    [Tooltip("Grip multiplier applied to both rear tires when the tire-response override is enabled.")]
    [SerializeField, Range(0f, 2f)] private float rearGripModifier = 1f;

    [Tooltip("Lateral slip speed that maps to the end of the rear grip curve. Must be greater than zero.")]
    [SerializeField, Min(0.01f)] private float rearMaxSlipSpeed = 8f;

    [Tooltip("Rear lateral-grip response over normalized slip. Copied per vehicle tire during initialization.")]
    [SerializeField] private AnimationCurve rearGripCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f);

    [Header("Handbrake Override")]
    [Tooltip("When disabled, both rear tires keep their own serialized handbrake values.")]
    [SerializeField] private bool overrideHandbrake;

    [Tooltip("Rear lateral-grip multiplier while the handbrake is active.")]
    [SerializeField, Range(0f, 1f)] private float rearHandbrakeGripMultiplier = 0.8f;

    [Tooltip("Per-rear-tire longitudinal handbrake drag cap in force-like Unity units.")]
    [SerializeField, Min(0f)] private float rearHandbrakeDragForce = 15f;

    [Header("Suspension Response Override")]
    [Tooltip("When disabled, every tire keeps its own serialized spring and damper strengths.")]
    [SerializeField] private bool overrideSuspension;

    [Tooltip("Spring strength applied to both front tires. Must be greater than zero.")]
    [SerializeField, Min(0.0001f)] private float frontSpringStrength = 250f;

    [Tooltip("Damper strength applied to both front tires. Zero is allowed.")]
    [SerializeField, Min(0f)] private float frontDamperStrength = 45f;

    [Tooltip("Spring strength applied to both rear tires. Must be greater than zero.")]
    [SerializeField, Min(0.0001f)] private float rearSpringStrength = 250f;

    [Tooltip("Damper strength applied to both rear tires. Zero is allowed.")]
    [SerializeField, Min(0f)] private float rearDamperStrength = 45f;

    [Header("Anti-Roll Response Override")]
    [Tooltip("When disabled, both anti-roll bars keep their authored strengths and force caps. This never changes the prefab-owned enable gates.")]
    [SerializeField] private bool overrideAntiRoll;

    [Tooltip("Requested front anti-roll strength. Zero is allowed.")]
    [SerializeField, Min(0f)] private float frontAntiRollStrength;

    [Tooltip("Requested front per-side force cap. It may not exceed the vehicle's authored front cap.")]
    [SerializeField, Min(0f)] private float frontAntiRollMaxForcePerSide;

    [Tooltip("Requested rear anti-roll strength. Zero is allowed.")]
    [SerializeField, Min(0f)] private float rearAntiRollStrength;

    [Tooltip("Requested rear per-side force cap. It may not exceed the vehicle's authored rear cap.")]
    [SerializeField, Min(0f)] private float rearAntiRollMaxForcePerSide;

    public bool OverrideSteering => overrideSteering;
    public float LowSpeedSteerAngle => lowSpeedSteerAngle;
    public float HighSpeedSteerAngle => highSpeedSteerAngle;
    public float HighSpeedThreshold => highSpeedThreshold;
    public float SteerSmoothTime => steerSmoothTime;
    public bool OverrideTireResponse => overrideTireResponse;
    public float FrontGripModifier => frontGripModifier;
    public float FrontMaxSlipSpeed => frontMaxSlipSpeed;
    public AnimationCurve FrontGripCurve => frontGripCurve;
    public float RearGripModifier => rearGripModifier;
    public float RearMaxSlipSpeed => rearMaxSlipSpeed;
    public AnimationCurve RearGripCurve => rearGripCurve;
    public bool OverrideHandbrake => overrideHandbrake;
    public float RearHandbrakeGripMultiplier => rearHandbrakeGripMultiplier;
    public float RearHandbrakeDragForce => rearHandbrakeDragForce;
    public bool OverrideSuspension => overrideSuspension;
    public float FrontSpringStrength => frontSpringStrength;
    public float FrontDamperStrength => frontDamperStrength;
    public float RearSpringStrength => rearSpringStrength;
    public float RearDamperStrength => rearDamperStrength;
    public bool OverrideAntiRoll => overrideAntiRoll;
    public float FrontAntiRollStrength => frontAntiRollStrength;
    public float FrontAntiRollMaxForcePerSide => frontAntiRollMaxForcePerSide;
    public float RearAntiRollStrength => rearAntiRollStrength;
    public float RearAntiRollMaxForcePerSide => rearAntiRollMaxForcePerSide;
}
