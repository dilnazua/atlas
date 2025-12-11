using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class JobCreationResponse
{
    public string job_id;
    public string task_id;
    public string status;
    public string message;
}

[System.Serializable]
public class JobStatusResponse
{
    public string job_id;
    public string status; // "pending", "processing", "completed", "failed"
    public float progress; // 0.0 to 1.0
    public string stage; // e.g., "feature_extraction", "mesh_generation", etc.
    public string message;
    public string error;
}

[System.Serializable]
public class APIResponse
{
    public string model_url;
    public string model_type;
    public string status;
    public string message;
    public ModelPosition position;
    public ModelData model_data;
    // Job creation fields
    public string job_id;
    public string task_id;
}

[System.Serializable]
public class ModelPosition
{
    public float x;
    public float y;
    public float z;
    public float rotation_y;
}

[System.Serializable]
public class ModelData
{
    public string prefab_name;
    public Vector3 scale;
    public Vector3 rotation;
}

public class PhotoManager : MonoBehaviour
{
    [Header("UI References")]
    public Button pickFromGalleryButton;
    public Button takePhotoButton;
    public Button uploadButton;
    public Text statusText;
    
    [Header("API Configuration")]
    [Tooltip("Your API base URL (configure in Unity Inspector)")]
    public string apiBaseUrl = "YOUR_API_URL";
    
    [Tooltip("Automatically upload photos after selection (if false, user must click Upload button)")]
    public bool autoUploadAfterSelection = true;
    
    [Tooltip("Your API key for authentication")]
    public string apiKey = "YOUR_API_KEY_HERE";
    
    [Tooltip("API request options (e.g., resolution_level)")]
    public int resolutionLevel = 2;
    
    [Header("Job Polling")]
    [Tooltip("How often to check job status (in seconds)")]
    public float pollInterval = 2f;
    
    [Tooltip("Maximum time to wait for job completion (in seconds)")]
    public float maxWaitTime = 300f; // 5 minutes
    
    [Header("Model Manager")]
    [Tooltip("Reference to ModelManager for handling API responses and AR spawning")]
    public ModelManager modelManager;
    
    // Helper property for job creation endpoint
    private string JobCreationEndpoint => $"{apiBaseUrl}/api/v1/jobs";
    
    // Helper method for job status endpoint
    private string JobStatusEndpoint(string jobId) => $"{apiBaseUrl}/api/v1/jobs/{jobId}";
    
    // Helper method for artifact endpoint
    private string ArtifactEndpoint(string jobId) => $"{apiBaseUrl}/api/v1/jobs/{jobId}/artifact";
    
    private List<Texture2D> selectedPhotos = new List<Texture2D>();
    private int minPhotosRequired = 1;
    
    /// <summary>
    /// Checks if we're running on a mobile platform (iOS or Android).
    /// </summary>
    private bool IsMobilePlatform()
    {
        return Application.platform == RuntimePlatform.Android || 
               Application.platform == RuntimePlatform.IPhonePlayer;
    }

    void Start()
    {
        Debug.Log("PHOTOMANAGER INITIALIZED");
        Debug.Log($"API Base URL: {apiBaseUrl}");
        Debug.Log($"Model Manager Assigned: {modelManager != null}");
        
        if (pickFromGalleryButton != null)
        pickFromGalleryButton.onClick.AddListener(PickMultipleFromGallery);
        if (takePhotoButton != null)
        takePhotoButton.onClick.AddListener(TakePhoto);
        if (uploadButton != null)
        uploadButton.onClick.AddListener(StartUpload);
        
        UpdateStatus();
        
        // Log configuration status
        if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_API_KEY"))
        {
            Debug.LogWarning("API Key not configured! Set it in PhotoManager Inspector.");
        }
        
        if (modelManager == null)
        {
            Debug.LogWarning("ModelManager not assigned! Models won't be spawned. Assign it in PhotoManager Inspector.");
        }
    }

