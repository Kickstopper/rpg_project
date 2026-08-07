using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UI.Battle; 

public class KillBillEffectManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Image redScreenOverlay; 

    public IEnumerator PlayKillBillDeathRoutine(GameObject dyingMonster, float duration = 1f)
    {
        if (redScreenOverlay == null) yield break;

        BattleEntity entity = dyingMonster.GetComponent<BattleEntity>();
        if (entity == null || entity.preferredImage == null) yield break;

        Image mainImage = entity.preferredImage;
        Graphic[] graphics = dyingMonster.GetComponentsInChildren<Graphic>();
        
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].color = Color.black; 
        }

        Canvas overrideCanvas = dyingMonster.GetComponent<Canvas>();
        bool addedCanvas = (overrideCanvas == null);
        if (addedCanvas) overrideCanvas = dyingMonster.AddComponent<Canvas>();
        
        int originalSortOrder = overrideCanvas.sortingOrder;
        bool originalOverride = overrideCanvas.overrideSorting;

        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 30000; 

        Vector3 originalScale = dyingMonster.transform.localScale;
        Quaternion originalRotation = dyingMonster.transform.localRotation;

        dyingMonster.transform.DOScale(originalScale * 1.5f, 0.15f).SetUpdate(true);
        float tiltAngle = Random.value > 0.5f ? 45f : -45f;
        dyingMonster.transform.DOLocalRotate(new Vector3(0, 0, tiltAngle), 0.15f).SetUpdate(true);

        redScreenOverlay.gameObject.SetActive(true);
        Time.timeScale = 0.15f; 

        // 실루엣 감상 대기
        yield return new WaitForSecondsRealtime(duration);

        // 정상 속도 복구 (빨간 화면은 유지)
        Time.timeScale = 1f; 

        // 파편들이 부모를 따라 대각선으로 기울어지는 것을 막기 위해, 폭발 직전 회전값을 정방향으로 원상 복구
        dyingMonster.transform.localRotation = originalRotation;
        
        // 폭발적인 픽셀 조각화 연출 시작
        foreach (var g in graphics)
        {
            if (g != null) g.enabled = false;
        }

        // 파편이 모두 날아가고 사라질 때까지 대기
        yield return StartCoroutine(PixelShatterRoutine(dyingMonster, mainImage, 10));

        // 연출 종료 후 상태 복구 (빨간 화면 끄기)
        redScreenOverlay.gameObject.SetActive(false); // 파편이 다 날아간 뒤에 빨간 화면 종료

        if (dyingMonster != null)
        {
            foreach (var g in graphics)
            {
                if (g != null)
                {
                    g.enabled = true;
                    if (g == mainImage) g.color = entity.originalColor;
                    else g.color = Color.white;
                }
            }

            if (addedCanvas) Destroy(overrideCanvas);
            else
            {
                overrideCanvas.overrideSorting = originalOverride;
                overrideCanvas.sortingOrder = originalSortOrder;
            }

            dyingMonster.transform.localScale = originalScale;
            
            dyingMonster.SetActive(false);
        }
    }

    private IEnumerator PixelShatterRoutine(GameObject targetObj, Image sourceImage, int gridSize)
    {
        GameObject shatterContainer = new GameObject("ShatterContainer", typeof(RectTransform));
        shatterContainer.transform.SetParent(targetObj.transform, false);
        shatterContainer.transform.localPosition = Vector3.zero;
        shatterContainer.transform.localRotation = Quaternion.identity;
        shatterContainer.transform.localScale = Vector3.one;

        RectTransform imgRect = sourceImage.rectTransform;
        float chunkWidth = imgRect.rect.width / gridSize;
        float chunkHeight = imgRect.rect.height / gridSize;

        Sprite sprite = sourceImage.sprite;
        if (sprite == null) yield break;

        Texture mainTex = sprite.texture;
        Rect texRect = sprite.textureRect;

        float uvStartX = texRect.x / mainTex.width;
        float uvStartY = texRect.y / mainTex.height;
        float uvWidth = texRect.width / mainTex.width;
        float uvHeight = texRect.height / mainTex.height;

        float uvChunkW = uvWidth / gridSize;
        float uvChunkH = uvHeight / gridSize;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                GameObject chunkObj = new GameObject($"Pixel_{x}_{y}", typeof(RectTransform));
                chunkObj.transform.SetParent(shatterContainer.transform, false);

                RawImage rawImg = chunkObj.AddComponent<RawImage>();
                rawImg.texture = mainTex;
                rawImg.color = Color.black; 

                rawImg.uvRect = new Rect(uvStartX + (x * uvChunkW), uvStartY + (y * uvChunkH), uvChunkW, uvChunkH);

                RectTransform rect = rawImg.rectTransform;
                rect.sizeDelta = new Vector2(chunkWidth, chunkHeight);

                float posX = (x - (gridSize / 2f) + 0.5f) * chunkWidth;
                float posY = (y - (gridSize / 2f) + 0.5f) * chunkHeight;
                rect.anchoredPosition = new Vector2(posX, posY);

                float delay = Random.Range(0f, 0.08f);
                float moveDuration = Random.Range(0.2f, 0.35f); 
                
                Vector2 explodeDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-0.5f, 1f)).normalized;
                float distance = Random.Range(100f, 400f); 
                Vector2 targetPos = rect.anchoredPosition + (explodeDir * distance);

                rect.DOAnchorPos(targetPos, moveDuration).SetEase(Ease.OutExpo).SetDelay(delay);
                rawImg.DOFade(0f, 0.15f).SetDelay(delay + (moveDuration * 0.5f));
            }
        }

        yield return YieldCache.WaitForSeconds(0.2f);
        Destroy(shatterContainer);
    }
}