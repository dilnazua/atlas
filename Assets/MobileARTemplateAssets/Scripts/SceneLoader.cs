using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script to load another scene when called from a button or other event.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Tooltip("The name of the scene to load. Make sure the scene is added to the Build Settings.")]
    [SerializeField]
    string m_SceneName = "";

    /// <summary>
    /// The name of the scene to load.
    /// </summary>
    public string sceneName
    {
        get => m_SceneName;
        set => m_SceneName = value;
    }

    [Tooltip("Alternative: Use scene build index instead of scene name. Set to -1 to use scene name instead.")]
    [SerializeField]
    int m_SceneBuildIndex = -1;

    /// <summary>
    /// Alternative: Use scene build index instead of scene name. Set to -1 to use scene name instead.
    /// </summary>
    public int sceneBuildIndex
    {
        get => m_SceneBuildIndex;
        set => m_SceneBuildIndex = value;
    }

    /// <summary>
    /// Loads the specified scene. This method can be called from a button's OnClick event.
    /// </summary>
    public void LoadScene()
    {
        if (m_SceneBuildIndex >= 0)
        {
            // Load by build index
            if (m_SceneBuildIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(m_SceneBuildIndex);
            }
            else
            {
                Debug.LogError($"SceneLoader: Scene build index {m_SceneBuildIndex} is out of range. Please check your Build Settings.");
            }
        }
        else if (!string.IsNullOrEmpty(m_SceneName))
        {
            // Load by name
            SceneManager.LoadScene(m_SceneName);
        }
        else
        {
            Debug.LogError("SceneLoader: No scene name or build index specified. Please set the scene name or build index in the inspector.");
        }
    }

    /// <summary>
    /// Loads a scene by name. Useful for loading different scenes from the same button.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    public void LoadSceneByName(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("SceneLoader: Scene name is empty.");
        }
    }

    /// <summary>
    /// Loads a scene by build index. Useful for loading different scenes from the same button.
    /// </summary>
    /// <param name="buildIndex">The build index of the scene to load.</param>
    public void LoadSceneByIndex(int buildIndex)
    {
        if (buildIndex >= 0 && buildIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(buildIndex);
        }
        else
        {
            Debug.LogError($"SceneLoader: Scene build index {buildIndex} is invalid. Please check your Build Settings.");
        }
    }
}

