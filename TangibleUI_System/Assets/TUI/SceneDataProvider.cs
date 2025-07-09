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

        Debug.Log("[SceneDataProvider] 正在通过 OVRSceneManager.LoadSceneModel() 发起场景加载请求...");
        sceneManager.LoadSceneModel();
    }
    private void OnSceneModelLoadedSuccessfully()
    {
        Debug.Log("[SceneDataProvider] 事件触发：场景模型加载成功！");

        // 加载完成后，查找所有场景锚点
        var sceneAnchors = FindObjectsOfType<OVRSceneAnchor>();
        Debug.Log($"[SceneDataProvider] 共找到 {sceneAnchors.Length} 个场景锚点。");

        // 通过我们自己的事件，将加载好的场景数据广播出去，供其他脚本（如AffordanceDetection.cs）使用
        OnSceneDataLoaded?.Invoke(sceneAnchors);
    }

    private void OnSceneModelLoadError()
    {
        Debug.LogError("[SceneDataProvider] 事件触发：场景模型加载时发生错误！");
    }

    private void OnNoSceneModelToLoad()
    {
        Debug.LogWarning("[SceneDataProvider] 事件触发：在此设备上没有找到可供加载的场景模型。请先在Quest中设置和扫描您的房间。");
    }
}
