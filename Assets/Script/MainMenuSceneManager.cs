using UnityEngine;
using UnityEngine.SceneManagement; // Necessário para carregar cenas

public class MudarCena : MonoBehaviour
{
    
    public void CarregarNovaCena(string nomeDaCena)
    {
        SceneManager.LoadScene(nomeDaCena);
    }
}