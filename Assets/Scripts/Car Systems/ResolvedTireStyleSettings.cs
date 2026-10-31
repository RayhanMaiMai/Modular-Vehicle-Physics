using UnityEngine;

public readonly struct ResolvedTireStyleSettings
{
    private readonly AnimationCurve gripCurve;

    public float MaxSlipSpeed { get; }
    public float GripModifier { get; }
    public float HandbrakeGripMultiplier { get; }
    public float HandbrakeDragForce { get; }
    public int GripCurveKeyCount => gripCurve != null ? gripCurve.length : 0;

    public ResolvedTireStyleSettings(
        AnimationCurve gripCurve,
        float maxSlipSpeed,
        float gripModifier,
        float handbrakeGripMultiplier,
        float handbrakeDragForce)
    {
        this.gripCurve = gripCurve;
        MaxSlipSpeed = maxSlipSpeed;
        GripModifier = gripModifier;
        HandbrakeGripMultiplier = handbrakeGripMultiplier;
        HandbrakeDragForce = handbrakeDragForce;
    }

    public ResolvedTireStyleSettings WithTireResponse(
        AnimationCurve gripCurve,
        float maxSlipSpeed,
        float gripModifier)
    {
        return new ResolvedTireStyleSettings(
            gripCurve,
            maxSlipSpeed,
            gripModifier,
            HandbrakeGripMultiplier,
            HandbrakeDragForce);
    }

    public ResolvedTireStyleSettings WithHandbrake(float gripMultiplier, float dragForce)
    {
        return new ResolvedTireStyleSettings(
            gripCurve,
            MaxSlipSpeed,
            GripModifier,
            gripMultiplier,
            dragForce);
    }

    public float EvaluateGrip(float normalizedSlip)
    {
        return gripCurve.Evaluate(normalizedSlip);
    }
}

public readonly struct ResolvedVehicleTireStyleSettings
{
    public ResolvedTireStyleSettings FrontLeft { get; }
    public ResolvedTireStyleSettings FrontRight { get; }
    public ResolvedTireStyleSettings RearLeft { get; }
    public ResolvedTireStyleSettings RearRight { get; }

    public ResolvedVehicleTireStyleSettings(
        ResolvedTireStyleSettings frontLeft,
        ResolvedTireStyleSettings frontRight,
        ResolvedTireStyleSettings rearLeft,
        ResolvedTireStyleSettings rearRight)
    {
        FrontLeft = frontLeft;
        FrontRight = frontRight;
        RearLeft = rearLeft;
        RearRight = rearRight;
    }
}

public static class TireStyleSettingsResolver
{
    public static ResolvedTireStyleSettings CaptureBaseline(
        AnimationCurve gripCurve,
        float maxSlipSpeed,
        float gripModifier,
        float handbrakeGripMultiplier,
        float handbrakeDragForce,
        out bool usedSafeFallback)
    {
        usedSafeFallback = !IsTireResponseValid(gripCurve, maxSlipSpeed, gripModifier)
            || !IsHandbrakeValid(handbrakeGripMultiplier, handbrakeDragForce);

        if (usedSafeFallback)
        {
            return CreateSafeFallback();
        }

        return new ResolvedTireStyleSettings(
            CloneCurve(gripCurve),
            maxSlipSpeed,
            gripModifier,
            handbrakeGripMultiplier,
            handbrakeDragForce);
    }

    public static ResolvedVehicleTireStyleSettings Resolve(
        ResolvedVehicleTireStyleSettings baseline,
        DrivingStyleProfile styleProfile,
        out string diagnostic)
    {
        return Resolve(
            baseline,
            styleProfile,
            out diagnostic,
            out _,
            out _);
    }

