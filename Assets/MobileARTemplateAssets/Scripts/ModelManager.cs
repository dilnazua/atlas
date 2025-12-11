using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
#if AR_FOUNDATION_PRESENT
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
#if GLTFAST
using GLTFast;
using GLTFast.Materials;
#endif

/// <summary>
/// Manages API response handling and AR model spawning.
/// </summary>
public class ModelManager : MonoBehaviour
{
    [Header("AR Model Spawning")]
    [Tooltip("Object spawner for placing models in AR space")]
    public ObjectSpawner arObjectSpawner;
    
    [Tooltip("Prefabs that can be spawned (indexed by model_type from API)")]
    public List<GameObject> modelPrefabs = new List<GameObject>();
    
    [Tooltip("Default spawn position offset from camera (if no AR plane found)")]
    public Vector3 defaultSpawnOffset = new Vector3(0, 0, 2f);
    
    [Tooltip("Minimum distance from camera to spawn model")]
    public float minSpawnDistance = 1f;
    
    [Tooltip("Maximum distance from camera to spawn model")]
    public float maxSpawnDistance = 5f;
    
    [Header("GLB Model Loading")]
    [Tooltip("Enable if a GLB runtime loader installed")]
    public bool enableGLBLoading = false;
    
    [Tooltip("Parent transform for spawned models (optional)")]
    public Transform modelParent;
    
    void Start()
    {
        // Check if GLB loader is available and log status
        CheckGLBLoaderStatus();
    }
    
    /// <summary>
    /// Checks if GLB runtime loader (glTFast) is installed and logs the status.
    /// </summary>
    private void CheckGLBLoaderStatus()
    {
        #if GLTFAST
        Debug.Log("GLB Runtime Loader (glTFast) is INSTALLED and AVAILABLE");
        if (!enableGLBLoading)
        {
            Debug.LogWarning("GLB loading is DISABLED. Enable it in ModelManager Inspector to load GLB files at runtime.");
        }
        else
        {
            Debug.Log("GLB loading is ENABLED. GLB files will be loaded at runtime.");
        }
        #else
        Debug.LogWarning("GLB Runtime Loader (glTFast) is NOT INSTALLED");
        #endif
    }

    /// <summary>
    /// Loads a GLB model file and spawns it in AR space.
    /// </summary>
    /// <param name="glbFilePath">Path to the GLB file</param>
    /// <param name="statusText">UI text component for status updates</param>
    public IEnumerator LoadGLBModel(string glbFilePath, Text statusText = null)
    {
        Debug.Log($"LOADING GLB MODEL");
        Debug.Log($"GLB File Path: {glbFilePath}");
        
        if (statusText != null)
            statusText.text = "Loading 3D model...";
        
        // Check if file exists
        if (!System.IO.File.Exists(glbFilePath))
        {
            Debug.LogError($"GLB file not found: {glbFilePath}");
            if (statusText != null)
                statusText.text = "Error: GLB file not found";
            yield break;
        }
        
        if (enableGLBLoading)
        {
            #if GLTFAST
            // Load GLB using glTFast
            yield return StartCoroutine(LoadGLBWithGLTFast(glbFilePath, statusText));
            #else
            Debug.LogError("GLB loading is enabled but glTFast package is not installed!");
            
            if (statusText != null)
                statusText.text = "Error: GLB loader not installed";
            
            // Fallback to placeholder
            yield return StartCoroutine(SpawnPlaceholderModel(statusText));
            #endif
        }
        else
        {
            Debug.Log($"GLB model downloaded and saved to: {glbFilePath}");
            Debug.LogWarning("Runtime GLB loading is disabled.");
            
            if (statusText != null)
                statusText.text = $"Model saved to: {glbFilePath}";
            
            // Spawn a placeholder or use fallback prefab
            yield return StartCoroutine(SpawnPlaceholderModel(statusText));
        }
    }
    
