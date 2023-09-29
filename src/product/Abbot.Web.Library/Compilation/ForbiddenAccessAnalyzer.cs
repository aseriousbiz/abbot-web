using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Serious.Abbot.Compilation;

// CREDIT: https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.BannedApiAnalyzers/Core/SymbolIsBannedAnalyzer.cs
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ForbiddenAccessAnalyzer : DiagnosticAnalyzer
{
    public const string NamespaceDiagnosticId = "AB0001";
    public const string NamespaceTypeDiagnosticId = "AB0002";
    public const string TypeDiagnosticId = "AB0003";
    public const string AttributeDiagnosticId = "AB0004";
    public const string DarkArtsId = "AB0005";

    static readonly LocalizableString Description = "Abbot limits the code that may be used in an Abbot skill.";
    const string Title = "Forbidden Access Analyzer";
    const string Category = "API Usage";
    const DiagnosticSeverity Severity = DiagnosticSeverity.Error;

    static readonly HashSet<string> TypeDenyList = new(StringComparer.Ordinal)
    {
        "System.AppDomain",
        "System.Console",
        "System.Environment",
        "System.IntPtr",
        "nint",
        "System.Type",
        "System.TypedReference"
    };

    static readonly HashSet<string> NamespaceDenyList = new(StringComparer.Ordinal)
    {
        "System.Diagnostics",
        "System.Diagnostics.Tracing",
        "System.IO",
        "System.IO.Compression",
        "System.IO.MemoryMappedFiles",
        "System.Reflection",
        "System.Reflection.Emit",
        "System.Reflection.Metadata",
        "System.Runtime.CompilerServices",
        "System.Runtime.InteropServices",
        "System.Security",
        "System.Security.AccessControl",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.CodeAnalysis.CSharp.Scripting",
        "Microsoft.Win32.Registry"
    };

    // Types in forbidden namespaces we'll still allow.
    static readonly HashSet<string> TypeAllowList = new()
    {
        "System.IO.StringWriter"
    };

    static readonly HashSet<string> AttributeNamespaceAllowList = new()
    {
        "Newtonsoft.Json",
        "System.ComponentModel.DataAnnotations",
        "System.Text.Json",
    };

    static readonly DiagnosticDescriptor ForbiddenNamespaceRule = new(
        NamespaceDiagnosticId,
        Title,
        "Importing the namespace {0} is not allowed in an Abbot skill",
        Category,
        Severity,
        isEnabledByDefault: true,
        Description);

    static readonly DiagnosticDescriptor ForbiddenNamespaceTypeRule = new(
        NamespaceTypeDiagnosticId,
        Title,
        "Access to types in the {0} namespace are not allowed in an Abbot skill",
        Category,
        Severity,
        isEnabledByDefault: true,
        Description);

    static readonly DiagnosticDescriptor ForbiddenTypeRule = new(
        TypeDiagnosticId,
        Title,
        "Access to {0} is not allowed in an Abbot skill",
        Category,
        Severity,
        isEnabledByDefault: true,
        Description);

    static readonly DiagnosticDescriptor ForbiddenAttributeRule = new(
        AttributeDiagnosticId,
        Title,
        "Attribute '{0}' is not allowed in an Abbot skill",
        Category,
        Severity,
        isEnabledByDefault: true,
        Description);

    static readonly DiagnosticDescriptor ForbiddenDarkArtsRule = new(
        DarkArtsId,
        Title,
        "The dark arts of C# are not allowed in an Abbot skill",
        Category,
        Severity,
        isEnabledByDefault: true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        ForbiddenTypeRule,
        ForbiddenNamespaceTypeRule,
        ForbiddenAttributeRule,
        ForbiddenNamespaceRule,
        ForbiddenDarkArtsRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
    {
        compilationContext.RegisterSyntaxNodeAction(context => {
            var model = context.SemanticModel;
            var node = context.Node;

            switch (node)
            {
                case AttributeSyntax attr:
                    if (!IsAttributeAllowed(attr, model))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(ForbiddenAttributeRule, context.Node.GetLocation(), attr.Name));
                    }

                    return;
                case UsingDirectiveSyntax usingDirective:
                    {
                        var usingName = usingDirective.Name is QualifiedNameSyntax qualifiedName
                                        && (usingDirective.Alias is not null
                                            || !usingDirective.StaticKeyword.IsKind(SyntaxKind.None))
                            ? qualifiedName.Left.ToString()
                            : usingDirective.Name?.ToString();

                        if (usingName is not null && NamespaceDenyList.Contains(usingName))
                        {
                            context.ReportDiagnostic(
                                Diagnostic.Create(ForbiddenNamespaceRule, context.Node.GetLocation(), usingName));
                        }

                        return;
                    }
                case MakeRefExpressionSyntax _:
                case RefValueExpressionSyntax _:
                case RefTypeExpressionSyntax _:
                case ArgumentListSyntax _:
                    context.ReportDiagnostic(
                        Diagnostic.Create(ForbiddenDarkArtsRule, context.Node.GetLocation()));

                    return;
            }

            var symbolInfo = node switch
            {
                ObjectCreationExpressionSyntax objectCreationExpression
                    => model.GetSymbolInfo(objectCreationExpression.Type),
                BinaryExpressionSyntax binaryExpression => model.GetSymbolInfo(binaryExpression.Right),
                MemberAccessExpressionSyntax memberAccessExpression
                    => model.GetSymbolInfo(memberAccessExpression.Expression),
                CastExpressionSyntax castExpression => model.GetSymbolInfo(castExpression.Type),
                _ => throw new InvalidOperationException("Unexpected expression type.")
            };

            var symbol = symbolInfo.Symbol;

            var typeSymbol = symbol switch
            {
                INamedTypeSymbol namedTypeSymbol => namedTypeSymbol,
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                ILocalSymbol localSymbol => localSymbol.Type,
                _ => null
            };

            var typeName = typeSymbol?.ToString();

            if (typeName is null)
            {
                return;
            }

            VerifyTypeHierarchy(context.ReportDiagnostic, typeSymbol, node);
        },
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxKind.CastExpression,
            SyntaxKind.AsExpression,
            SyntaxKind.Attribute,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.UsingDirective,
            SyntaxKind.RefValueExpression,
            SyntaxKind.MakeRefExpression,
            SyntaxKind.RefTypeExpression);

        compilationContext.RegisterOperationAction(context => {
            var type = context.Operation switch
            {
                //IObjectCreationOperation objectCreation => objectCreation.Type,
                //IInvocationOperation invocationOperation => invocationOperation.TargetMethod.ContainingType,
                //IMemberReferenceOperation memberReference => memberReference.Member?.ContainingType,
                ITypeOfOperation typeOfOperation => typeOfOperation.Type,
                IArrayCreationOperation arrayCreation => arrayCreation.Type,
                IAddressOfOperation addressOf => addressOf.Type,
                IConversionOperation conversion => conversion.OperatorMethod?.ContainingType,
                IUnaryOperation unary => unary.OperatorMethod?.ContainingType,
                IBinaryOperation binary => binary.OperatorMethod?.ContainingType,
                IIncrementOrDecrementOperation incrementOrDecrement
                    => incrementOrDecrement.OperatorMethod?.ContainingType,
                _ => throw new NotImplementedException($"Unhandled OperationKind: {context.Operation.Kind}")
            };

            VerifyTypeHierarchy(context.ReportDiagnostic, type, context.Operation.Syntax);
        },
            //OperationKind.ObjectCreation,
            //OperationKind.Invocation,
            OperationKind.EventReference,
            //OperationKind.FieldReference,
            //OperationKind.MethodReference,
            //OperationKind.PropertyReference,
            OperationKind.ArrayCreation,
            OperationKind.AddressOf,
            OperationKind.Conversion,
            OperationKind.UnaryOperator,
            OperationKind.BinaryOperator,
            OperationKind.Increment,
            OperationKind.Decrement,
            OperationKind.TypeOf);
    }

    static bool IsAttributeAllowed(AttributeSyntax attr, SemanticModel model)
    {
        var attributeType = model.GetTypeInfo(attr);
        if (attributeType.Type is null or IErrorTypeSymbol)
        {
            // Type couldn't be resolved
            return false;
        }

        // Walk namespaces
        var ns = attributeType.Type?.ContainingNamespace;
        while (ns is { IsGlobalNamespace: false })
        {
            if (ns.ToString() is { } nsName && AttributeNamespaceAllowList.Contains(nsName))
            {
                return true;
            }

            ns = ns.ContainingNamespace;
        }

        return false;
    }

    void VerifyTypeHierarchy(Action<Diagnostic> reportDiagnostic, ITypeSymbol? type, SyntaxNode syntaxNode)
    {
        var originalTypeName = type?.ToString();
        if (originalTypeName is null)
        {
            return;
        }

        do
        {
            if (!VerifyType(reportDiagnostic, type, syntaxNode, originalTypeName))
            {
                return;
            }

            if (type is null)
            {
                // Type will be null for arrays and pointers.
                return;
            }

            type = type.BaseType;
        } while (type is not null);
    }

    bool VerifyType(Action<Diagnostic> reportDiagnostic, ITypeSymbol? type, SyntaxNode syntaxNode,
        string originalTypeName)
    {
        do
        {
            if (!VerifyTypeArguments(reportDiagnostic, type, originalTypeName, syntaxNode, out type))
            {
                return false;
            }

            var typeName = type?.ToString();
            if (typeName is null || type is null)
            {
                return true;
            }

            if (TypeDenyList.Contains(typeName))
            {
                reportDiagnostic(Diagnostic.Create(ForbiddenTypeRule, syntaxNode.GetLocation(), typeName));
                return false;
            }

            if (!VerifyTypeNamespace(reportDiagnostic, type, syntaxNode, typeName, originalTypeName))
                return false;

            type = type.ContainingType;
        } while (type is not null);

        return true;
    }

    static bool VerifyTypeNamespace(
        Action<Diagnostic> reportDiagnostic,
        ISymbol type,
        SyntaxNode syntaxNode,
        string typeName,
        string originalTypeName)
    {
        if (!IsValidNamespace(type, typeName, originalTypeName, out var typeNamespace))
        {
            reportDiagnostic(Diagnostic.Create(
                ForbiddenNamespaceTypeRule,
                syntaxNode.GetLocation(),
                typeNamespace));

            return false;
        }

        return true;
    }

    static bool IsValidNamespace(ISymbol type, string typeName, string originalTypeName, out string? typeNamespace)
    {
        typeNamespace = type.ContainingNamespace?.ToString();
        return typeNamespace is null
               || !NamespaceDenyList.Contains(typeNamespace)
               || TypeAllowList.Contains(typeName)
               || TypeAllowList.Contains(originalTypeName);
    }

    bool VerifyTypeArguments(
        Action<Diagnostic> reportDiagnostic,
        ITypeSymbol? type,
        string originalTypeName,
        SyntaxNode syntaxNode,
        out ITypeSymbol? originalDefinition)
    {
        switch (type)
        {
            case INamedTypeSymbol namedTypeSymbol:
                originalDefinition = namedTypeSymbol.ConstructedFrom;
                foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                {
                    if (typeArgument.TypeKind != TypeKind.TypeParameter &&
                        typeArgument.TypeKind != TypeKind.Error &&
                        !VerifyType(reportDiagnostic, typeArgument, syntaxNode, originalTypeName))
                    {
                        return false;
                    }
                }

                break;

            case IArrayTypeSymbol arrayTypeSymbol:
                originalDefinition = null;
                return VerifyType(reportDiagnostic, arrayTypeSymbol.ElementType, syntaxNode, originalTypeName);

            case IPointerTypeSymbol pointerTypeSymbol:
                originalDefinition = null;
                return VerifyType(reportDiagnostic, pointerTypeSymbol.PointedAtType, syntaxNode, originalTypeName);

            default:
                originalDefinition = type?.OriginalDefinition;
                break;
        }

        return true;
    }
}
