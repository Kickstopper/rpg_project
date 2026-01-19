using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(RawImage))]
    public class AutoTilingBackground : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _scrollSpeedX = 0.05f;
        [SerializeField] private float _scrollSpeedY = 0.05f;
        
        // 이미지 원본 사이즈 (121 * 94)
        [SerializeField] private Vector2 _textureSize = new Vector2(121, 94);

        private RawImage _rawImage;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Start()
        {
            // 게임 시작 시 화면(RectTransform) 크기에 맞춰 UV 스케일 조정
            UpdateTiling();
        }

        private void Update()
        {
            // 스크롤 처리
            Rect uvRect = _rawImage.uvRect;
            uvRect.x += _scrollSpeedX * Time.deltaTime;
            uvRect.y += _scrollSpeedY * Time.deltaTime;
            _rawImage.uvRect = uvRect;
        }

        // 화면 크기가 바뀔 때(해상도 변경 등)를 대비해 수동으로 호출 가능
        public void UpdateTiling()
        {
            if (_rawImage == null)
            {
                _rawImage = GetComponent<RawImage>();
                _rectTransform = GetComponent<RectTransform>();
            }
            if (_rawImage == null || _rawImage.texture == null) return;

            // 현재 RawImage의 크기 (화면 꽉 채운 상태라면 화면 크기)
            float width = _rectTransform.rect.width;
            float height = _rectTransform.rect.height;

            // 화면 크기 / 패턴 크기 = 반복 횟수
            float repeatX = width / _textureSize.x;
            float repeatY = height / _textureSize.y;

            // UV Rect의 W, H에 적용 (이미지 비율 유지됨)
            Rect currentUV = _rawImage.uvRect;
            currentUV.width = repeatX;
            currentUV.height = repeatY;
            _rawImage.uvRect = currentUV;
        }
        
        // 에디터에서 테스트용 (RectTransform 크기가 변하면 자동 갱신)
        private void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying) UpdateTiling();
        }
    }
    
}

