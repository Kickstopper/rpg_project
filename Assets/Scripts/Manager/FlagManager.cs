using System.Collections.Generic;
using UnityEngine;
namespace Manager
{
    public class FlagManager : MonoBehaviour
    {
        public static FlagManager Instance;
        private HashSet<string> activeFlags = new HashSet<string>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        public void ClearFlag()
        {
            activeFlags = new HashSet<string>();
        }
        
        public List<string> GetSaveData()
        {
            return new List<string>(activeFlags);
        }
        public void LoadFromSaveData(List<string> savedList)
        {
            activeFlags = new HashSet<string>(savedList);
        }

        public void AddFlag(string flag)
        {
            activeFlags.Add(flag);
        }
        
        public bool CheckFlag(string flag)
        {
            return activeFlags.Contains(flag);
        }
    }

}
