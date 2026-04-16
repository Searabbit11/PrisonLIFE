using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class Converter : MonoBehaviour
{
    [Header("Process Points")]
    [SerializeField] private Transform rawOreStackPivot;  // 바닥 대기 구역 (원자재)
    [SerializeField] private Transform meltPoint;         // 제련기 상단 입구
    [SerializeField] private Transform beltStartPoint;    // 주괴 시작점
    [SerializeField] private Transform pressPoint;        // 압착 지점
    [SerializeField] private Transform beltEndPoint;      // 벨트 끝
    [SerializeField] private Transform outputStackPoint;  // 최종 적재 지점

    [Header("Prefabs")]
    [SerializeField] private GameObject ingotPrefab;      // 가공 중간 단계 은주괴
    [SerializeField] private GameObject handcuffPrefab;   // 최종 수갑

    [Header("Spawn Rotations")]
    [SerializeField] private Vector3 ingotSpawnRotation = Vector3.zero;
    [SerializeField] private Vector3 handcuffSpawnRotation = Vector3.zero;

    [Header("Visuals")]
    [SerializeField] private Transform customCubePress;   // 압착기 역할을 할 Cube 오브젝트

    [Header("Settings")]
    [SerializeField] private float receiveCooldown = 0.1f; // 플레이어로부터 광석을 받는 간격 (빠르게)
    [SerializeField] private float processCooldown = 1.0f; // 가공기가 광석을 처리하는 간격 (일정하게)
    [SerializeField] private float absorbWaitTime = 0.5f;  // 광물이 바닥에 적재된 후 대기하는 시간
    [SerializeField] private float absorbMoveDuration = 0.4f; // 용광로로 날아가는 시간
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float moveDuration = 1.0f;    // 컨베이어 벨트 이동 시간
    
    [Header("Raw Ore Stacking (Input)")]
    [SerializeField] private float rawOreStackZOffset = 0.4f;
    [SerializeField] private float rawOreStackYOffset = 0.15f;
    private readonly List<GameObject> rawOresOnFloor = new List<GameObject>();
    private readonly List<float> rawOreArrivalTimes = new List<float>();

    [Header("Output Stacking")]
    [SerializeField] private float stackYOffset = 0.15f;
    [SerializeField] private float collectInterval = 0.1f;
    private readonly List<GameObject> producedHandcuffs = new List<GameObject>(); // 생산된 수갑 관리 리스트
    private float nextCollectTime;

    private float nextReceiveTime;
    private float nextProcessTime;

    private void Update()
    {
        ProcessRawOresAutomatically();
    }

    /// <summary>
    /// 입구 구역(Input Zone)에서 플레이어의 광석을 수거합니다.
    /// </summary>
    public void ProcessOreInput(GameObject player)
    {
        if (Time.time < nextReceiveTime) return;
        if (player.TryGetComponent<StackManager>(out var playerStack) == false) return;

        GameObject ore = playerStack.PopItem();
        if (ore != null)
        {
            nextReceiveTime = Time.time + receiveCooldown;
            ReceiveOreToFloor(ore);
        }
    }

    public void ReceiveNpcOre(GameObject ore)
    {
        if (ore == null) return;
        ReceiveOreToFloor(ore);
    }

    /// <summary>
    /// 출구 구역(Output Zone)에서 플레이어에게 수갑을 전달합니다.
    /// </summary>
    public void ProcessHandcuffOutput(GameObject player)
    {
        if (Time.time < nextCollectTime || producedHandcuffs.Count == 0) return;
        if (player.TryGetComponent<StackManager>(out var playerStack) == false) return;

        nextCollectTime = Time.time + collectInterval;

        // LIFO: 가장 나중에 쌓인 수갑부터 수거
        int lastIndex = producedHandcuffs.Count - 1;
        GameObject handcuff = producedHandcuffs[lastIndex];
        producedHandcuffs.RemoveAt(lastIndex);

        // 1. 안착할 '로컬' 목표 위치를 먼저 가져옵니다.
        Vector3 localTargetPos = playerStack.GetNextHandcuffPosition();

        // 2. 점프 시작 전 부모를 플레이어 피벗으로 변경합니다.
        playerStack.PushHandcuff(handcuff);

        // 3. 부모가 바뀌었으므로 'DOLocalJump'를 사용하여 정확한 로컬 좌표로 날아갑니다.
        handcuff.transform.DOLocalJump(localTargetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
        // 회전 역시 피벗의 정면을 바라보도록 로컬 0으로 맞춥니다.
        handcuff.transform.DOLocalRotate(Vector3.zero, jumpDuration);
    }


    private void ReceiveOreToFloor(GameObject ore)
    {
        // 연출 중 물리 충돌 방지
        if (ore.TryGetComponent<Collider>(out var col)) col.enabled = false;
        if (ore.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        int index = rawOresOnFloor.Count;
        rawOresOnFloor.Add(ore);
        // 바닥에 도착하는 예상 시간(현재 시간 + 점프 시간) 기록
        rawOreArrivalTimes.Add(Time.time + jumpDuration);

        // 2열 쌓기 위치 계산
        Vector3 targetPos = GetRawOrePosition(index);

        // 플레이어 등에서 바닥 대기 구역으로 점프
        ore.transform.DOJump(targetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
        ore.transform.DORotateQuaternion(rawOreStackPivot.rotation, jumpDuration);
    }

    private Vector3 GetRawOrePosition(int index)
    {
        int row = index % 2;      // 0 또는 1 (앞/뒤)
        int layer = index / 2;    // 층수 (높이)

        Vector3 targetOffset = new Vector3(
            0f,
            layer * rawOreStackYOffset,
            row == 0 ? -rawOreStackZOffset / 2f : rawOreStackZOffset / 2f
        );

        return rawOreStackPivot.position + (rawOreStackPivot.rotation * targetOffset);
    }

    private void ProcessRawOresAutomatically()
    {
        // 가공 쿨다운 체크 및 바닥에 광석이 있는지 확인
        if (Time.time < nextProcessTime || rawOresOnFloor.Count == 0) return;

        int lastIndex = rawOresOnFloor.Count - 1;

        // 맨 위에 있는 광석이 바닥에 도착한 후 지정된 대기 시간을 채웠는지 확인
        if (Time.time < rawOreArrivalTimes[lastIndex] + absorbWaitTime) return;

        nextProcessTime = Time.time + processCooldown;

        // 가장 위에 있는 광석(리스트의 마지막) 꺼내기
        GameObject oreToProcess = rawOresOnFloor[lastIndex];
        rawOresOnFloor.RemoveAt(lastIndex);
        rawOreArrivalTimes.RemoveAt(lastIndex);

        // 바닥에서 제련기 입구(meltPoint)로 점프 후 가공 작업 시작
        Sequence seq = DOTween.Sequence();
        seq.Append(oreToProcess.transform.DOJump(meltPoint.position, jumpPower, 1, absorbMoveDuration).SetEase(Ease.OutQuad));
        seq.AppendCallback(() =>
        {
            Destroy(oreToProcess);
            SpawnAndProcessIngot();
        });
    }

    private void SpawnAndProcessIngot()
    {
        if (ingotPrefab == null) return;

        // 제련 완료 후 벨트 시작점에서 주괴 생성
        GameObject ingot = Instantiate(ingotPrefab, beltStartPoint.position, Quaternion.Euler(ingotSpawnRotation));

        Sequence seq = DOTween.Sequence();

        // 이동: beltStartPoint에서 pressPoint까지 컨베이어 벨트 이동
        seq.Append(ingot.transform.DOMove(pressPoint.position, moveDuration).SetEase(Ease.Linear));

        // 압착 및 변환: pressPoint 도착 시
        seq.AppendCallback(() =>
        {
            if (customCubePress != null)
            {
                Vector3 originalPressPos = customCubePress.position;
                Vector3 downPos = originalPressPos + Vector3.down * 0.5f;

                Sequence pressSeq = DOTween.Sequence();
                
                // 압착기 빠르게 내려찍기
                pressSeq.Append(customCubePress.DOMove(downPos, 0.1f).SetEase(Ease.InExpo));
                
                pressSeq.AppendCallback(() => 
                {
                    // 카메라 피드백
                    if (Camera.main != null)
                    {
                        Camera.main.transform.DOShakePosition(0.1f, 0.1f);
                    }
                    
                    Destroy(ingot);
                    
                    if (handcuffPrefab != null)
                    {
                        GameObject handcuff = Instantiate(handcuffPrefab, pressPoint.position, Quaternion.Euler(handcuffSpawnRotation));
                        MoveToOutput(handcuff);
                    }
                });
                
                // 원래 위치로 복귀
                pressSeq.Append(customCubePress.DOMove(originalPressPos, 0.1f).SetEase(Ease.OutExpo));
            }
            else
            {
                Destroy(ingot);
                if (handcuffPrefab != null)
                {
                    GameObject handcuff = Instantiate(handcuffPrefab, pressPoint.position, Quaternion.Euler(handcuffSpawnRotation));
                    MoveToOutput(handcuff);
                }
            }
        });
    }

    private void MoveToOutput(GameObject handcuff)
    {
        Sequence seq = DOTween.Sequence();

        // 출력 이동: pressPoint에서 beltEndPoint까지 컨베이어 벨트 이동
        seq.Append(handcuff.transform.DOMove(beltEndPoint.position, moveDuration).SetEase(Ease.Linear));

        // 적재: beltEndPoint 도착 후 stackPoint로 점프하며 1열 적재
        seq.AppendCallback(() =>
        {
            Vector3 targetOffset = new Vector3(0f, producedHandcuffs.Count * stackYOffset, 0f);
            Vector3 finalTargetPos = outputStackPoint.position + (outputStackPoint.rotation * targetOffset);

            handcuff.transform.DOJump(finalTargetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
            handcuff.transform.DORotateQuaternion(outputStackPoint.rotation, jumpDuration);
            
            // 리스트에 추가하여 추후 플레이어가 수거 가능하게 함
            producedHandcuffs.Add(handcuff);
        });
    }
}
