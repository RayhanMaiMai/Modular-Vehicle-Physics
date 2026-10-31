public readonly struct ResolvedSteeringSettings
{
    public float LowSpeedSteerAngle { get; }
    public float HighSpeedSteerAngle { get; }
    public float HighSpeedThreshold { get; }
    public float SteerSmoothTime { get; }

    public ResolvedSteeringSettings(
        float lowSpeedSteerAngle,
        float highSpeedSteerAngle,
        float highSpeedThreshold,
        float steerSmoothTime)
    {
        LowSpeedSteerAngle = lowSpeedSteerAngle;
        HighSpeedSteerAngle = highSpeedSteerAngle;
        HighSpeedThreshold = highSpeedThreshold;
        SteerSmoothTime = steerSmoothTime;
    }
}

public static class SteeringSettingsResolver
{
    private static readonly ResolvedSteeringSettings SafeFallback = new ResolvedSteeringSettings(
        35f,
        10f,
        20f,
        0.15f);

    public static ResolvedSteeringSettings Resolve(
        float baselineLowSpeedSteerAngle,
        float baselineHighSpeedSteerAngle,
        float baselineHighSpeedThreshold,
        float baselineSteerSmoothTime,
        DrivingStyleProfile styleProfile,
        out string diagnostic)
    {
        return Resolve(
            baselineLowSpeedSteerAngle,
            baselineHighSpeedSteerAngle,
            baselineHighSpeedThreshold,
            baselineSteerSmoothTime,
            styleProfile,
            out diagnostic,
            out _);
    }

    public static ResolvedSteeringSettings Resolve(
        float baselineLowSpeedSteerAngle,
        float baselineHighSpeedSteerAngle,
        float baselineHighSpeedThreshold,
        float baselineSteerSmoothTime,
        DrivingStyleProfile styleProfile,
        out string diagnostic,
        out bool overrideApplied)
    {
        ResolvedSteeringSettings baseline = new ResolvedSteeringSettings(
            baselineLowSpeedSteerAngle,
            baselineHighSpeedSteerAngle,
            baselineHighSpeedThreshold,
            baselineSteerSmoothTime);

        diagnostic = null;
        overrideApplied = false;
        bool baselineUsesSafeFallback = false;

        if (!IsValid(baseline))
        {
            baselineUsesSafeFallback = true;
            baseline = SafeFallback;
        }

        if (styleProfile == null || !styleProfile.OverrideSteering)
        {
            if (baselineUsesSafeFallback)
            {
                diagnostic = "The serialized steering baseline contains a non-finite or out-of-range value. "
                    + "Using the safe code fallback; correct the CarController steering fields in the Inspector.";
            }

            return baseline;
        }

        ResolvedSteeringSettings styleOverride = new ResolvedSteeringSettings(
            styleProfile.LowSpeedSteerAngle,
            styleProfile.HighSpeedSteerAngle,
            styleProfile.HighSpeedThreshold,
            styleProfile.SteerSmoothTime);

        if (!IsValid(styleOverride))
        {
            diagnostic = $"Driving style '{styleProfile.name}' contains a non-finite or out-of-range steering value. "
                + (baselineUsesSafeFallback
                    ? "The vehicle baseline is also invalid, so the safe code fallback is active. Correct both Inspector configurations."
                    : "Using this vehicle's serialized steering baseline instead; correct the style asset in the Inspector.");
            return baseline;
        }

        if (baselineUsesSafeFallback)
        {
            diagnostic = "The serialized steering baseline contains a non-finite or out-of-range value. "
                + "The valid style override is active, but correct the CarController fields before disabling it.";
        }

        overrideApplied = true;
        return styleOverride;
    }

    private static bool IsValid(ResolvedSteeringSettings settings)
    {
        return IsFiniteAndNonnegative(settings.LowSpeedSteerAngle)
            && IsFiniteAndNonnegative(settings.HighSpeedSteerAngle)
            && IsFinite(settings.HighSpeedThreshold)
            && settings.HighSpeedThreshold > 0f
            && IsFiniteAndNonnegative(settings.SteerSmoothTime);
    }

    private static bool IsFiniteAndNonnegative(float value)
    {
        return IsFinite(value) && value >= 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
