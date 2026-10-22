using UnityEngine;

/// <summary>
/// Som de motor em loop. O pitch (e o volume) sobem com a velocidade do kart.
/// Colocar no kart (ou num filho) e atribuir um AudioClip em loop.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class KartEngineSound : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KartController kart;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private AudioSource source;

    [Header("Som")]
    [SerializeField] private AudioClip engineClip;

    [Header("Pitch")]
    [SerializeField, Range(0.1f, 3f)] private float idlePitch = 0.8f;
    [SerializeField, Range(0.1f, 3f)] private float maxPitch = 2.2f;
    [Tooltip("Velocidade (m/s) a que o pitch chega ao maximo. Igual a maxForwardSpeed do KartMotor.")]
    [SerializeField, Min(0.1f)] private float referenceSpeed = 35f;
    [Tooltip("Curva do pitch: X = velocidade / referenceSpeed, Y = 0..1 entre idlePitch e maxPitch.")]
    [SerializeField] private AnimationCurve pitchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float idleVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.8f;
    [Tooltip("Multiplicador do volume quando o kart esta no ar.")]
    [SerializeField, Range(0f, 1f)] private float airborneVolumeMultiplier = 0.6f;

    [Header("Suavizacao")]
    [SerializeField, Min(0.1f)] private float pitchSharpness = 6f;
    [SerializeField, Min(0.1f)] private float volumeSharpness = 8f;

    [Header("3D")]
    [Tooltip("0 = som 2D (igual para todos), 1 = totalmente 3D.")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;

    private float currentPitch;
    private float currentVolume;

    private void Reset()
    {
        kart = GetComponentInParent<KartController>();
        rb = GetComponentInParent<Rigidbody>();
        source = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (kart == null) kart = GetComponentInParent<KartController>();
        if (rb == null) rb = GetComponentInParent<Rigidbody>();
        if (source == null) source = GetComponent<AudioSource>();

        source.clip = engineClip != null ? engineClip : source.clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;

        currentPitch = idlePitch;
        currentVolume = idleVolume;
        source.pitch = currentPitch;
        source.volume = currentVolume;
    }

    private void OnEnable()
    {
        if (source != null && source.clip != null && !source.isPlaying)
            source.Play();
    }

    private void OnDisable()
    {
        if (source != null)
            source.Stop();
    }

    private void Update()
    {
        if (rb == null || source == null)
            return;

        bool grounded = kart == null || kart.IsGrounded;

        // Velocidade ao longo do chao: ignora a componente vertical (saltos/quedas).
        float speed = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
        float t = Mathf.Clamp01(speed / referenceSpeed);

        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, pitchCurve.Evaluate(t));
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, t);
        if (!grounded)
            targetVolume *= airborneVolumeMultiplier;

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, 1f - Mathf.Exp(-pitchSharpness * Time.deltaTime));
        currentVolume = Mathf.Lerp(currentVolume, targetVolume, 1f - Mathf.Exp(-volumeSharpness * Time.deltaTime));

        source.pitch = currentPitch;
        source.volume = currentVolume;
    }
}
