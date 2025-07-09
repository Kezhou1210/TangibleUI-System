using UnityEngine;
using Meta.XR.MRUtilityKit;
using UnityEngine.SceneManagement;
public class SceneDataProvider : MonoBehaviour
{
    public event System.Action<OVRSceneAnchor[]> OnSceneDataLoaded;

    [SerializeField]
    private OVRSceneManager sceneManager;

    private void Awake()
    {
        if (sceneManager == null)
        {
            Debug.LogError("no ovrscenemanager");
            return;
        }

        sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoadedSuccessfully;
        sceneManager.NoSceneModelToLoad += OnNoSceneModelToLoad;
    }

    private void OnDestroy()
    {
        if (sceneManager != null)
        {
            sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoadedSuccessfully;
            sceneManager.NoSceneModelToLoad -= OnNoSceneModelToLoad;
        }
    }

    void Start()
    {
        RequestSceneLoad();
    }
    public void RequestSceneLoad()
    {
        if (sceneManager == null) return;

        Debug.Log("[SceneDataProvider] ����ͨ�� OVRSceneManager.LoadSceneModel() ���𳡾���������...");
        sceneManager.LoadSceneModel();
    }
    private void OnSceneModelLoadedSuccessfully()
    {
        Debug.Log("[SceneDataProvider] �¼�����������ģ�ͼ��سɹ���");

        // ������ɺ󣬲������г���ê��
        var sceneAnchors = FindObjectsOfType<OVRSceneAnchor>();
        Debug.Log($"[SceneDataProvider] ���ҵ� {sceneAnchors.Length} ������ê�㡣");

        // ͨ�������Լ����¼��������غõĳ������ݹ㲥��ȥ���������ű�����AffordanceDetection.cs��ʹ��
        OnSceneDataLoaded?.Invoke(sceneAnchors);
    }

    private void OnSceneModelLoadError()
    {
        Debug.LogError("[SceneDataProvider] �¼�����������ģ�ͼ���ʱ��������");
    }

    private void OnNoSceneModelToLoad()
    {
        Debug.LogWarning("[SceneDataProvider] �¼��������ڴ��豸��û���ҵ��ɹ����صĳ���ģ�͡�������Quest�����ú�ɨ�����ķ��䡣");
    }
}
