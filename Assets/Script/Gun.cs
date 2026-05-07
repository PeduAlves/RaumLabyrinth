using UnityEngine;

public class Gun : MonoBehaviour
{ // later update this to be a parent class for different types of guns, with different shooting mechanics and stats
    [SerializeField] 
    private Camera PlayerCamera;
    private float maxDistance = 100f;
    private float alignmentSpeed = 20f;
    private float minReajustDistance = 1f;

    [Header("Bullet Settings")]
    public int Damage = 1;
    public float Range = 100f;
    public LayerMask HitLayers;

    void Update()
    {
        AlignWeapon();
    }

    public void Shoot()
    {
        // The ray starts at the gun's position and goes in its 'forward' (Z axis)
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // Debug line to see the shot in the Scene view
        Debug.DrawRay(transform.position, transform.forward * Range, Color.yellow, 0.1f);

        if (Physics.Raycast(ray, out hit, Range, HitLayers))
        {

            // 1. Check if the object is damageable (like the Golem)
            Damagable target = hit.collider.GetComponentInParent<Damagable>();

            if (target != null)
            {
                target.TakeDamage(Damage);
            }

        }
    }

    void AlignWeapon()
    {
        // 1. Define o raio saindo do centro da tela
        Ray ray = PlayerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint;

        // 2. Determina o ponto de destino
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(maxDistance);
        }

        // ToDo: debug only, remove later
        Debug.DrawLine(ray.origin, targetPoint, Color.red);

        if (Vector3.Distance(transform.position, targetPoint) > minReajustDistance)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * alignmentSpeed);
        }
    }
}