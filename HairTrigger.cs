using UnityEngine;

public class HairTrigger : MonoBehaviour
{
	public GameObject hairSprite;

	public SpriteRenderer headSprite;

	private void Update()
	{
		if (headSprite.sprite != null)
		{
			hairSprite.SetActive(value: false);
		}
		else if (headSprite.sprite == null)
		{
			hairSprite.gameObject.SetActive(value: true);
		}
	}
}
