using System;

namespace ReadyM.Api.Multiplayer.Generators;

/// <summary>
/// The annotated float or double counts every change as a change, instead of only a change over 0.1.
/// </summary>
/// <remarks>
/// Plain float fields ignore a write within 0.1 of the stored value, which keeps values that drift every frame from
/// flooding the network. Data that is set once and must arrive exactly, like an item's weight of 0.1, needs this.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ExactComparisonAttribute : Attribute;
