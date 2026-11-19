using System.Collections.Immutable;
using System.Reflection;
using Metaspesa.Domain.SharedKernel;

namespace Metaspesa.ArchitectureTests.Events;

public class EventPayloadTests {
  [Trait("ArchitectureRule", "ARCH-EVT-001")]
  [Fact]
  public void DomainEvents_AreDeclaredInAContextEventsNamespace() {
    // Arrange
    Type[] events = [.. typeof(IDomainEvent).Assembly.GetTypes()
      .Where(type => !type.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(type))
    ];

    // Act
    Type[] violations = [.. events.Where(type => type.Namespace is null ||
      !type.Namespace.EndsWith(".Events", StringComparison.Ordinal) ||
      type.Namespace.StartsWith("Metaspesa.Domain.SharedKernel", StringComparison.Ordinal))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-EVT-001")]
  [Fact]
  public void EventPayloadFields_AreReadonly() {
    // Arrange
    Type[] payloads = [.. typeof(IDomainEvent).Assembly.GetTypes()
      .Where(type => type.IsClass &&
        type.Namespace?.EndsWith(".Events", StringComparison.Ordinal) == true)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    FieldInfo[] mutableFields = [.. payloads.SelectMany(type => type.GetFields(flags))
      .Where(field => !field.IsInitOnly)
    ];

    // Assert
    Assert.Empty(mutableFields);
  }

  [Trait("ArchitectureRule", "ARCH-EVT-001")]
  [Fact]
  public void EventPayloadValues_AreScalarsOrContextPayloads() {
    // Arrange
    Assembly domain = typeof(IDomainEvent).Assembly;
    Type[] payloads = [.. domain.GetTypes()
      .Where(type => type.IsClass && type.Namespace?.EndsWith(".Events", StringComparison.Ordinal) == true)
    ];
    Type[] scalars = [
      typeof(string),
      typeof(decimal),
      typeof(Guid),
      typeof(Uri),
      typeof(DateTime),
      typeof(DateTimeOffset),
      typeof(DateOnly),
      typeof(TimeOnly),
      typeof(TimeSpan)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    Type[] violations = [.. payloads.SelectMany(type => type.GetFields(flags))
      .SelectMany(field => ExpandType(field.FieldType))
      .Where(type => !type.IsGenericType)
      .Where(type => !type.IsPrimitive && !scalars.Contains(type) && !payloads.Contains(type))
      .Distinct()
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-EVT-001")]
  [Fact]
  public void EventPayloadContainers_UseNullableOrReadOnlyCollectionShapes() {
    // Arrange
    Type[] payloads = [.. typeof(IDomainEvent).Assembly.GetTypes()
      .Where(type => type.IsClass &&
        type.Namespace?.EndsWith(".Events", StringComparison.Ordinal) == true)
    ];
    Type[] containers = [
      typeof(Nullable<>),
      typeof(IReadOnlyCollection<>),
      typeof(IReadOnlyList<>),
      typeof(ImmutableArray<>)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    Type[] violations = [.. payloads.SelectMany(type => type.GetFields(flags))
      .SelectMany(field => ExpandType(field.FieldType))
      .Where(type => type.IsGenericType && !containers.Contains(type.GetGenericTypeDefinition()))
      .Distinct()
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-EVT-001")]
  [Fact]
  public void EventPayloads_DoNotReferenceForeignContextTypes() {
    // Arrange
    Assembly domain = typeof(IDomainEvent).Assembly;
    Type[] payloads = [.. domain.GetTypes()
      .Where(type => type.IsClass &&
        type.Namespace?.EndsWith(".Events", StringComparison.Ordinal) == true)
    ];
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Act
    FieldInfo[] violations = [.. payloads.SelectMany(type => type.GetFields(flags))
      .Where(field => ExpandType(field.FieldType).Any(type =>
        type.Assembly == domain &&
        type.Namespace != field.DeclaringType?.Namespace))
    ];

    // Assert
    Assert.Empty(violations);
  }

  private static IEnumerable<Type> ExpandType(Type type) {
    yield return type;
    foreach (Type argument in type.GetGenericArguments()) {
      foreach (Type nested in ExpandType(argument)) {
        yield return nested;
      }
    }
  }
}