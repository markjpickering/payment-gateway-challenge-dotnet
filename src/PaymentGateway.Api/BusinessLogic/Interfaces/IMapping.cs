namespace PaymentGateway.BusinessLogic.Interfaces;

/// <summary>
/// A hand-written mapping from one type to another (ADR-14). The name is deliberately not
/// <c>IMapper</c>, so it is never confused with AutoMapper, which the project does not use.
/// </summary>
public interface IMapping<in TSource, out TDestination>
{
    TDestination Map(TSource source);
}