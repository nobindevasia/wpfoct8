# Interface Architecture Guide: Designing for Easy Extensibility

## Overview

This guide explains how the D2G.Iris.ML project uses interfaces and design patterns to create a highly extensible machine learning framework. The architecture leverages three key design patterns: **Strategy Pattern**, **Factory Pattern**, and **Template Method Pattern**.

## Table of Contents

1. [Architecture Principles](#architecture-principles)
2. [Core Interface Patterns](#core-interface-patterns)
3. [Factory Pattern Implementation](#factory-pattern-implementation)
4. [Strategy Pattern Implementation](#strategy-pattern-implementation)
5. [Template Method Pattern](#template-method-pattern)
6. [Adding New Components](#adding-new-components)
7. [Benefits of This Architecture](#benefits-of-this-architecture)

---

## Architecture Principles

### 1. Separation of Concerns
Each interface has a single, well-defined responsibility:
- `IModelTrainer`: Training ML models
- `IDataBalancer`: Balancing datasets
- `IFeatureSelector`: Selecting features
- `IDataLoader`: Loading data from sources
- `IDataProcessor`: Processing raw data
- `IConfigManager`: Managing configuration

### 2. Dependency Inversion Principle
High-level modules depend on abstractions (interfaces), not concrete implementations. This allows easy swapping of implementations without changing consuming code.

### 3. Open/Closed Principle
The system is **open for extension** (add new implementations) but **closed for modification** (existing code doesn't change).

---

## Core Interface Patterns

### 1. Service Interfaces (Strategy Pattern)

These interfaces define the contract for specific operations:

#### IModelTrainer
**Location**: `D2G.Iris.ML/Core/Interfaces/IModelTrainer.cs:7`

```csharp
public interface IModelTrainer
{
    Task<TrainingResult> TrainModel(
        MLContext mlContext,
        IDataView dataView,
        string[] featureNames,
        ModelConfig config,
        ProcessedData processedData);
}
```

**Purpose**: Abstracts different model training strategies (Binary Classification, Multi-Class Classification, Regression)

**Current Implementations**:
- `BinaryClassificationTrainer`
- `MultiClassClassificationTrainer`
- `RegressionTrainer`

**Base Class**: `BaseModelTrainer` at `D2G.Iris.ML/Training/BaseModelTrainer.cs:15`

---

#### IDataBalancer
**Location**: `D2G.Iris.ML/Core/Interfaces/IDataBalancer.cs:6`

```csharp
public interface IDataBalancer
{
    Task<IDataView> BalanceDataset(
        MLContext mlContext,
        IDataView data,
        string[] featureNames,
        DataBalancingConfig config,
        string targetField);
}
```

**Purpose**: Abstracts different data balancing strategies

**Current Implementations**:
- `NoDataBalancer` - No balancing applied
- `SmoteDataBalancer` - SMOTE (Synthetic Minority Over-sampling Technique) at `D2G.Iris.ML/DataBalancing/SmoteDataBalancer.cs:14`

**Enum**: `DataBalanceMethod` at `D2G.Iris.ML/Core/Enums/DataBalanceMethod.cs:3`
```csharp
public enum DataBalanceMethod
{
    None,
    SMOTE
}
```

---

#### IFeatureSelector
**Location**: `D2G.Iris.ML/Core/Interfaces/IFeatureSelector.cs:7`

```csharp
public interface IFeatureSelector
{
    Task<(IDataView transformedData, string[] selectedFeatures, string report)> SelectFeatures(
        MLContext mlContext,
        IDataView data,
        string[] candidateFeatures,
        ModelType modelType,
        string targetField,
        FeatureEngineeringConfig config);
}
```

**Purpose**: Abstracts different feature selection strategies

**Current Implementations**:
- `NoFeatureSelector` - No feature selection
- `CorrelationFeatureSelector` - Correlation-based selection
- `PCAFeatureSelector` - Principal Component Analysis

**Base Class**: `BaseFeatureSelector` at `D2G.Iris.ML/FeatureEngineering/BaseFeatureSelector.cs:11`

**Enum**: `FeatureSelectionMethod` at `D2G.Iris.ML/Core/Enums/FeatureSelectionMethod.cs:3`
```csharp
public enum FeatureSelectionMethod
{
    None,
    Correlation,
    PCA
}
```

---

### 2. Factory Interfaces (Factory Pattern)

These interfaces define contracts for creating concrete strategy implementations:

#### IModelTrainerFactory
**Location**: `D2G.Iris.ML/Core/Interfaces/IModelTrainerFactory.cs:5`

```csharp
public interface IModelTrainerFactory
{
    IModelTrainer CreateTrainer(ModelType modelType);
}
```

**Purpose**: Creates appropriate trainer based on model type

**Implementation**: `ModelTrainerFactory` at `D2G.Iris.ML/Training/ModelTrainerFactory.cs:8`

---

#### IDataBalancerFactory
**Location**: `D2G.Iris.ML/Core/Interfaces/IDataBalancerFactory.cs:5`

```csharp
public interface IDataBalancerFactory
{
    IDataBalancer CreateBalancer(DataBalanceMethod method);
}
```

**Purpose**: Creates appropriate data balancer based on method

**Implementation**: `DataBalancerFactory` at `D2G.Iris.ML/DataBalancing/DataBalancerFactory.cs:9`

---

#### IFeatureSelectorFactory
**Location**: `D2G.Iris.ML/Core/Interfaces/IFeatureSelectorFactory.cs:5`

```csharp
public interface IFeatureSelectorFactory
{
    IFeatureSelector CreateSelector(FeatureSelectionMethod method);
}
```

**Purpose**: Creates appropriate feature selector based on method

**Implementation**: `FeatureSelectorFactory` at `D2G.Iris.ML/FeatureEngineering/FeatureSelectorFactory.cs:8`

---

#### ITrainerFactory
**Location**: `D2G.Iris.ML/Core/Interfaces/ITrainerFactory.cs:7`

```csharp
public interface ITrainerFactory
{
    IEstimator<ITransformer> GetTrainer(ModelType modelType, TrainingParameters parameters);
}
```

**Purpose**: Creates ML.NET trainer estimators based on model type and parameters

---

### 3. Utility Interfaces

#### IDataLoader
**Location**: `D2G.Iris.ML/Core/Interfaces/IDataLoader.cs:11`

```csharp
public interface IDataLoader
{
    IDataView LoadDataFromSql(
        string sqlConnectionString,
        string tableName,
        IEnumerable<string> featureColumns,
        ModelType modelType,
        string targetColumn,
        string whereSyntax = "");
}
```

**Purpose**: Abstracts data loading from different sources

---

#### IDataProcessor
**Location**: `D2G.Iris.ML/Core/Interfaces/IDataProcessor.cs:6`

```csharp
public interface IDataProcessor
{
    Task<ProcessedData> ProcessData(
        MLContext mlContext,
        IDataView rawData,
        string[] enabledFields,
        ModelConfig config);
}
```

**Purpose**: Abstracts data processing pipeline

---

#### IConfigManager
**Location**: `D2G.Iris.ML/Core/Interfaces/IConfigManager.cs:10`

```csharp
public interface IConfigManager
{
    ModelConfig LoadConfiguration(string configPath);
    void ValidateConfiguration(ModelConfig config);
}
```

**Purpose**: Abstracts configuration management

---

## Factory Pattern Implementation

### Pattern Structure

The factory pattern is implemented in three layers:

1. **Enum**: Defines available options
2. **Factory Interface**: Defines creation contract
3. **Concrete Factory**: Implements creation logic

### Example: DataBalancerFactory

**Step 1: Define Enum**
```csharp
// D2G.Iris.ML/Core/Enums/DataBalanceMethod.cs
public enum DataBalanceMethod
{
    None,
    SMOTE
}
```

**Step 2: Define Strategy Interface**
```csharp
// D2G.Iris.ML/Core/Interfaces/IDataBalancer.cs
public interface IDataBalancer
{
    Task<IDataView> BalanceDataset(...);
}
```

**Step 3: Define Factory Interface**
```csharp
// D2G.Iris.ML/Core/Interfaces/IDataBalancerFactory.cs
public interface IDataBalancerFactory
{
    IDataBalancer CreateBalancer(DataBalanceMethod method);
}
```

**Step 4: Implement Concrete Factory**
```csharp
// D2G.Iris.ML/DataBalancing/DataBalancerFactory.cs
public class DataBalancerFactory : IDataBalancerFactory
{
    public IDataBalancer CreateBalancer(DataBalanceMethod method)
    {
        return method switch
        {
            DataBalanceMethod.None => new NoDataBalancer(),
            DataBalanceMethod.SMOTE => new SmoteDataBalancer(),
            _ => throw new ArgumentException($"Unsupported data balance method: {method}")
        };
    }
}
```

### Why This Pattern Works

1. **Single Point of Change**: Only the factory needs updating when adding new implementations
2. **Type Safety**: Enums provide compile-time checking
3. **Encapsulation**: Client code doesn't need to know about concrete classes
4. **Testability**: Easy to mock factories for unit tests

---

## Strategy Pattern Implementation

### Pattern Structure

The strategy pattern separates the algorithm interface from its implementations:

1. **Strategy Interface**: Defines the contract
2. **Concrete Strategies**: Implement specific algorithms
3. **Base Class** (optional): Provides shared functionality

### Example: Feature Selection

**Strategy Interface**
```csharp
public interface IFeatureSelector
{
    Task<(IDataView transformedData, string[] selectedFeatures, string report)> SelectFeatures(...);
}
```

**Base Class** (Template Method Pattern)
```csharp
// D2G.Iris.ML/FeatureEngineering/BaseFeatureSelector.cs
public abstract class BaseFeatureSelector : IFeatureSelector
{
    protected readonly MLContext _mlContext;
    protected readonly StringBuilder _report;

    // Template method pattern - common functionality
    protected IDataView CreateFeaturesColumn(IDataView data, string[] featureNames) { ... }
    protected void InitializeReport(string methodName) { ... }
    protected void AddFeatureSelectionSummary(...) { ... }

    // Strategy method - must be implemented by concrete classes
    public abstract Task<(IDataView, string[], string)> SelectFeatures(...);
}
```

**Concrete Strategy**
```csharp
public class CorrelationFeatureSelector : BaseFeatureSelector
{
    public override async Task<(IDataView, string[], string)> SelectFeatures(...)
    {
        // Specific correlation-based implementation
        InitializeReport("Correlation");
        // ... algorithm implementation
        AddFeatureSelectionSummary(...);
        return (transformedData, selectedFeatures, _report.ToString());
    }
}
```

### Benefits

1. **Swappable Algorithms**: Change feature selection method without changing client code
2. **Code Reuse**: Base class provides common utilities
3. **Consistency**: All implementations follow the same contract
4. **Isolation**: Algorithm changes don't affect clients

---

## Template Method Pattern

### Pattern in BaseModelTrainer

**Location**: `D2G.Iris.ML/Training/BaseModelTrainer.cs:15`

The `BaseModelTrainer` abstract class demonstrates the Template Method pattern:

```csharp
public abstract class BaseModelTrainer : IModelTrainer
{
    // Abstract method - subclasses must implement
    public abstract Task<TrainingResult> TrainModel(...);

    // Template methods - reusable utilities
    protected DataSplit SplitTrainTestData(MLContext mlContext, IDataView dataView, double testFraction) { ... }

    protected IEstimator<ITransformer> GetBasePipeline(MLContext mlContext) { ... }

    protected async Task<ITransformer> TrainModelAsync(IEstimator<ITransformer> pipeline, IDataView trainData) { ... }

    protected BinaryClassificationMetrics EvaluateBinaryClassification(...) { ... }
    protected MulticlassClassificationMetrics EvaluateMultiClassClassification(...) { ... }
    protected RegressionMetrics EvaluateRegression(...) { ... }

    protected string SaveModel(...) { ... }

    // Utility methods
    protected void PrintBinaryClassificationMetrics(...) { ... }
    protected string CleanTrainerName(string trainerName) { ... }
    protected string SanitizeFileName(string name, string defaultName = "Model") { ... }
}
```

### Benefits

1. **Code Reuse**: Common operations (splitting data, saving models, printing metrics) are shared
2. **Consistency**: All trainers use the same utility methods
3. **Maintainability**: Bug fixes in base class benefit all implementations
4. **Flexibility**: Subclasses can override specific behaviors while reusing others

---

## Adding New Components

### Example 1: Adding a New Data Balancing Method

Let's add an "Undersampling" method:

**Step 1: Update Enum**
```csharp
// D2G.Iris.ML/Core/Enums/DataBalanceMethod.cs
public enum DataBalanceMethod
{
    None,
    SMOTE,
    Undersampling  // NEW
}
```

**Step 2: Create Implementation**
```csharp
// D2G.Iris.ML/DataBalancing/UndersamplingBalancer.cs
using D2G.Iris.ML.Core.Interfaces;
using D2G.Iris.ML.Core.Models;
using Microsoft.ML;

namespace D2G.Iris.ML.DataBalancing
{
    public class UndersamplingBalancer : IDataBalancer
    {
        public async Task<IDataView> BalanceDataset(
            MLContext mlContext,
            IDataView data,
            string[] featureNames,
            DataBalancingConfig config,
            string targetField)
        {
            // Your undersampling implementation here
            // ...
            return await Task.FromResult(data);
        }
    }
}
```

**Step 3: Update Factory**
```csharp
// D2G.Iris.ML/DataBalancing/DataBalancerFactory.cs
public class DataBalancerFactory : IDataBalancerFactory
{
    public IDataBalancer CreateBalancer(DataBalanceMethod method)
    {
        return method switch
        {
            DataBalanceMethod.None => new NoDataBalancer(),
            DataBalanceMethod.SMOTE => new SmoteDataBalancer(),
            DataBalanceMethod.Undersampling => new UndersamplingBalancer(),  // NEW
            _ => throw new ArgumentException($"Unsupported data balance method: {method}")
        };
    }
}
```

**That's it!** No other code needs to change. The client code continues to work:

```csharp
var factory = new DataBalancerFactory();
var balancer = factory.CreateBalancer(DataBalanceMethod.Undersampling);
var balanced = await balancer.BalanceDataset(...);
```

---

### Example 2: Adding a New Feature Selection Method

Let's add "Mutual Information" feature selection:

**Step 1: Update Enum**
```csharp
// D2G.Iris.ML/Core/Enums/FeatureSelectionMethod.cs
public enum FeatureSelectionMethod
{
    None,
    Correlation,
    PCA,
    MutualInformation  // NEW
}
```

**Step 2: Create Implementation**
```csharp
// D2G.Iris.ML/FeatureEngineering/MutualInformationFeatureSelector.cs
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;
using Microsoft.ML;

namespace D2G.Iris.ML.FeatureEngineering
{
    public class MutualInformationFeatureSelector : BaseFeatureSelector
    {
        public MutualInformationFeatureSelector(MLContext mlContext) : base(mlContext) { }

        public override async Task<(IDataView transformedData, string[] selectedFeatures, string report)> SelectFeatures(
            MLContext mlContext,
            IDataView data,
            string[] candidateFeatures,
            ModelType modelType,
            string targetField,
            FeatureEngineeringConfig config)
        {
            InitializeReport("Mutual Information");

            // Your mutual information implementation here
            // Use base class utilities: CreateFeaturesColumn(), AddFeatureSelectionSummary()

            var selectedFeatures = candidateFeatures; // Replace with actual selection
            var transformedData = CreateFeaturesColumn(data, selectedFeatures);

            AddFeatureSelectionSummary(candidateFeatures.Length, selectedFeatures.Length, selectedFeatures);

            return await Task.FromResult((transformedData, selectedFeatures, _report.ToString()));
        }
    }
}
```

**Step 3: Update Factory**
```csharp
// D2G.Iris.ML/FeatureEngineering/FeatureSelectorFactory.cs
public class FeatureSelectorFactory : IFeatureSelectorFactory
{
    private readonly MLContext _mlContext;

    public FeatureSelectorFactory(MLContext mlContext)
    {
        _mlContext = mlContext;
    }

    public IFeatureSelector CreateSelector(FeatureSelectionMethod method)
    {
        return method switch
        {
            FeatureSelectionMethod.None => new NoFeatureSelector(_mlContext),
            FeatureSelectionMethod.Correlation => new CorrelationFeatureSelector(_mlContext),
            FeatureSelectionMethod.PCA => new PCAFeatureSelector(_mlContext),
            FeatureSelectionMethod.MutualInformation => new MutualInformationFeatureSelector(_mlContext),  // NEW
            _ => throw new ArgumentException($"Unsupported feature selection method: {method}")
        };
    }
}
```

---

### Example 3: Adding a New Model Type

Let's add "Clustering" model type:

**Step 1: Update Enum**
```csharp
// D2G.Iris.ML/Core/Enums/ModelType.cs
public enum ModelType
{
    BinaryClassification = 0,
    MultiClassClassification = 1,
    Regression = 2,
    Clustering = 3  // NEW
}
```

**Step 2: Create Trainer Implementation**
```csharp
// D2G.Iris.ML/Training/ClusteringTrainer.cs
using D2G.Iris.ML.Core.Interfaces;
using D2G.Iris.ML.Core.Models;
using Microsoft.ML;

namespace D2G.Iris.ML.Training
{
    public class ClusteringTrainer : BaseModelTrainer
    {
        public ClusteringTrainer(MLContext mlContext, ITrainerFactory trainerFactory)
            : base(mlContext, trainerFactory) { }

        public override async Task<TrainingResult> TrainModel(
            MLContext mlContext,
            IDataView dataView,
            string[] featureNames,
            ModelConfig config,
            ProcessedData processedData)
        {
            // Use base class utilities
            var split = SplitTrainTestData(mlContext, dataView, config.TrainingParameters.TestFraction);
            var pipeline = GetBasePipeline(mlContext);

            // Add clustering-specific training logic
            var trainer = _trainerFactory.GetTrainer(Core.Enums.ModelType.Clustering, config.TrainingParameters);
            var fullPipeline = pipeline.Append(trainer);

            var model = await TrainModelAsync(fullPipeline, split.TrainSet);

            // Your clustering evaluation logic

            var modelPath = SaveModel(mlContext, model, dataView, "Clustering", "KMeans", featureNames, Core.Enums.ModelType.Clustering);

            return new TrainingResult { /* populate result */ };
        }
    }
}
```

**Step 3: Update Model Trainer Factory**
```csharp
// D2G.Iris.ML/Training/ModelTrainerFactory.cs
public class ModelTrainerFactory : IModelTrainerFactory
{
    private readonly MLContext _mlContext;
    private readonly ITrainerFactory _trainerFactory;

    public ModelTrainerFactory(MLContext mlContext, ITrainerFactory trainerFactory)
    {
        _mlContext = mlContext;
        _trainerFactory = trainerFactory;
    }

    public IModelTrainer CreateTrainer(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.BinaryClassification => new BinaryClassificationTrainer(_mlContext, _trainerFactory),
            ModelType.MultiClassClassification => new MultiClassClassificationTrainer(_mlContext, _trainerFactory),
            ModelType.Regression => new RegressionTrainer(_mlContext, _trainerFactory),
            ModelType.Clustering => new ClusteringTrainer(_mlContext, _trainerFactory),  // NEW
            _ => throw new ArgumentException($"Unsupported model type: {modelType}")
        };
    }
}
```

**Step 4: Update Trainer Factory** (if needed)
```csharp
// Update ITrainerFactory implementation to support clustering trainers
public IEstimator<ITransformer> GetTrainer(ModelType modelType, TrainingParameters parameters)
{
    return modelType switch
    {
        ModelType.Clustering => _mlContext.Clustering.Trainers.KMeans(numberOfClusters: parameters.NumberOfClusters),
        // ... other cases
    };
}
```

---

## Benefits of This Architecture

### 1. Easy Extension (Open/Closed Principle)
- Add new algorithms without modifying existing code
- New features are added by creating new classes, not editing old ones
- Reduces risk of breaking existing functionality

### 2. Maintainability
- Changes are localized to specific implementations
- Clear separation of concerns makes code easier to understand
- Base classes provide reusable utilities, reducing duplication

### 3. Testability
- Easy to mock interfaces for unit testing
- Can test components in isolation
- Can create test implementations without affecting production code

### 4. Flexibility
- Swap implementations at runtime
- Configure behavior through dependency injection
- Support multiple versions of algorithms simultaneously

### 5. Type Safety
- Enums provide compile-time checking
- Interface contracts prevent runtime errors
- Factory pattern ensures only valid instances are created

### 6. Consistency
- All implementations follow the same contract
- Base classes enforce consistent patterns
- Template methods ensure uniform behavior

### 7. Scalability
- Easy to add new team members - clear structure to follow
- Parallel development - different developers can work on different implementations
- Can extend system without understanding all existing code

---

## Design Pattern Summary

| Pattern | Purpose | Example in Codebase |
|---------|---------|---------------------|
| **Strategy Pattern** | Define family of algorithms, make them interchangeable | `IModelTrainer`, `IDataBalancer`, `IFeatureSelector` |
| **Factory Pattern** | Encapsulate object creation logic | `IModelTrainerFactory`, `IDataBalancerFactory`, `IFeatureSelectorFactory` |
| **Template Method** | Define algorithm skeleton, let subclasses override steps | `BaseModelTrainer`, `BaseFeatureSelector` |
| **Dependency Inversion** | Depend on abstractions, not concretions | All interfaces - client code depends on `IModelTrainer`, not concrete trainers |

---

## Key Architectural Files

### Interface Definitions
- `D2G.Iris.ML/Core/Interfaces/IModelTrainer.cs`
- `D2G.Iris.ML/Core/Interfaces/IDataBalancer.cs`
- `D2G.Iris.ML/Core/Interfaces/IFeatureSelector.cs`
- `D2G.Iris.ML/Core/Interfaces/IModelTrainerFactory.cs`
- `D2G.Iris.ML/Core/Interfaces/IDataBalancerFactory.cs`
- `D2G.Iris.ML/Core/Interfaces/IFeatureSelectorFactory.cs`

### Enumerations
- `D2G.Iris.ML/Core/Enums/ModelType.cs`
- `D2G.Iris.ML/Core/Enums/DataBalanceMethod.cs`
- `D2G.Iris.ML/Core/Enums/FeatureSelectionMethod.cs`

### Base Classes
- `D2G.Iris.ML/Training/BaseModelTrainer.cs`
- `D2G.Iris.ML/FeatureEngineering/BaseFeatureSelector.cs`

### Factory Implementations
- `D2G.Iris.ML/Training/ModelTrainerFactory.cs`
- `D2G.Iris.ML/DataBalancing/DataBalancerFactory.cs`
- `D2G.Iris.ML/FeatureEngineering/FeatureSelectorFactory.cs`

### Example Concrete Implementations
- `D2G.Iris.ML/DataBalancing/SmoteDataBalancer.cs`
- `D2G.Iris.ML/FeatureEngineering/CorrelationFeatureSelector.cs`

---

## Best Practices

### When Adding New Components

1. **Update the Enum First**: Add your new option to the relevant enum
2. **Implement the Interface**: Create a class implementing the strategy interface
3. **Consider Base Classes**: Inherit from base classes when available for code reuse
4. **Update the Factory**: Add the switch case to create your implementation
5. **Test Independently**: Write unit tests for your implementation
6. **Update Documentation**: Document your algorithm and its parameters

### Naming Conventions

- **Interfaces**: Start with `I` (e.g., `IModelTrainer`)
- **Factory Interfaces**: End with `Factory` (e.g., `IModelTrainerFactory`)
- **Concrete Classes**: Descriptive names (e.g., `SmoteDataBalancer`, `CorrelationFeatureSelector`)
- **Base Classes**: Start with `Base` (e.g., `BaseModelTrainer`)

### Code Organization

```
D2G.Iris.ML/
├── Core/
│   ├── Interfaces/          # All interface definitions
│   ├── Enums/               # All enumerations
│   └── Models/              # Data models and configurations
├── Training/                # Model training implementations
│   ├── BaseModelTrainer.cs
│   ├── ModelTrainerFactory.cs
│   └── [Concrete trainers]
├── DataBalancing/           # Data balancing implementations
│   ├── DataBalancerFactory.cs
│   └── [Concrete balancers]
└── FeatureEngineering/      # Feature selection implementations
    ├── BaseFeatureSelector.cs
    ├── FeatureSelectorFactory.cs
    └── [Concrete selectors]
```

---

## Conclusion

This interface-based architecture provides a robust foundation for extending the ML framework. By following the established patterns (Strategy, Factory, Template Method), developers can add new functionality quickly and safely, without modifying existing code. The clear separation of concerns, combined with strong typing through interfaces and enums, creates a maintainable and scalable codebase.

When adding new features, follow the examples in this guide to maintain consistency and leverage the full benefits of this architectural approach.
