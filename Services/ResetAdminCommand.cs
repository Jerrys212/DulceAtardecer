using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using DulceAtardecer.Repository.IRepository;
using Microsoft.AspNetCore.Identity;

namespace DulceAtardecer.Services;

public static class ResetAdminCommand
{
    public const string Name = "reset-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args is not [Name, "--username", var username] || string.IsNullOrWhiteSpace(username))
        {
            Console.Error.WriteLine($"Uso: dotnet DulceAtardecer.dll {Name} --username <username>");
            return 1;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        ApplicationUser? user = await userManager.FindByNameAsync(username);
        if (user is null || !await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            Console.Error.WriteLine($"No existe un administrador con el usuario '{username}'.");
            return 1;
        }

        if (!user.IsActive)
        {
            Console.Error.WriteLine($"El administrador '{username}' está desactivado.");
            return 1;
        }

        string temporaryPassword = await userRepository.ResetPasswordAsync(user.Id, CancellationToken.None);

        Console.WriteLine($"Contraseña temporal para '{username}': {temporaryPassword}");
        Console.WriteLine("Deberá cambiarla al iniciar sesión. Todas sus sesiones fueron cerradas.");
        return 0;
    }
}
