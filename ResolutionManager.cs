using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
    private int targetWidth = 1920;
    private int targetHeight = 1080;

    void Start()
    {
        Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
    }

    void Update()
    {
        // Enforce the aspect ratio in fullscreen by letterboxing/pillarboxing
        float targetAspect = (float)targetWidth / targetHeight;
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        if (scaleHeight < 1.0f)
        {
            Camera.main.rect = new Rect(0, (1.0f - scaleHeight) / 2.0f, 1.0f, scaleHeight);
        }
        else
        {
            float scaleWidth = 1.0f / scaleHeight;
            Camera.main.rect = new Rect((1.0f - scaleWidth) / 2.0f, 0, scaleWidth, 1.0f);
        }
    }
}
