using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;

namespace Manager
{
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance;
        
        private List<QuestData> allQuests = new List<QuestData>();

        // 퀘스트 상태를 메모리에서 관리하는 딕셔너리
        private Dictionary<string, bool> completedQuests = new Dictionary<string, bool>();
        private Dictionary<string, bool> activeQuests = new Dictionary<string, bool>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 게임 실행 시 CSV에서 읽어온 데이터를 주입
        public void InitializeQuests(List<QuestData> questDataList)
        {
            allQuests = questDataList;
            Debug.Log($"[QuestManager] {allQuests.Count}개의 퀘스트 데이터 로드 완료.");
        }

        // 전체 퀘스트 목록 반환
        public List<QuestData> GetAllQuests()
        {
            return allQuests;
        }

        // 특정 ID의 퀘스트 데이터 반환
        public QuestData GetQuestData(string questID)
        {
            return allQuests.Find(q => q.QuestID == questID);
        }

        // 현재 진행 중인 퀘스트 목록만 반환
        public List<QuestData> GetActiveQuests()
        {
            return allQuests.Where(q => IsQuestActive(q.QuestID)).ToList();
        }

        // 아직 수락하지 않은(진행 가능) 퀘스트 목록만 반환
        public List<QuestData> GetAvailableQuests()
        {
            return allQuests.Where(q => !IsQuestActive(q.QuestID) && !IsQuestCompleted(q.QuestID)).ToList();
        }
        
        // 데이터 초기화 (New Game)
        public void NewGame()
        {
            completedQuests.Clear();
            activeQuests.Clear();
            
            Debug.Log("[QuestManager] 퀘스트 데이터가 초기화되었습니다. (New Game)");
        }

        // 데이터 저장
        public void Save(SaveData data)
        {
            data.completedQuestIDs = new List<string>(completedQuests.Keys);
            data.activeQuestIDs = new List<string>(activeQuests.Keys);
            
            Debug.Log($"[QuestManager] 퀘스트 저장 완료: 완료 {completedQuests.Count}개, 진행중 {activeQuests.Count}개");
        }

        // 데이터 불러오기
        public void Load(SaveData data)
        {
            // 기존 메모리 데이터 초기화
            completedQuests.Clear();
            activeQuests.Clear();

            // 세이브 파일에서 완료된 퀘스트 복원
            if (data.completedQuestIDs != null)
            {
                foreach (string qId in data.completedQuestIDs)
                {
                    completedQuests[qId] = true;
                    
                    // 기존 FlagManager와 호환성을 위해 플래그도 함께 세팅
                    if (ManagerRoot.Flag != null)
                    {
                        ManagerRoot.Flag.AddFlag($"QuestComplete_{qId}");
                    }
                }
            }

            // 세이브 파일에서 진행 중인 퀘스트 복원
            if (data.activeQuestIDs != null)
            {
                foreach (string qId in data.activeQuestIDs)
                {
                    activeQuests[qId] = true;
                }
            }
            
            Debug.Log($"[QuestManager] 퀘스트 로드 완료: 완료 {completedQuests.Count}개, 진행중 {activeQuests.Count}개");
        }

        // 퀘스트 제어 API
        
        // 퀘스트 수락
        public void AcceptQuest(string questID)
        {
            if (!completedQuests.ContainsKey(questID) && !activeQuests.ContainsKey(questID))
            {
                activeQuests[questID] = true;
                Debug.Log($"[QuestManager] 퀘스트 수락: {questID}");
            }
        }

        // 퀘스트 완료
        public void CompleteQuest(string questID)
        {
            if (!completedQuests.ContainsKey(questID))
            {
                // 완료 목록에 추가하고, 진행 중 목록에서는 제거
                completedQuests[questID] = true;
                if (activeQuests.ContainsKey(questID))
                {
                    activeQuests.Remove(questID);
                }

                // 범용 이벤트/UI 처리(OfficeUI 등)를 위해 FlagManager에도 기록
                if (ManagerRoot.Flag != null)
                {
                    ManagerRoot.Flag.AddFlag($"QuestComplete_{questID}");
                }
                
                Debug.Log($"[QuestManager] 퀘스트 완료: {questID}");
            }
        }

        // 오피스 진입 시 보상을 받을 수 있는 퀘스트 목록 반환
        public List<QuestData> GetReadyToReportQuests()
        {
            List<QuestData> readyQuests = new List<QuestData>();
            
            foreach (var qId in activeQuests.Keys)
            {
                // 던전에서 조건을 달성하여 Ready 플래그가 켜진 퀘스트인지 확인
                if (ManagerRoot.Flag != null && ManagerRoot.Flag.CheckFlag($"QuestReady_{qId}"))
                {
                    QuestData data = GetQuestData(qId);
                    if (data != null) readyQuests.Add(data);
                }
            }
            return readyQuests;
        }

        // 퀘스트 완료 여부 확인
        public bool IsQuestCompleted(string questID)
        {
            return completedQuests.ContainsKey(questID) && completedQuests[questID];
        }

        // 퀘스트 진행 중 여부 확인
        public bool IsQuestActive(string questID)
        {
            return activeQuests.ContainsKey(questID) && activeQuests[questID];
        }
    }
}