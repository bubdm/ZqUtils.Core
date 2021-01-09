﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ZqUtils.Core.ObjectMethodExecutors
{
    /// <summary>
    /// AwaitableInfo
    /// </summary>
    public struct AwaitableInfo
    {
        /// <summary>
        /// AwaiterType
        /// </summary>
        public Type AwaiterType { get; }

        /// <summary>
        /// AwaiterIsCompletedProperty
        /// </summary>
        public PropertyInfo AwaiterIsCompletedProperty { get; }

        /// <summary>
        /// AwaiterGetResultMethod
        /// </summary>
        public MethodInfo AwaiterGetResultMethod { get; }

        /// <summary>
        /// AwaiterOnCompletedMethod
        /// </summary>
        public MethodInfo AwaiterOnCompletedMethod { get; }

        /// <summary>
        /// AwaiterUnsafeOnCompletedMethod
        /// </summary>
        public MethodInfo AwaiterUnsafeOnCompletedMethod { get; }

        /// <summary>
        /// ResultType
        /// </summary>
        public Type ResultType { get; }

        /// <summary>
        /// GetAwaiterMethod
        /// </summary>
        public MethodInfo GetAwaiterMethod { get; }

        /// <summary>
        /// AwaitableInfo
        /// </summary>
        /// <param name="awaiterType"></param>
        /// <param name="awaiterIsCompletedProperty"></param>
        /// <param name="awaiterGetResultMethod"></param>
        /// <param name="awaiterOnCompletedMethod"></param>
        /// <param name="awaiterUnsafeOnCompletedMethod"></param>
        /// <param name="resultType"></param>
        /// <param name="getAwaiterMethod"></param>
        public AwaitableInfo(
            Type awaiterType,
            PropertyInfo awaiterIsCompletedProperty,
            MethodInfo awaiterGetResultMethod,
            MethodInfo awaiterOnCompletedMethod,
            MethodInfo awaiterUnsafeOnCompletedMethod,
            Type resultType,
            MethodInfo getAwaiterMethod)
        {
            AwaiterType = awaiterType;
            AwaiterIsCompletedProperty = awaiterIsCompletedProperty;
            AwaiterGetResultMethod = awaiterGetResultMethod;
            AwaiterOnCompletedMethod = awaiterOnCompletedMethod;
            AwaiterUnsafeOnCompletedMethod = awaiterUnsafeOnCompletedMethod;
            ResultType = resultType;
            GetAwaiterMethod = getAwaiterMethod;
        }

        /// <summary>
        /// IsTypeAwaitable
        /// </summary>
        /// <param name="type"></param>
        /// <param name="awaitableInfo"></param>
        /// <returns></returns>
        public static bool IsTypeAwaitable(Type type, out AwaitableInfo awaitableInfo)
        {
            // Based on Roslyn code: http://source.roslyn.io/#Microsoft.CodeAnalysis.Workspaces/Shared/Extensions/ISymbolExtensions.cs,db4d48ba694b9347

            // Awaitable must have method matching "object GetAwaiter()"
            var getAwaiterMethod = type.GetRuntimeMethods().FirstOrDefault(m =>
                m.Name.Equals("GetAwaiter", StringComparison.OrdinalIgnoreCase)
                && m.GetParameters().Length == 0
                && m.ReturnType != null);
            if (getAwaiterMethod == null)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            var awaiterType = getAwaiterMethod.ReturnType;

            // Awaiter must have property matching "bool IsCompleted { get; }"
            var isCompletedProperty = awaiterType.GetRuntimeProperties().FirstOrDefault(p =>
                p.Name.Equals("IsCompleted", StringComparison.OrdinalIgnoreCase)
                && p.PropertyType == typeof(bool)
                && p.GetMethod != null);
            if (isCompletedProperty == null)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            // Awaiter must implement INotifyCompletion
            var awaiterInterfaces = awaiterType.GetInterfaces();
            var implementsINotifyCompletion = awaiterInterfaces.Any(t => t == typeof(INotifyCompletion));
            if (!implementsINotifyCompletion)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            // INotifyCompletion supplies a method matching "void OnCompleted(Action action)"
            var iNotifyCompletionMap = awaiterType
                .GetTypeInfo()
                .GetRuntimeInterfaceMap(typeof(INotifyCompletion));
            var onCompletedMethod = iNotifyCompletionMap.InterfaceMethods.Single(m =>
                m.Name.Equals("OnCompleted", StringComparison.OrdinalIgnoreCase)
                && m.ReturnType == typeof(void)
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(Action));

            // Awaiter optionally implements ICriticalNotifyCompletion
            var implementsICriticalNotifyCompletion =
                awaiterInterfaces.Any(t => t == typeof(ICriticalNotifyCompletion));
            MethodInfo unsafeOnCompletedMethod;
            if (implementsICriticalNotifyCompletion)
            {
                // ICriticalNotifyCompletion supplies a method matching "void UnsafeOnCompleted(Action action)"
                var iCriticalNotifyCompletionMap = awaiterType
                    .GetTypeInfo()
                    .GetRuntimeInterfaceMap(typeof(ICriticalNotifyCompletion));
                unsafeOnCompletedMethod = iCriticalNotifyCompletionMap.InterfaceMethods.Single(m =>
                    m.Name.Equals("UnsafeOnCompleted", StringComparison.OrdinalIgnoreCase)
                    && m.ReturnType == typeof(void)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(Action));
            }
            else
            {
                unsafeOnCompletedMethod = null;
            }

            // Awaiter must have method matching "void GetResult" or "T GetResult()"
            var getResultMethod = awaiterType.GetRuntimeMethods().FirstOrDefault(m =>
                m.Name.Equals("GetResult")
                && m.GetParameters().Length == 0);
            if (getResultMethod == null)
            {
                awaitableInfo = default(AwaitableInfo);
                return false;
            }

            awaitableInfo = new AwaitableInfo(
                awaiterType,
                isCompletedProperty,
                getResultMethod,
                onCompletedMethod,
                unsafeOnCompletedMethod,
                getResultMethod.ReturnType,
                getAwaiterMethod);
            return true;
        }
    }
}
