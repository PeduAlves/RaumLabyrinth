using UnityEngine;
using UnityEngine.Serialization;
using FMODUnity;

public class FirstPersonShooter : MonoBehaviour, Damagable
{
    [Header("Vida")]
    [SerializeField] private int maxHealth = 2;
    [Tooltip("Segundos para regenerar 1 de vida após parar de tomar dano")]
    [FormerlySerializedAs("regenRate")]
    [SerializeField] private float regenInterval = 5f;

    [Header("UI")]
    [SerializeField] private GameObject damageOverlay;
    [SerializeField] private GameObject playerUI;

    private Gun equippedWeapon;
    private int currentHealth;
    private bool isRegenerating;
    private float regenTimer;

    // Exposto para HUD / barra de vida.
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;

    void Start()
    {
        equippedWeapon = GetComponentInChildren<Gun>();
        currentHealth = maxHealth;
        if (damageOverlay != null) damageOverlay.SetActive(false);
    }

    void Update()
    {
        if (IsDead) return;

        if (Input.GetButtonDown("Fire1") && equippedWeapon != null)
        {
            equippedWeapon.Shoot();
        }

        HandleRegen();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // Sofreu dano e não morreu: reinicia a contagem de regeneração e mostra o overlay.
        isRegenerating = currentHealth < maxHealth;
        regenTimer = 0f;
        if (damageOverlay != null) damageOverlay.SetActive(currentHealth < maxHealth);
    }

    private void HandleRegen()
    {
        if (!isRegenerating) return;

        regenTimer += Time.deltaTime;
        if (regenTimer < regenInterval) return;

        regenTimer = 0f;
        currentHealth = Mathf.Min(currentHealth + 1, maxHealth);

        if (currentHealth >= maxHealth)
        {
            isRegenerating = false;
            if (damageOverlay != null) damageOverlay.SetActive(false);
        }
    }

    private void Die()
    {
        currentHealth = 0;
        if (playerUI != null) playerUI.SetActive(false);

        GameObject sceneManagerObj = GameObject.Find("SceneManager");
        if (sceneManagerObj != null)
        {
            MazeSceneManager mazeSceneManager = sceneManagerObj.GetComponent<MazeSceneManager>();
            if (mazeSceneManager != null) mazeSceneManager.showGameOver();
        }
    }
}