    /// <summary>
    /// Spawns a placeholder model when GLB loading is not available.
    /// </summary>
    private IEnumerator SpawnPlaceholderModel(Text statusText = null)
    {
        // Use first available prefab as placeholder, or create a simple cube
        if (modelPrefabs.Count > 0 && modelPrefabs[0] != null)
        {
            Vector3 spawnPosition = Vector3.zero; 
            Vector3 spawnNormal = Vector3.up;
            bool foundARPlane = false;
            
            #if AR_FOUNDATION_PRESENT
            ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
            if (planeManager != null)
            {
                foreach (var trackable in planeManager.trackables)
                {
                    if (trackable.alignment == PlaneAlignment.HorizontalUp)
                    {
                        spawnPosition = trackable.center;
                        spawnNormal = trackable.normal;
                        foundARPlane = true;
                        break;
                    }
                }
            }
            #endif
            
            if (!foundARPlane)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Vector3 cameraForward = mainCamera.transform.forward;
                    cameraForward.y = 0;
                    cameraForward.Normalize();
                    float distance = Mathf.Clamp(defaultSpawnOffset.z, minSpawnDistance, maxSpawnDistance);
                    spawnPosition = mainCamera.transform.position + cameraForward * distance;
                    spawnPosition.y = mainCamera.transform.position.y;
                }
                else
                {
                    spawnPosition = defaultSpawnOffset;
                }
            }
            
            GameObject placeholder = Instantiate(modelPrefabs[0], spawnPosition, Quaternion.identity);
            if (modelParent != null)
                placeholder.transform.SetParent(modelParent);
            
