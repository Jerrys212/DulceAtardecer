using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using Mapster;

namespace DulceAtardecer.Mapping;

public static class MapsterConfig
{
    public static void RegisterMappings()
    {
        TypeAdapterConfig<UserWithRole, CurrentUserDto>.NewConfig()
            .MapToConstructor(true)
            .Map(dest => dest.Id, src => src.User.Id)
            .Map(dest => dest.Username, src => src.User.UserName ?? string.Empty)
            .Map(dest => dest.FullName, src => src.User.FullName)
            .Map(dest => dest.Role, src => src.Role)
            .Map(dest => dest.MustChangePassword, src => src.User.MustChangePassword);

        TypeAdapterConfig<UserWithRole, UserDto>.NewConfig()
            .MapToConstructor(true)
            .Map(dest => dest.Id, src => src.User.Id)
            .Map(dest => dest.Username, src => src.User.UserName ?? string.Empty)
            .Map(dest => dest.FullName, src => src.User.FullName)
            .Map(dest => dest.Role, src => src.Role)
            .Map(dest => dest.IsActive, src => src.User.IsActive)
            .Map(dest => dest.MustChangePassword, src => src.User.MustChangePassword)
            .Map(dest => dest.DeactivatedAt, src => src.User.DeactivatedAt)
            .Map(dest => dest.CreatedAt, src => src.User.CreatedAt)
            .Map(dest => dest.UpdatedAt, src => src.User.UpdatedAt);

        TypeAdapterConfig<Session, SessionDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Subcategory, SubcategoryDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Category, CategoryDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Subcategory, ActiveSubcategoryDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Category, ActiveCategoryDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Extra, ExtraDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Extra, ActiveExtraDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Product, ProductDto>.NewConfig()
            .MapToConstructor(true)
            .Map(dest => dest.CategoryId, src => src.Subcategory!.CategoryId)
            .Map(dest => dest.CategoryName, src => src.Subcategory!.Category!.Name)
            .Map(dest => dest.SubcategoryName, src => src.Subcategory!.Name);

        TypeAdapterConfig<Product, AvailableProductDto>.NewConfig()
            .MapToConstructor(true)
            .Map(dest => dest.CategoryId, src => src.Subcategory!.CategoryId)
            .Map(dest => dest.CategoryName, src => src.Subcategory!.Category!.Name)
            .Map(dest => dest.SubcategoryName, src => src.Subcategory!.Name);

        TypeAdapterConfig<SaleItemExtraRequestDto, SaleLineExtra>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<SaleItemRequestDto, SaleLine>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<SaleItemExtra, SaleItemExtraDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<SaleItem, SaleItemDto>.NewConfig()
            .MapToConstructor(true);

        TypeAdapterConfig<Sale, SaleDto>.NewConfig()
            .MapToConstructor(true)
            .Map(dest => dest.CashierName, src => src.Cashier!.FullName);
    }
}
