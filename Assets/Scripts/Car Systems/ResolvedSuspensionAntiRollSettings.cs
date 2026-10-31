public readonly struct ResolvedSuspensionSettings
{
    public float SpringStrength { get; }
    public float DamperStrength { get; }

    public ResolvedSuspensionSettings(float springStrength, float damperStrength)
    {
        SpringStrength = springStrength;
        DamperStrength = damperStrength;
    }
}

public readonly struct ResolvedVehicleSuspensionSettings
{
    public ResolvedSuspensionSettings FrontLeft { get; }
    public ResolvedSuspensionSettings FrontRight { get; }
    public ResolvedSuspensionSettings RearLeft { get; }
    public ResolvedSuspensionSettings RearRight { get; }

    public ResolvedVehicleSuspensionSettings(
        ResolvedSuspensionSettings frontLeft,
        ResolvedSuspensionSettings frontRight,
        ResolvedSuspensionSettings rearLeft,
        ResolvedSuspensionSettings rearRight)
    {
        FrontLeft = frontLeft;
        FrontRight = frontRight;
        RearLeft = rearLeft;
        RearRight = rearRight;
    }
}

public static class SuspensionSettingsResolver
{
    public static ResolvedSuspensionSettings CaptureBaseline(
        float springStrength,
        float damperStrength,
        out bool usedSafeFallback)
    {
        usedSafeFallback = !IsValid(springStrength, damperStrength);
        return usedSafeFallback
            ? new ResolvedSuspensionSettings(250f, 45f)
            : new ResolvedSuspensionSettings(springStrength, damperStrength);
    }

    public static ResolvedVehicleSuspensionSettings Resolve(
        ResolvedVehicleSuspensionSettings baseline,
        DrivingStyleProfile styleProfile,
        out string diagnostic,
        out bool overrideApplied)
    {
        diagnostic = null;
        overrideApplied = false;

        if (styleProfile == null || !styleProfile.OverrideSuspension)
        {
            return baseline;
        }

        bool frontIsValid = IsValid(
            styleProfile.FrontSpringStrength,
            styleProfile.FrontDamperStrength);
        bool rearIsValid = IsValid(
            styleProfile.RearSpringStrength,
            styleProfile.RearDamperStrength);

        if (!frontIsValid || !rearIsValid)
        {
            diagnostic = $"Driving style '{styleProfile.name}' has an invalid suspension section. "
                + "All four tires are using their independently authored spring and damper baselines.";
            return baseline;
        }

        overrideApplied = true;
        return new ResolvedVehicleSuspensionSettings(
            new ResolvedSuspensionSettings(
                styleProfile.FrontSpringStrength,
                styleProfile.FrontDamperStrength),
            new ResolvedSuspensionSettings(
                styleProfile.FrontSpringStrength,
                styleProfile.FrontDamperStrength),
            new ResolvedSuspensionSettings(
                styleProfile.RearSpringStrength,
                styleProfile.RearDamperStrength),
            new ResolvedSuspensionSettings(
                styleProfile.RearSpringStrength,
                styleProfile.RearDamperStrength));
    }

    public static ResolvedSuspensionSettings CreateSafeFallback()
    {
        return new ResolvedSuspensionSettings(250f, 45f);
    }

    private static bool IsValid(float springStrength, float damperStrength)
    {
        return IsFinite(springStrength)
            && springStrength > 0f
            && IsFinite(damperStrength)
            && damperStrength >= 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public readonly struct ResolvedAntiRollSettings
{
    public bool GateEnabled { get; }
    public float Strength { get; }
    public float MaxForcePerSide { get; }

    public ResolvedAntiRollSettings(bool gateEnabled, float strength, float maxForcePerSide)
    {
        GateEnabled = gateEnabled;
        Strength = strength;
        MaxForcePerSide = maxForcePerSide;
    }

    public ResolvedAntiRollSettings WithResponse(float strength, float maxForcePerSide)
    {
        return new ResolvedAntiRollSettings(GateEnabled, strength, maxForcePerSide);
    }
}

public readonly struct ResolvedVehicleAntiRollSettings
{
    public ResolvedAntiRollSettings Front { get; }
    public ResolvedAntiRollSettings Rear { get; }

    public ResolvedVehicleAntiRollSettings(
        ResolvedAntiRollSettings front,
        ResolvedAntiRollSettings rear)
    {
        Front = front;
        Rear = rear;
    }
}

public static class AntiRollSettingsResolver
{
    public static ResolvedVehicleAntiRollSettings Resolve(
        ResolvedVehicleAntiRollSettings baseline,
        bool hasUnambiguousLinkage,
        DrivingStyleProfile styleProfile,
        out string diagnostic,
        out bool overrideApplied)
    {
        diagnostic = null;
        overrideApplied = false;

        if (styleProfile == null || !styleProfile.OverrideAntiRoll)
        {
            return baseline;
        }

        if (!hasUnambiguousLinkage)
        {
            diagnostic = $"Driving style '{styleProfile.name}' requested anti-roll override, but exact front/rear linkage to the controller tires and root chassis is missing or ambiguous. "
                + "Both bars retain their authored behavior; correct the component references without guessing by name or hierarchy order.";
            return baseline;
        }

        if (!IsAuthoredBaselineValid(baseline.Front) || !IsAuthoredBaselineValid(baseline.Rear))
        {
            diagnostic = $"Driving style '{styleProfile.name}' requested anti-roll override, but an authored strength or force cap is invalid. "
                + "Both bars retain their authored behavior; correct the prefab configuration first.";
            return baseline;
        }

        bool requestedValuesAreValid = IsRequestedPairValid(
                styleProfile.FrontAntiRollStrength,
                styleProfile.FrontAntiRollMaxForcePerSide)
            && IsRequestedPairValid(
                styleProfile.RearAntiRollStrength,
                styleProfile.RearAntiRollMaxForcePerSide);

        if (!requestedValuesAreValid)
        {
            diagnostic = $"Driving style '{styleProfile.name}' has an invalid anti-roll section. "
                + "Strengths and per-side force caps must be finite and nonnegative; both bars retain their authored behavior.";
            return baseline;
        }

        if (styleProfile.FrontAntiRollMaxForcePerSide > baseline.Front.MaxForcePerSide
            || styleProfile.RearAntiRollMaxForcePerSide > baseline.Rear.MaxForcePerSide)
        {
            diagnostic = $"Driving style '{styleProfile.name}' requests an anti-roll force cap above this vehicle's authored axle cap. "
                + "The entire anti-roll override was rejected; reduce the requested caps instead of increasing the safety ceiling.";
            return baseline;
        }

        overrideApplied = true;
        return new ResolvedVehicleAntiRollSettings(
            baseline.Front.WithResponse(
                styleProfile.FrontAntiRollStrength,
                styleProfile.FrontAntiRollMaxForcePerSide),
            baseline.Rear.WithResponse(
                styleProfile.RearAntiRollStrength,
                styleProfile.RearAntiRollMaxForcePerSide));
    }

    public static ResolvedAntiRollSettings CreateUnavailableBaseline()
    {
        return new ResolvedAntiRollSettings(false, 0f, 0f);
    }

    private static bool IsAuthoredBaselineValid(ResolvedAntiRollSettings settings)
    {
        return IsRequestedPairValid(settings.Strength, settings.MaxForcePerSide);
    }

    private static bool IsRequestedPairValid(float strength, float maxForcePerSide)
    {
        return IsFinite(strength)
            && strength >= 0f
            && IsFinite(maxForcePerSide)
            && maxForcePerSide >= 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
