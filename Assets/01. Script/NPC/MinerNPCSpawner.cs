using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MinerNPCSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private MinerNPC minerPrefab;
    [SerializeField] private Transform[] spawnPoints = new Transform[3];
    [SerializeField] private Converter targetConverter;

    [Header("Mining Targets")]
    [SerializeField] private Transform resourceRoot;
    [SerializeField] private ResourceNode[] resourceNodes;
    [SerializeField] private int activeColumns = 3;
    [SerializeField] private int activeRows = 16;
    [SerializeField] private float gridTolerance = 0.2f;
    [SerializeField] private bool startFromMaxX = true;

    private readonly List<MinerNPC> spawnedMiners = new List<MinerNPC>();

    public void SpawnMiners()
    {
        if (minerPrefab == null)
        {
            Debug.LogWarning("[MinerNPCSpawner] Miner prefab is missing.", this);
            return;
        }

        ResourceNode[] nodes = ResolveResourceNodes();
        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogWarning("[MinerNPCSpawner] Resource nodes are missing.", this);
            return;
        }

        if (spawnedMiners.Count > 0) return;

        int spawnCount = Mathf.Max(spawnPoints != null ? spawnPoints.Length : 0, 3);
        List<ResourceNode>[] lanes = BuildLanes(nodes, spawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform spawnPoint = GetSpawnPoint(i);
            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.right * i;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            MinerNPC miner = Instantiate(minerPrefab, position, rotation);
            ResourceNode[] lane = lanes[Mathf.Min(i, lanes.Length - 1)].ToArray();
            miner.Initialize(lane, targetConverter);
            spawnedMiners.Add(miner);
        }
    }

    private Transform GetSpawnPoint(int index)
    {
        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length) return null;
        return spawnPoints[index];
    }

    private ResourceNode[] ResolveResourceNodes()
    {
        if (resourceRoot != null)
        {
            ResourceNode[] collected = resourceRoot.GetComponentsInChildren<ResourceNode>(true);
            if (collected != null && collected.Length > 0)
            {
                resourceNodes = collected;
                return collected;
            }
        }

        return resourceNodes;
    }

    private List<ResourceNode>[] BuildLanes(ResourceNode[] nodes, int laneCount)
    {
        List<ResourceNode>[] lanes = new List<ResourceNode>[laneCount];
        for (int i = 0; i < laneCount; i++) lanes[i] = new List<ResourceNode>();

        if (nodes == null || nodes.Length == 0) return lanes;

        float[] xs = new float[nodes.Length];
        float[] zs = new float[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            Vector3 p = nodes[i].transform.position;
            xs[i] = p.x;
            zs[i] = p.z;
        }

        float[] uniqueXs = GetUniqueAxis(xs, gridTolerance);
        float[] uniqueZs = GetUniqueAxis(zs, gridTolerance);

        int selectedColumnCount = Mathf.Clamp(activeColumns, 1, Mathf.Min(uniqueXs.Length, laneCount));
        int selectedRowCount = Mathf.Clamp(activeRows, 1, uniqueZs.Length);

        float[] selectedXs = SelectColumns(uniqueXs, selectedColumnCount, startFromMaxX);
        float[] selectedZs = SelectRows(uniqueZs, selectedRowCount);

        for (int laneIndex = 0; laneIndex < selectedXs.Length; laneIndex++)
        {
            float x = selectedXs[laneIndex];
            for (int zIndex = 0; zIndex < selectedZs.Length; zIndex++)
            {
                ResourceNode node = FindNode(nodes, x, selectedZs[zIndex]);
                if (node != null) lanes[laneIndex].Add(node);
            }
        }

        return lanes;
    }

    private ResourceNode FindNode(ResourceNode[] nodes, float targetX, float targetZ)
    {
        ResourceNode best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < nodes.Length; i++)
        {
            ResourceNode node = nodes[i];
            if (node == null) continue;

            Vector3 p = node.transform.position;
            if (Mathf.Abs(p.x - targetX) > gridTolerance) continue;
            if (Mathf.Abs(p.z - targetZ) > gridTolerance) continue;

            float d = Mathf.Abs(p.x - targetX) + Mathf.Abs(p.z - targetZ);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = node;
            }
        }

        return best;
    }

    private static float[] GetUniqueAxis(float[] values, float tolerance)
    {
        List<float> unique = new List<float>();

        Array.Sort(values);
        for (int i = 0; i < values.Length; i++)
        {
            float v = values[i];
            bool exists = false;
            for (int j = 0; j < unique.Count; j++)
            {
                if (Mathf.Abs(unique[j] - v) <= tolerance)
                {
                    exists = true;
                    break;
                }
            }

            if (exists == false) unique.Add(v);
        }

        return unique.ToArray();
    }

    private static float[] SelectColumns(float[] sortedXs, int count, bool fromMax)
    {
        float[] selected = new float[count];
        if (fromMax)
        {
            for (int i = 0; i < count; i++)
            {
                selected[i] = sortedXs[sortedXs.Length - 1 - i];
            }

            return selected;
        }

        for (int i = 0; i < count; i++)
        {
            selected[i] = sortedXs[i];
        }

        return selected;
    }

    private static float[] SelectRows(float[] sortedZs, int count)
    {
        float[] selected = new float[count];
        for (int i = 0; i < count; i++)
        {
            selected[i] = sortedZs[i];
        }

        return selected;
    }
}
