using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AsciiObjectPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject tmpPrefab; // 2-1에서 만든 프리팹 등록
    [SerializeField] private int poolSize = 2500;   // 미리 만들어둘 오브젝트 개수

    // 꺼내서 쓰고 있는 오브젝트와 보관 중인 오브젝트를 관리할 큐(Queue)
    private Queue<GameObject> poolQueue = new Queue<GameObject>();

    private void Awake()
    {
        InitializePool();
    }

    // 게임이 시작될 때 설정한 개수만큼 미리 생성하여 숨겨둡니다.
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(tmpPrefab, this.transform);
            obj.SetActive(false); // 비활성화 상태로 보관
            poolQueue.Enqueue(obj);
        }
    }

    // 풀에서 오브젝트를 하나 꺼내오는 함수
    public GameObject GetObjectFromPool(Transform parent, Vector2 position)
    {
        if (poolQueue.Count > 0)
        {
            GameObject obj = poolQueue.Dequeue();
            
            // 위치와 부모 Canvas를 설정한 뒤 활성화
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            obj.SetActive(true);
            
            return obj;
        }
        else
        {
            // 만약 미리 만든 개수가 부족하면 하나 더 만듦
            GameObject obj = Instantiate(tmpPrefab, parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            return obj;
        }
    }

    // 사용이 끝난 오브젝트를 다시 풀에 반납하는 함수
    public void ReturnObjectToPool(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(this.transform); // 풀 오브젝트 하위로 복귀
        poolQueue.Enqueue(obj);
    }

    // 화면에 켜져 있는 모든 문자를 한 번에 정리할 때 사용하는 함수
    public void ReturnAllObjects(List<GameObject> activeList)
    {
        foreach (GameObject obj in activeList)
        {
            ReturnObjectToPool(obj);
        }
        activeList.Clear();
    }
}