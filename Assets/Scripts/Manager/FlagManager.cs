using System.Collections.Generic;
using UnityEngine;

namespace Manager
{
    public class FlagManager : MonoBehaviour
    {
        private HashSet<string> activeFlags = new HashSet<string>();

        public void SetFlag(string flag, bool state)
        {
            if (state)
            {
                activeFlags.Add(flag);
            }
            else
            {
                activeFlags.Remove(flag);
            }
        }

        // 특정 플래그가 켜져 있는지 확인
        public bool CheckFlag(string flag)
        {
            return activeFlags.Contains(flag);
        }

        // 플래그 명시적 제거 (SetFlag(flag, false)와 동일)
        public void RemoveFlag(string flag)
        {
            activeFlags.Remove(flag);
        }

        // 세이브 / 로드 / 초기화 로직
        public void ClearAll()
        {
            activeFlags.Clear(); 
        }
        
        public List<string> GetSaveData()
        {
            // HashSet을 List로 변환하여 반환 (직렬화용)
            return new List<string>(activeFlags);
        }

        public void LoadFromSaveData(List<string> savedList)
        {
            activeFlags.Clear();
            
            // 세이브 파일이 null이 아닐 때만 복원
            if (savedList != null)
            {
                activeFlags.UnionWith(savedList);
            }
        }
    }
}