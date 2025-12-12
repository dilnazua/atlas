# Atlas - 3D Object Reconstruction and Spatial Relocation Using Mobile Augmented Reality

Atlas is a Unity-based mobile AR application that enables users to reconstruct 3D objects from photos and place them in augmented reality space. The application captures or uploads multiple images, processes them through a backend API for 3D reconstruction, and then renders the resulting 3D models in AR.

## Project Overview

Atlas combines mobile AR technology with photogrammetry-based 3D reconstruction to create an immersive experience where users can:
- Capture or upload multiple photos of real-world objects
- Process images through a 3D reconstruction API
- View and interact with reconstructed 3D models in augmented reality
- Place models accurately in 3D space using AR plane detection

## Features

### Core Functionality
- **Photo Capture**: Take photos directly using the device camera
- **Gallery Integration**: Upload multiple photos from device gallery
- **3D Reconstruction API Integration**: Submit photos to a backend service for processing
- **Job-based Processing**: Asynchronous job creation and status polling
- **GLB Model Loading**: Runtime loading of GLB/glTF 3D models using glTFast
- **AR Model Placement**: Automatic placement of 3D models on detected AR planes
- **AR Interaction**: Tap-to-place, scale, rotate, and manipulate AR objects
- **Debug Tools**: AR plane visualization and debug menus for development

### User Interface
- Welcome and onboarding panels
- Photo selection and upload interface
- Real-time status updates during processing
- AR object creation menu
- Object deletion and management
- Responsive UI for both portrait and landscape orientations

## 🛠️ Technology Stack

### Unity Packages
- **AR Foundation** (v6.2.0): Cross-platform AR framework
  - ARCore (v6.2.0): Android AR support
  - ARKit (v6.2.0): iOS AR support
- **XR Interaction Toolkit** (v3.2.1): AR interaction systems
- **Universal Render Pipeline** (v17.2.0): Modern rendering pipeline
- **Input System** (v1.14.2): Enhanced input handling
- **glTFast**: Runtime GLB/glTF model loading

### Third-Party Assets
- **Native Gallery**: Gallery photo picker for iOS and Android
- **Native Camera**: Camera photo capture for iOS and Android

### Development Tools
- Unity Editor (see Requirements)
- Platform-specific AR SDKs (ARCore/ARKit)

## Requirements

### Development
- **Unity Version**: Compatible with Unity 2022.3 LTS or later (project uses URP 17.2.0)
- **Platform Support**: iOS 11+ or Android 7.0+ (API Level 24+)
- **Development Environment**:
  - For iOS: macOS with Xcode installed
  - For Android: Android SDK and JDK installed

### Runtime
- **iOS**: 
  - Device with ARKit support (iPhone 6s or later, iPad Pro, etc.)
  - iOS 11.0 or later
- **Android**:
  - Device with ARCore support
  - Android 7.0 (API Level 24) or later
  - ARCore must be installed on the device

### API Requirements
- Access to a 3D reconstruction API endpoint
- Valid API key for authentication
- Network connectivity for uploads and downloads

## Getting Started

### Installation

1. **Clone the Repository**
   ```bash
   git clone <repository-url>
   cd atlas
   ```

2. **Open in Unity**
   - Launch Unity Hub
   - Click "Add" and select the `atlas` folder
   - Open the project (Unity will import packages automatically)

3. **Configure Platform**
   - For iOS: File → Build Settings → Switch Platform → iOS
   - For Android: File → Build Settings → Switch Platform → Android
   - Configure XR Plug-in Management:
     - Edit → Project Settings → XR Plug-in Management
     - Enable ARKit (iOS) or ARCore (Android)

4. **Configure API Settings**
   - Open `Assets/Scenes/SampleScene.unity`
   - Find the `PhotoManager` component in the scene
   - Set the `API Base URL` to your reconstruction API endpoint
   - Set the `API Key` for authentication
   - Configure `Resolution Level` if needed

5. **Configure Model Manager**
   - Find the `ModelManager` component in the scene
   - Assign model prefabs to the `Model Prefabs` list (optional fallback)
   - Enable `Enable GLB Loading` if glTFast is installed
   - Set spawn distance parameters as needed

### Building the Project

#### iOS Build
1. Switch platform to iOS
2. File → Build Settings → Build
3. Open the generated Xcode project
4. Configure signing and capabilities in Xcode
5. Build and run on device

#### Android Build
1. Switch platform to Android
2. Configure Player Settings:
   - Minimum API Level: 24 (Android 7.0)
   - Target API Level: Latest
3. File → Build Settings → Build
4. Install APK on ARCore-compatible device

## 📁 Project Structure

```
atlas/
├── Assets/
│   ├── MobileARTemplateAssets/
│   │   ├── Scripts/
│   │   │   ├── ARTemplateMenuManager.cs    # Main UI and menu management
│   │   │   ├── PhotoManager.cs             # Photo capture/upload and API integration
│   │   │   ├── ModelManager.cs             # 3D model spawning and GLB loading
│   │   │   ├── GoalManager.cs              # Onboarding and tutorial system
│   │   │   ├── SceneLoader.cs              # Scene management utilities
│   │   │   └── ARFeatheredPlaneMeshVisualizerCompanion.cs
│   │   ├── Prefabs/                        # AR plane and object prefabs
│   │   ├── UI/                             # User interface assets
│   │   ├── Materials/                      # Shader materials
│   │   └── Shaders/                        # Custom shaders
│   ├── Runtime/
│   │   ├── NativeCamera/                   # Camera integration
│   │   └── NativeGallery/                  # Gallery integration
│   ├── Plugins/
│   │   └── glTFast/                        # GLB runtime loader
│   ├── Scenes/
│   │   └── SampleScene.unity               # Main AR scene
│   └── Settings/                           # URP and project settings
├── ProjectSettings/                        # Unity project configuration
├── Packages/                               # Package dependencies
└── README.md                               # This file
```

