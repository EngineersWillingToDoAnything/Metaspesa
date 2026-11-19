using System.Reflection;
using Metaspesa.Application.Abstractions.Core;
using Metaspesa.Application.Identity;
using Metaspesa.ArchitectureTests.Dependencies;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Metaspesa.ArchitectureTests.Application;

public class HandlerBoundaryTests {
  [Trait("ArchitectureRule", "ARCH-APP-001")]
  [Fact]
  public void Application_DoesNotInvokeOtherUseCaseHandlers() {
    // Arrange
    Assembly assembly = typeof(LoginUser).Assembly;
    string[] handlers = [.. assembly.GetTypes()
      .Where(type => type.Name == "Handler")
      .Select(DependencyAnalysis.TypeName)
    ];
    Type[] contractDefinitions = [
      typeof(ICommandHandler<>),
      typeof(ICommandHandler<,>),
      typeof(IQueryHandler<>),
      typeof(IQueryHandler<,>)
    ];
    string[] contracts = [.. contractDefinitions.Select(DependencyAnalysis.TypeName)];

    // Act
    DependencyAnalysis.MethodUse[] violations = [.. DependencyAnalysis
      .MethodUses(assembly)
      .Where(call => call.Method == "Handle")
      .Where(call => handlers.Contains(call.Target) || contracts.Contains(call.Target))
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-APP-001")]
  [Fact]
  public void HandlerConstructors_DoNotReceiveOtherUseCaseHandlers() {
    // Arrange
    Type[] handlers = [.. typeof(LoginUser).Assembly.GetTypes()
      .Where(type => type.Name == "Handler")
    ];
    Type[] contractDefinitions = [
      typeof(ICommandHandler<>),
      typeof(ICommandHandler<,>),
      typeof(IQueryHandler<>),
      typeof(IQueryHandler<,>)
    ];

    // Act
    string[] violations = [.. handlers
      .SelectMany(type => type.GetConstructors())
      .SelectMany(constructor => constructor.GetParameters())
      .Where(parameter => handlers.Contains(parameter.ParameterType) ||
        parameter.ParameterType.IsGenericType && contractDefinitions.Contains(parameter.ParameterType.GetGenericTypeDefinition()))
      .Select(parameter => $"{parameter.Member.DeclaringType}: {parameter.Name}")
    ];

    // Assert
    Assert.Empty(violations);
  }

  [Trait("ArchitectureRule", "ARCH-APP-002")]
  [Fact]
  public void HandlerFields_AreNotAssignedOutsideConstructors() {
    // Arrange
    using var module = ModuleDefinition.ReadModule(typeof(LoginUser).Assembly.Location);
    string[] handlers = [.. module.GetTypes()
      .Where(type => type.Name == "Handler")
      .Select(type => type.FullName)
    ];

    // Act
    string[] assignments = [.. module.GetTypes()
      .SelectMany(type => type.Methods)
      .Where(method => method.HasBody && !method.IsConstructor)
      .SelectMany(method => method.Body.Instructions
        .Where(instruction =>
          instruction.OpCode == OpCodes.Stfld &&
          instruction.Operand is FieldReference field &&
          handlers.Contains(field.DeclaringType.FullName))
        .Select(instruction => $"{method.FullName}: {instruction.Operand}"))
    ];

    // Assert
    Assert.Empty(assignments);
  }
}