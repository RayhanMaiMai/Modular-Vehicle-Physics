using UnityEngine;

[CreateAssetMenu(fileName = "NewCarProfile", menuName = "Car Physics/Car Profile")]
public class CarProfile : ScriptableObject
{
    [Header("Engine & Speed")]
    public float topSpeed = 15f;

    [Tooltip("Whole-vehicle longitudinal motor-force command budget in force-like Unity units, before gearbox and power-curve modulation.")]
    public float maxAccelerationForce = 5f;

    [Tooltip("Whole-vehicle service-brake cap in force-like Unity units, before input and ABS modulation.")]
    public float maxBrakingForce = 8f;
    
    [Tooltip("Sumbu X = Rasio Kecepatan (0-1). Sumbu Y = Torsi (0-1)")]
    public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
}
