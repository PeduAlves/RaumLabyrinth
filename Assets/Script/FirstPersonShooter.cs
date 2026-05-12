using UnityEngine;

public class FirstPersonShooter : MonoBehaviour, Damagable
{

    private Gun equipedWeapom;

    [Header("Player Stats")]
    [SerializeField] private int maxHealth = 1;
    private int currentHealth;

    [Header("UI Elements")]
    [SerializeField] private GameObject playerUI;

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
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
    }

}
