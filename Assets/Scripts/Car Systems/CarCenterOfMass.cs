using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarCenterOfMass : MonoBehaviour
{
    [Tooltip("Geser nilai Y ke negatif untuk menurunkan titik berat ke as roda (misal: -0.5)")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Memaksa titik keseimbangan mobil berada di bawah
        rb.centerOfMass = centerOfMassOffset;
    }

    private void OnDrawGizmos()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        // Menggambar bola merah di Scene View untuk melihat di mana titik beratnya
        Gizmos.color = Color.red;
        Vector3 comWorldPosition = transform.TransformPoint(Application.isPlaying ? rb.centerOfMass : centerOfMassOffset);
        Gizmos.DrawSphere(comWorldPosition, 0.1f);
    }
}