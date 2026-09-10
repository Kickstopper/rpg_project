using System.Collections.Generic;
using UnityEngine;

namespace RPGProject.Feature.Exploration
{
    [CreateAssetMenu(fileName = "DungeonMapCatalog", menuName = "Dungeon/Map Catalog")]
    public sealed class DungeonMapCatalog : ScriptableObject
    {
        [Tooltip("DungeonMapEditor에서 저장하고 게임에 등록한 맵입니다.")]
        public List<TextAsset> maps = new List<TextAsset>();
        public List<DungeonTheme> themes = new List<DungeonTheme>();
    }
}
