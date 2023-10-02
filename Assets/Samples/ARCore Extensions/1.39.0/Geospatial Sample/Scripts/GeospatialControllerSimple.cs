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
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
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
        private ARSessionOrigin _sessionOrigin;
        private AREarthManager _earthManager;
        private ARStreetscapeGeometryManager _streetScapeGeometryManager;


        [Header("UI Elements")]
        [SerializeField] Text DebugText;

        [SerializeField]
        private Material[] StreetscapeGeometryMaterialBuilding;
        [SerializeField]
        private Material StreetscapeGeometryMaterialTerrain;

        private int _buildingMatIndex = 0;
        private Dictionary<TrackableId, GameObject> _streetScapeGeometries = new();

        private bool _isInitialized = false;

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;

            _sessionOrigin = FindObjectOfType<ARSessionOrigin>();
            _earthManager = _sessionOrigin.GetComponent<AREarthManager>();
            _streetScapeGeometryManager = _sessionOrigin.GetComponent<ARStreetscapeGeometryManager>();
        }

        /// <summary>
        /// Unity's OnEnable() method.
        /// </summary>
        public void OnEnable()
        {
            _streetScapeGeometryManager.StreetscapeGeometriesChanged += GetStreetscapeGeometry;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Debug.Log("Stop location services.");
            Input.location.Stop();
            _streetScapeGeometryManager.StreetscapeGeometriesChanged -= GetStreetscapeGeometry;
        }

        private IEnumerator Start()
        {
            Debug.Log("Starting location services.");
            yield return StartLocationService();

            yield return new WaitUntil(() =>
            {
                var state = ARSession.state;
                return state != ARSessionState.SessionInitializing &&
                    state != ARSessionState.SessionTracking;
            });

            Debug.Log("ARSession state: " + ARSession.state);

            var featureSupport = _earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
            if (featureSupport != FeatureSupported.Supported)
            {
                Debug.LogWarning("Geospatial mode is not supported.");
            }

            yield return new WaitUntil(() =>
                _earthManager.EarthState != EarthState.ErrorEarthNotReady);

            var earthState = _earthManager.EarthState;
            if (earthState != EarthState.Enabled)
            {
                Debug.LogWarning($"Geospatial sample encountered an EarthState error: {earthState}");
                yield break;
            }
            _isInitialized = true;
        }


        private void Update()
        {
            UpdateDebugInfo();
        }


        private void GetStreetscapeGeometry(ARStreetscapeGeometriesChangedEventArgs eventArgs)
        {
            foreach (var added in eventArgs.Added)
            {
                InstantiateRenderObject(added);
            }
            foreach (var updated in eventArgs.Updated)
            {
                InstantiateRenderObject(updated);
                UpdateRenderObject(updated);
            }
            foreach (var removed in eventArgs.Removed)
            {
                DestroyRenderObject(removed);
            }
        }

        private void InstantiateRenderObject(ARStreetscapeGeometry geometry)
        {
            if (geometry.mesh == null)
            {
                return;
            }

            // Check if a render object already exists for this streetscapegeometry and
            // create one if not.
            if (_streetScapeGeometries.ContainsKey(geometry.trackableId))
            {
                return;
            }

            var go = new GameObject("StreetscapeGeometryMesh",
                typeof(MeshFilter), typeof(MeshRenderer));

            if (go)
            {
                go.transform.position = new Vector3(0, 0.5f, 0);
                go.GetComponent<MeshFilter>().mesh = geometry.mesh;

                // Add a material with transparent diffuse shader.
                if (geometry.streetscapeGeometryType == StreetscapeGeometryType.Building)
                {
                    go.GetComponent<MeshRenderer>().material =
                        StreetscapeGeometryMaterialBuilding[_buildingMatIndex];
                    _buildingMatIndex =
                        (_buildingMatIndex + 1) % StreetscapeGeometryMaterialBuilding.Length;
                }
                else
                {
                    go.GetComponent<MeshRenderer>().material =
                        StreetscapeGeometryMaterialTerrain;
                }

                go.transform.SetPositionAndRotation(
                    geometry.pose.position, geometry.pose.rotation);

                _streetScapeGeometries.Add(geometry.trackableId, go);
            }
        }


        private void UpdateRenderObject(ARStreetscapeGeometry geometry)
        {
            if (_streetScapeGeometries.TryGetValue(geometry.trackableId, out GameObject go))
            {
                go.transform.SetPositionAndRotation(
                    geometry.pose.position, geometry.pose.rotation);
            }
        }


        private void DestroyRenderObject(ARStreetscapeGeometry geometry)
        {
            if (_streetScapeGeometries.TryGetValue(geometry.trackableId, out GameObject go))
            {
                Destroy(go);
                _streetScapeGeometries.Remove(geometry.trackableId);
            }
        }


        private IEnumerator StartLocationService()
        {
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
                yield break;
            }

            Debug.Log("Starting location service.");
            Input.location.Start();

            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarningFormat(
                    "Location service ended with {0} status.", Input.location.status);
                Input.location.Stop();
            }
        }


        private void UpdateDebugInfo()
        {
            var pose = _earthManager.EarthState == EarthState.Enabled &&
                _earthManager.EarthTrackingState == TrackingState.Tracking ?
                _earthManager.CameraGeospatialPose : new GeospatialPose();
            var supported = _earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
            DebugText.text =
                $"IsInitialized: {_isInitialized}\n" +
                $"SessionState: {ARSession.state}\n" +
                $"LocationServiceStatus: {Input.location.status}\n" +
                $"FeatureSupported: {supported}\n" +
                $"EarthState: {_earthManager.EarthState}\n" +
                $"EarthTrackingState: {_earthManager.EarthTrackingState}\n" +
                $"  LAT/LNG: {pose.Latitude:F6}, {pose.Longitude:F6}\n" +
                $"  HorizontalAcc: {pose.HorizontalAccuracy:F6}\n" +
                $"  ALT: {pose.Altitude:F2}\n" +
                $"  VerticalAcc: {pose.VerticalAccuracy:F2}\n" +
                $"  EunRotation: {pose.EunRotation:F2}\n" +
                $"  OrientationYawAcc: {pose.OrientationYawAccuracy:F2}";
        }
    }
}
