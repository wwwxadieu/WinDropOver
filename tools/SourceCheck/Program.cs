using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Checks the C# under the given directories for the mistakes Roslyn can see without being able
// to resolve any types — which is all that is available here, since the WPF references these
// files need do not exist off Windows.
//
// Two classes of problem, both of which have reached CI red in this repository:
//   * syntax errors, from an edit that did not close what it opened
//   * a local declared where an enclosing scope already has that name (CS0136)

var roots = args.Length > 0 ? args : ["src"];
var failures = 0;
var filesChecked = 0;

foreach (var file in roots.SelectMany(r => Directory.EnumerateFiles(r, "*.cs", SearchOption.AllDirectories))
                          .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
                          .OrderBy(f => f))
{
    filesChecked++;
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

    foreach (var diagnostic in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
    {
        var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
        Console.WriteLine($"{file}({line}): {diagnostic.Id}: {diagnostic.GetMessage()}");
        failures++;
    }

    foreach (var body in tree.GetRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
    {
        failures += ReportShadowedLocals(file, body);
    }
}

Console.WriteLine(failures == 0
    ? $"SourceCheck: {filesChecked} tệp, không có vấn đề."
    : $"SourceCheck: {failures} vấn đề trong {filesChecked} tệp.");
return failures == 0 ? 0 : 1;

// A local may not reuse a name already introduced by an enclosing scope in the same method. Both
// ordinary declarations and pattern variables count, which is what makes this easy to trip over:
// `if (x is not Foo source)` puts `source` in the method's scope, and a `var source` inside the
// following block is then an error rather than a shadow.
static int ReportShadowedLocals(string file, BaseMethodDeclarationSyntax method)
{
    var problems = 0;
    var declarations = new List<(string Name, int Start, int End, int Line)>();

    foreach (var node in method.DescendantNodes())
    {
        var (name, token) = node switch
        {
            VariableDeclaratorSyntax v when v.Parent?.Parent is LocalDeclarationStatementSyntax
                => (v.Identifier.ValueText, v.Identifier),
            DeclarationPatternSyntax d when d.Designation is SingleVariableDesignationSyntax s
                => (s.Identifier.ValueText, s.Identifier),
            SingleVariableDesignationSyntax s when s.Parent is DeclarationExpressionSyntax
                => (s.Identifier.ValueText, s.Identifier),
            _ => (null, default)
        };
        if (name is null)
        {
            continue;
        }

        // The scope a declaration lives in is its nearest enclosing block, or the whole method
        // for a pattern variable in a condition — approximated by the enclosing statement's
        // parent block, which is what makes an `is` in an `if` visible after that `if`.
        var scope = (SyntaxNode?)node.Ancestors().OfType<BlockSyntax>().FirstOrDefault() ?? method;
        declarations.Add((name, scope.SpanStart, scope.Span.End,
            token.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
    }

    foreach (var group in declarations.GroupBy(d => d.Name).Where(g => g.Count() > 1))
    {
        var ordered = group.OrderBy(d => d.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            // Nested inside an earlier declaration's scope means the name is already taken.
            if (ordered[i].Start >= ordered[0].Start && ordered[i].End <= ordered[0].End)
            {
                Console.WriteLine(
                    $"{file}({ordered[i].Line}): CS0136: tên '{group.Key}' đã được dùng ở phạm vi bao ngoài (dòng {ordered[0].Line}).");
                problems++;
            }
        }
    }
    return problems;
}
