using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

public class EnemyFootstepSystem : MonoBehaviour
{
    [Header("Настройки звуков")]
    public AudioSource footstepAudioSource;
    public float stepInterval = 0.6f;           // Интервал между шагами при ходьбе
    public float chaseStepInterval = 0.4f;      // Интервал между шагами при преследовании

    [Header("Surface Detection")]
    public float raycastDistance = 1.5f;
    public LayerMask groundLayerMask = ~0;

    [Header("Surface Sounds")]
    public SurfaceSoundList[] surfaceSounds;

    [Header("Настройки громкости")]
    [Range(0f, 2f)]
    public float footstepVolume = 0.5f;
    [Range(0f, 2f)]
    public float pitchMin = 0.9f;
    [Range(0f, 2f)]
    public float pitchMax = 1.1f;
    public float soundMaxDistance = 20f;
    public AudioMixerGroup sfxGroup;

    private EnemyAI enemyAI;
    private NavMeshAgent agent;
    private CharacterController characterController;
    private Animator animator;
    private float stepTimer = 0f;
    private bool isGrounded = false;
    private int lastPlayedIndex = -1;
    private bool isMoving = false;
    private bool isChasing = false;

    [System.Serializable]
    public class SurfaceSoundList
    {
        public string surfaceTag = "Default";
        public AudioClip[] footstepClips;
        public float volumeMultiplier = 1f;
        public float pitchMin = 0.9f;
        public float pitchMax = 1.1f;
    }

    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        SetupAudioSource();