    /// <summary>
    /// Picks multiple photos from the device gallery.
    /// Can be called from other scripts like ARTemplateMenuManager.
    /// On mobile: Uses native gallery picker.
    /// On Editor/Desktop: Uses file system picker (allows multiple selections).
    /// </summary>
    public void PickMultipleFromGallery()
    {
        Debug.Log("PICK MULTIPLE FROM GALLERY CALLED");
        
        if (IsMobilePlatform())
        {
            Debug.Log("Using native gallery (mobile platform detected)");
            // Use native gallery on mobile devices
            NativeGallery.GetImagesFromGallery((paths) =>
            {
                Debug.Log($"NativeGallery callback received. Paths: {(paths != null ? paths.Length.ToString() : "null")}");
                if (paths != null && paths.Length > 0)
                {
                    foreach (string path in paths)
                    {
                        Texture2D texture = NativeGallery.LoadImageAtPath(path, 2048);
                        if (texture != null)
                        {
                            selectedPhotos.Add(texture);
                            Debug.Log($"Added photo: {path} ({texture.width}x{texture.height})");
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to load texture from: {path}");
                        }
                    }
                    UpdateStatus();
                    
                    if (statusText != null)
                        statusText.text = $"Added {paths.Length} photo(s) from gallery";
                    Debug.Log($"Total photos selected: {selectedPhotos.Count}");
                }
                else
                {
                    Debug.LogWarning("No photos selected or paths is null");
                }
            }, "Select at least 10 photos", "image/*");
        }
        else
        {
            Debug.Log("Using file system picker (Editor/Desktop platform detected)");
            // Use file system picker in Editor/Desktop
            StartCoroutine(PickMultipleFilesFromSystem());
        }
    }
    
    /// <summary>
    /// Coroutine to pick multiple image files from the file system (Editor/Desktop fallback).
    /// Allows user to select multiple files one by one.
    /// </summary>
    private IEnumerator PickMultipleFilesFromSystem()
    {
        #if UNITY_EDITOR
        List<string> selectedPaths = new List<string>();
        string lastDirectory = "";
        
        // Allow user to select multiple files
        while (true)
        {
            string[] filters = new string[] { "Image files", "png,jpg,jpeg", "All files", "*" };
            string path = EditorUtility.OpenFilePanelWithFilters("Select Image File", lastDirectory, filters);
            
            if (string.IsNullOrEmpty(path))
            {
                // User cancelled or closed the dialog
                break;
            }
            
            selectedPaths.Add(path);
            
            // Save directory for next selection
            lastDirectory = Path.GetDirectoryName(path);
            
            // Ask if user wants to select more files
            int result = EditorUtility.DisplayDialogComplex(
                "Add More Photos?",
                $"Selected {selectedPaths.Count} photo(s). Do you want to select more?",
                "Yes, Add More",
                "No, Done",
                "Cancel All"
            );
            
            if (result == 1) // "No, Done"
            {
                break;
            }
            else if (result == 2) // "Cancel All"
            {
                selectedPaths.Clear();
                break;
            }
            // result == 0 means "Yes, Add More" - continue loop
        }
        
        // Load all selected images
        if (selectedPaths.Count > 0)
        {
            int loadedCount = 0;
            foreach (string path in selectedPaths)
            {
                // Load image from file system
                if (File.Exists(path))
                {
                    byte[] fileData = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2);
                    
                    if (texture.LoadImage(fileData))
                    {
                        // Resize if too large
                        if (texture.width > 2048 || texture.height > 2048)
                        {
                            Texture2D resized = ResizeTexture(texture, 2048);
                            Destroy(texture);
                            texture = resized;
                            Debug.Log($"  Resized to: {texture.width}x{texture.height}");
                        }
                        
                        selectedPhotos.Add(texture);
                        loadedCount++;
                        Debug.Log($"  Successfully loaded: {texture.width}x{texture.height}");
                    }
                    else
                    {
                        // Loading failed - destroy the texture and log error
                        Destroy(texture);
                        Debug.LogWarning($"Failed to load image: {path}");
                    }
                }
            }
            
            UpdateStatus();
            
            if (statusText != null)
                statusText.text = $"Added {loadedCount} photo(s) from file system";
            
            Debug.Log($"Loaded {loadedCount} out of {selectedPaths.Count} selected images.");
        }
        else
        {
            if (statusText != null)
                statusText.text = "No photos selected";
        }
        #else
        // Fallback for standalone desktop builds (not in editor)
        if (statusText != null)
            statusText.text = "Gallery access only available on mobile devices";
        Debug.LogWarning("Gallery picker is only available on mobile devices or in Unity Editor.");
        #endif
        
