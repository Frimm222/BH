using UnityEngine;
using System.Collections.Generic;

public class FootstepSystem : MonoBehaviour
{
    [Header("Настройки звуков")]
    public AudioSource footstepAudioSource;
    public float stepInterval = 0.5f;           // Интервал между шагами при ходьбе
    public float runStepInterval = 0.3f;        // Интервал между шагами при беге
    public float sprintStepInterval = 0.2f;     // Интервал между шагами при спринте

    [Header("Surface Detection")]
    public float raycastDistance = 1.5f;
    public LayerMask groundLayerMask = ~0;

    [Header("Surface Sounds")]
    public SurfaceSoundList[] surfaceSounds;

    private PlayerController playerController;
    private CharacterController characterController;
    private float stepTimer = 0f;
    private bool isGrounded = false;
    private bool isMoving = false;
    private bool isRunning = false;
    private bool isSprinting = false;
    private string currentSurfaceTag = "Default";
    private int lastPlayedIndex = -1;
    private bool hasPlayedLandingSound = false;

    [System.Serializable]
    public class SurfaceSoundList
    {
        public string surfaceTag = "Default";
        public AudioClip[] footstepClips;
        public AudioClip landingClip;           // Звук приземления
        public float volumeMultiplier = 1f;
        public float pitchMin = 0.9f;
        public float pitchMax = 1.1f;
    }

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Настраиваем AudioSource для шагов
        footstepAudioSource.spatialBlend = 1f;
        footstepAudioSource.dopplerLevel = 0f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        footstepAudioSource.maxDistance = 15f;
        footstepAudioSource.volume = 0.5f;
        footstepAudioSource.pitch = 1f;

