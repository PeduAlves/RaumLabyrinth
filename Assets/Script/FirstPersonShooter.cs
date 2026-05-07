using UnityEngine;

public class FirstPersonShooter : MonoBehaviour
{

    [SerializeField]
    private Gun equipedWeapom;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
