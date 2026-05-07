using UnityEngine;

public class EnemyPart : MonoBehaviour, Damagable
{
    [SerializeField]
    private EnemyDamagablePart part;
    [SerializeField]
    private int MAX_HEALTH = 1;
    [SerializeField]
    private float regenerationTime = 15f;

    private MeshRenderer mesh;
    private MeshCollider collider;
    private float regenerationTimer = 0f;
    private int health;

    private Enemy mainBody;
    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
        {
            health = 0;
            mesh.enabled = false;
            collider.enabled = false;
            mainBody.TakeDamage(part);
        }
        mainBody.Stun(0.5f);
        
    }

    void regenerate()
    {
        health += 1;
        mesh.enabled = true;
        collider.enabled = true;
        mainBody.Heal(part);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mesh = GetComponentsInChildren<MeshRenderer>()[1];
        collider = GetComponent<MeshCollider>();
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
