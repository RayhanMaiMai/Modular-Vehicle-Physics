using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TiltShiftCamera : MonoBehaviour
{
    [Header("Camera Tracking")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 15f, -15f);
    public float smoothSpeed = 5f;

    [Header("Tilt-Shift DoF")]
    public Volume postProcessVolume;
    
    private DepthOfField dofComponent;

    void Start()
    {
        if (postProcessVolume != null && postProcessVolume.profile.TryGet(out dofComponent))
        {
            Debug.Log("Depth of Field berhasil diakses.");
        }
        else
        {
            Debug.LogWarning("Depth of Field tidak ditemukan di Volume Profile!");
        }
    }

    // Eksekusi fungsi ini dari PossessionManager
    public void SetCameraProfile(Transform newTarget, Vector3 newOffset)
    {
        target = newTarget;
        offset = newOffset;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Smooth Follow Camera (Lerp akan menangani transisi dengan sendirinya)
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 2. Dinamik DoF Auto-Focus
        if (dofComponent != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            dofComponent.focusDistance.value = distanceToTarget;
        }
    }
}