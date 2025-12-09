// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for class parsing in the JavaScript parser.
/// 
/// This includes class declarations, class expressions, methods, getters/setters,
/// static members, constructors, and inheritance.
/// </summary>
public class ClassParserTests
{
    #region Class Declarations

    [Fact]
    public void ParseClassDeclaration_Empty_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("class Foo { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_WithConstructor_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                constructor() {
                    this.x = 1;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_WithConstructorParams_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Point {
                constructor(x, y) {
                    this.x = x;
                    this.y = y;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_WithMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                bar() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_WithMultipleMethods_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Calculator {
                add(a, b) {
                    return a + b;
                }
                
                subtract(a, b) {
                    return a - b;
                }
                
                multiply(a, b) {
                    return a * b;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_WithoutName_ThrowsSyntaxError()
    {
        var atoms = new AtomTable();
        var parser = new Parser("class { }", "test.js", atoms);

        Assert.Throws<JSSyntaxError>(() => parser.ParseStatement());
    }

    #endregion

    #region Class Expressions

    [Fact]
    public void ParseClassExpression_Anonymous_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var Foo = class { };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassExpression_Named_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var Foo = class Bar { };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassExpression_InParens_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("(class { });", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassExpression_WithMethods_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            var Foo = class {
                constructor(x) {
                    this.x = x;
                }
                
                getValue() {
                    return this.x;
                }
            };
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Class Inheritance

    [Fact]
    public void ParseClassDeclaration_Extends_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("class Dog extends Animal { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_ExtendsWithMethods_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Dog extends Animal {
                constructor(name) {
                    this.name = name;
                }
                
                bark() {
                    return 'woof';
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_ExtendsMemberExpression_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("class Foo extends lib.Base { }", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassExpression_Extends_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser("var Dog = class extends Animal { };", "test.js", atoms);
        parser.ParseStatement();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Getters and Setters

    [Fact]
    public void ParseClassDeclaration_Getter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                get value() {
                    return this._value;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_Setter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                set value(v) {
                    this._value = v;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_GetterAndSetter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                get value() {
                    return this._value;
                }
                
                set value(v) {
                    this._value = v;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_GetterWithBody_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Rectangle {
                get area() {
                    return this.width * this.height;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Static Members

    [Fact]
    public void ParseClassDeclaration_StaticMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static bar() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_StaticGetter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static get count() {
                    return 0;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_StaticSetter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static set count(v) {
                    this._count = v;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_MixedStaticAndInstance_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Counter {
                static count = 0;
                
                constructor() {
                    Counter.count++;
                    this.id = Counter.count;
                }
                
                static getCount() {
                    return Counter.count;
                }
                
                getId() {
                    return this.id;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Computed Property Names

    [Fact]
    public void ParseClassDeclaration_ComputedMethodName_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                ['method']() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_ComputedMethodNameWithExpression_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                ['get' + 'Value']() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_ComputedGetter_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                get ['value']() {
                    return this._value;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_ComputedStaticMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static ['create']() {
                    return new Foo();
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Class Fields

    [Fact]
    public void ParseClassDeclaration_InstanceField_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                x = 1;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_InstanceFieldNoInit_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                x;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_MultipleFields_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Point {
                x = 0;
                y = 0;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_StaticField_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static count = 0;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_FieldsAndMethods_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Point {
                x = 0;
                y = 0;
                
                move(dx, dy) {
                    this.x += dx;
                    this.y += dy;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Async and Generator Methods

    [Fact]
    public void ParseClassDeclaration_AsyncMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                async fetch() {
                    return await doFetch();
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_GeneratorMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                *generate() {
                    yield 1;
                    yield 2;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_AsyncGeneratorMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                async *generate() {
                    yield await 1;
                    yield await 2;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_StaticAsyncMethod_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static async create() {
                    return await new Foo();
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Special Method Names

    [Fact]
    public void ParseClassDeclaration_MethodNamedGet_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                get() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_MethodNamedSet_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                set() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_MethodNamedStatic_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                static() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_MethodNamedAsync_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                async() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region String and Number Method Names

    [Fact]
    public void ParseClassDeclaration_StringMethodName_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                'my method'() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_NumberMethodName_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                42() {
                    return 42;
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Multiple Class Declarations

    [Fact]
    public void ParseMultipleClasses_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Animal {
                constructor(name) {
                    this.name = name;
                }
            }
            
            class Dog extends Animal {
                bark() {
                    return 'woof';
                }
            }
            
            class Cat extends Animal {
                meow() {
                    return 'meow';
                }
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion

    #region Empty Class Body Elements

    [Fact]
    public void ParseClassDeclaration_SemicolonsOnly_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                ;
                ;;
                ;
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    [Fact]
    public void ParseClassDeclaration_SemicolonsBetweenMethods_Succeeds()
    {
        var atoms = new AtomTable();
        var parser = new Parser(@"
            class Foo {
                foo() {}
                ;
                bar() {}
            }
        ", "test.js", atoms);
        parser.ParseProgram();

        Assert.True(parser.CurrentToken.IsEOF);
    }

    #endregion
}