            Debug.Log($"Placeholder model spawned at: {spawnPosition}");
            if (statusText != null)
                statusText.text = "Placeholder model spawned (GLB loader needed)";
        }
        
        yield return null;
    }

    /// <summary>
    /// Handles the API response and spawns the model in AR space.
    /// Called by PhotoManager after successful photo upload.
    /// </summary>
    /// <param name="responseJson">JSON response string from API</param>
    /// <param name="statusText">UI text component for status updates (optional)</param>
    public IEnumerator HandleAPIResponse(string responseJson, Text statusText = null)
    {
        if (statusText != null)
            statusText.text = "Parsing API response...";
        
        // Parse JSON response
        APIResponse apiResponse = ParseAPIResponse(responseJson);
        
        if (apiResponse == null)
        {
            if (statusText != null)
                statusText.text = "Failed to parse API response";
            Debug.LogError("Failed to parse API response");
            yield break;
        }
        
        // Check if API returned success
        if (apiResponse.status != null && apiResponse.status.ToLower() != "success")
        {
            string errorMsg = !string.IsNullOrEmpty(apiResponse.message) ? apiResponse.message : "API returned non-success status";
            if (statusText != null)
                statusText.text = $"API Error: {errorMsg}";
            Debug.LogError($"API Error: {errorMsg}");
            yield break;
        }
        
        if (statusText != null)
            statusText.text = "Placing model in AR space...";
        
        // Spawn the model in AR space
        yield return StartCoroutine(SpawnARModel(apiResponse, statusText));
        
        if (statusText != null)
            statusText.text = "Model placed successfully!";
    }
    
    /// <summary>
    /// Parses the JSON response from the API.
    /// </summary>
    private APIResponse ParseAPIResponse(string jsonText)
    {
        try
        {
            APIResponse response = JsonUtility.FromJson<APIResponse>(jsonText);
            
            // Handle nested JSON strings (if API returns JSON as a string)
            if (response == null || string.IsNullOrEmpty(response.status))
            {
                // Try parsing as nested JSON
                response = JsonUtility.FromJson<APIResponse>(jsonText);
            }
            
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse API response: {e.Message}\nResponse text: {jsonText}");
            return null;
        }
    }
    
    /// <summary>
    /// Spawns the model in AR space based on API response.
    /// </summary>
    private IEnumerator SpawnARModel(APIResponse response, Text statusText = null)
    {
        Vector3 spawnPosition = Vector3.zero;
        Vector3 spawnNormal = Vector3.up;
        bool foundARPlane = false;
        
        #if AR_FOUNDATION_PRESENT
        // Try to find an AR plane to spawn on
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager != null)
        {
            foreach (var trackable in planeManager.trackables)
            {
                if (trackable.alignment == PlaneAlignment.HorizontalUp)
                {
                    // Use AR plane center position
                    spawnPosition = trackable.center;
                    spawnNormal = trackable.normal;
                    foundARPlane = true;
                    Debug.Log($"Found AR plane at position: {spawnPosition}");
                    break;
                }
            }
        }
        #endif
        
        // Fallback: Use default position relative to camera
        if (!foundARPlane)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 cameraForward = mainCamera.transform.forward;
                cameraForward.y = 0; // Keep horizontal
                cameraForward.Normalize();
                
                float distance = Mathf.Clamp(defaultSpawnOffset.z, minSpawnDistance, maxSpawnDistance);
                spawnPosition = mainCamera.transform.position + cameraForward * distance;
                spawnPosition.y = mainCamera.transform.position.y; // Keep at camera height
                Debug.Log($"Using default spawn position: {spawnPosition}");
            }
            else
            {
                spawnPosition = defaultSpawnOffset;
            }
        }
        
        // Override with API-provided position if available
        if (response.position != null)
        {
            spawnPosition = new Vector3(response.position.x, response.position.y, response.position.z);
            Debug.Log($"Using API-provided position: {spawnPosition}");
        }
        
        // Spawn the model
        GameObject modelPrefab = GetModelPrefab(response);
        if (modelPrefab != null)
        {
            GameObject spawnedModel = Instantiate(modelPrefab, spawnPosition, Quaternion.identity);
            
            // Apply rotation from API if provided
            if (response.position != null && response.position.rotation_y != 0)
            {
                spawnedModel.transform.Rotate(Vector3.up, response.position.rotation_y);
            }
            else
            {
                // Face the camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Vector3 directionToCamera = mainCamera.transform.position - spawnPosition;
                    directionToCamera.y = 0; // Keep horizontal
                    if (directionToCamera != Vector3.zero)
                    {
                        spawnedModel.transform.rotation = Quaternion.LookRotation(directionToCamera, spawnNormal);
                    }
                }
            }
            
            // Apply scale if provided
            if (response.model_data != null && response.model_data.scale != Vector3.zero)
            {
                spawnedModel.transform.localScale = response.model_data.scale;
            }
            
            // Apply rotation if provided in model_data
            if (response.model_data != null && response.model_data.rotation != Vector3.zero)
            {
                spawnedModel.transform.Rotate(response.model_data.rotation);
            }
            
            Debug.Log($"Model '{modelPrefab.name}' spawned successfully at: {spawnPosition}");
        }
        else
        {
            Debug.LogError("No model prefab found for API response. Check model_type or modelPrefabs list.");
            if (statusText != null)
                statusText.text = "Error: Model prefab not found";
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Gets the appropriate prefab based on API response.
    /// </summary>
    private GameObject GetModelPrefab(APIResponse response)
    {
        // Try to get prefab by model_type or prefab_name
        if (!string.IsNullOrEmpty(response.model_type))
        {
            // Look for prefab matching model_type
            foreach (var prefab in modelPrefabs)
            {
                if (prefab != null && prefab.name.ToLower().Contains(response.model_type.ToLower()))
                {
                    return prefab;
                }
            }
        }
        
        if (response.model_data != null && !string.IsNullOrEmpty(response.model_data.prefab_name))
        {
            // Look for prefab matching prefab_name (exact match)
            foreach (var prefab in modelPrefabs)
            {
                if (prefab != null && prefab.name == response.model_data.prefab_name)
                {
                    return prefab;
                }
            }
        }
        
        // Fallback: Use first prefab if available
        if (modelPrefabs.Count > 0 && modelPrefabs[0] != null)
        {
            Debug.LogWarning("No matching prefab found, using first prefab in list");
            return modelPrefabs[0];
        }
        else if (arObjectSpawner != null && arObjectSpawner.objectPrefabs.Count > 0)
        {
            int index = Mathf.Clamp(arObjectSpawner.spawnOptionIndex, 0, arObjectSpawner.objectPrefabs.Count - 1);
            Debug.LogWarning("No prefabs in ModelManager, using ObjectSpawner prefab");
            return arObjectSpawner.objectPrefabs[index];
        }
        
        Debug.LogError("No model prefabs available!");
        return null;
    }
    
    #if GLTFAST
    /// <summary>
    /// Loads a GLB model using glTFast library.
    /// </summary>
    private IEnumerator LoadGLBWithGLTFast(string glbFilePath, Text statusText = null)
    {
        Debug.Log($"Loading GLB with glTFast: {glbFilePath}");
        
        if (statusText != null)
            statusText.text = "Loading 3D model...";
        
        // Create a temporary GameObject to hold the loaded model
        GameObject modelContainer = new GameObject($"GLB_Model_{System.IO.Path.GetFileNameWithoutExtension(glbFilePath)}");
        
        // Get the file as a file:// URI for local files
        string uri = "file://" + glbFilePath;
        
        var gltf = new GltfImport();
        
        // Load the GLB file (async method returns Task<bool>)
        var loadTask = gltf.Load(uri);
        
        while (!loadTask.IsCompleted)
        {
            yield return null;
            // Update progress if available
            try
            {
                if (statusText != null)
                    statusText.text = "Loading model...";
            }
            catch { }
        }
        
        // Check if loading was successful
        bool loadSuccess = false;
        try
        {
            loadSuccess = loadTask.Result;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading GLB: {e.Message}");
        }
        
        if (loadSuccess)
        {
            Debug.Log("GLB file loaded successfully!");
            
            if (statusText != null)
                statusText.text = "Instantiating model...";
            
            // Instantiate the model (async method returns Task<bool>)
            // The model will be instantiated as a child of modelContainer.transform
            var instantiateTask = gltf.InstantiateMainSceneAsync(modelContainer.transform);
            
            // Wait for instantiation
            while (!instantiateTask.IsCompleted)
            {
                yield return null;
            }
            
            bool instantiateSuccess = false;
            try
            {
                instantiateSuccess = instantiateTask.Result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error instantiating GLB: {e.Message}");
            }
            
            if (instantiateSuccess)
            {
                Debug.Log("GLB model instantiated successfully!");
                
                // Position the model in AR space
                Vector3 spawnPosition = Vector3.zero;
                Vector3 spawnNormal = Vector3.up;
                bool foundARPlane = false;
                
                #if AR_FOUNDATION_PRESENT
                ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
                if (planeManager != null)
                {
                    foreach (var trackable in planeManager.trackables)
                    {
                        if (trackable.alignment == PlaneAlignment.HorizontalUp)
                        {
                            spawnPosition = trackable.center;
                            spawnNormal = trackable.normal;
                            foundARPlane = true;
                            break;
                        }
                    }
                }
                #endif
                
                if (!foundARPlane)
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        Vector3 cameraForward = mainCamera.transform.forward;
                        cameraForward.y = 0;
                        cameraForward.Normalize();
                        float distance = Mathf.Clamp(defaultSpawnOffset.z, minSpawnDistance, maxSpawnDistance);
                        spawnPosition = mainCamera.transform.position + cameraForward * distance;
                        spawnPosition.y = mainCamera.transform.position.y;
                    }
                }
                
                modelContainer.transform.position = spawnPosition;
                
                // Face the camera
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 directionToCamera = cam.transform.position - spawnPosition;
                    directionToCamera.y = 0;
                    if (directionToCamera != Vector3.zero)
                    {
                        modelContainer.transform.rotation = Quaternion.LookRotation(directionToCamera, spawnNormal);
                    }
                }
                
                // Set parent if specified
                if (modelParent != null)
                {
                    modelContainer.transform.SetParent(modelParent);
                }
                
                if (statusText != null)
                    statusText.text = "Model loaded successfully!";
                
                Debug.Log($"GLB model spawned at: {spawnPosition}");
            }
            else
            {
                Debug.LogError("Failed to instantiate GLB model");
                if (statusText != null)
                    statusText.text = "Error: Failed to instantiate model";
                Destroy(modelContainer);
                
                // Fallback to placeholder
                yield return StartCoroutine(SpawnPlaceholderModel(statusText));
            }
        }
        else
        {
            Debug.LogError("Failed to load GLB file");
            if (statusText != null)
                statusText.text = "Error: Failed to load GLB file";
            Destroy(modelContainer);
            
            // Fallback to placeholder
            yield return StartCoroutine(SpawnPlaceholderModel(statusText));
        }
    }
    #endif
}

