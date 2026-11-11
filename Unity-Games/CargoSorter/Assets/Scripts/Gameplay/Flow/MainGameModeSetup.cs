using System.Collections.Generic;
using UnityEngine;

public sealed class MainGameModeSetup : MonoBehaviour
{
    [SerializeField] private MainGameMode mainGameMode;
    [SerializeField] private GameObject setupRoot;
    [SerializeField] private TrolleyController trolleyController;
    [SerializeField] private List<ProductDefinition> productDefinitions = new List<ProductDefinition>();
    [SerializeField] private List<BinTarget> binTargets = new List<BinTarget>();

    public MainGameMode MainGameMode => mainGameMode;
    public GameObject SetupRoot => setupRoot;
    public TrolleyController TrolleyController => trolleyController;
    public IReadOnlyList<ProductDefinition> ProductDefinitions => productDefinitions;
    public IReadOnlyList<BinTarget> BinTargets => binTargets;
}
