﻿using System;
using System.Linq;
using System.Reflection.Emit;
using XamlX.Ast;
using XamlX.Emit;
using XamlX.IL;
using XamlX.TypeSystem;

namespace OpenNefia.XamlInjectors.CompilerExtensions
{
    public abstract class ONXamlVecLikeConstAstNode<T>
        : XamlAstNode, IXamlAstValueNode, IXamlAstILEmitableNode
        where T : unmanaged
    {
        private readonly IXamlConstructor _constructor;
        protected readonly T[] Values;

        public ONXamlVecLikeConstAstNode(
            IXamlLineInfo lineInfo,
            IXamlType type, IXamlConstructor constructor,
            IXamlType componentType, T[] values)
            : base(lineInfo)
        {
            _constructor = constructor;
            Values = values;

            var @params = constructor.Parameters;

            if (@params.Count != values.Length)
                throw new ArgumentException("Invalid amount of parameters");

            if (@params.Any(c => c != componentType))
                throw new ArgumentException("Invalid constructor: not all parameters match component type");

            Type = new XamlAstClrTypeReference(lineInfo, type, false);
        }

        public IXamlAstTypeReference Type { get; }

        public virtual XamlILNodeEmitResult Emit(
            XamlEmitContext<IXamlILEmitter, XamlILNodeEmitResult> context,
            IXamlILEmitter codeGen)
        {
            codeGen.Newobj(_constructor);

            return XamlILNodeEmitResult.Type(0, Type.GetClrType());
        }
    }

    public sealed class ONXamlSingleVecLikeConstAstNode : ONXamlVecLikeConstAstNode<float>
    {
        public ONXamlSingleVecLikeConstAstNode(
            IXamlLineInfo lineInfo,
            IXamlType type, IXamlConstructor constructor,
            IXamlType componentType, float[] values)
            : base(lineInfo, type, constructor, componentType, values)
        {
        }

        public override XamlILNodeEmitResult Emit(XamlEmitContext<IXamlILEmitter, XamlILNodeEmitResult> context,
            IXamlILEmitter codeGen)
        {
            foreach (var value in Values)
            {
                codeGen.Emit(OpCodes.Ldc_R4, value);
            }

            return base.Emit(context, codeGen);
        }
    }

    public sealed class RXamlInt32VecLikeConstAstNode : ONXamlVecLikeConstAstNode<int>
    {
        public RXamlInt32VecLikeConstAstNode(
            IXamlLineInfo lineInfo,
            IXamlType type, IXamlConstructor constructor,
            IXamlType componentType, int[] values)
            : base(lineInfo, type, constructor, componentType, values)
        {
        }

        public override XamlILNodeEmitResult Emit(XamlEmitContext<IXamlILEmitter, XamlILNodeEmitResult> context,
            IXamlILEmitter codeGen)
        {
            foreach (var value in Values)
            {
                codeGen.Emit(OpCodes.Ldc_I4, value);
            }

            return base.Emit(context, codeGen);
        }
    }
}
