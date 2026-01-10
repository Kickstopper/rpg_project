using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        private Dictionary<string, DungeonMapState> allDiscoveredMaps = new();
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 부모가 있다면 관계를 끊고 최상위로 나옴.
                transform.SetParent(null);
            
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public DungeonMapState GetDungeonMapState(string id)
        {
            if (allDiscoveredMaps.ContainsKey(id)) return allDiscoveredMaps[id];
            return null;
        }

        public void AddDungeonMapState(string id, DungeonMapState mapState)
        {
            if (allDiscoveredMaps.ContainsKey(id)) return;

            allDiscoveredMaps[id] = mapState;
        }

    }

}
