using UnityEngine;

public class EnemyPart : MonoBehaviour, Damagable
{

    public EnemyDamagablePart partType;
    [SerializeField]
    private int MAX_HEALTH = 1;
    [SerializeField]
    private float regenerationTime = 15f;

    private SkinnedMeshRenderer mesh;
    public bool isDisabled = false;
    private float regenerationTimer = 0f;
    private int health;

    private Enemy mainBody;
    public void TakeDamage(int amount)
    {
        if (isDisabled) return;
        health -= amount;
        if (health <= 0)
        {
            health = 0;
            mesh.enabled = false;
            isDisabled = true;
            mainBody.TakeDamage(partType);
        }
        
    }

    void regenerate()
    {
        health += 1;
        mesh.enabled = true;
        isDisabled = false;
        mainBody.Heal(partType);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mesh = GetComponent<SkinnedMeshRenderer>();
        mainBody = GetComponentInParent<Enemy>();
        health = MAX_HEALTH;
    }

    // Update is called once per frame
    void Update()
    {
        if (health < MAX_HEALTH)
        {
            regenerationTimer += Time.deltaTime;
            if (regenerationTimer >= regenerationTime)
            {
                regenerate();
                regenerationTimer = 0f;
            }
        }
    }
}
