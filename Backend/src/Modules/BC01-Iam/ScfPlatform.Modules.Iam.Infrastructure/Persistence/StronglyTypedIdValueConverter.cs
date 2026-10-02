using ScfPlatform.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>Builds a <see cref="ValueConverter{TModel,TProvider}"/> between one of this module's strongly-typed ids and the <c>uuid</c> column that stores it (00-Shared-Foundations.md §6.4).</summary>
public static class StronglyTypedIdValueConverter
{
    public static ValueConverter<TId, Guid> Create<TId>(Func<Guid, TId> factory)
        where TId : IStronglyTypedId =>
        new(id => id.Value, value => factory(value));
}
