using UnityEngine;

public class FirstPersonShooter : MonoBehaviour, Damagable
{

    private Gun equipedWeapom;

    [Header("UI Settings")]
    [SerializeField] private GameObject damageOverlay;

    [Header("Player Stats")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private float regenRate = 5f;
    private bool isDamaged = false;
    private float damageTimer = 0f;
    private int currentHealth;

    [Header("UI Elements")]
    [SerializeField] private GameObject playerUI;

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        isDamaged = true;
        damageOverlay.SetActive(true);
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        
        playerUI.SetActive(false);
        MazeSceneManager mazeSceneManager = GameObject.Find("SceneManager").GetComponent<MazeSceneManager>();
        mazeSceneManager.showGameOver();

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        equipedWeapom = GetComponentInChildren<Gun>();
        currentHealth = maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            equipedWeapom.Shoot();
        }

        if (isDamaged)
        {
            damageTimer += Time.deltaTime;
            if (damageTimer >= regenRate)
            {
                currentHealth +=1;
                damageTimer = 0f;
                if (currentHealth == maxHealth)
                {
                    isDamaged = false;
                    damageOverlay.SetActive(false);
                }
            }
        }
    }

}
