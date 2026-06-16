using UnityEngine;
using UnityEngine.InputSystem.Controls;

public class EnemyShockwave : DamageDealer
{
    public void DestroyItself()
    {
        Destroy(gameObject);
    }

}
