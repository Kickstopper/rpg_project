using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;
using Controller;

namespace UI.Shop
{
    public class ShopEquipUI : MonoBehaviour
    {
        
        [Header("Shop Equip UI Container")]
        public GameObject EquipUIContainer;

        [Header("Character Select UI")]
        public GameObject charSelectPanel; // 캐릭터 버튼들이 배치될 패널
        public Transform charListContent;  // 버튼들이 생성될 부모 Transform
        public GameObject charButtonPrefab; // TextMeshProUGUI가 포함된 버튼 프리팹

        [Header("Common Equip UI Reference")]
        public GameObject innerEquipUI; // 공통 EquipUI
        public EquipUIController innerEquipUIController;

        private List<RuntimeCharacterData> partyMembers;
        private List<Button> charButtons = new List<Button>();
        private int currentCharIndex = 0;
        private bool isEquipping = false;

        public void OpenEquipMode()
        {
            EquipUIContainer.SetActive(true);
            isEquipping = false;
            
            charSelectPanel.SetActive(true);
            innerEquipUI.SetActive(false);

            innerEquipUIController.onCloseCallback = OnEquipUIClose;

            PopulateCharacterList();
        }

        private void PopulateCharacterList()
        {
            foreach (Transform child in charListContent) Destroy(child.gameObject);
            charButtons.Clear();

            if (PartyManager.Instance == null) return;
            partyMembers = PartyManager.Instance.partyData;

            for (int i = 0; i < partyMembers.Count; i++)
            {
                GameObject go = Instantiate(charButtonPrefab, charListContent);
                Button btn = go.GetComponent<Button>();
                TextMeshProUGUI txt = go.GetComponentInChildren<TextMeshProUGUI>();
                
                if (txt != null) txt.text = partyMembers[i].name;

                int index = i;
                btn.onClick.AddListener(() => ConfirmSelection(index));
                
                charButtons.Add(btn);
            }

            currentCharIndex = 0;
            if (charButtons.Count > 0) SelectCharacter(currentCharIndex);
        }

        void Update()
        {
            // 이미 캐릭터를 선택해 장비 모드에 진입했다면, 입력을 무시하고 EquipUIController에게 맡김
            if (isEquipping || charButtons.Count == 0) return;

            HandleInput();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveSelection(1);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                ConfirmSelection(currentCharIndex);
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift))
            {
                CloseShopEquip();
            }
        }

        private void MoveSelection(int dir)
        {
            int nextIndex = currentCharIndex + dir;
            if (nextIndex < 0) nextIndex = charButtons.Count - 1;
            if (nextIndex >= charButtons.Count) nextIndex = 0;

            SelectCharacter(nextIndex);
        }

        private void SelectCharacter(int index)
        {
            currentCharIndex = index;
            charButtons[index].Select();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
        }

        private void ConfirmSelection(int index)
        {
            currentCharIndex = index;
            isEquipping = true;
            
            // 패널 전환
            //charSelectPanel.SetActive(false);
            innerEquipUI.SetActive(true);
            
            innerEquipUIController.SetCharacter(partyMembers[index]); // 개별 캐릭터 UI 초기화
        }

        private void OnEquipUIClose()
        {
            isEquipping = false;
            innerEquipUI.SetActive(false);
            charSelectPanel.SetActive(true);
            
            // 캐릭터 선택 창으로 돌아왔을 때, 이전에 선택했던 캐릭터에 포커스 복구
            if (charButtons.Count > 0) SelectCharacter(currentCharIndex);
        }

        public void CloseShopEquip()
        {
            EquipUIContainer.SetActive(false);
        }
    }
}