using System.Collections;
using UnityEngine;

/// <summary>
/// "Frame stop" / hit stop: congela (ou desacelera) o jogo por um instante curto
/// para dar peso a impactos (tiro acertando, ataque do inimigo).
///
/// Uso: HitStop.Do(0.06f);   // congela ~60ms de tempo real
///
/// Não precisa de setup de cena — uma instância é criada automaticamente na primeira
/// chamada. Chamadas sobrepostas estendem a duração em vez de empilhar.
/// </summary>
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    private float freezeTimer = 0f;
    private bool frozen = false;

    /// <summary>
    /// Congela o jogo por <paramref name="durationRealSeconds"/> segundos reais.
    /// </summary>
    /// <param name="timeScaleDuringFreeze">
    /// 0 = congelamento total. Use um valor pequeno (ex: 0.1) para "slow motion".
    /// </param>
    public static void Do(float durationRealSeconds, float timeScaleDuringFreeze = 0f)
    {
        if (durationRealSeconds <= 0f) return;

        if (Instance == null)
        {
            GameObject go = new GameObject("HitStop");
            Instance = go.AddComponent<HitStop>();
        }

        Instance.Freeze(durationRealSeconds, timeScaleDuringFreeze);
    }

    private void Freeze(float durationRealSeconds, float timeScaleDuringFreeze)
    {
        // Estende a duração se já estiver congelado.
        freezeTimer = Mathf.Max(freezeTimer, durationRealSeconds);
        if (!frozen)
            StartCoroutine(FreezeRoutine(timeScaleDuringFreeze));
    }

    private IEnumerator FreezeRoutine(float scale)
    {
        frozen = true;

        // Guarda o timeScale anterior (defaulta para 1 se o jogo já estava pausado).
        float previous = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = scale;

        while (freezeTimer > 0f)
        {
            freezeTimer -= Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = previous;
        frozen = false;
    }
}
