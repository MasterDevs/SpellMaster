using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace SpellMaster
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SpellMasterAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "SpellMaster";

        private const string Category = "Spelling";

        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public string MyProperty { get; set; }
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Field, SymbolKind.Method, SymbolKind.NamedType, SymbolKind.Namespace, SymbolKind.Property);
            //context.RegisterSyntaxNodeAction(CheckXMLComments, SyntaxKind.MethodDeclaration, SyntaxKind.ClassDeclaration, SyntaxKind.PropertyDeclaration);
            //context.RegisterSyntaxTreeAction(HandleSyntaxTree);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var sym = context.Symbol;

            //-- No need to check Getters & Setters for a property
            if (sym.GetType().FullName == "Microsoft.CodeAnalysis.CSharp.Symbols.SourcePropertyAccessorSymbol")
                return;

            // Find just those named type symbols with names containing lowercase letters.
            if (Speller.Default.HasMisspelling(sym.Name))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, sym.Locations[0], sym.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private void CheckXMLComments(SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
        {
            //-- If we don't have any XML comments, then just move on
            var node = syntaxNodeAnalysisContext.Node as MethodDeclarationSyntax;

            if (null == node || node.HasStructuredTrivia)
                return;

            var xmlTrivia = node.GetLeadingTrivia()
                .Select(i => i.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (null == xmlTrivia) return;

            var summary = xmlTrivia.ChildNodes()
                .OfType<XmlElementSyntax>()
                .FirstOrDefault(i => i.StartTag.Name.ToString().Equals("summary"));

            if (null != summary && Speller.Default.HasMisspelling(summary.GetText().ToString()))
            {
                syntaxNodeAnalysisContext.ReportDiagnostic(Diagnostic.Create(Rule, summary.GetLocation()));
            }
        }

        private void HandleSyntaxTree(SyntaxTreeAnalysisContext context)
        {
            var root = context.Tree.GetCompilationUnitRoot(context.CancellationToken);

            var commentNodes = from node in root.DescendantTrivia()
                               where node.IsKind(SyntaxKind.MultiLineCommentTrivia) || node.IsKind(SyntaxKind.SingleLineCommentTrivia) || node.IsKind(SyntaxKind.XmlComment)
                               select node;

            if (!commentNodes.Any())
            {
                return;
            }
            foreach (var node in commentNodes)
            {
                string commentText = "";
                switch (node.Kind())
                {
                    case SyntaxKind.SingleLineCommentTrivia:
                        commentText = node.ToString().TrimStart('/');
                        break;

                    case SyntaxKind.MultiLineCommentTrivia:
                        var nodeText = node.ToString();

                        commentText = nodeText.Substring(2, nodeText.Length - 4);
                        break;

                    default: break;
                }

                if (Speller.Default.HasMisspelling(commentText))
                {
                    var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), commentText);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}