    public static ResolvedVehicleTireStyleSettings Resolve(
        ResolvedVehicleTireStyleSettings baseline,
        DrivingStyleProfile styleProfile,
        out string diagnostic,
        out bool tireResponseOverrideApplied,
        out bool handbrakeOverrideApplied)
    {
        ResolvedVehicleTireStyleSettings resolved = baseline;
        diagnostic = null;
        tireResponseOverrideApplied = false;
        handbrakeOverrideApplied = false;

        if (styleProfile == null)
        {
            return resolved;
        }

        if (styleProfile.OverrideTireResponse)
        {
            bool frontIsValid = IsTireResponseValid(
                styleProfile.FrontGripCurve,
                styleProfile.FrontMaxSlipSpeed,
                styleProfile.FrontGripModifier);
            bool rearIsValid = IsTireResponseValid(
                styleProfile.RearGripCurve,
                styleProfile.RearMaxSlipSpeed,
                styleProfile.RearGripModifier);

            if (frontIsValid && rearIsValid)
            {
                tireResponseOverrideApplied = true;
                resolved = new ResolvedVehicleTireStyleSettings(
                    resolved.FrontLeft.WithTireResponse(
                        CloneCurve(styleProfile.FrontGripCurve),
                        styleProfile.FrontMaxSlipSpeed,
                        styleProfile.FrontGripModifier),
                    resolved.FrontRight.WithTireResponse(
                        CloneCurve(styleProfile.FrontGripCurve),
                        styleProfile.FrontMaxSlipSpeed,
                        styleProfile.FrontGripModifier),
                    resolved.RearLeft.WithTireResponse(
                        CloneCurve(styleProfile.RearGripCurve),
                        styleProfile.RearMaxSlipSpeed,
                        styleProfile.RearGripModifier),
                    resolved.RearRight.WithTireResponse(
                        CloneCurve(styleProfile.RearGripCurve),
                        styleProfile.RearMaxSlipSpeed,
                        styleProfile.RearGripModifier));
            }
            else
            {
                diagnostic = $"Driving style '{styleProfile.name}' has an invalid tire-response section. "
                    + "All four tires are using their authored response baselines; correct both axle settings and curves.";
            }
        }

        if (styleProfile.OverrideHandbrake)
        {
            if (IsHandbrakeValid(
                styleProfile.RearHandbrakeGripMultiplier,
                styleProfile.RearHandbrakeDragForce))
            {
                handbrakeOverrideApplied = true;
                resolved = new ResolvedVehicleTireStyleSettings(
                    resolved.FrontLeft,
                    resolved.FrontRight,
                    resolved.RearLeft.WithHandbrake(
                        styleProfile.RearHandbrakeGripMultiplier,
                        styleProfile.RearHandbrakeDragForce),
                    resolved.RearRight.WithHandbrake(
                        styleProfile.RearHandbrakeGripMultiplier,
                        styleProfile.RearHandbrakeDragForce));
            }
            else
            {
                string handbrakeDiagnostic = $"Driving style '{styleProfile.name}' has an invalid handbrake section. "
                    + "Both rear tires are using their authored handbrake baselines; correct the rear multiplier and drag cap.";
                diagnostic = AppendDiagnostic(diagnostic, handbrakeDiagnostic);
            }
        }

        return resolved;
    }

    public static ResolvedTireStyleSettings CreateSafeFallback()
    {
        return new ResolvedTireStyleSettings(
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f),
            8f,
            1f,
            1f,
            15f);
    }

    private static bool IsTireResponseValid(AnimationCurve curve, float maxSlipSpeed, float gripModifier)
    {
        return IsCurveValid(curve)
            && IsFinite(maxSlipSpeed)
            && maxSlipSpeed > 0f
            && IsFinite(gripModifier)
            && gripModifier >= 0f
            && gripModifier <= 2f;
    }

    private static bool IsHandbrakeValid(float gripMultiplier, float dragForce)
    {
        return IsFinite(gripMultiplier)
            && gripMultiplier >= 0f
            && gripMultiplier <= 1f
            && IsFinite(dragForce)
            && dragForce >= 0f;
    }

    private static bool IsCurveValid(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0)
        {
            return false;
        }

        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            Keyframe key = keys[i];
            if (!IsFinite(key.time)
                || !IsFinite(key.value)
                || key.value < 0f
                || float.IsNaN(key.inTangent)
                || float.IsNaN(key.outTangent)
                || !IsFinite(key.inWeight)
                || !IsFinite(key.outWeight))
            {
                return false;
            }
        }

        return true;
    }

    private static AnimationCurve CloneCurve(AnimationCurve source)
    {
        if (source == null)
        {
            return null;
        }

        AnimationCurve clone = new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
        return clone;
    }

    private static string AppendDiagnostic(string existing, string addition)
    {
        return string.IsNullOrEmpty(existing) ? addition : existing + " " + addition;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
