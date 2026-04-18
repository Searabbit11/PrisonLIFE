using System.Collections.Generic;
using UnityEngine;

public sealed class ConverterZone : MonoBehaviour
{
    public enum ZoneType { Input, Output }

    [SerializeField] private Converter parentConverter;
    [SerializeField] private ZoneType type;
    [Header("Zone Visual")]
    [SerializeField] private Renderer zoneRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor = Color.green;

    private readonly HashSet<int> playerColliderIds = new();
    private MaterialPropertyBlock zonePropertyBlock;
    private int colorPropertyId = -1;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (zoneRenderer == null)
        {
            zoneRenderer = GetComponent<Renderer>();
        }

        if (zoneRenderer == null) return;

        zonePropertyBlock = new MaterialPropertyBlock();
        CacheColorProperty();
        SetZoneActiveVisual(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (playerColliderIds.Add(other.GetInstanceID()) && playerColliderIds.Count == 1)
        {
            SetZoneActiveVisual(true);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (parentConverter == null) return;

        if (other.CompareTag("Player"))
        {
            if (type == ZoneType.Input)
            {
                parentConverter.ProcessOreInput(other.gameObject);
            }
            else
            {
                parentConverter.ProcessHandcuffOutput(other.gameObject);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (playerColliderIds.Remove(other.GetInstanceID()) && playerColliderIds.Count == 0)
        {
            SetZoneActiveVisual(false);
        }
    }

    private void SetZoneActiveVisual(bool isActive)
    {
        if (zoneRenderer == null || zonePropertyBlock == null || colorPropertyId == -1) return;

        zoneRenderer.GetPropertyBlock(zonePropertyBlock);
        zonePropertyBlock.SetColor(colorPropertyId, isActive ? activeColor : normalColor);
        zoneRenderer.SetPropertyBlock(zonePropertyBlock);
    }

    private void CacheColorProperty()
    {
        var sharedMat = zoneRenderer.sharedMaterial;
        if (sharedMat == null) return;

        if (sharedMat.HasProperty(BaseColorId))
        {
            colorPropertyId = BaseColorId;
        }
        else if (sharedMat.HasProperty(ColorId))
        {
            colorPropertyId = ColorId;
        }

        if (colorPropertyId != -1)
        {
            normalColor = sharedMat.GetColor(colorPropertyId);
        }
    }
}
