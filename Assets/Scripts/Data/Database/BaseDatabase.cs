using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Data
{
    public abstract class BaseDatabase<T> : ScriptableObject where T : BaseRootData
    {
        public List<T> items = new List<T>();
        private Dictionary<string, T> lookupTable;

        public void Initialize()
        {
            lookupTable = new Dictionary<string, T>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!lookupTable.ContainsKey(item.id)) lookupTable.Add(item.id, item);
            }
        }

        public T GetItem(string id)
        {
            if (lookupTable == null) Initialize();
            return lookupTable.ContainsKey(id) ? lookupTable[id] : null;
        }

        // -----------------------------------------------------------------------
        // 프로젝트 전체를 뒤져서 찾는다.
        // -----------------------------------------------------------------------
        public void LoadAllFromResources()
        {
    #if UNITY_EDITOR
            items.Clear();

            // 1. T 타입(예: WeaponData)의 모든 에셋 GUID를 찾는다.
            // "t:"는 타입 검색 필터.
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            foreach (string guid in guids)
            {
                // 2. GUID를 파일 경로로 변환.
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // 3. 경로를 통해 실제 에셋을 로드.
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset != null)
                {
                    items.Add(asset);
                }
            }

            // 4. ID 순서대로 정렬 (선택 사항)
            items = items.OrderBy(x => x.id).ToList();

            // 5. 변경 사항 저장
            EditorUtility.SetDirty(this);
            Debug.Log($"[Database] 프로젝트 전체에서 {items.Count}개의 {typeof(T).Name} 데이터를 찾아 등록했습니다.");
    #else
            Debug.LogWarning("이 기능은 에디터에서만 사용할 수 있습니다.");
    #endif
        }
    }

}

