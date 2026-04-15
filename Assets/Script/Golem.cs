using UnityEngine;

public class Enemy : MonoBehaviour
{

    private SphereCollider visionRange;
    [SerializeField] private float visionAngle = 80f;
    [SerializeField] private float perceptionDistance = 5;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float idleSpeed = 0.5f;
    [SerializeField] private float pursueSpeed = 1;
    [SerializeField] private float turningSpeed = 1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        visionRange = GetComponent<SphereCollider>();

    }

    void PlayerDetector(Collider target)
    {
        Vector3 headCenter = transform.TransformPoint(visionRange.center);
        Vector3 targetDirection = (target.transform.position - headCenter).normalized;
        if ((Vector3.Angle(transform.forward, targetDirection) < visionAngle / 2) ||
            (Vector3.Distance(transform.position, target.transform.position) < perceptionDistance))
        {
            float distanciaParaAlvo = Vector3.Distance(headCenter, target.transform.position);

            if (Physics.Raycast(headCenter, targetDirection, distanciaParaAlvo, obstacleLayer))
            {
                Debug.Log("desvi o jogador!");
                Debug.DrawRay(headCenter, targetDirection * distanciaParaAlvo, Color.red);
            }
            else if (Physics.Raycast(headCenter, targetDirection, distanciaParaAlvo, playerLayer))
            {
                Debug.Log("vi o jogador!");
                Debug.DrawRay(headCenter, targetDirection * distanciaParaAlvo, Color.green);
                PlayerPursue(target);
            }
        }
    }

    void PlayerPursue(Collider target)
    {

        Vector3 targetPos = target.transform.position;
        targetPos.y = transform.position.y;
        Vector3 targetDirection = (targetPos - transform.position).normalized;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turningSpeed * Time.deltaTime);
        }

        transform.Translate(Vector3.forward * pursueSpeed * Time.deltaTime);
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            PlayerDetector(other);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
