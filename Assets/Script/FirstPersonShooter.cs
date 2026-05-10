using UnityEngine;

public class FirstPersonShooter : MonoBehaviour
{

    private Gun equipedWeapom;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        equipedWeapom = GetComponentInChildren<Gun>();
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
