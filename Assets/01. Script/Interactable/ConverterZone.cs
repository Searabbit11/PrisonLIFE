using UnityEngine;

public sealed class ConverterZone : MonoBehaviour
{
    public enum ZoneType { Input, Output }

    [SerializeField] private Converter parentConverter;
    [SerializeField] private ZoneType type;

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
}
