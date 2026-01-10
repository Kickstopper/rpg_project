using UnityEngine;
namespace Data
{
    [CreateAssetMenu(fileName = "NewLevel", menuName = "Dungeon/LevelData")]
    public class LevelData : ScriptableObject
    {
        [Header("기본 정보")]
        public string levelID;   // 예: "B1F", "Cave_01" (검색용 고유 키)
        public string displayName; // 예: "지하 1층 - 어둠의 입구" (UI 표시용)

        [Header("맵 디자인")]
        public DungeonTheme currentTheme; 
        [TextArea(10, 20)] 
        public string mapString;

        [Header("시작 좌표와 방향")]
        public int startPosX;
        public int startPosY;
        public int startDir;

        [Header("변환된 데이터 (확인용)")]
        // 2차원 배열은 인스펙터에 안 보이므로, 1차원 리스트로 관리하거나
        // 런타임 중에만 int[,]로 변환해서 씀.
        public int width;
        public int height;

        // 문자열을 분석해서 2차원 배열로 만들어주는 함수
        public int[,] GetMapData()
        {
            // 줄바꿈 문자로 쪼개기
            string[] lines = mapString.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            height = lines.Length;
            width = lines[0].Length; // 첫 줄의 길이를 너비로 기준 잡음

            int[,] map = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                // 위에서부터 읽으므로 y축 반전이 필요할 수 있음 (취향 차이)
                string line = lines[y].Trim(); 
                for (int x = 0; x < width; x++)
                {
                    if (x < line.Length)
                    {
                        // 문자 하나를 숫자로 변환 ('0' -> 0, '1' -> 1)
                        // 만약 문자가 숫자 외의 것(예: S)이라면 별도 처리 필요
                        char c = line[x];
                        if (char.IsDigit(c))
                        {
                            map[x, y] = c - '0'; // 문자를 숫자로 변환하는 트릭
                        }
                        else
                        {
                            map[x, y] = 0; // 숫자가 아니면 0(길)으로 처리 등
                        }
                    }
                }
            }
            return map;
        }

        public SpriteInfo[] DUMMY_MAP_SPRITE_DATA = new SpriteInfo[19] 
        {
            new SpriteInfo { x = 20.5f, y = 11.5f, texIdx = 6 },
            new SpriteInfo { x = 18.5f, y = 4.5f, texIdx = 6 },
            new SpriteInfo { x = 10.0f, y = 4.5f, texIdx = 6 },
            new SpriteInfo { x = 10.0f, y = 12.5f, texIdx = 6 },
            new SpriteInfo { x = 3.5f,  y = 6.5f, texIdx = 6 },
            new SpriteInfo { x = 3.5f,  y = 20.5f, texIdx = 6 },
            new SpriteInfo { x = 3.5f,  y = 14.5f, texIdx = 6 },
            new SpriteInfo { x = 14.5f, y = 20.5f, texIdx = 6 },
            new SpriteInfo { x = 18.5f, y = 10.5f, texIdx = 6 },
            new SpriteInfo { x = 18.5f, y = 11.5f, texIdx = 6 },
            new SpriteInfo { x = 18.5f, y = 12.5f, texIdx = 6 },
            new SpriteInfo { x = 21.5f, y = 1.5f, texIdx = 6 },
            new SpriteInfo { x = 15.5f, y = 1.5f, texIdx = 6 },
            new SpriteInfo { x = 16.0f, y = 1.8f, texIdx = 6 },
            new SpriteInfo { x = 16.2f, y = 1.2f, texIdx = 7 },
            new SpriteInfo { x = 3.5f,  y = 2.5f, texIdx = 6 },
            new SpriteInfo { x = 9.5f,  y = 15.5f, texIdx = 6 },
            new SpriteInfo { x = 10.0f, y = 15.1f, texIdx = 7 },
            new SpriteInfo { x = 10.5f, y = 15.8f, texIdx = 7 }
        };
    }


    public struct SpriteInfo
    {
        public float x;
        public float y;
        public int texIdx;
    }
}