        if (surfaceSounds.Length == 0)
        {
            Debug.LogWarning("No surface sounds configured! Adding default surfaces.");
            AddDefaultSurfaces();
        }
    }

    void SetupAudioSource()
    {
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Настраиваем AudioSource для 3D звука
        footstepAudioSource.spatialBlend = 1f;
        footstepAudioSource.dopplerLevel = 0f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        footstepAudioSource.maxDistance = soundMaxDistance;
        footstepAudioSource.minDistance = 1f;
        footstepAudioSource.volume = footstepVolume;
        footstepAudioSource.pitch = 1f;
        footstepAudioSource.outputAudioMixerGroup = sfxGroup;
    }

    void AddDefaultSurfaces()
    {
        List<SurfaceSoundList> surfaces = new List<SurfaceSoundList>();

        SurfaceSoundList defaultSurface = new SurfaceSoundList();
        defaultSurface.surfaceTag = "Default";
        defaultSurface.footstepClips = new AudioClip[0];
        surfaces.Add(defaultSurface);

        SurfaceSoundList terrainSurface = new SurfaceSoundList();
        terrainSurface.surfaceTag = "Terrain";
        terrainSurface.footstepClips = new AudioClip[0];
        surfaces.Add(terrainSurface);

        SurfaceSoundList groundSurface = new SurfaceSoundList();
        groundSurface.surfaceTag = "Ground";
        groundSurface.footstepClips = new AudioClip[0];
        surfaces.Add(groundSurface);

        SurfaceSoundList grassSurface = new SurfaceSoundList();
        grassSurface.surfaceTag = "Grass";
        grassSurface.footstepClips = new AudioClip[0];
        surfaces.Add(grassSurface);

        SurfaceSoundList woodSurface = new SurfaceSoundList();
        woodSurface.surfaceTag = "Wood";
        woodSurface.footstepClips = new AudioClip[0];
        surfaces.Add(woodSurface);

        SurfaceSoundList stoneSurface = new SurfaceSoundList();
        stoneSurface.surfaceTag = "Stone";
        stoneSurface.footstepClips = new AudioClip[0];
        surfaces.Add(stoneSurface);

        SurfaceSoundList metalSurface = new SurfaceSoundList();
        metalSurface.surfaceTag = "Metal";
        metalSurface.footstepClips = new AudioClip[0];
        surfaces.Add(metalSurface);

        surfaceSounds = surfaces.ToArray();
        Debug.Log("Added default surfaces to EnemyFootstepSystem");
    }

    void Update()
    {
        if (enemyAI == null) return;

        // Проверяем, жив ли враг
        if (enemyAI.IsDead()) return;

        // Проверяем grounded (если есть CharacterController)
        if (characterController != null)
        {
            isGrounded = characterController.isGrounded;
        }
        else
        {
            isGrounded = true; // Если нет CharacterController, считаем что на земле
        }

        // Определяем состояние движения
        UpdateMovementState();

        if (!isGrounded || !isMoving)
        {
            stepTimer = 0f;
            return;
        }

        // Выбираем интервал в зависимости от состояния
        float currentInterval = isChasing ? chaseStepInterval : stepInterval;

        stepTimer += Time.deltaTime;

        if (stepTimer >= currentInterval)
        {
            stepTimer = 0f;
            PlayFootstep();
        }
    }

    void UpdateMovementState()
    {
        // Определяем движется ли враг
        if (agent != null)
        {
            isMoving = agent.velocity.magnitude > 0.1f;
        }
        else if (animator != null)
        {
            isMoving = animator.GetBool("IsMoving");
        }
        else
        {
            isMoving = false;
        }

        // Определяем преследует ли враг
        if (animator != null)
        {
            isChasing = animator.GetBool("IsChasing");
        }
    }

    void PlayFootstep()
    {
        string surfaceTag = DetectSurface();

        AudioClip clip = GetRandomClipForSurface(surfaceTag);

        if (clip == null)
        {
            clip = GetRandomClipForSurface("Default");
        }

        if (clip == null)
        {
            return;
        }

        PlayFootstepSound(clip, surfaceTag);
    }

    string DetectSurface()
    {
        if (characterController == null) return "Default";

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayerMask))
        {
            if (!string.IsNullOrEmpty(hit.collider.tag))
            {
                return hit.collider.tag;
            }

            string objectName = hit.collider.gameObject.name.ToLower();
            if (objectName.Contains("grass")) return "Grass";
            if (objectName.Contains("wood")) return "Wood";
            if (objectName.Contains("stone")) return "Stone";
            if (objectName.Contains("metal")) return "Metal";
            if (objectName.Contains("terrain")) return "Terrain";
            if (objectName.Contains("ground")) return "Ground";

            return "Default";
        }

        return "Default";
    }

    AudioClip GetRandomClipForSurface(string surfaceTag)
    {
        foreach (SurfaceSoundList surface in surfaceSounds)
        {
            if (surface.surfaceTag == surfaceTag)
            {
                if (surface.footstepClips.Length == 0)
                {
                    return null;
                }

                int randomIndex;
                do
                {
                    randomIndex = Random.Range(0, surface.footstepClips.Length);
                }
                while (surface.footstepClips.Length > 1 && randomIndex == lastPlayedIndex);

                lastPlayedIndex = randomIndex;
                return surface.footstepClips[randomIndex];
            }
        }

        return null;
    }

    void PlayFootstepSound(AudioClip clip, string surfaceTag)
    {
        if (clip == null || footstepAudioSource == null) return;

        // Находим настройки поверхности
        float volume = footstepVolume;
        float pitchMinLocal = pitchMin;
        float pitchMaxLocal = pitchMax;

        foreach (SurfaceSoundList surface in surfaceSounds)
        {
            if (surface.surfaceTag == surfaceTag)
            {
                volume = footstepVolume * surface.volumeMultiplier;
                pitchMinLocal = surface.pitchMin;
                pitchMaxLocal = surface.pitchMax;
                break;
            }
        }

        // Если преследует - делаем шаги громче и быстрее
        if (isChasing)
        {
            volume *= 1.2f;
            pitchMinLocal *= 1.05f;
            pitchMaxLocal *= 1.05f;
        }

        footstepAudioSource.volume = Mathf.Clamp01(volume * Random.Range(0.8f, 1.2f));
        footstepAudioSource.pitch = Random.Range(pitchMinLocal, pitchMaxLocal);
        footstepAudioSource.PlayOneShot(clip);
    }

    public void AddSurface(string tag, AudioClip[] clips, float volume = 1f, float pitchMin = 0.9f, float pitchMax = 1.1f)
    {
        List<SurfaceSoundList> surfaces = new List<SurfaceSoundList>(surfaceSounds);

        SurfaceSoundList newSurface = new SurfaceSoundList();
        newSurface.surfaceTag = tag;
        newSurface.footstepClips = clips;
        newSurface.volumeMultiplier = volume;
        newSurface.pitchMin = pitchMin;
        newSurface.pitchMax = pitchMax;

        surfaces.Add(newSurface);
        surfaceSounds = surfaces.ToArray();
    }

    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.green;
    //    Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, Vector3.down * raycastDistance);
    //    Gizmos.DrawWireSphere(transform.position - Vector3.up * (raycastDistance - 0.5f), 0.2f);

    //    Gizmos.color = new Color(0, 1, 0, 0.2f);
    //    Gizmos.DrawWireSphere(transform.position, soundMaxDistance);
    //}
}