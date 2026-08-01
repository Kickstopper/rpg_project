using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class Pseudo3DPropsManager : MonoBehaviour
    {
        class PropItem
        {
            public RectTransform rect;
            public Image img;
            public float currentZ; 
        }

        [Header("References")]
        public Pseudo3DRoad roadScroller;
        public RectTransform container;
        [Tooltip("좌/우 가로등이 모두 그려진 통짜 UI Image 프리팹")]
        public GameObject propPrefab; 

        [Header("Spawning & Speed")]
        public float approachSpeed = 8.0f; 
        public float spawnZ = 10f;
        public float cullZ = 0.5f;
        public float spacingZ = 2.0f;
        
        [Header("Position & Visuals")]
        public float baseScale = 14f;
        public float yOffset = 0f; 

        [Header("Shader Sync")]
        public float horizonY = 0.5f;

        private List<PropItem> _activeProps = new List<PropItem>();
        private Queue<PropItem> _propPool = new Queue<PropItem>();
        
        private RawImage _roadImage;
        private float _distanceTraveled = 0f;

        private void OnEnable()
        {
            _distanceTraveled = 0f;
            
            if (roadScroller != null)
            {
                _roadImage = roadScroller.GetComponent<RawImage>();
            }

            // 시작할 때 미리 가로등 깔아두기
            float initialZ = cullZ;
            while (initialZ <= spawnZ)
            {
                SpawnProp(initialZ);
                initialZ += spacingZ;
            }
        }

        private void Update()
        {
            if (roadScroller == null || !roadScroller.isMoving || _roadImage == null) return;

            Material currentMat = _roadImage.material;
            if (currentMat == null) return;

            float moveAmount = approachSpeed * Time.deltaTime;
            _distanceTraveled += moveAmount;

            while (_distanceTraveled >= spacingZ)
            {
                _distanceTraveled -= spacingZ;
                SpawnProp(spawnZ + _distanceTraveled);
            }

            float currentCurve = currentMat.GetFloat("_CurveAmount");
            Color roadTint = currentMat.GetColor("_Color");
            Color skyBottom = currentMat.GetColor("_SkyBottomColor");

            float width = container.rect.width;
            float height = container.rect.height;

            for (int i = _activeProps.Count - 1; i >= 0; i--)
            {
                PropItem prop = _activeProps[i];
                prop.currentZ -= moveAmount; 

                if (prop.currentZ <= cullZ)
                {
                    prop.rect.gameObject.SetActive(false);
                    _propPool.Enqueue(prop);
                    _activeProps.RemoveAt(i);
                    continue;
                }

                float depth = 1f / prop.currentZ; 
                float yNorm = horizonY * (1f - depth); 
                float scale = depth * baseScale; 
                
                // 실시간 커브 적용
                float curveOffset = (yNorm * yNorm) * currentCurve;
                
                prop.rect.anchoredPosition = new Vector2(
                    curveOffset * width, 
                    (yNorm * height) + yOffset
                );
                
                prop.rect.localScale = new Vector3(scale, scale, 1f);
                prop.img.color = Color.Lerp(skyBottom, roadTint, depth);
            }
        }

        private void SpawnProp(float zPos)
        {
            PropItem prop;
            if (_propPool.Count > 0)
            {
                prop = _propPool.Dequeue();
                prop.rect.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = Instantiate(propPrefab, container);
                prop = new PropItem
                {
                    rect = go.GetComponent<RectTransform>(),
                    img = go.GetComponent<Image>()
                };
                
                prop.rect.anchorMin = new Vector2(0.5f, 0f);
                prop.rect.anchorMax = new Vector2(0.5f, 0f);
                prop.rect.pivot = new Vector2(0.5f, 0f);
            }

            prop.currentZ = zPos;
            prop.rect.SetAsFirstSibling();
            _activeProps.Add(prop);
        }
        
        private void OnDisable()
        {
            foreach(var prop in _activeProps)
            {
                prop.rect.gameObject.SetActive(false);
                _propPool.Enqueue(prop);
            }
            _activeProps.Clear();
            _distanceTraveled = 0f;
        }
    }
}