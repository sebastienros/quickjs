// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for ES module parsing (import/export declarations).
/// 
/// ES modules are the official JavaScript module system introduced in ES2015.
/// The parser must handle various import/export syntaxes.
/// </summary>
public class ModuleParserTests
{
    #region Import Declarations

    [Fact]
    public void ParseImport_SideEffectOnly_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_DefaultExport_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import foo from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_NamedImport_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import { foo } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_MultipleNamedImports_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import { foo, bar, baz } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_NamedImportWithAlias_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import { foo as myFoo } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_NamespaceImport_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import * as ns from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_DefaultAndNamed_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import foo, { bar, baz } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_DefaultAndNamespace_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import foo, * as ns from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_KeywordAsName_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import { default as myDefault } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseImport_InScriptMode_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import foo from 'module.js';", "test.js", atoms, isModule: false);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    [Fact]
    public void ParseImport_WithoutSemicolon_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("import foo from 'module.js'\n", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Export Declarations

    [Fact]
    public void ParseExport_Named_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export { foo };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_MultipleNamed_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export { foo, bar, baz };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_NamedWithAlias_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export { foo as myFoo };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_VarDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export var foo = 1;", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_LetDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export let foo = 1;", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_ConstDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export const foo = 1;", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_FunctionDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export function foo() { return 42; }", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_AsyncFunctionDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export async function foo() { return 42; }", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_ClassDeclaration_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export class Foo { }", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_Default_Expression_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export default 42;", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_Default_Object_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export default { a: 1, b: 2 };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_Default_Function_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export default function() { return 42; };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_Default_NamedFunction_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export default function foo() { return 42; };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_Default_Class_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export default class { };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_Default_NamedClass_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export default class Foo { };", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_StarReexport_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export * from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_StarAsNamespace_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export * as ns from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_NamedReexport_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export { foo, bar } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_NamedReexportWithAlias_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export { foo as myFoo } from 'module.js';", "test.js", atoms, isModule: true);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseExport_InScriptMode_ThrowsError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("export { foo };", "test.js", atoms, isModule: false);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    #endregion

    #region Module Programs

    [Fact]
    public void ParseModule_MultipleImportsAndExports_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            import foo from 'foo.js';
            import { bar, baz } from 'bar.js';
            
            export const x = 1;
            export function test() { return foo + bar; }
            export default x;
        ", "test.js", atoms, isModule: true);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseModule_ImportFollowedByCode_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            import { helper } from 'utils.js';
            
            function main() {
                return helper(42);
            }
            
            export { main };
        ", "test.js", atoms, isModule: true);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseModule_ExportClassWithMethods_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            export class Calculator {
                constructor(value) {
                    this.value = value;
                }
                
                add(x) {
                    return this.value + x;
                }
                
                static create(value) {
                    return new Calculator(value);
                }
            }
        ", "test.js", atoms, isModule: true);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion
}
