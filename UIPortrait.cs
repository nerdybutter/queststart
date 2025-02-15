using UnityEngine;
using UnityEngine.UI;

public partial class UIPortrait : MonoBehaviour
{
    public GameObject panel;
    public Image image;

    void Update()
    {
        Player player = Player.localPlayer;

        if (player)
        {
            panel.SetActive(true);
            image.sprite = player.GetComponent<PlayerEquipment>().slotInfo[8].location.GetComponentInChildren<SubAnimation>().spritesToAnimate[0];
        }
        else panel.SetActive(false);
    }
}
