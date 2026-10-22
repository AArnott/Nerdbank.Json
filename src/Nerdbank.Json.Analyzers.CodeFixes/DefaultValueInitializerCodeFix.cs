// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Nerdbank.Json.Analyzers.CodeFixes;

/// <summary>
/// Describes constant initializers with default-value metadata without changing their runtime values.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class DefaultValueInitializerCodeFix : CodeFixProvider
{
	/// <inheritdoc/>
	public override ImmutableArray<string> FixableDiagnosticIds => [DefaultValueInitializerAnalyzer.MissingDefaultValueAttributeDiagnosticId];

	/// <inheritdoc/>
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc/>
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null || semanticModel is null)
		{
			return;
		}

		SyntaxNode node = root.FindNode(context.Span);
		VariableDeclaratorSyntax? variable = node.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
		PropertyDeclarationSyntax? property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
		MemberDeclarationSyntax? member;
		ExpressionSyntax? initializer;
		TypeSyntax? typeSyntax;
		ITypeSymbol? type;
		if (variable is { Initializer: { } fieldInitializer, Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: FieldDeclarationSyntax field } declaration })
		{
			member = field;
			initializer = fieldInitializer.Value;
			typeSyntax = declaration.Type;
			type = (semanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as IFieldSymbol)?.Type;
		}
		else if (property is { Initializer: { } propertyInitializer })
		{
			member = property;
			initializer = propertyInitializer.Value;
			typeSyntax = property.Type;
			type = semanticModel.GetDeclaredSymbol(property, context.CancellationToken)?.Type;
		}
		else
		{
			return;
		}

		if (type is null || !semanticModel.GetConstantValue(initializer, context.CancellationToken).HasValue)
		{
			return;
		}

		ExpressionSyntax argument = initializer.WithoutTrivia();
		TypeInfo typeInfo = semanticModel.GetTypeInfo(initializer, context.CancellationToken);
		if (initializer.IsKind(SyntaxKind.DefaultLiteralExpression) ||
			(typeInfo.Type is not null && !SymbolEqualityComparer.Default.Equals(typeInfo.Type, type)))
		{
			// Preserve the boxed primitive type used by DefaultValueAttribute.
			if (argument is BinaryExpressionSyntax or ConditionalExpressionSyntax)
			{
				argument = SyntaxFactory.ParenthesizedExpression(argument);
			}

			argument = SyntaxFactory.CastExpression(typeSyntax.WithoutTrivia(), argument);
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				"Add [DefaultValue(...)]",
				cancellationToken => AddAttributeAsync(context.Document, member, argument, cancellationToken),
				equivalenceKey: nameof(DefaultValueInitializerCodeFix)),
			context.Diagnostics);
	}

	private static async Task<Document> AddAttributeAsync(Document document, MemberDeclarationSyntax member, ExpressionSyntax argument, CancellationToken cancellationToken)
	{
		SyntaxNode root = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
		AttributeListSyntax attribute = SyntaxFactory.AttributeList(
			SyntaxFactory.SingletonSeparatedList(
				SyntaxFactory.Attribute(
					SyntaxFactory.ParseName("global::System.ComponentModel.DefaultValue"),
					SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(argument))))));

		MemberDeclarationSyntax updated = member.WithoutLeadingTrivia() switch
		{
			FieldDeclarationSyntax field => field.AddAttributeLists(attribute),
			PropertyDeclarationSyntax property => property.AddAttributeLists(attribute),
			_ => member,
		};
		updated = updated.WithLeadingTrivia(member.GetLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
		return document.WithSyntaxRoot(root.ReplaceNode(member, updated));
	}
}