        yield return null;
    }
    
    /// <summary>
    /// Resizes a texture to fit within maxSize while maintaining aspect ratio.
    /// </summary>
    private Texture2D ResizeTexture(Texture2D source, int maxSize)
    {
        int width = source.width;
        int height = source.height;
        
        // Calculate new size maintaining aspect ratio
        if (width > maxSize || height > maxSize)
        {
            float ratio = (float)width / height;
            if (width > height)
            {
                width = maxSize;
                height = Mathf.RoundToInt(maxSize / ratio);
            }
            else
            {
                height = maxSize;
                width = Mathf.RoundToInt(maxSize * ratio);
            }
        }
        
        // Create resized texture
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(source, rt);
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        
        Texture2D resized = new Texture2D(width, height);
        resized.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resized.Apply();
        
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        
        return resized;
    }

    /// <summary>
    /// Takes a photo using the device camera.
    /// Can be called from other scripts like ARTemplateMenuManager.
    /// </summary>
    public void TakePhoto()
    {
        NativeCamera.TakePicture((path) =>
        {
            if (path != null)
            {
                Texture2D texture = NativeGallery.LoadImageAtPath(path, 2048);
                if (texture != null)
                {
                    selectedPhotos.Add(texture);
                    UpdateStatus();
                }
            }
        }, 2048);
    }

    void UpdateStatus()
    {
        if (statusText != null)
        {
            statusText.text = $"Photos: {selectedPhotos.Count}/{minPhotosRequired}";
        }
        
        if (uploadButton != null)
        {
            bool canUpload = selectedPhotos.Count >= minPhotosRequired;
            uploadButton.interactable = canUpload;
            if (canUpload)
            {
                Debug.Log($"Upload button is now ENABLED ({selectedPhotos.Count} photos selected)");
            }
        }
        
        // Auto-upload if enabled and we have enough photos
        if (autoUploadAfterSelection && selectedPhotos.Count >= minPhotosRequired)
        {
            Debug.Log("Auto-upload enabled: Starting upload automatically...");
            StartUpload();
        }
    }

    void StartUpload()
    {
        Debug.Log("START UPLOAD CALLED");
        Debug.Log($"Selected photos count: {selectedPhotos.Count}, Minimum required: {minPhotosRequired}");
        
        if (selectedPhotos.Count >= minPhotosRequired)
        {
            Debug.Log("Starting upload coroutine...");
            StartCoroutine(UploadPhotosCoroutine());
        }
        else
        {
            Debug.LogWarning($"Cannot upload: Need at least {minPhotosRequired} photo(s), but only {selectedPhotos.Count} selected");
            if (statusText != null)
                statusText.text = $"Need at least {minPhotosRequired} photo(s)";
        }
    }

    /// <summary>
    /// Uploads photos to the API and spawns the returned model in AR space.
    /// </summary>
    IEnumerator UploadPhotosCoroutine()
    {
        Debug.Log("STARTING API UPLOAD");
        Debug.Log($"Uploading {selectedPhotos.Count} photo(s) to API");
        
        if (statusText != null)
            statusText.text = "Uploading photos...";
        
        // Validate API endpoint
        if (string.IsNullOrEmpty(apiBaseUrl) || apiBaseUrl.Contains("YOUR_API_URL") || apiBaseUrl.Contains("YOUR_IP") || apiBaseUrl.Contains("YOUR_API"))
        {
            Debug.LogError($"Invalid API Base URL: {apiBaseUrl}. Please set a valid URL in the PhotoManager component.");
            if (statusText != null)
                statusText.text = "Error: API endpoint not configured";
            yield break;
        }
        
        // Validate API key
        if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_API_KEY"))
        {
            Debug.LogWarning("API Key not set or using placeholder value. Request may fail authentication.");
        }
        
        string jobEndpoint = JobCreationEndpoint;
        Debug.Log($"API Base URL: {apiBaseUrl}");
        Debug.Log($"Job Creation Endpoint: {jobEndpoint}");
        Debug.Log($"Resolution Level: {resolutionLevel}");
        Debug.Log($"API Key Set: {!string.IsNullOrEmpty(apiKey) && !apiKey.Contains("YOUR_API_KEY")}");
        
        // Prepare multipart form data with photos (using "images" field name to match API)
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        
        Debug.Log($"Preparing {selectedPhotos.Count} image(s) for upload...");
        long totalSize = 0;
        
        for (int i = 0; i < selectedPhotos.Count; i++)
        {
            byte[] imageData = selectedPhotos[i].EncodeToJPG(75);
            totalSize += imageData.Length;
            // API expects field name "images" (same name for all files)
            formData.Add(new MultipartFormFileSection($"images", imageData, $"image_{i}.jpg", "image/jpeg"));
            Debug.Log($"  - Image {i + 1}: {imageData.Length / 1024} KB ({selectedPhotos[i].width}x{selectedPhotos[i].height})");
        }
        
        Debug.Log($"Total upload size: {totalSize / 1024} KB ({totalSize / 1024 / 1024.0:F2} MB)");
        
        string optionsJson = $"{{\"resolution_level\": {resolutionLevel}}}";
        formData.Add(new MultipartFormDataSection("options", optionsJson));
        Debug.Log($"Options JSON: {optionsJson}");
        
        Debug.Log("Creating UnityWebRequest...");
        UnityWebRequest www = UnityWebRequest.Post(jobEndpoint, formData);
        www.timeout = 120;
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            Debug.Log("Authorization header added");
        }
        else
        {
            Debug.LogWarning("No API key provided - request may fail");
        }
        
        // Send request
        Debug.Log($"Sending POST request to: {jobEndpoint}");
        Debug.Log("Waiting for API response...");
        
        yield return www.SendWebRequest();
        
        Debug.Log($"API RESPONSE RECEIVED");
        Debug.Log($"Response Code: {www.responseCode}");
        Debug.Log($"Result: {www.result}");
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Check if we got a 201 (Created) response
            if (www.responseCode == 201)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log("JOB CREATED SUCCESSFULLY!");
                Debug.Log($"Response Length: {responseText.Length} characters");
                Debug.Log($"Full API Response: {responseText}");
                
                // Parse job creation response
                JobCreationResponse jobResponse = ParseJobCreationResponse(responseText);
                
                if (jobResponse != null && !string.IsNullOrEmpty(jobResponse.job_id))
                {
                    Debug.Log($"Job ID: {jobResponse.job_id}");
                    Debug.Log($"Task ID: {jobResponse.task_id ?? "N/A"}");
                    
                    if (statusText != null)
                        statusText.text = $"Job created! Processing... (Job: {jobResponse.job_id})";
                    
                    // Poll for job completion and download artifact
                    yield return StartCoroutine(PollJobStatusAndDownload(jobResponse.job_id, statusText));
                    
                    // Clear photos after successful processing
                    ClearSelectedPhotos();
                }
                else
                {
                    Debug.LogError("Failed to parse job creation response or missing job_id");
                    if (statusText != null)
                        statusText.text = "Error: Invalid response from API";
                }
            }
            else
            {
                Debug.LogWarning($"Unexpected success response code: {www.responseCode}");
                Debug.Log($"Response: {www.downloadHandler.text}");
            }
        }
        else
        {
            Debug.LogError($"API CALL FAILED!");
            Debug.LogError($"Error Type: {www.result}");
            Debug.LogError($"Error Message: {www.error}");
            Debug.LogError($"Response Code: {www.responseCode}");
            
            if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
            {
                Debug.LogError($"Error Response Body: {www.downloadHandler.text}");
            }
            
            if (statusText != null)
                statusText.text = $"Upload Failed: {www.error}";
            
            // Provide helpful error messages
            if (www.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError("Connection Error");
            }
            else if (www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"HTTP Error {www.responseCode} - Server returned an error");
            }
        }
        
        Debug.Log("UPLOAD COROUTINE COMPLETE");
        
        UpdateStatus();
    }
    
    /// <summary>
    /// Polls job status until completion, then downloads and loads the artifact.
    /// </summary>
    private IEnumerator PollJobStatusAndDownload(string jobId, Text statusText = null)
    {
        Debug.Log($"STARTING JOB STATUS POLLING");
        Debug.Log($"Job ID: {jobId}");
        float elapsedTime = 0f;
        
        while (elapsedTime < maxWaitTime)
        {
            if (statusText != null)
                statusText.text = $"Checking job status... ({Mathf.FloorToInt(elapsedTime)}s)";
            
            // Get job status
            JobStatusResponse statusResponse = null;
            yield return StartCoroutine(GetJobStatusCoroutine(jobId, (response) => statusResponse = response));
            
            if (statusResponse == null)
            {
                Debug.LogError("Failed to get job status");
                if (statusText != null)
                    statusText.text = "Error: Failed to get job status";
                yield break;
            }
            
            Debug.Log($"Job Status: {statusResponse.status}, Progress: {statusResponse.progress:F1}%, Stage: {statusResponse.stage ?? "N/A"}");
            
            if (statusText != null)
            {
                if (!string.IsNullOrEmpty(statusResponse.stage))
                    statusText.text = $"Processing: {statusResponse.stage} ({statusResponse.progress:F0}%)";
                else
                    statusText.text = $"Processing... ({statusResponse.progress:F0}%)";
            }
            
            // Check if job is completed
            if (statusResponse.status == "completed")
            {
                Debug.Log("Job completed! Downloading artifact...");
                if (statusText != null)
                    statusText.text = "Job completed! Downloading model...";
                
                // Download and load artifact
                yield return StartCoroutine(DownloadAndLoadArtifact(jobId, statusText));
                yield break;
            }
            else if (statusResponse.status == "failed")
            {
                string errorMsg = !string.IsNullOrEmpty(statusResponse.message) ? statusResponse.message : "Unknown error";
                Debug.LogError($"Job failed: {errorMsg}");

                if (statusText != null)
                    statusText.text = $"Job failed: {errorMsg}";
                
                yield break;
            }
            
            // Wait before next poll
            yield return new WaitForSeconds(pollInterval);
            elapsedTime += pollInterval;
        }
        
        Debug.LogError($"Timeout: Job did not complete within {maxWaitTime} seconds");
        if (statusText != null)
            statusText.text = $"Timeout: Job took longer than {maxWaitTime}s";
    }
    
    /// <summary>
    /// Gets the current status of a job (wrapper for coroutine).
    /// </summary>
    private IEnumerator GetJobStatusCoroutine(string jobId, System.Action<JobStatusResponse> callback)
    {
        string statusEndpoint = JobStatusEndpoint(jobId);
        
        Debug.Log($"Getting job status from: {statusEndpoint}");
        
        UnityWebRequest www = UnityWebRequest.Get(statusEndpoint);
        www.timeout = 30;
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        }
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success && www.responseCode == 200)
        {
            string responseText = www.downloadHandler.text;
            Debug.Log($"Job status response: {responseText}");
            JobStatusResponse statusResponse = ParseJobStatusResponse(responseText);
            callback?.Invoke(statusResponse);
        }
        else
        {
            Debug.LogError($"Failed to get job status: {www.error}");
            Debug.LogError($"Response Code: {www.responseCode}");
            if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
            {
                Debug.LogError($"Error Response: {www.downloadHandler.text}");
            }
            callback?.Invoke(null);
        }
    }
    
    /// <summary>
    /// Downloads the GLB artifact and loads it in Unity.
    /// </summary>
    private IEnumerator DownloadAndLoadArtifact(string jobId, Text statusText = null)
    {
        string artifactEndpoint = ArtifactEndpoint(jobId);
        
        Debug.Log($"DOWNLOADING ARTIFACT");
        Debug.Log($"Artifact URL: {artifactEndpoint}");
        
        if (statusText != null)
            statusText.text = "Downloading 3D model...";
        
        UnityWebRequest www = UnityWebRequest.Get(artifactEndpoint);
        www.timeout = 120;
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        }
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success && www.responseCode == 200)
        {
            byte[] glbData = www.downloadHandler.data;
            Debug.Log($"Artifact downloaded! Size: {glbData.Length / 1024} KB ({glbData.Length / 1024 / 1024.0:F2} MB)");
            
            if (statusText != null)
                statusText.text = "Model downloaded! Loading...";
            
            // Save GLB to temporary file
            string tempPath = $"{Application.persistentDataPath}/model_{jobId}.glb";
            System.IO.File.WriteAllBytes(tempPath, glbData);
            Debug.Log($"GLB saved to: {tempPath}");
            
            // Load and spawn the model via ModelManager
            if (modelManager != null)
            {
                yield return StartCoroutine(modelManager.LoadGLBModel(tempPath, statusText));
            }
            else
            {
                Debug.LogWarning("ModelManager not assigned! GLB downloaded but won't be loaded.");
                if (statusText != null)
                    statusText.text = "GLB downloaded but ModelManager not configured";
            }
        }
        else
        {
            Debug.LogError($"Failed to download artifact!");
            Debug.LogError($"Error: {www.error}");
            Debug.LogError($"Response Code: {www.responseCode}");
            if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
            {
                Debug.LogError($"Error Response: {www.downloadHandler.text}");
            }
            if (statusText != null)
                statusText.text = $"Download failed: {www.error}";
        }
    }
    
    /// <summary>
    /// Parses the job creation response JSON.
    /// </summary>
    private JobCreationResponse ParseJobCreationResponse(string jsonText)
    {
        try
        {
            JobCreationResponse response = JsonUtility.FromJson<JobCreationResponse>(jsonText);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse job creation response: {e.Message}\nResponse: {jsonText}");
            return null;
        }
    }
    
    /// <summary>
    /// Parses the job status response JSON.
    /// </summary>
    private JobStatusResponse ParseJobStatusResponse(string jsonText)
    {
        try
        {
            JobStatusResponse response = JsonUtility.FromJson<JobStatusResponse>(jsonText);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse job status response: {e.Message}\nResponse: {jsonText}");
            return null;
        }
    }
    
    /// <summary>
    /// Parses the JSON response from the API (legacy support).
    /// </summary>
    private APIResponse ParseAPIResponse(string jsonText)
    {
        try
        {
            // Handle both full JSON objects and simple strings
            APIResponse response = JsonUtility.FromJson<APIResponse>(jsonText);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse API response: {e.Message}\nResponse text: {jsonText}");
            return null;
        }
    }
    
    /// <summary>
    /// Clears all selected photos from memory.
    /// </summary>
    private void ClearSelectedPhotos()
    {
        foreach (var photo in selectedPhotos)
        {
            if (photo != null)
                Destroy(photo);
        }
        selectedPhotos.Clear();
    }
    
    void OnDestroy()
    {
        ClearSelectedPhotos();
    }
}