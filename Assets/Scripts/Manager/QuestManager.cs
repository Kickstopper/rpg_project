using System.Collections.Generic;
using UnityEngine;
using Data;
using System.Linq;

namespace Manager
{
    public class QuestManager : MonoBehaviour
    {
        private List<QuestData> allQuests = new List<QuestData>();

        // 퀘스트 상태를 메모리에서 관리하는 딕셔너리
        private Dictionary<string, bool> completedQuests = new Dictionary<string, bool>();
        private Dictionary<string, QuestProgress> activeQuests = new Dictionary<string, QuestProgress>();

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

        // =========================================================
        // 2. 데이터 저장 (Save)
        // =========================================================
        public void Save(SaveData data)
        {
            // 완료된 퀘스트 ID 저장 (기존과 동일)
            data.completedQuestIDs = new List<string>(completedQuests.Keys);
            
            // 진행 중인 퀘스트 데이터 저장
            // Dictionary의 Value(QuestProgress 객체들)를 그대로 List로 변환하여 저장합니다.
            data.activeQuests = new List<QuestProgress>(activeQuests.Values);
            
            Debug.Log($"[QuestManager] 퀘스트 저장 완료: 완료 {completedQuests.Count}개, 진행중 {activeQuests.Count}개");
        }

        // =========================================================
        // 3. 데이터 불러오기 (Load)
        // =========================================================
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
                    
                    // 기존 FlagManager와 호환성을 위해 플래그 세팅
                    if (ManagerRoot.Flag != null)
                    {
                        ManagerRoot.Flag.SetFlag($"QuestComplete_{qId}", true);
                    }
                }
            }

            // 세이브 파일에서 진행 중인 퀘스트 복원
            if (data.activeQuests != null)
            {
                foreach (QuestProgress progress in data.activeQuests)
                {
                    // 딕셔너리에 퀘스트 ID를 Key로 하여 복원된 진행도 객체 삽입
                    activeQuests[progress.questID] = progress;

                    // 만약 게임을 껐다 켰는데 이미 완료 조건을 다 채워둔(보고 대기 중인) 상태라면
                    // 오피스 UI에서 바로 보상을 줄 수 있도록 Ready 플래그를 다시 켜줌
                    if (progress.isReadyToReport && ManagerRoot.Flag != null)
                    {
                        ManagerRoot.Flag.SetFlag($"QuestReady_{progress.questID}", true);
                    }
                }
            }
            
            Debug.Log($"[QuestManager] 퀘스트 로드 완료: 완료 {completedQuests.Count}개, 진행중 {activeQuests.Count}개");
        }

        // 전투 종료 후 킬 카운트 정산
        public List<QuestData> ProcessBattleResult(string mapLocationID, List<string> killedMonsterIDs)
        {
            List<QuestData> newlyCompletedQuests = new List<QuestData>();

            foreach (var kvp in activeQuests)
            {
                QuestProgress progress = kvp.Value;
                if (progress.isReadyToReport) continue; // 이미 달성해서 보고 대기 중인 퀘스트는 패스

                QuestData data = GetQuestData(progress.questID);
                
                // 장소(LocationID)가 일치하는지 확인
                if (data.LocationID != mapLocationID) continue; 

                bool isUpdated = false;

                // 잡은 몬스터가 퀘스트 타겟인지 확인하고 카운트 증가
                foreach (string mID in killedMonsterIDs)
                {
                    if (progress.killCounts.ContainsKey(mID))
                    {
                        int reqCount = data.Targets.Find(t => t.monsterID == mID).requiredCount;
                        if (progress.killCounts[mID] < reqCount)
                        {
                            progress.killCounts[mID]++;
                            isUpdated = true;
                        }
                    }
                }

                // 카운트가 올랐다면, 모든 목표를 달성했는지 체크
                if (isUpdated)
                {
                    bool allMet = true;
                    foreach (var target in data.Targets)
                    {
                        if (progress.killCounts[target.monsterID] < target.requiredCount)
                        {
                            allMet = false;
                            break;
                        }
                    }

                    // 모두 달성했다면 Ready 상태로 변경하고 결과 리스트에 추가
                    if (allMet)
                    {
                        progress.isReadyToReport = true;
                        newlyCompletedQuests.Add(data);
                        
                        // OfficeUI에서 보상을 주기 위한 플래그 활성화
                        if (ManagerRoot.Flag != null) 
                            ManagerRoot.Flag.SetFlag($"QuestReady_{data.QuestID}", true);
                    }
                }
            }

            // 이번 전투로 방금 막 달성한 퀘스트 리스트를 반환합니다. (UI 표시용)
            return newlyCompletedQuests; 
        }

        // 퀘스트 수락. 퀘스트 수주 시 카운트를 0으로 초기화
        public void AcceptQuest(string questID)
        {
            if (!completedQuests.ContainsKey(questID) && !activeQuests.ContainsKey(questID))
            {
                QuestData data = GetQuestData(questID);
                QuestProgress progress = new QuestProgress { questID = questID };
                // 타겟 몬스터들의 킬 카운트를 0으로 세팅
                if (data.Targets != null)
                {
                    foreach (var target in data.Targets)
                    {
                        progress.killCounts[target.monsterID] = 0;
                    }
                }
                
                activeQuests[questID] = progress;
                Debug.Log($"[QuestManager] {questID} 수주 성공!");
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
                    ManagerRoot.Flag.SetFlag($"QuestComplete_{questID}", true);
                }
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
            return activeQuests.ContainsKey(questID);
        }
    }
}