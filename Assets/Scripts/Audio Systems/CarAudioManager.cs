using UnityEngine;

public class CarAudioManager : MonoBehaviour
{
    [Header("Audio Sources (Unity Native)")]
    [Tooltip("Gunakan 4 AudioSource terpisah di objek mobil. Pastikan Spatial Blend = 1 (3D)")]
    public AudioSource idleSource;
    public AudioSource highRpmSource; 
    public AudioSource sfxSource;
    public AudioSource hornSource; 

    [Header("Audio Clips")]
    public AudioClip startUpClip;
    public AudioClip idleClip;
    public AudioClip highRpmClip; 
    public AudioClip accelRevClip; 
    public AudioClip handbrakeClip;
    public AudioClip hornClip;

    [Header("Engine Tuning")]
    public float minPitch = 0.8f;
    public float maxPitch = 2.0f;
    public float pitchLerpSpeed = 5f;
    
    [Tooltip("Di rasio kecepatan berapa (0-1) suara High RPM mulai mendominasi?")]
    [Range(0.1f, 0.9f)] public float rpmCrossfadeThreshold = 0.4f;

    private bool isEngineRunning = false;

    private void Start()
    {
        // Setup dasar AudioSource untuk loop
        idleSource.clip = idleClip;
        idleSource.loop = true;
        
        highRpmSource.clip = highRpmClip;
        highRpmSource.loop = true;
        highRpmSource.volume = 0f; // Matikan volume torsi atas di awal

        hornSource.clip = hornClip;
        hornSource.loop = true;
    }

    public void TriggerEngineStart()
    {
        if (isEngineRunning) return;
        
        isEngineRunning = true;
        sfxSource.PlayOneShot(startUpClip);
        
        // Mulai loop mesin setelah suara stater hampir selesai
        idleSource.PlayDelayed(startUpClip.length * 0.5f);
        highRpmSource.PlayDelayed(startUpClip.length * 0.5f);
    }

    public void StopEngine()
    {
        isEngineRunning = false;
        idleSource.Stop();
        highRpmSource.Stop();
        hornSource.Stop();
    }

    public void TriggerAccelRev()
    {
        if (!isEngineRunning) return;
        sfxSource.PlayOneShot(accelRevClip); // Suara gerungan awal
    }

    public void UpdateEngineParameters(float normalizedSpeed, float accelInput)
    {
        if (!isEngineRunning) return;

        // 1. Kalkulasi Pitch (Simulasi Putaran Mesin)
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, normalizedSpeed);
        if (Mathf.Abs(accelInput) > 0.01f) targetPitch += 0.2f; // Ekstra erangan saat ditekan gas

        idleSource.pitch = Mathf.Lerp(idleSource.pitch, targetPitch, Time.deltaTime * pitchLerpSpeed);
        highRpmSource.pitch = idleSource.pitch;

        // 2. Crossfade Volume (Simulasi Perpindahan Gigi / Beban Atas)
        float idleTargetVol = normalizedSpeed < rpmCrossfadeThreshold ? 1f : 1f - normalizedSpeed;
        float highRpmTargetVol = normalizedSpeed > (rpmCrossfadeThreshold - 0.2f) ? normalizedSpeed : 0f;

        idleSource.volume = Mathf.Lerp(idleSource.volume, idleTargetVol, Time.deltaTime * 3f);
        highRpmSource.volume = Mathf.Lerp(highRpmSource.volume, highRpmTargetVol, Time.deltaTime * 3f);
    }

    public void TriggerHandbrake()
    {
        sfxSource.PlayOneShot(handbrakeClip);
    }

    public void SetHorn(bool isHonking)
    {
        if (isHonking && !hornSource.isPlaying) hornSource.Play();
        else if (!isHonking && hornSource.isPlaying) hornSource.Stop();
    }
}