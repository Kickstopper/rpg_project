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
            public Image baseImg;  
            public Image glowImg;  
            public float currentZ; 
        }

        [Header("References")]
        public Pseudo3DRoad roadScroller;
        public RectTransform container;
        public GameObject propPrefab; 

        [Header("Spawning & Speed")]
        public float approachSpeed = 8.0f; 
        public float spawnZ = 10f;
        public float cullZ = 0.5f;
        public float spacingZ = 2.0f;
        
        [Header("Position & Visuals")]
        public float baseScale = 2.5f;
        public float yOffset = 0f; 

        [Header("Shader Sync")]
        public float horizonY = 1.0f;

        private List<PropItem> _activeProps = new List<PropItem>();
        private Queue<PropItem> _propPool = new Queue<PropItem>();
        private RawImage _roadImage;
        private float _distanceTraveled = 0f;

        private void OnEnable()
        {
            _distanceTraveled = 0f;
            if (roadScroller != null) _roadImage = roadScroller.GetComponent<RawImage>();

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
            float currentHill = currentMat.GetFloat("_HillAmount");
            Color roadTint = currentMat.GetColor("_Color");
            Color skyBottom = currentMat.GetColor("_SkyBottomColor");

            float width = container.rect.width;
            float height = container.rect.height;

            float currentHour = FieldMapUIManager.Instance.CurrentSimulatedHour % 24f;
            if (currentHour < 0) currentHour += 24f;
            
            float glowAlpha = 0f;
            if (currentHour >= 18f && currentHour < 19f) glowAlpha = currentHour - 18f; 
            else if (currentHour >= 19f || currentHour < 6f) glowAlpha = 1f; 
            else if (currentHour >= 6f && currentHour < 7f) glowAlpha = 1f - (currentHour - 6f); 
            else glowAlpha = 0f; 

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
                float adjustedY = horizonY * (1f - depth); 
                
                float yNorm;
                if (Mathf.Abs(currentHill) < 0.001f) 
                {
                    yNorm = adjustedY;
                }
                else
                {
                    float discriminant = 1f - 4f * currentHill * adjustedY;
                    
                    if (discriminant < 0f) 
                    {
                        // 극단적인 내리막길로 가로등이 사각지대에 가려진 경우 화면 위로 숨김
                        yNorm = 2.0f; 
                    }
                    else 
                    {
                        yNorm = (1f - Mathf.Sqrt(discriminant)) / (2f * currentHill);
                    }
                }

                float scale = depth * baseScale; 
                float curveOffset = (adjustedY * adjustedY) * currentCurve;
                
                prop.rect.anchoredPosition = new Vector2(
                    curveOffset * width, 
                    (yNorm * height) + yOffset
                );
                
                prop.rect.localScale = new Vector3(scale, scale, 1f);
                
                if (prop.baseImg != null)
                {
                    prop.baseImg.color = Color.Lerp(skyBottom, roadTint, depth);
                }

                if (prop.glowImg != null)
                {
                    if (glowAlpha <= 0.01f)
                    {
                        if (prop.glowImg.gameObject.activeSelf)
                            prop.glowImg.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (!prop.glowImg.gameObject.activeSelf)
                            prop.glowImg.gameObject.SetActive(true);

                        Color glowColor = prop.glowImg.color;
                        glowColor.a = glowAlpha * depth; 
                        prop.glowImg.color = glowColor;
                    }
                }
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
                Transform glowT = go.transform.Find("GlowImage");

                prop = new PropItem
                {
                    rect = go.GetComponent<RectTransform>(),
                    baseImg = go.GetComponent<Image>(),
                    glowImg = glowT != null ? glowT.GetComponent<Image>() : null
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