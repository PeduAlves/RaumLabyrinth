using UnityEngine;

/// <summary>
/// Gatilho de vitória: quando o player encosta na torre, mostra a tela de vitória
/// (MazeSceneManager.showGameClear).
///
/// Precisa de um Collider marcado como "Is Trigger" no mesmo GameObject. O
/// CharacterController do player dispara o OnTriggerEnter normalmente (não precisa
/// de Rigidbody). Se a torre for instanciada pelo MazeGenerator sem este componente,
/// ele é adicionado automaticamente junto de um SphereCollider de gatilho.
/// </summary>
public class VictoryTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    private bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag(playerTag)) return;
        triggered = true;

        GameObject sceneManagerObj = GameObject.Find("SceneManager");
        if (sceneManagerObj != null)
        {
            MazeSceneManager manager = sceneManagerObj.GetComponent<MazeSceneManager>();
            if (manager != null)
            {
                manager.showGameClear();
                return;
            }
        }

        Debug.LogWarning("[VictoryTrigger] Objeto 'SceneManager' com MazeSceneManager não encontrado.");
    }
}
