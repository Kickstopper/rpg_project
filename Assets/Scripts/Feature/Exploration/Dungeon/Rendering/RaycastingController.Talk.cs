using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RPGProject.Shared.Input;
using RPGProject.Core;

namespace RPGProject.Feature.Exploration
{
    public partial class RaycastingController
    {
        [Header("Repeatable Cell Dialogue / TALK")]
        [Tooltip("이미 본 반복 대화는 해당 셀에서 TALK 입력으로 다시 실행합니다.")]
        public bool showRepeatableTalkPrompt = true;
        [Tooltip("비워 두면 기본 TALK 말풍선을 사용합니다. 지정한 Sprite는 던전 렌더링 중앙에 합성됩니다.")]
        public Sprite talkIconSprite;
        [Range(12, 256)] public int talkIconPixelWidth = 48;

        private bool _talkPromptShown;
        private bool _cellDialogueActive;
        private int _talkBlockedThroughFrame = -1;
        private float _talkReadyAt;
        private readonly List<RaycastResult> _talkPointerHits = new List<RaycastResult>();
        private PointerEventData _talkPointerData;
        private EventSystem _talkPointerEventSystem;

        public bool IsTalkPromptVisible => _talkPromptShown;

        private bool CanOfferTalk()
        {
            return showRepeatableTalkPrompt && isActiveAndEnabled && _canRender &&
                _currentMap != null && _player != null && _renderer != null && screenImage != null &&
                screenImage.isActiveAndEnabled && !_inputLocked && !_cellDialogueActive && !_player.IsMoving &&
                !IsTransitioning && !_isLookTransitioning && _currentLookState == LookState.None && !_isScanning &&
                !isUIHoldingMovement && inputCooldown <= 0 && Time.frameCount > _talkBlockedThroughFrame &&
                Time.unscaledTime >= _talkReadyAt &&
                (autoMapContainer == null || !autoMapContainer.activeSelf) &&
                (systemMessagePanel == null || !systemMessagePanel.activeSelf) &&
                (transitionManager == null || transitionManager.fadeOverlay == null ||
                    (transitionManager.fadeOverlay.alpha <= 0.001f && !transitionManager.fadeOverlay.blocksRaycasts)) &&
                ManagerRoot.GameState != null && ManagerRoot.GameState.CurrentState == GameState.Exploration &&
                ManagerRoot.GameState.dialogueController != null && !ManagerRoot.GameState.dialogueController.IsDialogueActive &&
                ManagerRoot.DungeonEvent != null;
        }

        private CellEventData CurrentTalkEvent()
        {
            if (!CanOfferTalk()) return null;
            // Logic coordinates change only when a grid step finishes. Never query the front cell.
            return ManagerRoot.DungeonEvent.FindRepeatableEvent(_currentMap, _player.LogicX, _player.LogicY);
        }

        private void RenderDungeonFrame(bool suppressTalk = false)
        {
            if (_renderer == null || _renderer.ScreenTexture == null || _currentMap == null || _player == null) return;
            _talkPromptShown = !suppressTalk && CurrentTalkEvent() != null;
            _renderer.SetTalkPrompt(_talkPromptShown, talkIconSprite, talkIconPixelWidth);
            _renderer.RenderFrame(_player, renderSettings);
        }

        private void LateUpdate()
        {
            // Update may return early during a menu, cooldown or movement callback. Clear stale pixels too.
            if (_talkPromptShown != (CurrentTalkEvent() != null)) RenderDungeonFrame();
        }

        private void OnDisable()
        {
            RenderDungeonFrame(true);
            _talkPromptShown = false;
            _cellDialogueActive = false;
        }

        private void BlockTalkInputAfterDialogue()
        {
            _talkBlockedThroughFrame = Time.frameCount + 1;
            _talkReadyAt = Time.unscaledTime + 0.12f;
            GameInput.ConsumeConfirmThisFrame();
        }

        public bool TryReplayCurrentCellDialogue()
        {
            if (!_talkPromptShown || GameInput.IsConfirmConsumed) return false;
            // Recheck flags, cell, history and dialogue data on the exact input frame.
            CellEventData ev = CurrentTalkEvent();
            if (ev == null) return false;
            _inputLocked = true;
            _player.SetRunning(false);
            isUIHoldingMovement = false;
            GameInput.ConsumeConfirmThisFrame();
            RenderDungeonFrame(true);
            StartCoroutine(ShowCellDialog(ev.eventID, ev.useForceDir ? (int)ev.evForceDir : -1, _player.LogicX, _player.LogicY));
            return true;
        }

        private bool TalkPointerPressed()
        {
            if (!_talkPromptShown || GameInput.IsConfirmConsumed) return false;
            if (Input.touchCount > 0)
            {
                // Two/three-finger menu/map gestures must not also start TALK.
                if (Input.touchCount != 1) return false;
                Touch touch = Input.GetTouch(0);
                return touch.phase == TouchPhase.Began && IsDungeonViewportPointer(touch.position, touch.fingerId);
            }
            return Input.GetMouseButtonDown(0) && IsDungeonViewportPointer(Input.mousePosition, -1);
        }

        private bool IsDungeonViewportPointer(Vector2 position, int pointerId)
        {
            if (screenImage == null) return false;
            Canvas canvas = screenImage.canvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(screenImage.rectTransform, position, camera)) return false;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return true;
            if (_talkPointerData == null || _talkPointerEventSystem != eventSystem)
            {
                _talkPointerEventSystem = eventSystem;
                _talkPointerData = new PointerEventData(eventSystem);
            }
            _talkPointerData.Reset();
            _talkPointerData.position = position;
            _talkPointerData.pointerId = pointerId;
            _talkPointerHits.Clear();
            eventSystem.RaycastAll(_talkPointerData, _talkPointerHits);
            foreach (var hit in _talkPointerHits)
            {
                if (hit.gameObject == null) continue;
                // Movement buttons/EventTriggers win over the viewport even if nested under it.
                if (hit.gameObject.GetComponentInParent<Selectable>() != null || hit.gameObject.GetComponentInParent<EventTrigger>() != null) return false;
                if (hit.gameObject.transform == screenImage.transform || hit.gameObject.transform.IsChildOf(screenImage.transform)) return true;
                // Other raycastable graphics above the viewport are UI, not a dungeon click.
                if (hit.gameObject.GetComponent<Graphic>() != null) return false;
            }
            return true; // RawImage.raycastTarget may legitimately be disabled.
        }

        private IEnumerator ShowCellDialog(string eventID, int forceDir, int x, int y)
        {
            // Capture the source map before a dialogue can teleport or change dungeon data.
            DungeonEventManager events = ManagerRoot.DungeonEvent;
            string mapID = events != null ? events.GetMapID(_currentMap) : _currentMap.mapID;
            return ShowDialog(eventID, forceDir, () =>
            {
                if (events != null) events.MarkCompleted(mapID, x, y, eventID);
            });
        }
    }
}
