// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace QuickJS;

/// <summary>
/// Provides metadata for all QuickJS bytecode opcodes.
/// </summary>
/// <remarks>
/// This corresponds to the opcode_info[] array in QuickJS.
/// Each entry describes the opcode's size, stack effects, and operand format.
/// </remarks>
public static class OpCodes
{
    private static readonly OpCodeInfo[] _info;
    private static readonly Dictionary<string, OpCodeInfo> _byName;

    static OpCodes()
    {
        // Initialize the opcode info array
        // Format: OpCode, Name, Size, Pop, Push, Format, IsTemporary
        var infos = new OpCodeInfo[]
        {
            // Invalid
            new(OpCode.Invalid, "invalid", 1, 0, 0, OpCodeFormat.None),

            // Push values
            new(OpCode.PushI32, "push_i32", 5, 0, 1, OpCodeFormat.I32),
            new(OpCode.PushConst, "push_const", 5, 0, 1, OpCodeFormat.Const),
            new(OpCode.FClosure, "fclosure", 5, 0, 1, OpCodeFormat.Const),
            new(OpCode.PushAtomValue, "push_atom_value", 5, 0, 1, OpCodeFormat.Atom),
            new(OpCode.PrivateSymbol, "private_symbol", 5, 0, 1, OpCodeFormat.Atom),
            new(OpCode.Undefined, "undefined", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.Null, "null", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.PushThis, "push_this", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.PushFalse, "push_false", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.PushTrue, "push_true", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.Object, "object", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.SpecialObject, "special_object", 2, 0, 1, OpCodeFormat.U8),
            new(OpCode.Rest, "rest", 3, 0, 1, OpCodeFormat.U16),

            // Stack manipulation
            new(OpCode.Drop, "drop", 1, 1, 0, OpCodeFormat.None),
            new(OpCode.Nip, "nip", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Nip1, "nip1", 1, 3, 2, OpCodeFormat.None),
            new(OpCode.Dup, "dup", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.Dup1, "dup1", 1, 2, 3, OpCodeFormat.None),
            new(OpCode.Dup2, "dup2", 1, 2, 4, OpCodeFormat.None),
            new(OpCode.Dup3, "dup3", 1, 3, 6, OpCodeFormat.None),
            new(OpCode.Insert2, "insert2", 1, 2, 3, OpCodeFormat.None),
            new(OpCode.Insert3, "insert3", 1, 3, 4, OpCodeFormat.None),
            new(OpCode.Insert4, "insert4", 1, 4, 5, OpCodeFormat.None),
            new(OpCode.Perm3, "perm3", 1, 3, 3, OpCodeFormat.None),
            new(OpCode.Perm4, "perm4", 1, 4, 4, OpCodeFormat.None),
            new(OpCode.Perm5, "perm5", 1, 5, 5, OpCodeFormat.None),
            new(OpCode.Swap, "swap", 1, 2, 2, OpCodeFormat.None),
            new(OpCode.Swap2, "swap2", 1, 4, 4, OpCodeFormat.None),
            new(OpCode.Rot3L, "rot3l", 1, 3, 3, OpCodeFormat.None),
            new(OpCode.Rot3R, "rot3r", 1, 3, 3, OpCodeFormat.None),
            new(OpCode.Rot4L, "rot4l", 1, 4, 4, OpCodeFormat.None),
            new(OpCode.Rot5L, "rot5l", 1, 5, 5, OpCodeFormat.None),

            // Function calls
            new(OpCode.CallConstructor, "call_constructor", 3, 2, 1, OpCodeFormat.NPop),
            new(OpCode.Call, "call", 3, 1, 1, OpCodeFormat.NPop),
            new(OpCode.TailCall, "tail_call", 3, 1, 0, OpCodeFormat.NPop),
            new(OpCode.CallMethod, "call_method", 3, 2, 1, OpCodeFormat.NPop),
            new(OpCode.TailCallMethod, "tail_call_method", 3, 2, 0, OpCodeFormat.NPop),
            new(OpCode.ArrayFrom, "array_from", 3, 0, 1, OpCodeFormat.NPop),
            new(OpCode.Apply, "apply", 3, 3, 1, OpCodeFormat.U16),
            new(OpCode.Return, "return", 1, 1, 0, OpCodeFormat.None),
            new(OpCode.ReturnUndef, "return_undef", 1, 0, 0, OpCodeFormat.None),
            new(OpCode.CheckCtorReturn, "check_ctor_return", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.CheckCtor, "check_ctor", 1, 0, 0, OpCodeFormat.None),
            new(OpCode.InitCtor, "init_ctor", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.CheckBrand, "check_brand", 1, 2, 2, OpCodeFormat.None),
            new(OpCode.AddBrand, "add_brand", 1, 2, 0, OpCodeFormat.None),
            new(OpCode.ReturnAsync, "return_async", 1, 1, 0, OpCodeFormat.None),
            new(OpCode.Throw, "throw", 1, 1, 0, OpCodeFormat.None),
            new(OpCode.ThrowError, "throw_error", 6, 0, 0, OpCodeFormat.AtomU8),
            new(OpCode.Eval, "eval", 5, 1, 1, OpCodeFormat.NPopU16),
            new(OpCode.ApplyEval, "apply_eval", 3, 2, 1, OpCodeFormat.U16),
            new(OpCode.RegExp, "regexp", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.GetSuper, "get_super", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.Import, "import", 1, 2, 1, OpCodeFormat.None),

            // Variable access
            new(OpCode.GetVarUndef, "get_var_undef", 3, 0, 1, OpCodeFormat.VarRef),
            new(OpCode.GetVar, "get_var", 3, 0, 1, OpCodeFormat.VarRef),
            new(OpCode.PutVar, "put_var", 3, 1, 0, OpCodeFormat.VarRef),
            new(OpCode.PutVarInit, "put_var_init", 3, 1, 0, OpCodeFormat.VarRef),
            new(OpCode.GetRefValue, "get_ref_value", 1, 2, 3, OpCodeFormat.None),
            new(OpCode.PutRefValue, "put_ref_value", 1, 3, 0, OpCodeFormat.None),

            // Property access
            new(OpCode.GetField, "get_field", 5, 1, 1, OpCodeFormat.Atom),
            new(OpCode.GetField2, "get_field2", 5, 1, 2, OpCodeFormat.Atom),
            new(OpCode.PutField, "put_field", 5, 2, 0, OpCodeFormat.Atom),
            new(OpCode.GetPrivateField, "get_private_field", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.PutPrivateField, "put_private_field", 1, 3, 0, OpCodeFormat.None),
            new(OpCode.DefinePrivateField, "define_private_field", 1, 3, 1, OpCodeFormat.None),
            new(OpCode.GetArrayEl, "get_array_el", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.GetArrayEl2, "get_array_el2", 1, 2, 2, OpCodeFormat.None),
            new(OpCode.GetArrayEl3, "get_array_el3", 1, 2, 3, OpCodeFormat.None),
            new(OpCode.PutArrayEl, "put_array_el", 1, 3, 0, OpCodeFormat.None),
            new(OpCode.GetSuperValue, "get_super_value", 1, 3, 1, OpCodeFormat.None),
            new(OpCode.PutSuperValue, "put_super_value", 1, 4, 0, OpCodeFormat.None),
            new(OpCode.DefineField, "define_field", 5, 2, 1, OpCodeFormat.Atom),
            new(OpCode.SetName, "set_name", 5, 1, 1, OpCodeFormat.Atom),
            new(OpCode.SetNameComputed, "set_name_computed", 1, 2, 2, OpCodeFormat.None),
            new(OpCode.SetProto, "set_proto", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.SetHomeObject, "set_home_object", 1, 2, 2, OpCodeFormat.None),
            new(OpCode.DefineArrayEl, "define_array_el", 1, 3, 2, OpCodeFormat.None),
            new(OpCode.Append, "append", 1, 3, 2, OpCodeFormat.None),
            new(OpCode.CopyDataProperties, "copy_data_properties", 2, 3, 3, OpCodeFormat.U8),
            new(OpCode.DefineMethod, "define_method", 6, 2, 1, OpCodeFormat.AtomU8),
            new(OpCode.DefineMethodComputed, "define_method_computed", 2, 3, 1, OpCodeFormat.U8),
            new(OpCode.DefineClass, "define_class", 6, 2, 2, OpCodeFormat.AtomU8),
            new(OpCode.DefineClassComputed, "define_class_computed", 6, 3, 3, OpCodeFormat.AtomU8),

            // Local/argument/closure variable access
            new(OpCode.GetLoc, "get_loc", 3, 0, 1, OpCodeFormat.Loc),
            new(OpCode.PutLoc, "put_loc", 3, 1, 0, OpCodeFormat.Loc),
            new(OpCode.SetLoc, "set_loc", 3, 1, 1, OpCodeFormat.Loc),
            new(OpCode.GetArg, "get_arg", 3, 0, 1, OpCodeFormat.Arg),
            new(OpCode.PutArg, "put_arg", 3, 1, 0, OpCodeFormat.Arg),
            new(OpCode.SetArg, "set_arg", 3, 1, 1, OpCodeFormat.Arg),
            new(OpCode.GetVarRef, "get_var_ref", 3, 0, 1, OpCodeFormat.VarRef),
            new(OpCode.PutVarRef, "put_var_ref", 3, 1, 0, OpCodeFormat.VarRef),
            new(OpCode.SetVarRef, "set_var_ref", 3, 1, 1, OpCodeFormat.VarRef),
            new(OpCode.SetLocUninitialized, "set_loc_uninitialized", 3, 0, 0, OpCodeFormat.Loc),
            new(OpCode.GetLocCheck, "get_loc_check", 3, 0, 1, OpCodeFormat.Loc),
            new(OpCode.PutLocCheck, "put_loc_check", 3, 1, 0, OpCodeFormat.Loc),
            new(OpCode.PutLocCheckInit, "put_loc_check_init", 3, 1, 0, OpCodeFormat.Loc),
            new(OpCode.GetLocCheckThis, "get_loc_checkthis", 3, 0, 1, OpCodeFormat.Loc),
            new(OpCode.GetVarRefCheck, "get_var_ref_check", 3, 0, 1, OpCodeFormat.VarRef),
            new(OpCode.PutVarRefCheck, "put_var_ref_check", 3, 1, 0, OpCodeFormat.VarRef),
            new(OpCode.PutVarRefCheckInit, "put_var_ref_check_init", 3, 1, 0, OpCodeFormat.VarRef),
            new(OpCode.CloseLoc, "close_loc", 3, 0, 0, OpCodeFormat.Loc),

            // Control flow
            new(OpCode.IfFalse, "if_false", 5, 1, 0, OpCodeFormat.Label),
            new(OpCode.IfTrue, "if_true", 5, 1, 0, OpCodeFormat.Label),
            new(OpCode.Goto, "goto", 5, 0, 0, OpCodeFormat.Label),
            new(OpCode.Catch, "catch", 5, 0, 1, OpCodeFormat.Label),
            new(OpCode.GoSub, "gosub", 5, 0, 0, OpCodeFormat.Label),
            new(OpCode.Ret, "ret", 1, 1, 0, OpCodeFormat.None),
            new(OpCode.NipCatch, "nip_catch", 1, 2, 1, OpCodeFormat.None),

            // Type conversion
            new(OpCode.ToObject, "to_object", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.ToPropKey, "to_propkey", 1, 1, 1, OpCodeFormat.None),

            // With statement
            new(OpCode.WithGetVar, "with_get_var", 10, 1, 0, OpCodeFormat.AtomLabelU8),
            new(OpCode.WithPutVar, "with_put_var", 10, 2, 1, OpCodeFormat.AtomLabelU8),
            new(OpCode.WithDeleteVar, "with_delete_var", 10, 1, 0, OpCodeFormat.AtomLabelU8),
            new(OpCode.WithMakeRef, "with_make_ref", 10, 1, 0, OpCodeFormat.AtomLabelU8),
            new(OpCode.WithGetRef, "with_get_ref", 10, 1, 0, OpCodeFormat.AtomLabelU8),

            // Reference creation
            new(OpCode.MakeLocRef, "make_loc_ref", 7, 0, 2, OpCodeFormat.AtomU16),
            new(OpCode.MakeArgRef, "make_arg_ref", 7, 0, 2, OpCodeFormat.AtomU16),
            new(OpCode.MakeVarRefRef, "make_var_ref_ref", 7, 0, 2, OpCodeFormat.AtomU16),
            new(OpCode.MakeVarRef, "make_var_ref", 5, 0, 2, OpCodeFormat.Atom),

            // Iteration
            new(OpCode.ForInStart, "for_in_start", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.ForOfStart, "for_of_start", 1, 1, 3, OpCodeFormat.None),
            new(OpCode.ForAwaitOfStart, "for_await_of_start", 1, 1, 3, OpCodeFormat.None),
            new(OpCode.ForInNext, "for_in_next", 1, 1, 3, OpCodeFormat.None),
            new(OpCode.ForOfNext, "for_of_next", 2, 3, 5, OpCodeFormat.U8),
            new(OpCode.ForAwaitOfNext, "for_await_of_next", 1, 3, 4, OpCodeFormat.None),
            new(OpCode.IteratorCheckObject, "iterator_check_object", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.IteratorGetValueDone, "iterator_get_value_done", 1, 2, 3, OpCodeFormat.None),
            new(OpCode.IteratorClose, "iterator_close", 1, 3, 0, OpCodeFormat.None),
            new(OpCode.IteratorNext, "iterator_next", 1, 4, 4, OpCodeFormat.None),
            new(OpCode.IteratorCall, "iterator_call", 2, 4, 5, OpCodeFormat.U8),
            new(OpCode.InitialYield, "initial_yield", 1, 0, 0, OpCodeFormat.None),
            new(OpCode.Yield, "yield", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.YieldStar, "yield_star", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.AsyncYieldStar, "async_yield_star", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.Await, "await", 1, 1, 1, OpCodeFormat.None),

            // Unary operations
            new(OpCode.Neg, "neg", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.Plus, "plus", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.Dec, "dec", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.Inc, "inc", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.PostDec, "post_dec", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.PostInc, "post_inc", 1, 1, 2, OpCodeFormat.None),
            new(OpCode.DecLoc, "dec_loc", 2, 0, 0, OpCodeFormat.Loc8),
            new(OpCode.IncLoc, "inc_loc", 2, 0, 0, OpCodeFormat.Loc8),
            new(OpCode.AddLoc, "add_loc", 2, 1, 0, OpCodeFormat.Loc8),
            new(OpCode.Not, "not", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.LNot, "lnot", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.TypeOf, "typeof", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.Delete, "delete", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.DeleteVar, "delete_var", 5, 0, 1, OpCodeFormat.Atom),

            // Binary operations
            new(OpCode.Mul, "mul", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Div, "div", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Mod, "mod", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Add, "add", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Sub, "sub", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Pow, "pow", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Shl, "shl", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Sar, "sar", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Shr, "shr", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Lt, "lt", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Lte, "lte", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Gt, "gt", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Gte, "gte", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.InstanceOf, "instanceof", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.In, "in", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Eq, "eq", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Neq, "neq", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.StrictEq, "strict_eq", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.StrictNeq, "strict_neq", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.And, "and", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Xor, "xor", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.Or, "or", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.IsUndefinedOrNull, "is_undefined_or_null", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.PrivateIn, "private_in", 1, 2, 1, OpCodeFormat.None),
            new(OpCode.PushBigIntI32, "push_bigint_i32", 5, 0, 1, OpCodeFormat.I32),
            new(OpCode.Nop, "nop", 1, 0, 0, OpCodeFormat.None),

            // Temporary opcodes (removed during compilation)
            new(OpCode.EnterScope, "enter_scope", 3, 0, 0, OpCodeFormat.U16, isTemporary: true),
            new(OpCode.LeaveScope, "leave_scope", 3, 0, 0, OpCodeFormat.U16, isTemporary: true),
            new(OpCode.Label, "label", 5, 0, 0, OpCodeFormat.Label, isTemporary: true),
            new(OpCode.ScopeGetVarUndef, "scope_get_var_undef", 7, 0, 1, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeGetVar, "scope_get_var", 7, 0, 1, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopePutVar, "scope_put_var", 7, 1, 0, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeDeleteVar, "scope_delete_var", 7, 0, 1, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeMakeRef, "scope_make_ref", 11, 0, 2, OpCodeFormat.AtomLabelU16, isTemporary: true),
            new(OpCode.ScopeGetRef, "scope_get_ref", 7, 0, 2, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopePutVarInit, "scope_put_var_init", 7, 0, 2, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeGetVarCheckThis, "scope_get_var_checkthis", 7, 0, 1, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeGetPrivateField, "scope_get_private_field", 7, 1, 1, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeGetPrivateField2, "scope_get_private_field2", 7, 1, 2, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopePutPrivateField, "scope_put_private_field", 7, 2, 0, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.ScopeInPrivateField, "scope_in_private_field", 7, 1, 1, OpCodeFormat.AtomU16, isTemporary: true),
            new(OpCode.GetFieldOptChain, "get_field_opt_chain", 5, 1, 1, OpCodeFormat.Atom, isTemporary: true),
            new(OpCode.GetArrayElOptChain, "get_array_el_opt_chain", 1, 2, 1, OpCodeFormat.None, isTemporary: true),
            new(OpCode.SetClassName, "set_class_name", 5, 1, 1, OpCodeFormat.U32, isTemporary: true),
            new(OpCode.LineNum, "line_num", 5, 0, 0, OpCodeFormat.U32, isTemporary: true),

            // Short opcodes (optimizations)
            new(OpCode.PushMinus1, "push_minus1", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push0, "push_0", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push1, "push_1", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push2, "push_2", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push3, "push_3", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push4, "push_4", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push5, "push_5", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push6, "push_6", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.Push7, "push_7", 1, 0, 1, OpCodeFormat.NoneInt),
            new(OpCode.PushI8, "push_i8", 2, 0, 1, OpCodeFormat.I8),
            new(OpCode.PushI16, "push_i16", 3, 0, 1, OpCodeFormat.I16),
            new(OpCode.PushConst8, "push_const8", 2, 0, 1, OpCodeFormat.Const8),
            new(OpCode.FClosure8, "fclosure8", 2, 0, 1, OpCodeFormat.Const8),
            new(OpCode.PushEmptyString, "push_empty_string", 1, 0, 1, OpCodeFormat.None),
            new(OpCode.GetLoc8, "get_loc8", 2, 0, 1, OpCodeFormat.Loc8),
            new(OpCode.PutLoc8, "put_loc8", 2, 1, 0, OpCodeFormat.Loc8),
            new(OpCode.SetLoc8, "set_loc8", 2, 1, 1, OpCodeFormat.Loc8),
            new(OpCode.GetLoc0, "get_loc0", 1, 0, 1, OpCodeFormat.NoneLoc),
            new(OpCode.GetLoc1, "get_loc1", 1, 0, 1, OpCodeFormat.NoneLoc),
            new(OpCode.GetLoc2, "get_loc2", 1, 0, 1, OpCodeFormat.NoneLoc),
            new(OpCode.GetLoc3, "get_loc3", 1, 0, 1, OpCodeFormat.NoneLoc),
            new(OpCode.PutLoc0, "put_loc0", 1, 1, 0, OpCodeFormat.NoneLoc),
            new(OpCode.PutLoc1, "put_loc1", 1, 1, 0, OpCodeFormat.NoneLoc),
            new(OpCode.PutLoc2, "put_loc2", 1, 1, 0, OpCodeFormat.NoneLoc),
            new(OpCode.PutLoc3, "put_loc3", 1, 1, 0, OpCodeFormat.NoneLoc),
            new(OpCode.SetLoc0, "set_loc0", 1, 1, 1, OpCodeFormat.NoneLoc),
            new(OpCode.SetLoc1, "set_loc1", 1, 1, 1, OpCodeFormat.NoneLoc),
            new(OpCode.SetLoc2, "set_loc2", 1, 1, 1, OpCodeFormat.NoneLoc),
            new(OpCode.SetLoc3, "set_loc3", 1, 1, 1, OpCodeFormat.NoneLoc),
            new(OpCode.GetArg0, "get_arg0", 1, 0, 1, OpCodeFormat.NoneArg),
            new(OpCode.GetArg1, "get_arg1", 1, 0, 1, OpCodeFormat.NoneArg),
            new(OpCode.GetArg2, "get_arg2", 1, 0, 1, OpCodeFormat.NoneArg),
            new(OpCode.GetArg3, "get_arg3", 1, 0, 1, OpCodeFormat.NoneArg),
            new(OpCode.PutArg0, "put_arg0", 1, 1, 0, OpCodeFormat.NoneArg),
            new(OpCode.PutArg1, "put_arg1", 1, 1, 0, OpCodeFormat.NoneArg),
            new(OpCode.PutArg2, "put_arg2", 1, 1, 0, OpCodeFormat.NoneArg),
            new(OpCode.PutArg3, "put_arg3", 1, 1, 0, OpCodeFormat.NoneArg),
            new(OpCode.SetArg0, "set_arg0", 1, 1, 1, OpCodeFormat.NoneArg),
            new(OpCode.SetArg1, "set_arg1", 1, 1, 1, OpCodeFormat.NoneArg),
            new(OpCode.SetArg2, "set_arg2", 1, 1, 1, OpCodeFormat.NoneArg),
            new(OpCode.SetArg3, "set_arg3", 1, 1, 1, OpCodeFormat.NoneArg),
            new(OpCode.GetVarRef0, "get_var_ref0", 1, 0, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.GetVarRef1, "get_var_ref1", 1, 0, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.GetVarRef2, "get_var_ref2", 1, 0, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.GetVarRef3, "get_var_ref3", 1, 0, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.PutVarRef0, "put_var_ref0", 1, 1, 0, OpCodeFormat.NoneVarRef),
            new(OpCode.PutVarRef1, "put_var_ref1", 1, 1, 0, OpCodeFormat.NoneVarRef),
            new(OpCode.PutVarRef2, "put_var_ref2", 1, 1, 0, OpCodeFormat.NoneVarRef),
            new(OpCode.PutVarRef3, "put_var_ref3", 1, 1, 0, OpCodeFormat.NoneVarRef),
            new(OpCode.SetVarRef0, "set_var_ref0", 1, 1, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.SetVarRef1, "set_var_ref1", 1, 1, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.SetVarRef2, "set_var_ref2", 1, 1, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.SetVarRef3, "set_var_ref3", 1, 1, 1, OpCodeFormat.NoneVarRef),
            new(OpCode.GetLength, "get_length", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.IfFalse8, "if_false8", 2, 1, 0, OpCodeFormat.Label8),
            new(OpCode.IfTrue8, "if_true8", 2, 1, 0, OpCodeFormat.Label8),
            new(OpCode.Goto8, "goto8", 2, 0, 0, OpCodeFormat.Label8),
            new(OpCode.Goto16, "goto16", 3, 0, 0, OpCodeFormat.Label16),
            new(OpCode.Call0, "call0", 1, 1, 1, OpCodeFormat.NPopX),
            new(OpCode.Call1, "call1", 1, 1, 1, OpCodeFormat.NPopX),
            new(OpCode.Call2, "call2", 1, 1, 1, OpCodeFormat.NPopX),
            new(OpCode.Call3, "call3", 1, 1, 1, OpCodeFormat.NPopX),
            new(OpCode.IsUndefined, "is_undefined", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.IsNull, "is_null", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.TypeOfIsUndefined, "typeof_is_undefined", 1, 1, 1, OpCodeFormat.None),
            new(OpCode.TypeOfIsFunction, "typeof_is_function", 1, 1, 1, OpCodeFormat.None),
        };

        // Build lookup arrays - use 512 to accommodate all opcodes including temporary and short ones
        _info = new OpCodeInfo[512];
        _byName = new Dictionary<string, OpCodeInfo>(infos.Length, StringComparer.OrdinalIgnoreCase);

        foreach (var info in infos)
        {
            _info[(int)info.OpCode] = info;
            _byName[info.Name] = info;
        }
    }

    /// <summary>
    /// Gets the metadata for an opcode.
    /// </summary>
    /// <param name="opCode">The opcode to look up.</param>
    /// <returns>The opcode metadata.</returns>
    public static OpCodeInfo GetInfo(OpCode opCode) => _info[(int)opCode];

    /// <summary>
    /// Gets the metadata for an opcode by name.
    /// </summary>
    /// <param name="name">The opcode name (case-insensitive).</param>
    /// <param name="info">The opcode metadata if found.</param>
    /// <returns>True if the opcode was found.</returns>
    public static bool TryGetByName(string name, out OpCodeInfo info)
        => _byName.TryGetValue(name, out info);

    /// <summary>
    /// Gets all opcode infos.
    /// </summary>
    public static IEnumerable<OpCodeInfo> All => _byName.Values;

    /// <summary>
    /// Gets the size in bytes for an opcode (including operands).
    /// </summary>
    public static int GetSize(OpCode opCode) => _info[(int)opCode].Size;

    /// <summary>
    /// Gets the stack delta (push - pop) for an opcode.
    /// </summary>
    public static int GetStackDelta(OpCode opCode) => _info[(int)opCode].StackDelta;
}
