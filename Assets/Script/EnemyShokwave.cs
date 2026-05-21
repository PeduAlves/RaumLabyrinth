using UnityEngine;

public class EnemyShokwave : DamageDealer
{

    public void DestroyItself()
    {
        Destroy(gameObject);
    }

}
