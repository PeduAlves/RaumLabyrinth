using UnityEngine;
using UnityEngine.SceneManagement; // Importante para carregar a cena

public class ColetavelVitoria : MonoBehaviour
{


    [SerializeField] LayerMask playerLayer;

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            // Check if the object has a Damagable component
            Damagable damagable = other.GetComponent<Damagable>();
            if (damagable != null)
            {
                MazeSceneManager mazeSceneManager = GameObject.Find("SceneManager").GetComponent<MazeSceneManager>();
                mazeSceneManager.showGameClear();
            }
        }
    }
}