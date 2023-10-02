// <copyright file="GeospatialController.cs" company="Google LLC">
//
// Copyright 2022 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace Google.XR.ARCoreExtensions.Samples.Geospatial
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID

    using UnityEngine.Android;
#endif

    /// <summary>
    /// Controller for Geospatial sample.
    /// </summary>
    public class GeospatialControllerSimple : MonoBehaviour
    {
        [Header("AR Components")]


        private ARSessionOrigin _sessionOrigin;
        private ARSession _session;
        private ARAnchorManager _anchorManager;
        private ARRaycastManager _raycastManager;
        private AREarthManager _earthManager;
        private ARStreetscapeGeometryManager _streetscapeGeometryManager;
        private ARCoreExtensions _arCoreExtensions;

        [SerializeField]
        private Material[] StreetscapeGeometryMaterialBuilding;

        /// <summary>
        /// The StreetscapeGeometry material for rendering geometry terrain meshes.
        /// </summary>
        [SerializeField]
        private Material StreetscapeGeometryMaterialTerrain;

        [Header("UI Elements")]


        [SerializeField] GameObject GeospatialPrefab;
        [SerializeField] GameObject TerrainPrefab;
        [SerializeField] GameObject ARViewCanvas;
        [SerializeField] Button ClearAllButton;
        [SerializeField] Toggle GeometryToggle;
        [SerializeField] Button AnchorSettingButton;
        [SerializeField] GameObject AnchorSettingPanel;
        [SerializeField] Toggle GeospatialAnchorToggle;
        [SerializeField] Toggle TerrainAnchorToggle;
        [SerializeField] Toggle RooftopAnchorToggle;
        [SerializeField] GameObject InfoPanel;
        [SerializeField] Text InfoText;
        [SerializeField] Text SnackBarText;
        [SerializeField] Text DebugText;

        private const string _localizingMessage = "Localizing your device to set anchor.";
        private const string _localizationInitializingMessage = "Initializing Geospatial functionalities.";
        private const string _localizationInstructionMessage = "Point your camera at buildings, stores, and signs near you.";
        private const string _localizationFailureMessage = "Localization not possible.\nClose and open the app to restart the session.";
        private const string _localizationSuccessMessage = "Localization completed.";
        private const float _timeoutSeconds = 180;
        private const float _errorDisplaySeconds = 3;
        private const int _storageLimit = 20;
        private const double _orientationYawAccuracyThreshold = 25;


        private const double _headingAccuracyThreshold = 25;
        private const double _horizontalAccuracyThreshold = 20;
        private bool _showAnchorSettingsPanel = false;
        private AnchorType _anchorType = AnchorType.Geospatial;
        private bool _streetscapeGeometryVisibility = false;

        private int _buildingMatIndex = 0;

        private Dictionary<TrackableId, GameObject> _streetscapegeometryGOs = new();
        List<ARStreetscapeGeometry> _addedStreetscapeGeometries = new();
        List<ARStreetscapeGeometry> _updatedStreetscapeGeometries = new();
        List<ARStreetscapeGeometry> _removedStreetscapeGeometries = new();
        private bool _clearStreetscapeGeometryRenderObjects = false;

        private bool _waitingForLocationService = false;
        private bool _isInARView = false;
        private bool _isReturning = false;
        private bool _isLocalizing = false;
        private bool _enablingGeospatial = false;
        private bool _shouldResolvingHistory = false;
        private float _localizationPassedTime = 0f;
        private float _configurePrepareTime = 3f;
        private GeospatialAnchorHistoryCollection _historyCollection = null;
        private List<GameObject> _anchorObjects = new List<GameObject>();
        private IEnumerator _startLocationService = null;
        private IEnumerator _asyncCheck = null;


        /// <summary>
        /// Callback handling "Clear All" button click event in AR View.
        /// </summary>
        public void OnClearAllClicked()
        {
            foreach (var anchor in _anchorObjects)
            {
                Destroy(anchor);
            }

            _anchorObjects.Clear();
            _historyCollection.Collection.Clear();
            SnackBarText.text = "Anchor(s) cleared!";
            ClearAllButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Callback handling "Geometry" toggle event in AR View.
        /// </summary>
        /// <param name="enabled">Whether to enable Streetscape Geometry visibility.</param>
        public void OnGeometryToggled(bool enabled)
        {
            _streetscapeGeometryVisibility = enabled;
            if (!_streetscapeGeometryVisibility)
            {
                _clearStreetscapeGeometryRenderObjects = true;
            }
        }

        /// <summary>
        /// Callback handling the  "Anchor Setting" panel display or hide event in AR View.
        /// </summary>
        public void OnAnchorSettingButtonClicked()
        {
            _showAnchorSettingsPanel = !_showAnchorSettingsPanel;
            if (_showAnchorSettingsPanel)
            {
                SetAnchorPanelState(true);
            }
            else
            {
                SetAnchorPanelState(false);
            }
        }

        /// <summary>
        /// Callback handling Geospatial anchor toggle event in AR View.
        /// </summary>
        /// <param name="enabled">Whether to enable Geospatial anchors.</param>
        public void OnGeospatialAnchorToggled(bool enabled)
        {
            // GeospatialAnchorToggle.GetComponent<Toggle>().isOn = true;;
            _anchorType = AnchorType.Geospatial;
            SetAnchorPanelState(false);
        }

        /// <summary>
        /// Callback handling Terrain anchor toggle event in AR View.
        /// </summary>
        /// <param name="enabled">Whether to enable Terrain anchors.</param>
        public void OnTerrainAnchorToggled(bool enabled)
        {
            // TerrainAnchorToggle.GetComponent<Toggle>().isOn = true;
            _anchorType = AnchorType.Terrain;
            SetAnchorPanelState(false);
        }

        /// <summary>
        /// Callback handling Rooftop anchor toggle event in AR View.
        /// </summary>
        /// <param name="enabled">Whether to enable Rooftop anchors.</param>
        public void OnRooftopAnchorToggled(bool enabled)
        {
            // RooftopAnchorToggle.GetComponent<Toggle>().isOn = true;
            _anchorType = AnchorType.Rooftop;
            SetAnchorPanelState(false);
        }

        /// <summary>
        /// Unity's Awake() method.
        /// </summary>
        public void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;

            _sessionOrigin = FindObjectOfType<ARSessionOrigin>();
            _anchorManager = _sessionOrigin.GetComponent<ARAnchorManager>();
            _raycastManager = _sessionOrigin.GetComponent<ARRaycastManager>();
            _earthManager = _sessionOrigin.GetComponent<AREarthManager>();
            _streetscapeGeometryManager = _sessionOrigin.GetComponent<ARStreetscapeGeometryManager>();

            _session = FindObjectOfType<ARSession>();
            _arCoreExtensions = FindObjectOfType<ARCoreExtensions>();
        }

        /// <summary>
        /// Unity's OnEnable() method.
        /// </summary>
        public void OnEnable()
        {
            _startLocationService = StartLocationService();
            StartCoroutine(_startLocationService);

            _isReturning = false;
            _enablingGeospatial = false;
            InfoPanel.SetActive(false);
            GeometryToggle.gameObject.SetActive(false);
            AnchorSettingButton.gameObject.SetActive(false);
            AnchorSettingPanel.SetActive(false);
            GeospatialAnchorToggle.gameObject.SetActive(false);
            TerrainAnchorToggle.gameObject.SetActive(false);
            RooftopAnchorToggle.gameObject.SetActive(false);
            ClearAllButton.gameObject.SetActive(false);
            DebugText.gameObject.SetActive(Debug.isDebugBuild && _earthManager != null);
            GeometryToggle.onValueChanged.AddListener(OnGeometryToggled);
            AnchorSettingButton.onClick.AddListener(OnAnchorSettingButtonClicked);
            GeospatialAnchorToggle.onValueChanged.AddListener(OnGeospatialAnchorToggled);
            TerrainAnchorToggle.onValueChanged.AddListener(OnTerrainAnchorToggled);
            RooftopAnchorToggle.onValueChanged.AddListener(OnRooftopAnchorToggled);

            _localizationPassedTime = 0f;
            _isLocalizing = true;
            SnackBarText.text = _localizingMessage;

            _historyCollection = new GeospatialAnchorHistoryCollection();
            _shouldResolvingHistory = _historyCollection.Collection.Count > 0;

            SwitchToARView(true);

            if (_streetscapeGeometryManager == null)
            {
                Debug.LogWarning("StreetscapeGeometryManager must be set in the " +
                    "GeospatialController Inspector to render StreetscapeGeometry.");
            }


            if (StreetscapeGeometryMaterialTerrain == null)
            {
                Debug.LogWarning("StreetscapeGeometryMaterialTerrain must be set in the " +
                    "GeospatialController Inspector to render StreetscapeGeometry.");
                return;
            }

            // get access to ARstreetscapeGeometries in ARStreetscapeGeometryManager
            if (_streetscapeGeometryManager)
            {
                _streetscapeGeometryManager.StreetscapeGeometriesChanged += GetStreetscapeGeometry;
            }
        }

        /// <summary>
        /// Unity's OnDisable() method.
        /// </summary>
        public void OnDisable()
        {
            StopCoroutine(_asyncCheck);
            _asyncCheck = null;
            StopCoroutine(_startLocationService);
            _startLocationService = null;
            Debug.Log("Stop location services.");
            Input.location.Stop();

            foreach (var anchor in _anchorObjects)
            {
                Destroy(anchor);
            }

            _anchorObjects.Clear();

            if (_streetscapeGeometryManager)
            {
                _streetscapeGeometryManager.StreetscapeGeometriesChanged -=
                    GetStreetscapeGeometry;
            }
        }

        /// <summary>
        /// Unity's Update() method.
        /// </summary>
        public void Update()
        {
            if (!_isInARView)
            {
                return;
            }

            UpdateDebugInfo();

            // Check session error status.
            LifecycleUpdate();
            if (_isReturning)
            {
                return;
            }

            if (ARSession.state != ARSessionState.SessionInitializing &&
                ARSession.state != ARSessionState.SessionTracking)
            {
                return;
            }

            // Check feature support and enable Geospatial API when it's supported.
            var featureSupport = _earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
            switch (featureSupport)
            {
                case FeatureSupported.Unknown:
                    return;
                case FeatureSupported.Unsupported:
                    ReturnWithReason("The Geospatial API is not supported by this device.");
                    return;
                case FeatureSupported.Supported:
                    if (_arCoreExtensions.ARCoreExtensionsConfig.GeospatialMode ==
                        GeospatialMode.Disabled)
                    {
                        Debug.Log("Geospatial sample switched to GeospatialMode.Enabled.");
                        _arCoreExtensions.ARCoreExtensionsConfig.GeospatialMode =
                            GeospatialMode.Enabled;
                        _arCoreExtensions.ARCoreExtensionsConfig.StreetscapeGeometryMode =
                            StreetscapeGeometryMode.Enabled;
                        _configurePrepareTime = 3.0f;
                        _enablingGeospatial = true;
                        return;
                    }

                    break;
            }

            // Waiting for new configuration to take effect.
            if (_enablingGeospatial)
            {
                _configurePrepareTime -= Time.deltaTime;
                if (_configurePrepareTime < 0)
                {
                    _enablingGeospatial = false;
                }
                else
                {
                    return;
                }
            }

            // Check earth state.
            var earthState = _earthManager.EarthState;
            if (earthState == EarthState.ErrorEarthNotReady)
            {
                SnackBarText.text = _localizationInitializingMessage;
                return;
            }
            else if (earthState != EarthState.Enabled)
            {
                string errorMessage =
                    "Geospatial sample encountered an EarthState error: " + earthState;
                Debug.LogWarning(errorMessage);
                SnackBarText.text = errorMessage;
                return;
            }

            // Check earth localization.
            bool isSessionReady = ARSession.state == ARSessionState.SessionTracking &&
                Input.location.status == LocationServiceStatus.Running;
            var earthTrackingState = _earthManager.EarthTrackingState;
            var pose = earthTrackingState == TrackingState.Tracking ?
                _earthManager.CameraGeospatialPose : new GeospatialPose();
            if (!isSessionReady || earthTrackingState != TrackingState.Tracking ||
                pose.OrientationYawAccuracy > _orientationYawAccuracyThreshold ||
                pose.HorizontalAccuracy > _horizontalAccuracyThreshold)
            {
                // Lost localization during the session.
                if (!_isLocalizing)
                {
                    _isLocalizing = true;
                    _localizationPassedTime = 0f;
                    GeometryToggle.gameObject.SetActive(false);
                    AnchorSettingButton.gameObject.SetActive(false);
                    AnchorSettingPanel.gameObject.SetActive(false);
                    GeospatialAnchorToggle.gameObject.SetActive(false);
                    TerrainAnchorToggle.gameObject.SetActive(false);
                    RooftopAnchorToggle.gameObject.SetActive(false);
                    ClearAllButton.gameObject.SetActive(false);
                    foreach (var go in _anchorObjects)
                    {
                        go.SetActive(false);
                    }
                }

                if (_localizationPassedTime > _timeoutSeconds)
                {
                    Debug.LogError("Geospatial sample localization timed out.");
                    ReturnWithReason(_localizationFailureMessage);
                }
                else
                {
                    _localizationPassedTime += Time.deltaTime;
                    SnackBarText.text = _localizationInstructionMessage;
                }
            }
            else if (_isLocalizing)
            {
                // Finished localization.
                _isLocalizing = false;
                _localizationPassedTime = 0f;
                GeometryToggle.gameObject.SetActive(true);
                AnchorSettingButton.gameObject.SetActive(true);
                ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
                SnackBarText.text = _localizationSuccessMessage;
                foreach (var go in _anchorObjects)
                {
                    go.SetActive(true);
                }

                ResolveHistory();
            }
            else
            {
                if (_streetscapeGeometryVisibility)
                {
                    foreach (
                        ARStreetscapeGeometry streetscapegeometry in _addedStreetscapeGeometries)
                    {
                        InstantiateRenderObject(streetscapegeometry);
                    }

                    foreach (
                        ARStreetscapeGeometry streetscapegeometry in _updatedStreetscapeGeometries)
                    {
                        // This second call to instantiate is required if geometry is toggled on
                        // or off after the app has started.
                        InstantiateRenderObject(streetscapegeometry);
                        UpdateRenderObject(streetscapegeometry);
                    }

                    foreach (
                        ARStreetscapeGeometry streetscapegeometry in _removedStreetscapeGeometries)
                    {
                        DestroyRenderObject(streetscapegeometry);
                    }
                }
                else if (_clearStreetscapeGeometryRenderObjects)
                {
                    DestroyAllRenderObjects();
                    _clearStreetscapeGeometryRenderObjects = false;
                }

                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began
                    && !EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                    && _anchorObjects.Count < _storageLimit)
                {
                    // Set anchor on screen tap.
                    PlaceAnchorByScreenTap(Input.GetTouch(0).position);
                }

                // Hide anchor settings and toggles if the storage limit has been reached.
                if (_anchorObjects.Count >= _storageLimit)
                {
                    AnchorSettingButton.gameObject.SetActive(false);
                    AnchorSettingPanel.gameObject.SetActive(false);
                    GeospatialAnchorToggle.gameObject.SetActive(false);
                    TerrainAnchorToggle.gameObject.SetActive(false);
                    RooftopAnchorToggle.gameObject.SetActive(false);
                }
                else
                {
                    AnchorSettingButton.gameObject.SetActive(true);
                }
            }

            InfoPanel.SetActive(true);
            if (earthTrackingState == TrackingState.Tracking)
            {
                InfoText.text = string.Format(
                "Latitude/Longitude: {1}°, {2}°{0}" +
                "Horizontal Accuracy: {3}m{0}" +
                "Altitude: {4}m{0}" +
                "Vertical Accuracy: {5}m{0}" +
                "Eun Rotation: {6}{0}" +
                "Orientation Yaw Accuracy: {7}°",
                Environment.NewLine,
                pose.Latitude.ToString("F6"),
                pose.Longitude.ToString("F6"),
                pose.HorizontalAccuracy.ToString("F6"),
                pose.Altitude.ToString("F2"),
                pose.VerticalAccuracy.ToString("F2"),
                pose.EunRotation.ToString("F1"),
                pose.OrientationYawAccuracy.ToString("F1"));
            }
            else
            {
                InfoText.text = "GEOSPATIAL POSE: not tracking";
            }
        }

        /// <summary>
        /// Connects the <c>ARStreetscapeGeometry</c> to the specified lists for access.
        /// </summary>
        /// <param name="eventArgs">The
        /// <c><see cref="ARStreetscapeGeometriesChangedEventArgs"/></c> containing the
        /// <c>ARStreetscapeGeometry</c>.
        /// </param>
        private void GetStreetscapeGeometry(ARStreetscapeGeometriesChangedEventArgs eventArgs)
        {
            _addedStreetscapeGeometries = eventArgs.Added;
            _updatedStreetscapeGeometries = eventArgs.Updated;
            _removedStreetscapeGeometries = eventArgs.Removed;
        }

        /// <summary>
        /// Sets up a render object for this <c>ARStreetscapeGeometry</c>.
        /// </summary>
        /// <param name="streetscapegeometry">The
        /// <c><see cref="ARStreetscapeGeometry"/></c> object containing the mesh
        /// to be rendered.</param>
        private void InstantiateRenderObject(ARStreetscapeGeometry streetscapegeometry)
        {
            if (streetscapegeometry.mesh == null)
            {
                return;
            }

            // Check if a render object already exists for this streetscapegeometry and
            // create one if not.
            if (_streetscapegeometryGOs.ContainsKey(streetscapegeometry.trackableId))
            {
                return;
            }

            GameObject renderObject = new GameObject(
                "StreetscapeGeometryMesh", typeof(MeshFilter), typeof(MeshRenderer));

            if (renderObject)
            {
                renderObject.transform.position = new Vector3(0, 0.5f, 0);
                renderObject.GetComponent<MeshFilter>().mesh = streetscapegeometry.mesh;

                // Add a material with transparent diffuse shader.
                if (streetscapegeometry.streetscapeGeometryType ==
                    StreetscapeGeometryType.Building)
                {
                    renderObject.GetComponent<MeshRenderer>().material =
                        StreetscapeGeometryMaterialBuilding[_buildingMatIndex];
                    _buildingMatIndex =
                        (_buildingMatIndex + 1) % StreetscapeGeometryMaterialBuilding.Length;
                }
                else
                {
                    renderObject.GetComponent<MeshRenderer>().material =
                        StreetscapeGeometryMaterialTerrain;
                }

                renderObject.transform.position = streetscapegeometry.pose.position;
                renderObject.transform.rotation = streetscapegeometry.pose.rotation;

                _streetscapegeometryGOs.Add(streetscapegeometry.trackableId, renderObject);
            }
        }

        /// <summary>
        /// Updates the render object transform based on this StreetscapeGeometries pose.
        /// It must be called every frame to update the mesh.
        /// </summary>
        /// <param name="streetscapegeometry">The <c><see cref="ARStreetscapeGeometry"/></c>
        /// object containing the mesh to be rendered.</param>
        private void UpdateRenderObject(ARStreetscapeGeometry streetscapegeometry)
        {
            if (_streetscapegeometryGOs.ContainsKey(streetscapegeometry.trackableId))
            {
                GameObject renderObject = _streetscapegeometryGOs[streetscapegeometry.trackableId];
                renderObject.transform.position = streetscapegeometry.pose.position;
                renderObject.transform.rotation = streetscapegeometry.pose.rotation;
            }
        }

        /// <summary>
        /// Destroys the render object associated with the
        /// <c><see cref="ARStreetscapeGeometry"/></c>.
        /// </summary>
        /// <param name="streetscapegeometry">The <c><see cref="ARStreetscapeGeometry"/></c>
        /// containing the render object to be destroyed.</param>
        private void DestroyRenderObject(ARStreetscapeGeometry streetscapegeometry)
        {
            if (_streetscapegeometryGOs.ContainsKey(streetscapegeometry.trackableId))
            {
                var geometry = _streetscapegeometryGOs[streetscapegeometry.trackableId];
                _streetscapegeometryGOs.Remove(streetscapegeometry.trackableId);
                Destroy(geometry);
            }
        }

        /// <summary>
        /// Destroys all stored <c><see cref="ARStreetscapeGeometry"/></c> render objects.
        /// </summary>
        private void DestroyAllRenderObjects()
        {
            var keys = _streetscapegeometryGOs.Keys;
            foreach (var key in keys)
            {
                var renderObject = _streetscapegeometryGOs[key];
                Destroy(renderObject);
            }

            _streetscapegeometryGOs.Clear();
        }

        /// <summary>
        /// Activate or deactivate all UI elements on the anchor setting Panel.
        /// </summary>
        /// <param name="state">A bool value to determine if the anchor settings panel is visible.
        private void SetAnchorPanelState(bool state)
        {
            AnchorSettingPanel.gameObject.SetActive(state);
            GeospatialAnchorToggle.gameObject.SetActive(state);
            TerrainAnchorToggle.gameObject.SetActive(state);
            RooftopAnchorToggle.gameObject.SetActive(state);
        }

        private IEnumerator CheckRooftopPromise(ResolveAnchorOnRooftopPromise promise,
            GeospatialAnchorHistory history)
        {
            yield return promise;

            var result = promise.Result;
            if (result.RooftopAnchorState == RooftopAnchorState.Success &&
                result.Anchor != null)
            {
                // Adjust the scale of the prefab anchor object to maintain visibility when it is
                // far away.
                result.Anchor.gameObject.transform.localScale *= GetRooftopAnchorScale(
                    result.Anchor.gameObject.transform.position,
                    Camera.main.transform.position);
                GameObject anchorGO = Instantiate(TerrainPrefab,
                    result.Anchor.gameObject.transform);
                anchorGO.transform.parent = result.Anchor.gameObject.transform;

                _anchorObjects.Add(result.Anchor.gameObject);
                _historyCollection.Collection.Add(history);

                SnackBarText.text = GetDisplayStringForAnchorPlacedSuccess();

                ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            }
            else
            {
                SnackBarText.text = GetDisplayStringForAnchorPlacedFailure();
            }

            yield break;
        }

        private IEnumerator CheckTerrainPromise(ResolveAnchorOnTerrainPromise promise,
            GeospatialAnchorHistory history)
        {
            yield return promise;

            var result = promise.Result;
            if (result.TerrainAnchorState == TerrainAnchorState.Success &&
                result.Anchor != null)
            {
                GameObject anchorGO = Instantiate(TerrainPrefab,
                    result.Anchor.gameObject.transform);
                anchorGO.transform.parent = result.Anchor.gameObject.transform;

                _anchorObjects.Add(result.Anchor.gameObject);
                _historyCollection.Collection.Add(history);

                SnackBarText.text = GetDisplayStringForAnchorPlacedSuccess();

                ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            }
            else
            {
                SnackBarText.text = GetDisplayStringForAnchorPlacedFailure();
            }

            yield break;
        }

        private float GetRooftopAnchorScale(Vector3 anchor, Vector3 camera)
        {
            // Return the scale in range [1, 2] after mapping a distance between camera and anchor
            // to [2, 20].
            float distance =
                Mathf.Sqrt(
                    Mathf.Pow(anchor.x - camera.x, 2.0f)
                    + Mathf.Pow(anchor.y - camera.y, 2.0f)
                    + Mathf.Pow(anchor.z - camera.z, 2.0f));
            float mapDistance = Mathf.Min(Mathf.Max(2.0f, distance), 20.0f);
            return (mapDistance - 2.0f) / (20.0f - 2.0f) + 1.0f;
        }

        private void PlaceAnchorByScreenTap(Vector2 position)
        {
            if (_streetscapeGeometryVisibility)
            {
                // Raycast against streetscapeGeometry.
                List<XRRaycastHit> hitResults = new List<XRRaycastHit>();
                if (_raycastManager.RaycastStreetscapeGeometry(position, ref hitResults))
                {
                    if (_anchorType == AnchorType.Rooftop || _anchorType == AnchorType.Terrain)
                    {
                        var streetscapeGeometry =
                            _streetscapeGeometryManager.GetStreetscapeGeometry(
                                hitResults[0].trackableId);
                        if (streetscapeGeometry == null)
                        {
                            return;
                        }

                        if (_streetscapegeometryGOs.ContainsKey(streetscapeGeometry.trackableId))
                        {
                            Pose modifiedPose = new Pose(hitResults[0].pose.position,
                                Quaternion.LookRotation(Vector3.right, Vector3.up));

                            GeospatialAnchorHistory history =
                                CreateHistory(modifiedPose, _anchorType);

                            // Anchor returned will be null, the coroutine will handle creating
                            // the anchor when the promise is done.
                            PlaceARAnchor(history, modifiedPose, hitResults[0].trackableId);
                        }
                    }
                    else
                    {
                        GeospatialAnchorHistory history = CreateHistory(hitResults[0].pose,
                            _anchorType);
                        var anchor = PlaceARAnchor(history, hitResults[0].pose,
                            hitResults[0].trackableId);
                        if (anchor != null)
                        {
                            _historyCollection.Collection.Add(history);
                        }

                        ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
                    }
                }

                return;
            }

            // Raycast against detected planes.
            List<ARRaycastHit> planeHitResults = new List<ARRaycastHit>();
            _raycastManager.Raycast(
                position, planeHitResults, TrackableType.Planes | TrackableType.FeaturePoint);
            if (planeHitResults.Count > 0)
            {
                GeospatialAnchorHistory history = CreateHistory(planeHitResults[0].pose,
                    _anchorType);

                if (_anchorType == AnchorType.Rooftop)
                {
                    // The coroutine will create the anchor when the promise is done.
                    Quaternion eunRotation = CreateRotation(history);
                    ResolveAnchorOnRooftopPromise rooftopPromise =
                        _anchorManager.ResolveAnchorOnRooftopAsync(
                            history.Latitude, history.Longitude,
                            0, eunRotation);

                    StartCoroutine(CheckRooftopPromise(rooftopPromise, history));
                    return;
                }

                var anchor = PlaceGeospatialAnchor(history);
                if (anchor != null)
                {
                    _historyCollection.Collection.Add(history);
                }

                ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            }
        }

        private GeospatialAnchorHistory CreateHistory(Pose pose, AnchorType anchorType)
        {
            GeospatialPose geospatialPose = _earthManager.Convert(pose);

            GeospatialAnchorHistory history = new GeospatialAnchorHistory(
                geospatialPose.Latitude, geospatialPose.Longitude, geospatialPose.Altitude,
                anchorType, geospatialPose.EunRotation);
            return history;
        }

        private Quaternion CreateRotation(GeospatialAnchorHistory history)
        {
            Quaternion eunRotation = history.EunRotation;
            if (eunRotation == Quaternion.identity)
            {
                // This history is from a previous app version and EunRotation was not used.
                eunRotation =
                    Quaternion.AngleAxis(180f - (float)history.Heading, Vector3.up);
            }

            return eunRotation;
        }

        private ARAnchor PlaceARAnchor(GeospatialAnchorHistory history, Pose pose = new Pose(),
            TrackableId trackableId = new TrackableId())
        {
            Quaternion eunRotation = CreateRotation(history);
            ARAnchor anchor = null;
            switch (history.AnchorType)
            {
                case AnchorType.Rooftop:
                    ResolveAnchorOnRooftopPromise rooftopPromise =
                        _anchorManager.ResolveAnchorOnRooftopAsync(
                            history.Latitude, history.Longitude,
                            0, eunRotation);

                    StartCoroutine(CheckRooftopPromise(rooftopPromise, history));
                    return null;

                case AnchorType.Terrain:
                    ResolveAnchorOnTerrainPromise terrainPromise =
                        _anchorManager.ResolveAnchorOnTerrainAsync(
                            history.Latitude, history.Longitude,
                            0, eunRotation);

                    StartCoroutine(CheckTerrainPromise(terrainPromise, history));
                    return null;

                case AnchorType.Geospatial:
                    ARStreetscapeGeometry streetscapegeometry =
                        _streetscapeGeometryManager.GetStreetscapeGeometry(trackableId);
                    if (streetscapegeometry != null)
                    {
                        anchor = _streetscapeGeometryManager.AttachAnchor(
                            streetscapegeometry, pose);
                    }

                    if (anchor != null)
                    {
                        _anchorObjects.Add(anchor.gameObject);
                        _historyCollection.Collection.Add(history);
                        ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);

                        SnackBarText.text = GetDisplayStringForAnchorPlacedSuccess();
                    }
                    else
                    {
                        SnackBarText.text = GetDisplayStringForAnchorPlacedFailure();
                    }

                    break;
            }

            return anchor;
        }

        private ARGeospatialAnchor PlaceGeospatialAnchor(
            GeospatialAnchorHistory history)
        {
            bool terrain = history.AnchorType == AnchorType.Terrain;
            Quaternion eunRotation = CreateRotation(history);
            ARGeospatialAnchor anchor = null;

            if (terrain)
            {
                // Anchor returned will be null, the coroutine will handle creating the
                // anchor when the promise is done.
                ResolveAnchorOnTerrainPromise promise =
                    _anchorManager.ResolveAnchorOnTerrainAsync(
                        history.Latitude, history.Longitude,
                        0, eunRotation);

                StartCoroutine(CheckTerrainPromise(promise, history));
                return null;
            }
            else
            {
                anchor = _anchorManager.AddAnchor(
                    history.Latitude, history.Longitude, history.Altitude, eunRotation);
            }

            if (anchor != null)
            {
                GameObject anchorGO = history.AnchorType == AnchorType.Geospatial ?
                    Instantiate(GeospatialPrefab, anchor.transform) :
                    Instantiate(TerrainPrefab, anchor.transform);
                anchor.gameObject.SetActive(!terrain);
                anchorGO.transform.parent = anchor.gameObject.transform;
                _anchorObjects.Add(anchor.gameObject);
                SnackBarText.text = GetDisplayStringForAnchorPlacedSuccess();
            }
            else
            {
                SnackBarText.text = GetDisplayStringForAnchorPlacedFailure();
            }

            return anchor;
        }

        private void ResolveHistory()
        {
            if (!_shouldResolvingHistory)
            {
                return;
            }

            _shouldResolvingHistory = false;
            foreach (var history in _historyCollection.Collection)
            {
                switch (history.AnchorType)
                {
                    case AnchorType.Rooftop:
                        PlaceARAnchor(history);
                        break;
                    case AnchorType.Terrain:
                        PlaceARAnchor(history);
                        break;
                    default:
                        PlaceGeospatialAnchor(history);
                        break;
                }
            }

            ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            SnackBarText.text = string.Format("{0} anchor(s) set from history.",
                _anchorObjects.Count);
        }

        private void SwitchToARView(bool enable)
        {
            _isInARView = enable;
            _sessionOrigin.gameObject.SetActive(enable);
            _session.gameObject.SetActive(enable);
            _arCoreExtensions.gameObject.SetActive(enable);
            ARViewCanvas.SetActive(enable);
            if (enable && _asyncCheck == null)
            {
                _asyncCheck = AvailabilityCheck();
                StartCoroutine(_asyncCheck);
            }
        }

        private IEnumerator AvailabilityCheck()
        {
            if (ARSession.state == ARSessionState.None)
            {
                yield return ARSession.CheckAvailability();
            }

            // Waiting for ARSessionState.CheckingAvailability.
            yield return null;

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                yield return ARSession.Install();
            }

            // Waiting for ARSessionState.Installing.
            yield return null;

            while (_waitingForLocationService)
            {
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning(
                    "Location services aren't running. VPS availability check is not available.");
                yield break;
            }

            // Update event is executed before coroutines so it checks the latest error states.
            if (_isReturning)
            {
                yield break;
            }

            var location = Input.location.lastData;
            var vpsAvailabilityPromise =
                AREarthManager.CheckVpsAvailabilityAsync(location.latitude, location.longitude);
            yield return vpsAvailabilityPromise;

            Debug.LogFormat("VPS Availability at ({0}, {1}): {2}",
                location.latitude, location.longitude, vpsAvailabilityPromise.Result);
        }

        private IEnumerator StartLocationService()
        {
            _waitingForLocationService = true;
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.Log("Requesting the fine location permission.");
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(3.0f);
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                Debug.Log("Location service is disabled by the user.");
                _waitingForLocationService = false;
                yield break;
            }

            Debug.Log("Starting location service.");
            Input.location.Start();

            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                yield return null;
            }

            _waitingForLocationService = false;
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarningFormat(
                    "Location service ended with {0} status.", Input.location.status);
                Input.location.Stop();
            }
        }

        private void LifecycleUpdate()
        {
            if (_isReturning)
            {
                return;
            }

            // Only allow the screen to sleep when not tracking.
            var sleepTimeout = SleepTimeout.NeverSleep;
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                sleepTimeout = SleepTimeout.SystemSetting;
            }

            Screen.sleepTimeout = sleepTimeout;

            // Quit the app if ARSession is in an error status.
            string returningReason = string.Empty;
            if (ARSession.state != ARSessionState.CheckingAvailability &&
                ARSession.state != ARSessionState.Ready &&
                ARSession.state != ARSessionState.SessionInitializing &&
                ARSession.state != ARSessionState.SessionTracking)
            {
                returningReason = string.Format(
                    "Geospatial sample encountered an ARSession error state {0}.\n" +
                    "Please restart the app.",
                    ARSession.state);
            }
            else if (Input.location.status == LocationServiceStatus.Failed)
            {
                returningReason =
                    "Geospatial sample failed to start location service.\n" +
                    "Please restart the app and grant the fine location permission.";
            }
            else if (_sessionOrigin == null || _session == null || _arCoreExtensions == null)
            {
                returningReason = string.Format(
                    "Geospatial sample failed due to missing AR Components.");
            }

            ReturnWithReason(returningReason);
        }

        private void ReturnWithReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return;
            }

            GeometryToggle.gameObject.SetActive(false);
            AnchorSettingButton.gameObject.SetActive(false);
            AnchorSettingPanel.gameObject.SetActive(false);
            GeospatialAnchorToggle.gameObject.SetActive(false);
            TerrainAnchorToggle.gameObject.SetActive(false);
            RooftopAnchorToggle.gameObject.SetActive(false);
            ClearAllButton.gameObject.SetActive(false);
            InfoPanel.SetActive(false);

            Debug.LogError(reason);
            SnackBarText.text = reason;
            _isReturning = true;
            Invoke(nameof(QuitApplication), _errorDisplaySeconds);
        }

        private void QuitApplication()
        {
            Application.Quit();
        }

        private void UpdateDebugInfo()
        {
            if (!Debug.isDebugBuild || _earthManager == null)
            {
                return;
            }

            var pose = _earthManager.EarthState == EarthState.Enabled &&
                _earthManager.EarthTrackingState == TrackingState.Tracking ?
                _earthManager.CameraGeospatialPose : new GeospatialPose();
            var supported = _earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
            DebugText.text =
                $"IsReturning: {_isReturning}\n" +
                $"IsLocalizing: {_isLocalizing}\n" +
                $"SessionState: {ARSession.state}\n" +
                $"LocationServiceStatus: {Input.location.status}\n" +
                $"FeatureSupported: {supported}\n" +
                $"EarthState: {_earthManager.EarthState}\n" +
                $"EarthTrackingState: {_earthManager.EarthTrackingState}\n" +
                $"  LAT/LNG: {pose.Latitude:F6}, {pose.Longitude:F6}\n" +
                $"  HorizontalAcc: {pose.HorizontalAccuracy:F6}\n" +
                $"  ALT: {pose.Altitude:F2}\n" +
                $"  VerticalAcc: {pose.VerticalAccuracy:F2}\n" +
                $". EunRotation: {pose.EunRotation:F2}\n" +
                $"  OrientationYawAcc: {pose.OrientationYawAccuracy:F2}";
        }

        /// <summary>
        /// Generates the placed anchor success string for the UI display.
        /// </summary>
        /// <returns> The string for the UI display for successful anchor placement.</returns>
        private string GetDisplayStringForAnchorPlacedSuccess()
        {
            return string.Format(
                    "{0} / {1} Anchor(s) Set!", _anchorObjects.Count, _storageLimit);
        }

        /// <summary>
        /// Generates the placed anchor failure string for the UI display.
        /// </summary>
        /// <returns> The string for the UI display for a failed anchor placement.</returns>
        private string GetDisplayStringForAnchorPlacedFailure()
        {
            return string.Format(
                    "Failed to set a {0} anchor!", _anchorType);
        }
    }
}
