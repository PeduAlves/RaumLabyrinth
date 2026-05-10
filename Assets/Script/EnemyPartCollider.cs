using UnityEngine;

public class EnemyPartCollider : MonoBehaviour, Damagable
{
    [SerializeField] private EnemyPart part;

    public void TakeDamage(int amount)
    {
        part.TakeDamage(amount);
    }

}
