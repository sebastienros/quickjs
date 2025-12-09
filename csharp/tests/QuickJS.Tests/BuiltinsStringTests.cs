// Licensed under the MIT License.

using Xunit;

namespace QuickJS.Tests;

public class BuiltinsStringTests
{
    private readonly JSRuntime _runtime = new();
    private readonly JSContext _context;

    public BuiltinsStringTests()
    {
        _context = _runtime.CreateContext();
    }

    [Fact]
    public void StringConstructor_ReturnsPrimitive()
    {
        var stringCtor = (JSFunction)_context.GetGlobalProperty("String").AsObject();
        var result = stringCtor.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(123) });
        Assert.True(result.IsString);
        Assert.Equal("123", result.ToString());
    }

    [Fact]
    public void NewString_ReturnsBoxedObjectWithLength()
    {
        var stringCtor = (JSFunction)_context.GetGlobalProperty("String").AsObject();
        var boxed = stringCtor.CallNative(JSValue.FromObject(new JSObject(_context.GetClassPrototype(JSClassId.String), JSClassId.String)), new[] { JSValue.FromString("abc") });
        Assert.True(boxed.IsObject);
        var obj = boxed.AsObject();
        Assert.Equal(JSClassId.String, obj.ClassId);
        Assert.Equal(3, obj.Get("length").ToInt32());
        Assert.Equal("b", obj.Get(1).ToString());
    }

    [Fact]
    public void StringPrototypeToString_WorksForWrapper()
    {
        var stringCtor = (JSFunction)_context.GetGlobalProperty("String").AsObject();
        var boxed = stringCtor.CallNative(JSValue.FromObject(new JSObject(_context.GetClassPrototype(JSClassId.String), JSClassId.String)), new[] { JSValue.FromString("xyz") });
        var obj = boxed.AsObject();
        var toStringFn = (JSFunction)obj.Prototype!.Get("toString").AsObject();
        var res = toStringFn.CallNative(JSValue.FromObject(obj), System.Array.Empty<JSValue>());
        Assert.Equal("xyz", res.ToString());
    }

    [Fact]
    public void StringPrototype_CharAt_ReturnsCharacter()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var charAtFn = (JSFunction)stringProto.Get("charAt").AsObject();

        var result = charAtFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromInt32(1) });
        Assert.Equal("e", result.ToString());
    }

    [Fact]
    public void StringPrototype_CharCodeAt_ReturnsCharCode()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var charCodeAtFn = (JSFunction)stringProto.Get("charCodeAt").AsObject();

        var result = charCodeAtFn.CallNative(JSValue.FromString("ABC"), new[] { JSValue.FromInt32(0) });
        Assert.Equal(65, result.ToInt32());
    }

    [Fact]
    public void StringPrototype_Concat_JoinsStrings()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var concatFn = (JSFunction)stringProto.Get("concat").AsObject();

        var result = concatFn.CallNative(JSValue.FromString("Hello"), new[] { JSValue.FromString(" "), JSValue.FromString("World") });
        Assert.Equal("Hello World", result.ToString());
    }

    [Fact]
    public void StringPrototype_IndexOf_FindsSubstring()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var indexOfFn = (JSFunction)stringProto.Get("indexOf").AsObject();

        Assert.Equal(2, indexOfFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromString("ll") }).ToInt32());
        Assert.Equal(-1, indexOfFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromString("z") }).ToInt32());
    }

    [Fact]
    public void StringPrototype_LastIndexOf_FindsLastOccurrence()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var lastIndexOfFn = (JSFunction)stringProto.Get("lastIndexOf").AsObject();

        // "hello hello".lastIndexOf("ll") = 8 (position of "ll" in second "hello")
        Assert.Equal(8, lastIndexOfFn.CallNative(JSValue.FromString("hello hello"), new[] { JSValue.FromString("ll") }).ToInt32());
    }

    [Fact]
    public void StringPrototype_Includes_ReturnsTrueIfContains()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var includesFn = (JSFunction)stringProto.Get("includes").AsObject();

        Assert.True(includesFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("world") }).IsTrue);
        Assert.False(includesFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("xyz") }).IsTrue);
    }

    [Fact]
    public void StringPrototype_StartsWith_ChecksPrefix()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var startsWithFn = (JSFunction)stringProto.Get("startsWith").AsObject();

        Assert.True(startsWithFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("hello") }).IsTrue);
        Assert.False(startsWithFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("world") }).IsTrue);
    }

    [Fact]
    public void StringPrototype_EndsWith_ChecksSuffix()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var endsWithFn = (JSFunction)stringProto.Get("endsWith").AsObject();

        Assert.True(endsWithFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("world") }).IsTrue);
        Assert.False(endsWithFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("hello") }).IsTrue);
    }

    [Fact]
    public void StringPrototype_Slice_ExtractsSubstring()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var sliceFn = (JSFunction)stringProto.Get("slice").AsObject();

        Assert.Equal("ll", sliceFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromInt32(2), JSValue.FromInt32(4) }).ToString());
        Assert.Equal("lo", sliceFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromInt32(-2) }).ToString());
    }

    [Fact]
    public void StringPrototype_Substring_ExtractsPortion()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var substringFn = (JSFunction)stringProto.Get("substring").AsObject();

        Assert.Equal("ell", substringFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromInt32(1), JSValue.FromInt32(4) }).ToString());
    }

    [Fact]
    public void StringPrototype_ToLowerCase_ConvertsToLowerCase()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var toLowerFn = (JSFunction)stringProto.Get("toLowerCase").AsObject();

        Assert.Equal("hello world", toLowerFn.CallNative(JSValue.FromString("HELLO WORLD"), System.Array.Empty<JSValue>()).ToString());
    }

    [Fact]
    public void StringPrototype_ToUpperCase_ConvertsToUpperCase()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var toUpperFn = (JSFunction)stringProto.Get("toUpperCase").AsObject();

        Assert.Equal("HELLO WORLD", toUpperFn.CallNative(JSValue.FromString("hello world"), System.Array.Empty<JSValue>()).ToString());
    }

    [Fact]
    public void StringPrototype_Trim_RemovesWhitespace()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var trimFn = (JSFunction)stringProto.Get("trim").AsObject();

        Assert.Equal("hello", trimFn.CallNative(JSValue.FromString("  hello  "), System.Array.Empty<JSValue>()).ToString());
    }

    [Fact]
    public void StringPrototype_TrimStart_RemovesLeadingWhitespace()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var trimStartFn = (JSFunction)stringProto.Get("trimStart").AsObject();

        Assert.Equal("hello  ", trimStartFn.CallNative(JSValue.FromString("  hello  "), System.Array.Empty<JSValue>()).ToString());
    }

    [Fact]
    public void StringPrototype_TrimEnd_RemovesTrailingWhitespace()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var trimEndFn = (JSFunction)stringProto.Get("trimEnd").AsObject();

        Assert.Equal("  hello", trimEndFn.CallNative(JSValue.FromString("  hello  "), System.Array.Empty<JSValue>()).ToString());
    }

    [Fact]
    public void StringPrototype_Split_DividesStringByDelimiter()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var splitFn = (JSFunction)stringProto.Get("split").AsObject();

        var result = splitFn.CallNative(JSValue.FromString("a,b,c"), new[] { JSValue.FromString(",") }).AsObject();
        Assert.Equal(3u, result.ArrayLength);
        Assert.Equal("a", result.Get(0).ToString());
        Assert.Equal("b", result.Get(1).ToString());
        Assert.Equal("c", result.Get(2).ToString());
    }

    [Fact]
    public void StringPrototype_Repeat_RepeatsString()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var repeatFn = (JSFunction)stringProto.Get("repeat").AsObject();

        Assert.Equal("abcabcabc", repeatFn.CallNative(JSValue.FromString("abc"), new[] { JSValue.FromInt32(3) }).ToString());
    }

    [Fact]
    public void StringPrototype_PadStart_PadsAtStart()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var padStartFn = (JSFunction)stringProto.Get("padStart").AsObject();

        Assert.Equal("00005", padStartFn.CallNative(JSValue.FromString("5"), new[] { JSValue.FromInt32(5), JSValue.FromString("0") }).ToString());
    }

    [Fact]
    public void StringPrototype_PadEnd_PadsAtEnd()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var padEndFn = (JSFunction)stringProto.Get("padEnd").AsObject();

        Assert.Equal("5----", padEndFn.CallNative(JSValue.FromString("5"), new[] { JSValue.FromInt32(5), JSValue.FromString("-") }).ToString());
    }

    [Fact]
    public void StringPrototype_At_ReturnsCharacterAtIndex()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var atFn = (JSFunction)stringProto.Get("at").AsObject();

        Assert.Equal("o", atFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromInt32(-1) }).ToString());
        Assert.Equal("h", atFn.CallNative(JSValue.FromString("hello"), new[] { JSValue.FromInt32(0) }).ToString());
    }

    [Fact]
    public void StringPrototype_Replace_ReplacesFirstOccurrence()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var replaceFn = (JSFunction)stringProto.Get("replace").AsObject();

        Assert.Equal("hallo world", replaceFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("e"), JSValue.FromString("a") }).ToString());
    }

    [Fact]
    public void StringPrototype_ReplaceAll_ReplacesAllOccurrences()
    {
        var stringProto = _context.GetClassPrototype(JSClassId.String)!;
        var replaceAllFn = (JSFunction)stringProto.Get("replaceAll").AsObject();

        // "hello world".replaceAll("l", "x") = "hexxo worxd"
        Assert.Equal("hexxo worxd", replaceAllFn.CallNative(JSValue.FromString("hello world"), new[] { JSValue.FromString("l"), JSValue.FromString("x") }).ToString());
    }
}
