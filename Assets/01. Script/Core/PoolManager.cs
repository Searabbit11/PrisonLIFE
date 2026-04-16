using System.Collections.Generic;
using UnityEngine;

public sealed class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    [SerializeField] private List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // DOTween 용량 설정 (트윈 500개, 시퀀스 100개로 확장)
        DG.Tweening.DOTween.SetTweensCapacity(500, 100);

        InitializePools();
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (poolDictionary.ContainsKey(tag) == false)
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return null;
        }

        GameObject objectToSpawn = poolDictionary[tag].Count > 0 
            ? poolDictionary[tag].Dequeue() 
            : CreateNewObject(tag);

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.SetPositionAndRotation(position, rotation);

        // 다시 큐에 넣는 작업은 ReturnToPool에서 명시적으로 처리하거나, 
        // 하이퍼캐주얼 특성상 순환 구조를 위해 여기서 다시 넣기도 함.
        // 여기서는 명시적 반환(ReturnToPool) 방식을 사용하겠습니다.

        return objectToSpawn;
    }

    private GameObject CreateNewObject(string tag)
    {
        // 풀이 모자랄 경우 동적 생성
        foreach (var p in pools)
        {
            if (p.tag == tag)
            {
                GameObject obj = Instantiate(p.prefab, transform);
                return obj;
            }
        }
        return null;
    }

    public void ReturnToPool(string tag, GameObject obj)
    {
        if (poolDictionary.ContainsKey(tag) == false) return;

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        poolDictionary[tag].Enqueue(obj);
    }
}
