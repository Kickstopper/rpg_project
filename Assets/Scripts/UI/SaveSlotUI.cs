using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data;
using Controller;
namespace UI
{
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI slotNumberText; // "Slot 1"
        public TextMeshProUGUI infoText;       // "Lv.10 Warrior / 던전입구"
        public TextMeshProUGUI timeText;       // "2024-05-20 14:00"
        public GameObject emptyObject;         // 빈 슬롯일 때 표시할 텍스트나 이미지
        public GameObject dataObject;          // 데이터가 있을 때 표시할 그룹

        [Header("Highlight Settings")]
        public Image highlightImage;   // 슬롯의 포커스 이미지
        public Color normalColor = new Color(0f, 0f, 0f, 0.5f); // 평상시 (반투명 검정 등)
        public Color focusColor = new Color(0.5f, 0.5f, 0.5f, 0.8f); // 포커스 (밝은 회색 등)
        public GameObject focusFrame; // (선택) 포커스 시 켜질 테두리 이미지 오브젝트
        
        private int mySlotIndex;
        private SaveLoadUIController controller;
        private bool hasData;

        public void Initialize(int index, SaveLoadUIController parentController)
        {
            mySlotIndex = index;
            controller = parentController;
            slotNumberText.text = $"SLOT {index + 1}";
            
            GetComponent<Button>().onClick.AddListener(OnClicked);
        }

        public void SetData(SaveData data)
        {
            if (data == null)
            {
                hasData = false;
                dataObject.SetActive(false);
                emptyObject.SetActive(true);
                infoText.text = "NO DATA";
                timeText.text = "--:--";
            }
            else
            {
                hasData = true;
                dataObject.SetActive(true);
                emptyObject.SetActive(false);

                // 대표 캐릭터 정보 표시 (예: 첫 번째 파티원)
                string leaderInfo = "Unknown";
                if (data.partyMembers != null && data.partyMembers.Count > 0)
                {
                    var leader = data.partyMembers[0];
                    // DB에서 이름 가져오기 등이 가능하다면 좋음. 여기선 ID와 레벨 표시
                    leaderInfo = $"Lv.{leader.level} {leader.characterId}"; 
                }

                // 장소 정보
                string location = string.IsNullOrEmpty(data.dungeonId) ? "Unknown" : data.dungeonId;

                infoText.text = $"{leaderInfo} | {location}";
                timeText.text = data.saveTime;
            }
        }

        void OnClicked()
        {
            controller.OnSlotSelected(mySlotIndex, hasData);
        }

        // 외부(Controller)에서 포커스 상태를 변경할 때 호출
        public void SetFocus(bool isFocused)
        {
            if (highlightImage != null)
            {
                highlightImage.color = isFocused ? focusColor : normalColor;
            }

            if (focusFrame != null)
            {
                focusFrame.SetActive(isFocused);
            }
        }
    }
}

