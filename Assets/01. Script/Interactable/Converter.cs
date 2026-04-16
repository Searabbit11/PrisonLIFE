using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class Converter : MonoBehaviour
{
    [Header("Process Points")]
    [SerializeField] private Transform rawOreStackPivot;
    [SerializeField] private Transform meltPoint;
    [SerializeField] private Transform beltStartPoint;
    [SerializeField] private Transform pressPoint;
    [SerializeField] private Transform beltEndPoint;
    [SerializeField] private Transform outputStackPoint;

    [Header("Prefabs")]
    [SerializeField] private GameObject ingotPrefab;
    [SerializeField] private GameObject handcuffPrefab;

    [Header("Spawn Rotations")]
    [SerializeField] private Vector3 ingotSpawnRotation = Vector3.zero;
    [SerializeField] private Vector3 handcuffSpawnRotation = Vector3.zero;

    [Header("Visuals")]
    [SerializeField] private Transform customCubePress;

    [Header("Settings")]
    [SerializeField] private float receiveCooldown = 0.1f;
    [SerializeField] private float processCooldown = 1.0f;
    [SerializeField] private float absorbMoveDuration = 0.4f;
    [SerializeField] private float jumpPower = 1.5f;
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float moveDuration = 1.0f;

    [Header("Raw Ore Stacking (Input)")]
    [SerializeField] private float rawOreStackZOffset = 0.4f;
    [SerializeField] private float rawOreStackYOffset = 0.15f;
    private readonly List<GameObject> rawOresOnFloor = new List<GameObject>();
    private readonly HashSet<GameObject> rawOresInTransit = new HashSet<GameObject>();

    [Header("Output Stacking")]
    [SerializeField] private float stackYOffset = 0.15f;
    [SerializeField] private float collectInterval = 0.1f;
    private readonly List<GameObject> producedHandcuffs = new List<GameObject>();

    private float nextCollectTime;
    private float nextReceiveTime;
    private float nextProcessTime;

    private void Update()
    {
        ProcessRawOresAutomatically();
    }

    public void ProcessOreInput(GameObject player)
    {
        if (Time.time < nextReceiveTime) return;
        if (player.TryGetComponent<StackManager>(out var playerStack) == false) return;

        GameObject ore = playerStack.PopItem();
        if (ore == null) return;

        nextReceiveTime = Time.time + receiveCooldown;
        ReceiveOreToFloor(ore);
    }

    public void ReceiveNpcOre(GameObject ore)
    {
        if (ore == null) return;
        ReceiveOreToFloor(ore);
    }

    public void ProcessHandcuffOutput(GameObject player)
    {
        if (Time.time < nextCollectTime || producedHandcuffs.Count == 0) return;
        if (player.TryGetComponent<StackManager>(out var playerStack) == false) return;

        nextCollectTime = Time.time + collectInterval;

        int lastIndex = producedHandcuffs.Count - 1;
        GameObject handcuff = producedHandcuffs[lastIndex];
        producedHandcuffs.RemoveAt(lastIndex);

        Vector3 localTargetPos = playerStack.GetNextHandcuffPosition();
        playerStack.PushHandcuff(handcuff);

        handcuff.transform.DOLocalJump(localTargetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
        handcuff.transform.DOLocalRotate(Vector3.zero, jumpDuration);
    }

    private void ReceiveOreToFloor(GameObject ore)
    {
        ore.transform.DOKill();
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
        rawOresInTransit.Add(ore);

        Vector3 targetPos = GetRawOrePosition(index);
        Sequence receiveSeq = DOTween.Sequence();
        receiveSeq.Append(ore.transform.DOJump(targetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad));
        receiveSeq.Join(ore.transform.DORotateQuaternion(rawOreStackPivot.rotation, jumpDuration));
        receiveSeq.OnComplete(() =>
        {
            if (ore == null) return;
            rawOresInTransit.Remove(ore);
        });
    }

    private Vector3 GetRawOrePosition(int index)
    {
        int row = index % 2;
        int layer = index / 2;

        Vector3 targetOffset = new Vector3(
            0f,
            layer * rawOreStackYOffset,
            row == 0 ? -rawOreStackZOffset * 0.5f : rawOreStackZOffset * 0.5f
        );

        return rawOreStackPivot.position + (rawOreStackPivot.rotation * targetOffset);
    }

    private void ProcessRawOresAutomatically()
    {
        if (Time.time < nextProcessTime || rawOresOnFloor.Count == 0) return;

        int lastIndex = rawOresOnFloor.Count - 1;
        GameObject oreToProcess = rawOresOnFloor[lastIndex];
        if (oreToProcess == null)
        {
            rawOresOnFloor.RemoveAt(lastIndex);
            return;
        }

        if (rawOresInTransit.Contains(oreToProcess))
        {
            return;
        }

        rawOresOnFloor.RemoveAt(lastIndex);
        rawOresInTransit.Remove(oreToProcess);

        nextProcessTime = Time.time + processCooldown;

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

        GameObject ingot = Instantiate(ingotPrefab, beltStartPoint.position, Quaternion.Euler(ingotSpawnRotation));

        Sequence seq = DOTween.Sequence();
        seq.Append(ingot.transform.DOMove(pressPoint.position, moveDuration).SetEase(Ease.Linear));
        seq.AppendCallback(() =>
        {
            if (customCubePress != null)
            {
                Vector3 originalPressPos = customCubePress.position;
                Vector3 downPos = originalPressPos + Vector3.down * 0.5f;

                Sequence pressSeq = DOTween.Sequence();
                pressSeq.Append(customCubePress.DOMove(downPos, 0.1f).SetEase(Ease.InExpo));
                pressSeq.AppendCallback(() =>
                {
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
                pressSeq.Append(customCubePress.DOMove(originalPressPos, 0.1f).SetEase(Ease.OutExpo));
                return;
            }

            Destroy(ingot);
            if (handcuffPrefab != null)
            {
                GameObject handcuff = Instantiate(handcuffPrefab, pressPoint.position, Quaternion.Euler(handcuffSpawnRotation));
                MoveToOutput(handcuff);
            }
        });
    }

    private void MoveToOutput(GameObject handcuff)
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(handcuff.transform.DOMove(beltEndPoint.position, moveDuration).SetEase(Ease.Linear));
        seq.AppendCallback(() =>
        {
            Vector3 targetOffset = new Vector3(0f, producedHandcuffs.Count * stackYOffset, 0f);
            Vector3 finalTargetPos = outputStackPoint.position + (outputStackPoint.rotation * targetOffset);

            handcuff.transform.DOJump(finalTargetPos, jumpPower, 1, jumpDuration).SetEase(Ease.OutQuad);
            handcuff.transform.DORotateQuaternion(outputStackPoint.rotation, jumpDuration);

            producedHandcuffs.Add(handcuff);
        });
    }
}
