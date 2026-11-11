using UnityEngine;

[CreateAssetMenu(fileName = "ProductDefinition", menuName = "Cargo Sorter/Product Definition")]
public sealed class ProductDefinition : ScriptableObject
{
    [SerializeField] private string productId;
    [SerializeField] private string displayName;
    [SerializeField] private ProductCategory category;
    [SerializeField] private Sprite sprite;

    public string ProductId => productId;
    public string DisplayName => displayName;
    public ProductCategory Category => category;
    public Sprite Sprite => sprite;
}
