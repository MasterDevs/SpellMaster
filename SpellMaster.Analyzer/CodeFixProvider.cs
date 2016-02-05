using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpellMaster
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SpellMasterCodeFixProvider)), Shared]
    public class SpellMasterCodeFixProvider : CodeFixProvider
    {
        private const string title = "Spellchecker";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(SpellMasterAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            try
            {
                var node = root.FindNode(diagnosticSpan);

                if (node == null) return;

                var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
                var symbol =
                    semanticModel.GetDeclaredSymbol(node, context.CancellationToken) ??
                    semanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;

                if (null == symbol) return;

                var val = Speller.Default.GetReplacement(symbol.Name);

                if (!string.IsNullOrEmpty(val))
                {
                    context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedSolution: c => MakeUppercaseAsync(context.Document, symbol, val, c),
                        equivalenceKey: title),
                    diagnostic);
                }
            }
            catch (ArgumentOutOfRangeException) { }
        }

        private async Task<Solution> MakeUppercaseAsync(Document document, ISymbol symbol, string replacementText, CancellationToken cancellationToken)
        {
            // Get the symbol representing the type to be renamed.

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, symbol, replacementText, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            return newSolution;
        }
    }
}