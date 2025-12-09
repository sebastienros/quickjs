// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Represents the JavaScript <c>arguments</c> object.
/// Supports mapped (non-strict) and unmapped (strict) semantics.
/// </summary>
public sealed class JSArgumentsObject : JSObject
{
    private readonly bool _mapped;
    private readonly JSFunction? _callee;
    private readonly int _actualCount;
    private readonly int _formalCount;

    // For mapped arguments, alias CallFrame args
    private readonly CallFrame? _frame;

    // For unmapped arguments, store a copy
    private readonly JSValue[] _values;

    // Extra elements beyond current slots (for unmapped/mapped beyond formal count)
    private readonly Dictionary<uint, JSValue> _extra = new();

    /// <summary>Create a new arguments object.</summary>
    /// <param name="frame">Current call frame.</param>
    /// <param name="callee">Function object for callee.</param>
    /// <param name="mapped">True for mapped (non-strict) arguments.</param>
    public JSArgumentsObject(CallFrame frame, JSFunction callee, bool mapped)
        : base(null, mapped ? JSClassId.MappedArguments : JSClassId.Arguments)
    {
        _mapped = mapped;
        _callee = callee;
        _frame = frame;
        _actualCount = frame.ActualArgCount;
        _formalCount = frame.Function?.ArgCount ?? 0;

        if (!_mapped)
        {
            _values = new JSValue[_actualCount];
            for (int i = 0; i < _actualCount; i++)
            {
                _values[i] = frame.GetArg(i);
            }
        }
        else
        {
            _values = Array.Empty<JSValue>();
        }
    }

    private bool TryGetIndex(string name, out uint index)
    {
        if (uint.TryParse(name, out var idx))
        {
            index = idx;
            return true;
        }
        index = 0;
        return false;
    }

    /// <summary>Get a named property from the arguments object.</summary>
    public JSValue GetProperty(string name)
    {
        if (name == "length")
            return JSValue.FromInt32(_actualCount);

        if (name == "callee")
        {
            // Strict mode / unmapped: callee is undefined
            if (!_mapped)
                return JSValue.Undefined;
            return _callee != null ? JSValue.FromObject(_callee) : JSValue.Undefined;
        }

        if (TryGetIndex(name, out var index))
        {
            return GetElement(index);
        }

        return base.Get(name);
    }

    /// <summary>Set a named property on the arguments object.</summary>
    public bool SetProperty(string name, JSValue value)
    {
        if (name == "length")
            return true; // ignore writes for now

        if (name == "callee")
            return true; // ignore writes

        if (TryGetIndex(name, out var index))
        {
            SetElement(index, value);
            return true;
        }

        return base.Set(name, value);
    }

    /// <summary>Get an indexed element.</summary>
    public JSValue GetElement(uint index)
    {
        if (_mapped)
        {
            if (index < _formalCount)
            {
                return _frame?.GetArg((int)index) ?? JSValue.Undefined;
            }
            if (_extra.TryGetValue(index, out var v))
                return v;
            return JSValue.Undefined;
        }
        else
        {
            if (index < _values.Length)
                return _values[index];
            if (_extra.TryGetValue(index, out var v))
                return v;
            return JSValue.Undefined;
        }
    }

    /// <summary>Set an indexed element.</summary>
    public void SetElement(uint index, JSValue value)
    {
        if (_mapped)
        {
            if (index < _formalCount)
            {
                _frame?.SetArg((int)index, value);
                return;
            }
            _extra[index] = value;
        }
        else
        {
            if (index < _values.Length)
            {
                _values[index] = value;
                return;
            }
            _extra[index] = value;
        }
    }
}
