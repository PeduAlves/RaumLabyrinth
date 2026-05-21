using UnityEngine;

public class DamageDealer : MonoBehaviour
{

    [SerializeField] int damage = 1;
    [SerializeField] LayerMask playerLayer;
    
    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            // Check if the object has a Damagable component
            Damagable damagable = other.GetComponent<Damagable>();
            if (damagable != null)
            {
                damagable.TakeDamage(damage);
            }
        }
    }
}
