using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AntiRollBar : MonoBehaviour
{
    [Header("Axle Wheels")]
    public TirePhysics wheelLeft;
    public TirePhysics wheelRight;

    [Header("Anti-Roll Settings")]
    [Tooltip("Kekuatan penyeimbang. Biasanya suspensi depan (Front) butuh nilai lebih besar dari belakang (Rear).")]
    public float antiRollForce = 5000f;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!wheelLeft || !wheelRight) return;

        // 1. Hitung rasio kompresi suspensi (1.0 = Rileks/Melayang, 0.0 = Mentok)
        float travelL = 1f; 
        float travelR = 1f;

        if (wheelLeft.IsGrounded)
            travelL = wheelLeft.CurrentSuspensionDistance / wheelLeft.suspensionRestDist;
        
        if (wheelRight.IsGrounded)
            travelR = wheelRight.CurrentSuspensionDistance / wheelRight.suspensionRestDist;

        // 2. Cari selisih tekanan (Jika mobil miring, nilai ini tidak akan nol)
        float travelDifference = travelL - travelR;

        // 3. Konversi menjadi gaya dorong penyeimbang
        float rollForce = travelDifference * antiRollForce;

        // 4. Aplikasikan gaya ke sasis mobil
        // Jika kiri tertekan (travelL kecil), sasis kiri didorong KE ATAS (-rollForce)
        if (wheelLeft.IsGrounded)
            rb.AddForceAtPosition(wheelLeft.transform.up * -rollForce, wheelLeft.transform.position);

        // Dan sasis kanan ditarik KE BAWAH (rollForce) agar mobil kembali rata
        if (wheelRight.IsGrounded)
            rb.AddForceAtPosition(wheelRight.transform.up * rollForce, wheelRight.transform.position);
    }
}