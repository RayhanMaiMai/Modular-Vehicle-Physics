using UnityEngine;

namespace Possession
{
    public interface IPossessable
    {
        Transform CameraTarget { get; }
        Vector3 CameraOffset { get; }
        
        void OnPossess();
        void OnUnpossess();
        void ProcessInput(); // Otoritas eksekusi dipindahkan ke sini
    }
}
