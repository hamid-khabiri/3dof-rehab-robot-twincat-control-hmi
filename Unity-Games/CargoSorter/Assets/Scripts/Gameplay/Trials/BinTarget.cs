using UnityEngine;

public sealed class BinTarget : MonoBehaviour
{
    [SerializeField] private ProductCategory category;
    [SerializeField] private Transform alignmentPoint;

    public ProductCategory Category => category;
    public Transform AlignmentPoint => alignmentPoint;
}
