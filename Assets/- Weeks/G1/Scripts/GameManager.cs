using UnityEngine;
using System.Collections;
using TMPro;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Prefabs (Red, Yellow, Blue Cubes)")]
    public GameObject[] cubePrefabs;

    [Header("Particle Effects (match cube order)")]
    public GameObject[] cubeParticles;

    [Header("Wrong Pop Particle")]
    public GameObject wrongPopParticle;

    [Header("Sound Effects (match cube order)")]
    public AudioClip[] cubeSounds;

    [Header("Audio Source for Pop Sounds")]
    public AudioSource popAudioSource;

    [Header("Wrong Pop Sound")]
    public AudioClip wrongPopSound;

    [Header("Miss Sound (Cross Activate)")]
    public AudioClip missSound;

    [Header("Target Texts (Red, Yellow, Blue)")]
    public TextMeshProUGUI[] targetTexts;

    [Header("Score UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI bestScoreText;

    [Header("UI Crosses")]
    public GameObject[] crossesOff;
    public GameObject[] crossesOn;

    [Header("Spawn Settings")]
    public float spawnInterval = 1.5f;
    public float tossForce = 8f;
    public float sidewaysForce = 2f;
    public float spinSpeed = 5f;

    [Header("Target Guarantee Settings")]
    [Tooltip("Guarantee the target cube within this many spawns. Set 0 to disable.")]
    public int maxNonTargetSpawns = 3;
    [Tooltip("Maximum times the same target can repeat in a row")]
    public int maxTargetRepeats = 2;

    [Header("UI Manager")]
    public UIManager uiManager; // Assign in Inspector

    [Header("Game Over Texts")]
    public GameObject wrongCubeText;   // Assign "You popped the WRONG cube" GameObject in Inspector
    public GameObject missedCubesText; // Assign "You missed the cubes 3 times" GameObject in Inspector

    Camera cam;
    int currentTargetIndex;
    int score = 0;
    int bestScore = 0;
    int crossesActivated = 0;
    int sameColorCount = 0;
    int lastSpawnedIndex = -1;
    int nonTargetStreak = 0;
    private int targetRepeatCount = 0;
    private int lastTargetIndex = -1;

    public Coroutine spawnRoutine; // For controlling spawns

    public bool isPaused { get; private set; } // Pause state property

    // --- Track spawned cubes ---
    private readonly List<GameObject> activeCubes = new List<GameObject>();

    private void Awake()
    {
        Application.targetFrameRate = 120;
        bestScore = PlayerPrefs.GetInt("BestScore", 0); // Load best score
        isPaused = false;
    }

    void Start()
    {
        cam = Camera.main;
        UpdateScoreUI();
        HideGameOverTexts();
        // Do NOT set target text here, UIManager handles it instantly before game panel transition!
    }

    // Called by UIManager after the transition and delay
    public void StartGameplay()
    {
        score = 0;
        crossesActivated = 0;
        UpdateScoreUI();
        HideGameOverTexts();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            SpawnCube();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnCube()
    {
        int prefabIndex;

        // Prevent same color more than twice in a row
        if (lastSpawnedIndex != -1 && sameColorCount >= 2)
        {
            do
            {
                prefabIndex = Random.Range(0, cubePrefabs.Length);
            }
            while (prefabIndex == lastSpawnedIndex);
            sameColorCount = 1;
        }
        else
        {
            prefabIndex = Random.Range(0, cubePrefabs.Length);
            if (prefabIndex == lastSpawnedIndex)
                sameColorCount++;
            else
                sameColorCount = 1;
        }

        // --- Guarantee: prevent endless drought of target cube ---
        if (maxNonTargetSpawns > 0 && prefabIndex != currentTargetIndex)
        {
            nonTargetStreak++;

            if (nonTargetStreak == maxNonTargetSpawns)
            {
                // 80% chance to force target now
                if (Random.value < 0.8f)
                {
                    prefabIndex = currentTargetIndex;
                    nonTargetStreak = 0;
                }
            }
            else if (nonTargetStreak > maxNonTargetSpawns)
            {
                // Beyond the limit → force target
                prefabIndex = currentTargetIndex;
                nonTargetStreak = 0;
            }
        }
        else if (prefabIndex == currentTargetIndex)
        {
            nonTargetStreak = 0;
        }

        lastSpawnedIndex = prefabIndex;
        GameObject prefab = cubePrefabs[prefabIndex];

        int side = Random.Range(0, 4);
        Vector3 spawnPos = Vector3.zero;
        Vector3 dirToCenter = Vector3.zero;

        Vector3 min = cam.ScreenToWorldPoint(new Vector3(0, 0, -cam.transform.position.z));
        Vector3 max = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, -cam.transform.position.z));

        switch (side)
        {
            case 0:
                float cubeHalf = prefab.transform.localScale.x / 2f;
                spawnPos = new Vector3(Random.Range(min.x + cubeHalf, max.x - cubeHalf), max.y + 1f, 0);
                dirToCenter = Vector3.down;
                break;
            case 1:
                spawnPos = new Vector3(Random.Range(min.x, max.x), min.y - 1f, 0);
                dirToCenter = Vector3.up;
                break;
            case 2:
                spawnPos = new Vector3(min.x - 1f, Random.Range(min.y, max.y), 0);
                dirToCenter = Vector3.right;
                break;
            case 3:
                spawnPos = new Vector3(max.x + 1f, Random.Range(min.y, max.y), 0);
                dirToCenter = Vector3.left;
                break;
        }

        GameObject cube = Instantiate(prefab, spawnPos, Quaternion.identity);
        Rigidbody rb = cube.GetComponent<Rigidbody>();
        if (rb == null) rb = cube.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.mass = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezePositionZ;

        Vector3 force = dirToCenter * sidewaysForce + Vector3.up * tossForce;
        rb.AddForce(force, ForceMode.Impulse);

        Vector3 randomTorque = new Vector3(
            Random.Range(-spinSpeed, spinSpeed),
            Random.Range(-spinSpeed, spinSpeed),
            Random.Range(-spinSpeed, spinSpeed)
        );
        rb.AddTorque(randomTorque, ForceMode.Impulse);

        CubeClickHandler handler = cube.AddComponent<CubeClickHandler>();
        handler.manager = this;
        handler.cubeIndex = prefabIndex;

        // Track this cube
        activeCubes.Add(cube);
    }

    public void OnCubeClicked(GameObject cube, int cubeIndex)
    {
        if (isPaused) return; // Prevent popping cubes when paused!

        if (cubeIndex == currentTargetIndex)
        {
            PlayParticle(cubeParticles[cubeIndex], cube.transform.position);
            PlaySound(cubeSounds[cubeIndex]);

            score++;

            // ✅ Instead of duplicating best-score logic, call the method
            SaveBestScore();

            UpdateScoreUI();
            // Retarget handled later with coroutine
            StartCoroutine(SetNewTargetNextFrame());
        }
        else
        {
            PlayParticle(wrongPopParticle, cube.transform.position);
            PlaySound(wrongPopSound);
            Debug.Log("Game Over - Wrong cube popped!");
            ShowWrongCubeGameOver();
            StopGame();
        }

        // Remove from active cubes
        activeCubes.Remove(cube);

        Destroy(cube);
    }

    // Delay retargeting by one frame to consume any duplicate input this frame.
    private IEnumerator SetNewTargetNextFrame()
    {
        yield return new WaitForEndOfFrame();
        SetNewTarget();
    }

    public void OnCubeMissed(int cubeIndex)
    {
        if (cubeIndex == currentTargetIndex)
        {
            ActivateCross();
        }
    }

    void PlayParticle(GameObject particlePrefab, Vector3 position)
    {
        if (particlePrefab != null)
        {
            GameObject effect = Instantiate(particlePrefab, position, Quaternion.identity);
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                Destroy(effect, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(effect, 2f);
            }
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (popAudioSource != null && clip != null)
        {
            popAudioSource.PlayOneShot(clip);
        }
    }

    // PUBLIC so UIManager can set target instantly before transition
    public void SetNewTarget()
    {
        int newTarget = currentTargetIndex;

        // Try up to 10 times to pick a good new target
        for (int attempt = 0; attempt < 10; attempt++)
        {
            newTarget = Random.Range(0, targetTexts.Length);

            // Rule 1: avoid exceeding repeat limit
            if (newTarget == lastTargetIndex && targetRepeatCount >= maxTargetRepeats)
                continue;

            // Rule 2: add bias against just toggling between two colors
            if (newTarget == lastTargetIndex && Random.value < 0.5f)
                continue;

            break; // found acceptable new target
        }

        // Update repeat streak
        if (newTarget == lastTargetIndex)
            targetRepeatCount++;
        else
            targetRepeatCount = 1;

        currentTargetIndex = newTarget;
        lastTargetIndex = newTarget;

        // Update UI
        for (int i = 0; i < targetTexts.Length; i++)
        {
            targetTexts[i].gameObject.SetActive(i == currentTargetIndex);
        }
    }


    void ActivateCross()
    {
        if (crossesActivated < crossesOn.Length)
        {
            crossesOff[crossesActivated].SetActive(false);
            crossesOn[crossesActivated].SetActive(true);

            PlaySound(missSound);

            crossesActivated++;

            if (crossesActivated >= crossesOn.Length)
            {
                Debug.Log("Game Over - 3 crosses reached!");
                ShowMissedCubesGameOver();
                StopGame();
            }
        }
    }

    void UpdateScoreUI()
    {
        scoreText.text = score.ToString();
        bestScoreText.text = bestScore.ToString();
    }

    void SaveBestScore()
    {
        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("BestScore", bestScore);
            PlayerPrefs.Save();
        }
        UpdateScoreUI();
    }

    public void StopGame()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        if (uiManager != null)
        {
            uiManager.ShowFinish();
        }
    }

    // Destroy all cubes with their particle effects
    public void DestroyAllCubesWithParticles()
    {
        for (int i = activeCubes.Count - 1; i >= 0; i--)
        {
            GameObject cube = activeCubes[i];
            if (cube != null)
            {
                int prefabIndex = -1;
                CubeClickHandler handler = cube.GetComponent<CubeClickHandler>();
                if (handler != null)
                    prefabIndex = handler.cubeIndex;

                // Play correct particle if index valid
                if (prefabIndex >= 0 && prefabIndex < cubeParticles.Length)
                    PlayParticle(cubeParticles[prefabIndex], cube.transform.position);
                else
                    PlayParticle(wrongPopParticle, cube.transform.position);

                Destroy(cube);
            }
        }
        activeCubes.Clear();
    }

    public void RestartGame()
    {
        // Destroy all remaining cubes with particles
        DestroyAllCubesWithParticles();

        score = 0;
        crossesActivated = 0;
        UpdateScoreUI();

        for (int i = 0; i < crossesOn.Length; i++)
        {
            crossesOff[i].SetActive(true);
            crossesOn[i].SetActive(false);
        }

        HideGameOverTexts();

        // UIManager handles SetNewTarget and the initial delay before StartGameplay

        isPaused = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        // StartGameplay will be called after UIManager's delay
    }

    // --- Game Over Message Controls ---

    void HideGameOverTexts()
    {
        if (wrongCubeText != null) wrongCubeText.SetActive(false);
        if (missedCubesText != null) missedCubesText.SetActive(false);
    }

    void ShowWrongCubeGameOver()
    {
        if (wrongCubeText != null) wrongCubeText.SetActive(true);
        if (missedCubesText != null) missedCubesText.SetActive(false);
    }

    void ShowMissedCubesGameOver()
    {
        if (wrongCubeText != null) wrongCubeText.SetActive(false);
        if (missedCubesText != null) missedCubesText.SetActive(true);
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }
}

// --- CubeClickHandler is also defined below ---

public class CubeClickHandler : MonoBehaviour
{
    public GameManager manager;
    public int cubeIndex;

    // Prevent double handling on mobile where a tap can emit both mouse and touch
    private bool handled = false;

    void Update()
    {
        // Use platform-specific input to avoid duplicate events
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            CheckClick(Input.mousePosition);
#elif UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            CheckClick(Input.GetTouch(0).position);
#else
        if (Input.GetMouseButtonDown(0))
            CheckClick(Input.mousePosition);
#endif
    }

    void CheckClick(Vector2 screenPos)
    {
        if (handled) return;

        if (manager != null && manager.isPaused)
            return; // Prevent popping while paused

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                handled = true; // consume this cube
                manager.OnCubeClicked(gameObject, cubeIndex);
            }
        }
    }
}
