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
    }
}
