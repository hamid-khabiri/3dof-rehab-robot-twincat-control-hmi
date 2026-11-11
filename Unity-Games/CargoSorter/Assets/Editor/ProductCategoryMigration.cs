#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProductCategoryMigration
{
    private const string ScenePath = "Assets/Scenes/Gameplay.unity";
    private const string ProductRoot = "Assets/Data/Products";

    private sealed class ProductSpec
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly ProductCategory Category;
        public readonly string SpritePath;
        public readonly string LegacyDefinitionPath;

        public ProductSpec(string id, string displayName, ProductCategory category, string spritePath,
            string legacyDefinitionPath = null)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            SpritePath = spritePath;
            LegacyDefinitionPath = legacyDefinitionPath;
        }
    }

    private static readonly ProductSpec[] ActiveProducts =
    {
        Vegetable("carrot", "Carrot", "Product_Produce_Carrot.png", "Assets/Data/Products/Produce/Product_Produce_Carrot.asset"),
        Vegetable("broccoli", "Broccoli", "Product_Produce_Broccoli.png", "Assets/Data/Products/Produce/Product_Produce_Broccoli.asset"),
        Vegetable("corn", "Corn", "Corn.png"),
        Vegetable("cucumber", "Cucumber", "Cucumber.png"),
        Vegetable("garlic", "Garlic", "Garlic.png"),
        Vegetable("mushroom", "Mushroom", "Mushroom.png"),
        Vegetable("onion", "Onion", "Onion.png"),
        Vegetable("pepper", "Pepper", "Pepper.png"),
        Vegetable("potato", "Potato", "Potato.png"),
        Vegetable("tomato", "Tomato", "Tomato.png"),

        Can("can", "Can", "Product_Packaged_Can.png", "Assets/Data/Products/Packaged/Product_Packaged_Can.asset"),
        Can("chicken_can", "Chicken Can", "Chicken_Can.png"),
        Can("coconut_can", "Coconut Can", "Coconut_Can.png"),
        Can("green_beans_can", "Green Beans Can", "GreenBeans_Can.png"),
        Can("mayonnaise_can", "Mayonnaise Can", "Mayonnaise_Can.png"),
        Can("mushroom_can", "Mushroom Can", "Mushroom_Can.png"),
        Can("pumpkin_can", "Pumpkin Can", "Pumpkin_Can.png"),
        Can("vegetable_can", "Vegetable Can", "Vegetable_Can.png"),

        FastFood("donut", "Donut", "Donut.png"),
        FastFood("french_fries", "French Fries", "French_Fries.png"),
        FastFood("fried_chicken", "Fried Chicken", "FriedChicken.png"),
        FastFood("fried_onion", "Fried Onion", "FriedOnion.png"),
        FastFood("hamburger", "Hamburger", "Hamburguer.png"),
        FastFood("hot_dog", "Hot Dog", "HotDog.png"),
        FastFood("milkshake", "Milkshake", "Milkshake.png"),
        FastFood("pizza", "Pizza", "Pizza.png"),
        FastFood("soda", "Soda", "Soda.png"),
        FastFood("taco", "Taco", "Taco.png")
    };

    [MenuItem("Tools/Cargo Sorter/Migrate Product Categories")]
    public static void Migrate()
    {
        EnsureFolder(ProductRoot);
        EnsureFolder($"{ProductRoot}/Vegetables");
        EnsureFolder($"{ProductRoot}/Cans");
        EnsureFolder($"{ProductRoot}/FastFood");

        var activeDefinitions = new List<ProductDefinition>(ActiveProducts.Length);
        foreach (ProductSpec spec in ActiveProducts)
            activeDefinitions.Add(CreateOrUpdateDefinition(spec));

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TrialManager trialManager = UnityEngine.Object.FindAnyObjectByType<TrialManager>(FindObjectsInactive.Include);
        if (trialManager == null)
            throw new InvalidOperationException($"No TrialManager was found in {ScenePath}.");

        SerializedObject trials = new SerializedObject(trialManager);
        SerializedProperty pool = trials.FindProperty("productDefinitions");
        pool.arraySize = activeDefinitions.Count;
        for (int index = 0; index < activeDefinitions.Count; index++)
            pool.GetArrayElementAtIndex(index).objectReferenceValue = activeDefinitions[index];
        trials.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trialManager);

        ConfigureBinTargets();
        ValidateActivePool(activeDefinitions);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Product category migration complete: {activeDefinitions.Count} active products; {ScenePath} saved.");
    }

    private static ProductDefinition CreateOrUpdateDefinition(ProductSpec spec)
    {
        string folder = $"{ProductRoot}/{spec.Category}";
        string definitionPath = $"{folder}/Product_{spec.Id}.asset";
        ProductDefinition definition = AssetDatabase.LoadAssetAtPath<ProductDefinition>(definitionPath);

        if (definition == null && !string.IsNullOrEmpty(spec.LegacyDefinitionPath))
        {
            ProductDefinition legacy = AssetDatabase.LoadAssetAtPath<ProductDefinition>(spec.LegacyDefinitionPath);
            if (legacy != null)
            {
                string moveError = AssetDatabase.MoveAsset(spec.LegacyDefinitionPath, definitionPath);
                if (!string.IsNullOrEmpty(moveError))
                    throw new InvalidOperationException($"Could not move {spec.LegacyDefinitionPath}: {moveError}");
                definition = AssetDatabase.LoadAssetAtPath<ProductDefinition>(definitionPath);
            }
        }

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ProductDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.SpritePath);
        if (sprite == null)
            throw new InvalidOperationException($"Required product sprite is missing or is not imported as a Sprite: {spec.SpritePath}");

        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("productId").stringValue = spec.Id;
        serialized.FindProperty("displayName").stringValue = spec.DisplayName;
        serialized.FindProperty("category").enumValueIndex = (int)spec.Category;
        serialized.FindProperty("sprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        definition.name = $"Product_{spec.Id}";
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void ConfigureBinTargets()
    {
        BinTarget[] targets = UnityEngine.Object.FindObjectsByType<BinTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (targets.Length != 3)
            throw new InvalidOperationException($"Expected exactly three BinTargets, but found {targets.Length}.");

        var categories = new HashSet<ProductCategory>();
        foreach (BinTarget target in targets)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty category = serialized.FindProperty("category");
            int preservedCategoryValue = category.enumValueIndex;
            if (preservedCategoryValue < 0 || preservedCategoryValue > 2)
                throw new InvalidOperationException($"BinTarget {target.name} has unsupported category value {preservedCategoryValue}.");
            category.enumValueIndex = preservedCategoryValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            categories.Add((ProductCategory)preservedCategoryValue);
            EditorUtility.SetDirty(target);
        }

        if (categories.Count != 3)
            throw new InvalidOperationException("The three BinTargets do not have three unique category values.");
    }

    private static void ValidateActivePool(IReadOnlyList<ProductDefinition> definitions)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var categories = new HashSet<ProductCategory>();
        foreach (ProductDefinition definition in definitions)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            if (definition == null || string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("The active pool contains a missing ProductDefinition asset.");
            if (string.IsNullOrWhiteSpace(definition.ProductId) || !ids.Add(definition.ProductId))
                throw new InvalidOperationException($"The active pool has an empty or duplicate product ID at {path}.");
            if (string.IsNullOrWhiteSpace(definition.DisplayName) || definition.Sprite == null)
                throw new InvalidOperationException($"The active product is incomplete: {path}");
            if (definition.Category < ProductCategory.Vegetables || definition.Category > ProductCategory.FastFood)
                throw new InvalidOperationException($"The active product has an invalid category: {path}");
            categories.Add(definition.Category);
        }

        if (categories.Count != 3)
            throw new InvalidOperationException("Every active category must contain at least one product.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException($"Invalid folder path: {path}");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static ProductSpec Vegetable(string id, string name, string sprite, string legacy = null) =>
        new ProductSpec(id, name, ProductCategory.Vegetables, $"Assets/Art/Products/Vegetables/{sprite}", legacy);

    private static ProductSpec Can(string id, string name, string sprite, string legacy = null) =>
        new ProductSpec(id, name, ProductCategory.Cans, $"Assets/Art/Products/Cans/{sprite}", legacy);

    private static ProductSpec FastFood(string id, string name, string sprite) =>
        new ProductSpec(id, name, ProductCategory.FastFood, $"Assets/Art/Products/FastFood/{sprite}");
}
#endif
