using Possession;
using UnityEngine;

public class PossessionManager : MonoBehaviour
{
    public static PossessionManager Instance { get; private set; }
    public IPossessable CurrentPawn { get; private set; }

    [Header("Initial Setup")]
    public MonoBehaviour startingPawn; 
    
    [Header("Camera System")]
    public TiltShiftCamera mainCamera; // Masukkan TiltShiftCamera di Inspector

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (startingPawn != null && startingPawn is IPossessable pawn)
        {
            Possess(pawn);
        }
    }

    public void Possess(IPossessable newPawn)
    {
        if (CurrentPawn != null)
        {
            CurrentPawn.OnUnpossess();
        }

        CurrentPawn = newPawn;
        CurrentPawn.OnPossess();

        // Instruksikan kamera untuk berpindah target dan mengubah jarak
        if (mainCamera != null)
        {
            mainCamera.SetCameraProfile(CurrentPawn.CameraTarget, CurrentPawn.CameraOffset);
        }
    }

    private void Update()
    {
        if (CurrentPawn != null)
        {
            CurrentPawn.ProcessInput();
        }
    }
}