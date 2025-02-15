// Existing UIRespawn script (unchanged)
using UnityEngine;
using UnityEngine.UI;

public partial class UIRespawn : MonoBehaviour
{
    public GameObject panel;
    public Button button;

    void Update()
    {
        Player player = Player.localPlayer;

        if (player != null && player.health.current == 0)
        {
            panel.SetActive(true);
            button.onClick.SetListener(() => { player.CmdRespawn(); });
        }
        else panel.SetActive(false);
    }
}
