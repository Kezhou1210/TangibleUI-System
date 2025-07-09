using UnityEngine;
using PassthroughCameraSamples;
public class PassThroughCameraDisplay : MonoBehaviour
{
    // Start is /called once before the first execution of Update after the MonoBehaviour is created
    public WebCamTextureManager webCamManager;
    public Renderer quadRenderer;

    private Texture2D picture;
    public string textureName;
    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            TakePicture();
        }
   }

    public void TakePicture()
    {
        int height = webCamManager.WebCamTexture.height;
        int width = webCamManager.WebCamTexture.width;

        if (picture == null)
        {
            picture = new Texture2D(width, height);
        }

        Color32[] pixels = new Color32[width * height];
        webCamManager.WebCamTexture.GetPixels32(pixels);

        picture.SetPixels32(pixels);
        picture.Apply();

        quadRenderer.material.SetTexture(textureName, picture); 

    }
}
