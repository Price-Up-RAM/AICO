using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class InitManager : MonoBehaviour
{
    private static InitManager instance;
    public static InitManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InitManager>();
            }
            return instance;
        }
    }

    private Task initTask;
    private bool isInitializing = false;
    private bool isInitialized = false;

    public bool IsInitialized
    {
        get
        {
            return isInitialized;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        InitManager manager = Instance;
        if (manager == null)
        {
            GameObject initObject = new GameObject("InitManager");
            manager = initObject.AddComponent<InitManager>();
            instance = manager;
        }

        manager.BeginInit();
    }

    private void Start()
    {
        BeginInit();
    }

    public Task WaitForInitAsync()
    {
        BeginInit();
        return initTask;
    }

    public void BeginInit()
    {
        if (initTask != null)
        {
            return;
        }

        initTask = InitAsync();
    }

    private async Task InitAsync()
    {
        if (isInitializing)
        {
            return;
        }

        if (isInitialized)
        {
            return;
        }

        isInitializing = true;
        Debug.Log("[InitManager] Init start");

        await SettingManager.Instance.InitAsync();
        InstallStatusManager.Instance.Init();
        UIManager.Instance.Init();
        LanguageManager.Instance.Init();
        await CharManager.Instance.InitAsync();

        isInitialized = true;
        isInitializing = false;
        Debug.Log("[InitManager] Init complete");
    }
}
