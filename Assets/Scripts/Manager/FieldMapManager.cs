using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    public class FieldMapManager : MonoBehaviour
    {
        [Header("Databases")]
        [Tooltip("게임 내 모든 맵의 고유 정보(스폰 좌표 등)를 등록합니다.")]
        public List<MapNodeData> allMapNodes = new List<MapNodeData>();
        
        [Tooltip("맵과 맵을 잇는 경로 정보(거리, 시간)를 등록합니다.")]
        public List<RouteData> allRoutes = new List<RouteData>();

        private Dictionary<string, MapNodeData> _mapNodeDict = new Dictionary<string, MapNodeData>();

        private void Awake()
        {
            foreach (var node in allMapNodes)
            {
                _mapNodeDict[node.mapID] = node;
            }
        }

        public MapNodeData GetNodeData(string mapID)
        {
            if (_mapNodeDict.ContainsKey(mapID)) return _mapNodeDict[mapID];
            return null;
        }

        // 현재 이동 가능한(해금된) 목적지 목록만 UI용으로 반환
        public List<FieldMapDestData> GetAvailableDestinations(string currentMapID)
        {
            List<FieldMapDestData> availableDestinations = new List<FieldMapDestData>();

            // 전체 경로 중 출발지가 현재 맵인 경로를 모두 찾음
            foreach (RouteData route in allRoutes)
            {
                if (route.fromMapID == currentMapID)
                {
                    // 도착지의 맵 고유 정보를 가져옴
                    if (_mapNodeDict.TryGetValue(route.toMapID, out MapNodeData targetMapNode))
                    {
                        // 경로 정보와 맵 정보를 합쳐 UI용 데이터로 변환
                        FieldMapDestData destData = new FieldMapDestData
                        {
                            mapID = route.toMapID,
                            
                            // MapNodeData에서 가져오는 정보
                            displayName = targetMapNode.displayName,
                            targetX = targetMapNode.spawnX,
                            targetY = targetMapNode.spawnY,
                            targetDir = targetMapNode.spawnDir,
                            
                            // RouteData에서 가져오는 정보
                            distance = route.distance,
                            timeHours = route.timeHours
                        };
                        
                        availableDestinations.Add(destData);
                    }
                    else
                    {
                        Debug.LogWarning($"[FieldMapManager] {route.toMapID}에 해당하는 MapNodeData를 찾을 수 없습니다!");
                    }
                }
            }

            return availableDestinations;
        }
    }
}