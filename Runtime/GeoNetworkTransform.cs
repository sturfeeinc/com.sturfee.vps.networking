using Mirror;
using Sturfee.DigitalTwin;
using SturfeeVPS.Core;
using SturfeeVPS.SDK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SturfeeVPS.Networking
{
    public class GeoNetworkTransform : NetworkBehaviour
    {
        [Header("Config")]
        public float PositionThreshold = 0.5f;
        public float RotationThreshold = 1f;
        public bool InterpolatePosition;
        public bool InterpolateRotation;
        public bool SnapToSurfaceNormals;
        public bool SnapToTerrain;
        public float StepOffset = 1;
        public string[] TerrainLayers = new string[] { SturfeeLayers.DigitalTwinTerrain };

        [Space(10)]
        [SyncVar]
        public GeoLocation Location;
        [SyncVar]
        public Quaternion Rotation;

        // Interpolation
        private Vector3 _lastPos = Vector3.zero;
        private Quaternion _lastRot = Quaternion.identity;
        private float _lerpRate = 15.0f;


        private void Update()
        {
            if (!isClient)
                return;

            if (hasAuthority)
            {                
                UpdateNetwork();
            }
            else
            {
                UpdateLocal();
            }
        }

        public void UpdateNetwork()
        {
            if (Vector3.Distance(_lastPos, transform.position) >= PositionThreshold)
            {
                _lastPos = transform.position;
                GeoLocation location = Converters.UnityToGeoLocation(transform.position);
                CmdTranslate(location);
            }

            if (Quaternion.Angle(_lastRot, transform.rotation) >= RotationThreshold)
            {
                _lastRot = transform.rotation;
                CmdRotate(transform.rotation);
            }
        }

        public void UpdateLocal(bool forceUpdate = false)
        {            
            if (Rotation != null)
            {
                if (_lastRot != Rotation)
                {
                    if (InterpolateRotation && !forceUpdate)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Rotation, Time.deltaTime * _lerpRate);
                    }
                    else
                    {
                        transform.rotation = Rotation;
                    }

                    _lastRot = transform.rotation;
                }
            }

            if (Location != null)
            {
                var localPos = Converters.GeoToUnityPosition(Location);
                if (_lastPos != localPos)
                {
                    if (SnapToSurfaceNormals)
                    {
                        localPos = GetRayCorrectedPosition(localPos, Rotation * Vector3.forward);
                    }

                    if (SnapToTerrain)
                    {
                        //localPos.y = ElevationProvider.Instance.GetTerrainElevation(localPos, StepOffset, LayerMask.GetMask(TerrainLayers)) + 1.5f;
                        localPos.y = GetElevation(Location);
                    }

                    if (InterpolatePosition && !forceUpdate)
                    {
                        transform.position = Vector3.Lerp(transform.position, localPos, Time.deltaTime * _lerpRate);
                    }
                    else
                    {
                        transform.position = localPos;
                    }

                    _lastPos = transform.position;
                }
            }
        }        

        private Vector3 GetRayCorrectedPosition(Vector3 pos, Vector3 normal)
        {
            Ray ray = new Ray(pos + (normal * 10), -normal);
            RaycastHit hit;

            if(Physics.Raycast(ray, out hit)){
                var hitPosition = hit.point;
                float diff = Vector3.Distance(pos, hitPosition);
                if(diff < 5)
                {
                    MyLogger.Log($"[GeoTansform] :: Position corrected using Raycast. Initial {pos}, after {hitPosition}");
                    return hitPosition;
                }
            }

            return pos;
        }


        private float GetElevation(GeoLocation location)
        {
            // Get elevation from Sturg/DT/HDSite tiles
            var terrainElevation = GetTerrainElevation(location);
            // Get a small elevation on top of terrain elevation if exists.     example => footpath
            var stepElevation = GetStepElevation(location, terrainElevation);

            return stepElevation;
        }

        private float GetTerrainElevation(GeoLocation location)
        {
            float elevation = 0;

            //check if Scanned using HD
            bool hdsiteElevationNotFound = true;
            var sturfeeVpsLocalizationProvider = XrSessionManager.GetSession().GetProvider<SturfeeVPSLocalizationProvider>();
            if (sturfeeVpsLocalizationProvider != null && sturfeeVpsLocalizationProvider.GetProviderStatus() == ProviderStatus.Ready)
            {
                // localized using HD scan
                if (sturfeeVpsLocalizationProvider.Scanner != null && sturfeeVpsLocalizationProvider.Scanner.ScanType == ScanType.HD)
                {
                    float hditeElevation = GetHDSiteElevation(location);
                    if(hditeElevation != 0)
                    {
                        hdsiteElevationNotFound = false;
                    }
                }                
            }

            if (hdsiteElevationNotFound)
            {
                var tilesProvider = XrSessionManager.GetSession().GetProvider<ITilesProvider>();
                if (tilesProvider != null && tilesProvider.GetProviderStatus() == ProviderStatus.Ready)
                {
                    elevation = tilesProvider.GetElevation(location);
                }
            }

            return elevation;
        }

        private float GetStepElevation(GeoLocation location, float terrainElevation)
        {
            var stepElevation = terrainElevation;

            Vector3 localPos = Converters.GeoToUnityPosition(location);
            localPos.y = terrainElevation + StepOffset;

            Ray ray = new Ray(localPos, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                stepElevation = hit.point.y;
                Debug.Log($" step elevation : {stepElevation}");
            }
            return stepElevation;
        }

        private  float GetHDSiteElevation(GeoLocation location)
        {
            RaycastHit hit;

            Vector3 unityPos = Converters.GeoToUnityPosition(location);
            unityPos.y += 100;

            Ray ray = new Ray(unityPos, Vector3.down);
            Debug.DrawRay(ray.origin, ray.direction * 10000, Color.blue, 2000);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask(SturfeeLayers.HDSiteTerrain)))
            {
                float elevation = hit.point.y;
                //SturfeeDebug.Log("Elevation : " + elevation);
                return elevation;
            }

            return 0;
        }


        [Command]
        private void CmdTranslate(GeoLocation location)
        {
            Location = location;
        }

        [Command]
        private void CmdRotate(Quaternion rotation)
        {
            Rotation = rotation;
        }
    } 
}