## Configuration

### API Configuration

The application requires API configuration in the `PhotoManager` component:

```csharp
API Base URL: "https://your-api-url.com"
API Key: "your-api-key-here"
Resolution Level: 2  // Adjust based on API requirements
Poll Interval: 2.0   // Seconds between job status checks
Max Wait Time: 300   // Maximum seconds to wait for job completion
```

### Model Manager Configuration

Configure model spawning behavior in the `ModelManager` component:

```csharp
Default Spawn Offset: (0, 0, 2)  // Default position relative to camera
Min Spawn Distance: 1.0          // Minimum distance from camera
Max Spawn Distance: 5.0          // Maximum distance from camera
Enable GLB Loading: true/false   // Enable runtime GLB loading
```

### AR Settings

Configure AR Foundation settings in:
- Edit → Project Settings → XR Plug-in Management → ARCore/ARKit
- Adjust plane detection, image tracking, and other AR features as needed

## API Integration

### API Endpoints

The application communicates with three main API endpoints:

1. **Job Creation** (POST)
   - Endpoint: `{apiBaseUrl}/api/v1/jobs`
   - Method: `POST`
   - Body: Multipart form data with `images[]` and `options`
   - Response: `JobCreationResponse` with `job_id` and `task_id`

2. **Job Status** (GET)
   - Endpoint: `{apiBaseUrl}/api/v1/jobs/{jobId}`
   - Method: `GET`
   - Response: `JobStatusResponse` with status, progress, and stage

3. **Artifact Download** (GET)
   - Endpoint: `{apiBaseUrl}/api/v1/jobs/{jobId}/artifact`
   - Method: `GET`
   - Response: GLB file binary data

### Request Format

**Job Creation Request:**
```
POST /api/v1/jobs
Authorization: Bearer {apiKey}
Content-Type: multipart/form-data

images[]: [JPEG image data]
images[]: [JPEG image data]
...
options: {"resolution_level": 2}
```

**Response Format:**
```json
{
  "job_id": "uuid-here",
  "task_id": "task-uuid",
  "status": "pending",
  "message": "Job created successfully"
}
```

### Job Status Response
```json
{
  "job_id": "uuid-here",
  "status": "processing|completed|failed",
  "progress": 0.75,
  "stage": "mesh_generation",
  "message": "Processing...",
  "error": null
}
```

## Usage

### User Workflow

1. **Launch Application**
   - App initializes AR session
   - Welcome panel appears

2. **Select Photos**
   - Choose "Take Photo" to use camera
   - Choose "Upload from Gallery" to select existing photos
   - Select multiple photos (minimum 1 required)

3. **Upload and Process**
   - Photos are automatically uploaded (or click Upload button)
   - Job is created on the server
   - Status updates show processing progress

4. **View Model in AR**
   - Once processing completes, GLB model downloads
   - Model automatically spawns on detected AR plane
   - Model can be manipulated (move, scale, rotate)

5. **Interact with Models**
   - Tap AR planes to place additional models
   - Select objects to delete them
   - Use debug menu for development tools

## Key Components

### PhotoManager
Handles all photo-related operations:
- Camera capture via Native Camera
- Gallery selection via Native Gallery
- API communication (job creation, status polling, artifact download)
- Image encoding and upload
- Error handling and status updates

### ModelManager
Manages 3D model operations:
- API response parsing
- Model prefab selection
- AR plane detection and placement
- GLB runtime loading via glTFast
- Model positioning and orientation
- Fallback placeholder spawning

### ARTemplateMenuManager
UI and interaction management:
- Menu show/hide animations
- Object creation menu
- Delete button management
- Debug menu toggling
- Welcome and upload panel navigation
- AR plane visualization control


## Permissions

The application requires the following permissions:

### iOS (Info.plist)
- `NSCameraUsageDescription`: "This app needs camera access to take photos for 3D reconstruction"
- `NSPhotoLibraryUsageDescription`: "This app needs photo library access to select images for 3D reconstruction"

### Android (AndroidManifest.xml)
- `CAMERA`: Camera access for photo capture
- `READ_EXTERNAL_STORAGE`: Read gallery images
- `WRITE_EXTERNAL_STORAGE`: Save downloaded models (if needed)
- `INTERNET`: Network access for API calls


## Additional Resources

- [Unity AR Foundation Documentation](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@latest)
- [XR Interaction Toolkit Documentation](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest)
- [glTFast Documentation](https://github.com/atteneder/glTFast)
- [Native Gallery Asset Store Page](https://assetstore.unity.com/packages/tools/integration/native-gallery-for-android-ios-112630)
- [Native Camera Asset Store Page](https://assetstore.unity.com/packages/tools/integration/native-camera-for-android-ios-116602)

---

