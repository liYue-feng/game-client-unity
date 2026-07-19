using Game.Gameplay;
using UnityEngine;
using UnityEngine.UI;

public sealed class WaveObjectiveView : MonoBehaviour
{
    public Text waveText;
    public Text aliveText;

    public void Render(WaveObjectiveState state)
    {
        if (waveText != null)
        {
            waveText.text = state.WaveText;
        }

        if (aliveText != null)
        {
            aliveText.text = state.AliveText;
        }
    }
}