        // Проверяем наличие поверхностей
        if (surfaceSounds.Length == 0)
        {
            Debug.LogWarning("No surface sounds configured! Adding default surfaces.");
            AddDefaultSurfaces();
        }
    }

    void AddDefaultSurfaces()
    {
        List<SurfaceSoundList> surfaces = new List<SurfaceSoundList>();

        // Default
        SurfaceSoundList defaultSurface = new SurfaceSoundList();
        defaultSurface.surfaceTag = "Default";
        defaultSurface.footstepClips = new AudioClip[0];
        surfaces.Add(defaultSurface);

        // Terrain
        SurfaceSoundList terrainSurface = new SurfaceSoundList();
        terrainSurface.surfaceTag = "Terrain";
        terrainSurface.footstepClips = new AudioClip[0];
        surfaces.Add(terrainSurface);

        // Ground
        SurfaceSoundList groundSurface = new SurfaceSoundList();
        groundSurface.surfaceTag = "Ground";
        groundSurface.footstepClips = new AudioClip[0];
        surfaces.Add(groundSurface);

        // Grass
        SurfaceSoundList grassSurface = new SurfaceSoundList();
        grassSurface.surfaceTag = "Grass";
        grassSurface.footstepClips = new AudioClip[0];
        surfaces.Add(grassSurface);

        // Wood
        SurfaceSoundList woodSurface = new SurfaceSoundList();
        woodSurface.surfaceTag = "Wood";
        woodSurface.footstepClips = new AudioClip[0];
        surfaces.Add(woodSurface);

        // Stone
        SurfaceSoundList stoneSurface = new SurfaceSoundList();
        stoneSurface.surfaceTag = "Stone";
        stoneSurface.footstepClips = new AudioClip[0];
        surfaces.Add(stoneSurface);

        // Metal
        SurfaceSoundList metalSurface = new SurfaceSoundList();
        metalSurface.surfaceTag = "Metal";
        metalSurface.footstepClips = new AudioClip[0];
        surfaces.Add(metalSurface);

        surfaceSounds = surfaces.ToArray();
        Debug.Log("Added default surfaces to FootstepSystem");
    }

    void Update()
    {
        if (playerController == null || characterController == null) return;

        // 🔥 ИСПОЛЬЗУЕМ ПУБЛИЧНЫЕ МЕТОДЫ ИЗ PLAYER CONTROLLER
        bool wasGrounded = isGrounded;
        isGrounded = characterController.isGrounded;

        // Проверяем приземление
        if (isGrounded && !wasGrounded && !hasPlayedLandingSound)
        {
            PlayLandingSound();
            hasPlayedLandingSound = true;
        }

        if (!isGrounded)
        {
            hasPlayedLandingSound = false;
            stepTimer = 0f;
            return;
        }

        // 🔥 ОПРЕДЕЛЯЕМ СОСТОЯНИЕ ДВИЖЕНИЯ
        bool isMoving = false;
        bool isRunning = false;
        bool isSprinting = false;

        // Получаем состояние из PlayerController
        isMoving = Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;
        isRunning = isMoving && !playerController.IsAiming() && !playerController.IsDashing() && isGrounded;
        isSprinting = isRunning && Input.GetKey(KeyCode.LeftShift);

        // Если не двигаемся - сбрасываем таймер
        if (!isMoving)
        {
            stepTimer = 0f;
            return;
        }

        // Определяем интервал шагов
        float currentInterval = stepInterval;
        if (isSprinting)
        {
            currentInterval = sprintStepInterval;
        }
        else if (isRunning)
        {
            currentInterval = runStepInterval;
        }

        // Обновляем таймер и воспроизводим шаг
        stepTimer += Time.deltaTime;

        if (stepTimer >= currentInterval)
        {
            stepTimer = 0f;
            PlayFootstep();
        }
    }

    void PlayFootstep()
    {
        // Определяем поверхность под ногами
        string surfaceTag = DetectSurface();

        // Находим звуки для этой поверхности
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

    void PlayLandingSound()
    {
        string surfaceTag = DetectSurface();
        AudioClip clip = GetLandingClipForSurface(surfaceTag);

        if (clip == null)
        {
            clip = GetLandingClipForSurface("Default");
        }

        if (clip != null)
        {
            PlayFootstepSound(clip, surfaceTag, true);
            Debug.Log($"Landing sound: {clip.name} on {surfaceTag}");
        }
    }

    string DetectSurface()
    {
        if (characterController == null) return "Default";

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        RaycastHit hit;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, groundLayerMask))
        {
            // Проверяем тег
            if (!string.IsNullOrEmpty(hit.collider.tag))
            {
                return hit.collider.tag;
            }

            // Проверяем имя объекта
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

    AudioClip GetLandingClipForSurface(string surfaceTag)
    {
        foreach (SurfaceSoundList surface in surfaceSounds)
        {
            if (surface.surfaceTag == surfaceTag)
            {
                return surface.landingClip;
            }
        }

        return null;
    }

    void PlayFootstepSound(AudioClip clip, string surfaceTag, bool isLanding = false)
    {
        if (clip == null || footstepAudioSource == null) return;

        // Находим настройки поверхности
        float volume = 0.5f;
        float pitchMin = 0.9f;
        float pitchMax = 1.1f;

        foreach (SurfaceSoundList surface in surfaceSounds)
        {
            if (surface.surfaceTag == surfaceTag)
            {
                volume = surface.volumeMultiplier;
                pitchMin = surface.pitchMin;
                pitchMax = surface.pitchMax;
                break;
            }
        }

        // Для приземления делаем громче и ниже
        if (isLanding)
        {
            volume *= 1.2f;
            pitchMin *= 0.9f;
            pitchMax *= 0.9f;
        }

        footstepAudioSource.volume = Mathf.Clamp01(volume * Random.Range(0.8f, 1.2f));
        footstepAudioSource.pitch = Random.Range(pitchMin, pitchMax);
        footstepAudioSource.PlayOneShot(clip);
    }

    // Метод для добавления новой поверхности
    public void AddSurface(string tag, AudioClip[] clips, AudioClip landingClip = null, float volume = 1f, float pitchMin = 0.9f, float pitchMax = 1.1f)
    {
        List<SurfaceSoundList> surfaces = new List<SurfaceSoundList>(surfaceSounds);

        SurfaceSoundList newSurface = new SurfaceSoundList();
        newSurface.surfaceTag = tag;
        newSurface.footstepClips = clips;
        newSurface.landingClip = landingClip;
        newSurface.volumeMultiplier = volume;
        newSurface.pitchMin = pitchMin;
        newSurface.pitchMax = pitchMax;

        surfaces.Add(newSurface);
        surfaceSounds = surfaces.ToArray();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, Vector3.down * raycastDistance);
        Gizmos.DrawWireSphere(transform.position - Vector3.up * (raycastDistance - 0.5f), 0.2f);
    }
}