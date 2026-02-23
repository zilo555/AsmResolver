using System;

namespace AsmResolver.DotNet.Code.Cil
{
    /// <summary>
    /// Provides all possible flags that can be assigned to a <see cref="CilMethodBody"/>, which alters the behaviour
    /// of the underlying method body serializer.
    /// </summary>
    [Flags]
    public enum CilMethodBodyBuildFlags
    {
        /// <summary>
        /// Indicates labels should be verified for validity.
        /// </summary>
        VerifyLabels = 1,

        /// <summary>
        /// Indicates the maximum stack depth should be calculated upon built.
        /// </summary>
        ComputeMaxStack = 2,

        /// <summary>
        /// Indicates that exception handlers should be sorted as described by ECMA-335 I.12.4.2.5
        /// </summary>
        SortExceptionHandlers = 4,

        /// <summary>
        /// Indicates the body should be subject to full validation before building.
        /// </summary>
        FullValidation = ComputeMaxStack | VerifyLabels | SortExceptionHandlers,
    }
}
