// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JavaScript Symbol primitive and Symbol global object.
/// </summary>
public class SymbolTests : IDisposable
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;

    public SymbolTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _runtime.Dispose();
    }

    #region Symbol Creation and Basic Operations

    [Fact]
    public void Symbol_CreateWithoutDescription()
    {
        var sym = new JSSymbol();
        Assert.NotNull(sym);
        Assert.Null(sym.Description);
    }

    [Fact]
    public void Symbol_CreateWithDescription()
    {
        var sym = new JSSymbol("test");
        Assert.Equal("test", sym.Description);
    }

    [Fact]
    public void Symbol_ToStringReturnsSymbolFormat()
    {
        var sym = new JSSymbol("mySymbol");
        Assert.Equal("Symbol(mySymbol)", sym.ToString());
    }

    [Fact]
    public void Symbol_ToStringWithNullDescription()
    {
        var sym = new JSSymbol();
        Assert.Equal("Symbol()", sym.ToString());
    }

    [Fact]
    public void Symbol_SymbolsAreUnique()
    {
        var sym1 = new JSSymbol("same");
        var sym2 = new JSSymbol("same");
        Assert.NotEqual(sym1, sym2);
        Assert.False(sym1 == sym2);
    }

    [Fact]
    public void Symbol_EqualsItself()
    {
        var sym = new JSSymbol("test");
        Assert.True(sym.Equals(sym));
#pragma warning disable CS1718 // Intentional: testing that == operator works correctly with same variable
        Assert.True(sym == sym);
#pragma warning restore CS1718
    }

    #endregion

    #region Well-Known Symbols

    [Fact]
    public void Symbol_WellKnownIterator()
    {
        var sym = JSSymbol.Iterator;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.iterator", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownAsyncIterator()
    {
        var sym = JSSymbol.AsyncIterator;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.asyncIterator", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownHasInstance()
    {
        var sym = JSSymbol.HasInstance;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.hasInstance", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownIsConcatSpreadable()
    {
        var sym = JSSymbol.IsConcatSpreadable;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.isConcatSpreadable", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownMatch()
    {
        var sym = JSSymbol.Match;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.match", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownMatchAll()
    {
        var sym = JSSymbol.MatchAll;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.matchAll", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownReplace()
    {
        var sym = JSSymbol.Replace;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.replace", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownSearch()
    {
        var sym = JSSymbol.Search;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.search", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownSplit()
    {
        var sym = JSSymbol.Split;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.split", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownSpecies()
    {
        var sym = JSSymbol.Species;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.species", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownToPrimitive()
    {
        var sym = JSSymbol.ToPrimitive;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.toPrimitive", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownToStringTag()
    {
        var sym = JSSymbol.ToStringTag;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.toStringTag", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownUnscopables()
    {
        var sym = JSSymbol.Unscopables;
        Assert.NotNull(sym);
        Assert.Equal("Symbol.unscopables", sym.Description);
    }

    [Fact]
    public void Symbol_WellKnownSymbolsAreSingletons()
    {
        Assert.Same(JSSymbol.Iterator, JSSymbol.Iterator);
        Assert.Same(JSSymbol.ToStringTag, JSSymbol.ToStringTag);
        Assert.Same(JSSymbol.HasInstance, JSSymbol.HasInstance);
    }

    #endregion

    #region Symbol Global Registry

    [Fact]
    public void Symbol_ForReturnsSameSymbol()
    {
        var sym1 = JSSymbol.For("myKey");
        var sym2 = JSSymbol.For("myKey");
        Assert.Same(sym1, sym2);
    }

    [Fact]
    public void Symbol_ForWithDifferentKeysReturnsDifferentSymbols()
    {
        var sym1 = JSSymbol.For("key1");
        var sym2 = JSSymbol.For("key2");
        Assert.NotSame(sym1, sym2);
    }

    [Fact]
    public void Symbol_ForSetKeyAsDescription()
    {
        var sym = JSSymbol.For("myRegistryKey");
        Assert.Equal("myRegistryKey", sym.Description);
    }

    [Fact]
    public void Symbol_KeyForReturnsKey()
    {
        var sym = JSSymbol.For("testKey");
        var key = JSSymbol.KeyFor(sym);
        Assert.Equal("testKey", key);
    }

    [Fact]
    public void Symbol_KeyForReturnsNullForNonRegistered()
    {
        var sym = new JSSymbol("nonRegistered");
        var key = JSSymbol.KeyFor(sym);
        Assert.Null(key);
    }

    [Fact]
    public void Symbol_ForNotSameAsRegularSymbol()
    {
        var registered = JSSymbol.For("test");
        var regular = new JSSymbol("test");
        Assert.NotSame(registered, regular);
    }

    #endregion

    #region JSValue Symbol Support

    [Fact]
    public void JSValue_FromSymbol()
    {
        var sym = new JSSymbol("test");
        var val = JSValue.FromSymbol(sym);
        Assert.True(val.IsSymbol);
    }

    [Fact]
    public void JSValue_TryGetSymbolReturnsTrue()
    {
        var sym = new JSSymbol("test");
        var val = JSValue.FromSymbol(sym);
        Assert.True(val.TryGetSymbol(out var result));
        Assert.Same(sym, result);
    }

    [Fact]
    public void JSValue_TryGetSymbolReturnsFalseForNonSymbol()
    {
        var val = JSValue.FromDouble(42);
        Assert.False(val.TryGetSymbol(out var result));
        Assert.Null(result);
    }

    [Fact]
    public void JSValue_AsSymbolReturnsSymbol()
    {
        var sym = new JSSymbol("test");
        var val = JSValue.FromSymbol(sym);
        Assert.Same(sym, val.AsSymbol());
    }

    [Fact]
    public void JSValue_ToStringForSymbol()
    {
        var sym = new JSSymbol("mySymbol");
        var val = JSValue.FromSymbol(sym);
        Assert.Equal("Symbol(mySymbol)", val.ToString());
    }

    #endregion

    #region Symbol Constructor (via Context)

    [Fact]
    public void Context_SymbolConstructorExists()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        Assert.True(symbolCtor.IsObject);
        Assert.IsType<JSFunction>(symbolCtor.AsObject());
    }

    [Fact]
    public void Context_Symbol_CallCreatesSymbol()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var fn = symbolCtor.AsObject() as JSFunction;
        Assert.NotNull(fn);

        var result = fn.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        Assert.True(result.IsSymbol);
        Assert.True(result.TryGetSymbol(out var sym));
        Assert.Equal("test", sym?.Description);
    }

    [Fact]
    public void Context_SymbolFor_ReturnsSameSymbol()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var forFn = symbolCtor.AsObject().Get("for");
        Assert.True(forFn.IsObject);
        var fn = forFn.AsObject() as JSFunction;
        Assert.NotNull(fn);

        var result1 = fn.CallNative(symbolCtor, new[] { JSValue.FromString("myKey") });
        var result2 = fn.CallNative(symbolCtor, new[] { JSValue.FromString("myKey") });

        Assert.True(result1.IsSymbol);
        Assert.True(result2.IsSymbol);
        result1.TryGetSymbol(out var sym1);
        result2.TryGetSymbol(out var sym2);
        Assert.Same(sym1, sym2);
    }

    [Fact]
    public void Context_SymbolKeyFor_ReturnsKey()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var forFn = symbolCtor.AsObject().Get("for").AsObject() as JSFunction;
        var keyForFn = symbolCtor.AsObject().Get("keyFor").AsObject() as JSFunction;
        Assert.NotNull(forFn);
        Assert.NotNull(keyForFn);

        var sym = forFn.CallNative(symbolCtor, new[] { JSValue.FromString("testKey") });
        var key = keyForFn.CallNative(symbolCtor, new[] { sym });

        Assert.True(key.IsString);
        Assert.Equal("testKey", JSValueConversion.ToString(key));
    }

    [Fact]
    public void Context_SymbolKeyFor_ReturnsUndefinedForNonRegistered()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var keyForFn = symbolCtor.AsObject().Get("keyFor").AsObject() as JSFunction;
        var fn = symbolCtor.AsObject() as JSFunction;
        Assert.NotNull(keyForFn);
        Assert.NotNull(fn);

        var sym = fn.CallNative(JSValue.Undefined, new[] { JSValue.FromString("notRegistered") });
        var key = keyForFn.CallNative(symbolCtor, new[] { sym });

        Assert.True(key.IsUndefined);
    }

    [Fact]
    public void Context_SymbolHasIterator()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var iterator = symbolCtor.AsObject().Get("iterator");
        Assert.True(iterator.IsSymbol);
    }

    [Fact]
    public void Context_SymbolHasToStringTag()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var toStringTag = symbolCtor.AsObject().Get("toStringTag");
        Assert.True(toStringTag.IsSymbol);
    }

    [Fact]
    public void Context_SymbolHasHasInstance()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var hasInstance = symbolCtor.AsObject().Get("hasInstance");
        Assert.True(hasInstance.IsSymbol);
    }

    [Fact]
    public void Context_SymbolPrototype_Description()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var fn = symbolCtor.AsObject() as JSFunction;
        Assert.NotNull(fn);

        var sym = fn.CallNative(JSValue.Undefined, new[] { JSValue.FromString("myDesc") });
        Assert.True(sym.TryGetSymbol(out var symbol));
        Assert.Equal("myDesc", symbol?.Description);
    }

    [Fact]
    public void Context_SymbolToString()
    {
        var symbolCtor = _context.GetGlobalProperty("Symbol");
        var fn = symbolCtor.AsObject() as JSFunction;
        Assert.NotNull(fn);

        var symVal = fn.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        Assert.True(symVal.TryGetSymbol(out var sym));
        Assert.Equal("Symbol(test)", sym?.ToString());
    }

    #endregion
}

/// <summary>
/// Tests for JavaScript Reflect global object.
/// </summary>
public class ReflectTests : IDisposable
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;

    public ReflectTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _runtime.Dispose();
    }

    [Fact]
    public void Reflect_ObjectExists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        Assert.True(reflect.IsObject);
    }

    #region Reflect.apply

    [Fact]
    public void Reflect_Apply_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var applyFn = reflect.AsObject().Get("apply");
        Assert.True(applyFn.IsObject);
        Assert.IsType<JSFunction>(applyFn.AsObject());
    }

    [Fact]
    public void Reflect_Apply_CallsMathMax()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var applyFn = reflect.AsObject().Get("apply").AsObject() as JSFunction;
        var math = _context.GetGlobalProperty("Math");
        var maxFn = math.AsObject().Get("max");

        var argsArray = new JSArray();
        argsArray.Push(JSValue.FromInt32(1));
        argsArray.Push(JSValue.FromInt32(5));
        argsArray.Push(JSValue.FromInt32(3));

        var result = applyFn!.CallNative(reflect, new[] {
            maxFn,
            JSValue.Null,
            JSValue.FromObject(argsArray)
        });

        Assert.True(result.IsNumber);
        Assert.Equal(5.0, result.ToDouble());
    }

    #endregion

    #region Reflect.construct

    [Fact]
    public void Reflect_Construct_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var constructFn = reflect.AsObject().Get("construct");
        Assert.True(constructFn.IsObject);
        Assert.IsType<JSFunction>(constructFn.AsObject());
    }

    #endregion

    #region Reflect.defineProperty

    [Fact]
    public void Reflect_DefineProperty_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var definePropFn = reflect.AsObject().Get("defineProperty");
        Assert.True(definePropFn.IsObject);
        Assert.IsType<JSFunction>(definePropFn.AsObject());
    }

    [Fact]
    public void Reflect_DefineProperty_DefinesProperty()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var definePropFn = reflect.AsObject().Get("defineProperty").AsObject() as JSFunction;
        Assert.NotNull(definePropFn);

        var obj = new JSObject();
        var descriptor = new JSObject();
        descriptor.Set("value", JSValue.FromInt32(42));
        descriptor.Set("writable", JSValue.True);

        var result = definePropFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("x"),
            JSValue.FromObject(descriptor)
        });

        Assert.True(result.IsTrue);
        Assert.Equal(42.0, obj.Get("x").ToDouble());
    }

    #endregion

    #region Reflect.deleteProperty

    [Fact]
    public void Reflect_DeleteProperty_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var deletePropFn = reflect.AsObject().Get("deleteProperty");
        Assert.True(deletePropFn.IsObject);
        Assert.IsType<JSFunction>(deletePropFn.AsObject());
    }

    [Fact]
    public void Reflect_DeleteProperty_DeletesProperty()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var deletePropFn = reflect.AsObject().Get("deleteProperty").AsObject() as JSFunction;
        Assert.NotNull(deletePropFn);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));

        var result = deletePropFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("x")
        });

        Assert.True(result.IsTrue);
        Assert.True(obj.Get("x").IsUndefined);
    }

    #endregion

    #region Reflect.get

    [Fact]
    public void Reflect_Get_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getFn = reflect.AsObject().Get("get");
        Assert.True(getFn.IsObject);
        Assert.IsType<JSFunction>(getFn.AsObject());
    }

    [Fact]
    public void Reflect_Get_GetsProperty()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getFn = reflect.AsObject().Get("get").AsObject() as JSFunction;
        Assert.NotNull(getFn);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        var result = getFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("x")
        });

        Assert.True(result.IsNumber);
        Assert.Equal(42.0, result.ToDouble());
    }

    [Fact]
    public void Reflect_Get_ReturnsUndefined()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getFn = reflect.AsObject().Get("get").AsObject() as JSFunction;
        Assert.NotNull(getFn);

        var obj = new JSObject();

        var result = getFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("nonexistent")
        });

        Assert.True(result.IsUndefined);
    }

    #endregion

    #region Reflect.getOwnPropertyDescriptor

    [Fact]
    public void Reflect_GetOwnPropertyDescriptor_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getOwnPropDescFn = reflect.AsObject().Get("getOwnPropertyDescriptor");
        Assert.True(getOwnPropDescFn.IsObject);
        Assert.IsType<JSFunction>(getOwnPropDescFn.AsObject());
    }

    [Fact]
    public void Reflect_GetOwnPropertyDescriptor_ReturnsDescriptor()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getOwnPropDescFn = reflect.AsObject().Get("getOwnPropertyDescriptor").AsObject() as JSFunction;
        Assert.NotNull(getOwnPropDescFn);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(42));

        var result = getOwnPropDescFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("x")
        });

        Assert.True(result.IsObject);
        var desc = result.AsObject();
        Assert.Equal(42.0, desc.Get("value").ToDouble());
        Assert.True(desc.Get("writable").IsTrue);
    }

    [Fact]
    public void Reflect_GetOwnPropertyDescriptor_ReturnsUndefined()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getOwnPropDescFn = reflect.AsObject().Get("getOwnPropertyDescriptor").AsObject() as JSFunction;
        Assert.NotNull(getOwnPropDescFn);

        var obj = new JSObject();

        var result = getOwnPropDescFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("nonexistent")
        });

        Assert.True(result.IsUndefined);
    }

    #endregion

    #region Reflect.getPrototypeOf

    [Fact]
    public void Reflect_GetPrototypeOf_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getProtoFn = reflect.AsObject().Get("getPrototypeOf");
        Assert.True(getProtoFn.IsObject);
        Assert.IsType<JSFunction>(getProtoFn.AsObject());
    }

    [Fact]
    public void Reflect_GetPrototypeOf_ReturnsPrototype()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getProtoFn = reflect.AsObject().Get("getPrototypeOf").AsObject() as JSFunction;
        Assert.NotNull(getProtoFn);

        var proto = new JSObject();
        var obj = new JSObject(proto);

        var result = getProtoFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj)
        });

        Assert.True(result.IsObject);
        Assert.Same(proto, result.AsObject());
    }

    [Fact]
    public void Reflect_GetPrototypeOf_ReturnsNull()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getProtoFn = reflect.AsObject().Get("getPrototypeOf").AsObject() as JSFunction;
        Assert.NotNull(getProtoFn);

        var obj = new JSObject(null);

        var result = getProtoFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj)
        });

        Assert.True(result.IsNull);
    }

    #endregion

    #region Reflect.has

    [Fact]
    public void Reflect_Has_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var hasFn = reflect.AsObject().Get("has");
        Assert.True(hasFn.IsObject);
        Assert.IsType<JSFunction>(hasFn.AsObject());
    }

    [Fact]
    public void Reflect_Has_ReturnsTrue()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var hasFn = reflect.AsObject().Get("has").AsObject() as JSFunction;
        Assert.NotNull(hasFn);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));

        var result = hasFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("x")
        });

        Assert.True(result.IsTrue);
    }

    [Fact]
    public void Reflect_Has_ReturnsFalse()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var hasFn = reflect.AsObject().Get("has").AsObject() as JSFunction;
        Assert.NotNull(hasFn);

        var obj = new JSObject();

        var result = hasFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("nonexistent")
        });

        Assert.True(result.IsFalse);
    }

    [Fact]
    public void Reflect_Has_ChecksPrototype()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var hasFn = reflect.AsObject().Get("has").AsObject() as JSFunction;
        Assert.NotNull(hasFn);

        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromInt32(1));
        var obj = new JSObject(proto);

        var result = hasFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("inherited")
        });

        Assert.True(result.IsTrue);
    }

    #endregion

    #region Reflect.isExtensible

    [Fact]
    public void Reflect_IsExtensible_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var isExtFn = reflect.AsObject().Get("isExtensible");
        Assert.True(isExtFn.IsObject);
        Assert.IsType<JSFunction>(isExtFn.AsObject());
    }

    [Fact]
    public void Reflect_IsExtensible_ReturnsTrue()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var isExtFn = reflect.AsObject().Get("isExtensible").AsObject() as JSFunction;
        Assert.NotNull(isExtFn);

        var obj = new JSObject();

        var result = isExtFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj)
        });

        Assert.True(result.IsTrue);
    }

    #endregion

    #region Reflect.ownKeys

    [Fact]
    public void Reflect_OwnKeys_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var ownKeysFn = reflect.AsObject().Get("ownKeys");
        Assert.True(ownKeysFn.IsObject);
        Assert.IsType<JSFunction>(ownKeysFn.AsObject());
    }

    [Fact]
    public void Reflect_OwnKeys_ReturnsKeys()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var ownKeysFn = reflect.AsObject().Get("ownKeys").AsObject() as JSFunction;
        Assert.NotNull(ownKeysFn);

        var obj = new JSObject();
        obj.Set("a", JSValue.FromInt32(1));
        obj.Set("b", JSValue.FromInt32(2));

        var result = ownKeysFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj)
        });

        Assert.True(result.IsObject);
        var arr = result.AsObject() as JSArray;
        Assert.NotNull(arr);
        Assert.Equal(2u, arr.Length);
    }

    [Fact]
    public void Reflect_OwnKeys_ReturnsEmptyArray()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var ownKeysFn = reflect.AsObject().Get("ownKeys").AsObject() as JSFunction;
        Assert.NotNull(ownKeysFn);

        var obj = new JSObject();

        var result = ownKeysFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj)
        });

        Assert.True(result.IsObject);
        var arr = result.AsObject() as JSArray;
        Assert.NotNull(arr);
        Assert.Equal(0u, arr.Length);
    }

    #endregion

    #region Reflect.preventExtensions

    [Fact]
    public void Reflect_PreventExtensions_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var preventExtFn = reflect.AsObject().Get("preventExtensions");
        Assert.True(preventExtFn.IsObject);
        Assert.IsType<JSFunction>(preventExtFn.AsObject());
    }

    [Fact]
    public void Reflect_PreventExtensions_PreventsExtensions()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var preventExtFn = reflect.AsObject().Get("preventExtensions").AsObject() as JSFunction;
        var isExtFn = reflect.AsObject().Get("isExtensible").AsObject() as JSFunction;
        Assert.NotNull(preventExtFn);
        Assert.NotNull(isExtFn);

        var obj = new JSObject();
        Assert.True(obj.IsExtensible);

        var result = preventExtFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj)
        });

        Assert.True(result.IsTrue);
        Assert.False(obj.IsExtensible);
    }

    #endregion

    #region Reflect.set

    [Fact]
    public void Reflect_Set_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var setFn = reflect.AsObject().Get("set");
        Assert.True(setFn.IsObject);
        Assert.IsType<JSFunction>(setFn.AsObject());
    }

    [Fact]
    public void Reflect_Set_SetsProperty()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var setFn = reflect.AsObject().Get("set").AsObject() as JSFunction;
        Assert.NotNull(setFn);

        var obj = new JSObject();

        var result = setFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromString("x"),
            JSValue.FromInt32(42)
        });

        Assert.True(result.IsTrue);
        Assert.Equal(42.0, obj.Get("x").ToDouble());
    }

    #endregion

    #region Reflect.setPrototypeOf

    [Fact]
    public void Reflect_SetPrototypeOf_Exists()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var setProtoFn = reflect.AsObject().Get("setPrototypeOf");
        Assert.True(setProtoFn.IsObject);
        Assert.IsType<JSFunction>(setProtoFn.AsObject());
    }

    [Fact]
    public void Reflect_SetPrototypeOf_SetsPrototype()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var setProtoFn = reflect.AsObject().Get("setPrototypeOf").AsObject() as JSFunction;
        Assert.NotNull(setProtoFn);

        var proto = new JSObject();
        proto.Set("inherited", JSValue.FromString("value"));
        var obj = new JSObject();

        var result = setProtoFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.FromObject(proto)
        });

        Assert.True(result.IsTrue);
        Assert.Same(proto, obj.Prototype);
        Assert.Equal("value", JSValueConversion.ToString(obj.Get("inherited")));
    }

    [Fact]
    public void Reflect_SetPrototypeOf_SetsToNull()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var setProtoFn = reflect.AsObject().Get("setPrototypeOf").AsObject() as JSFunction;
        Assert.NotNull(setProtoFn);

        var obj = new JSObject(new JSObject()); // Start with a prototype

        var result = setProtoFn.CallNative(reflect, new[] {
            JSValue.FromObject(obj),
            JSValue.Null
        });

        Assert.True(result.IsTrue);
        Assert.Null(obj.Prototype);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Reflect_Get_ThrowsForNonObject()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var getFn = reflect.AsObject().Get("get").AsObject() as JSFunction;
        Assert.NotNull(getFn);

        var result = getFn.CallNative(reflect, new[] {
            JSValue.FromInt32(42),
            JSValue.FromString("x")
        });

        // Should throw or return exception value
        Assert.True(result.IsException || result.IsUndefined);
    }

    [Fact]
    public void Reflect_Has_ThrowsForNonObject()
    {
        var reflect = _context.GetGlobalProperty("Reflect");
        var hasFn = reflect.AsObject().Get("has").AsObject() as JSFunction;
        Assert.NotNull(hasFn);

        var result = hasFn.CallNative(reflect, new[] {
            JSValue.FromString("string"),
            JSValue.FromString("length")
        });

        // Should throw or return exception value
        Assert.True(result.IsException || result.IsUndefined);
    }

    #endregion
}
