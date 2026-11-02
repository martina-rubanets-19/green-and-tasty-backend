using GroupPreOrder.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GroupPreOrder.Api;

/// <summary>Мапить доменні винятки на HTTP-коди: 404 / 400 / 403.</summary>
public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            BadRequestException => (StatusCodes.Status400BadRequest, "Bad Request"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (0, "")
        };
        if (status == 0) return false;

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Detail = ex.Message }, ct);
        return true;
    }
}

/*  Program.cs — реєстрація (NuGet: AWSSDK.DynamoDBv2, AWSSDK.Extensions.NETCore.Setup):

    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
    // IgnoreNullValues = true — обов'язково: reservationId гостьової сесії = null,
    // а записати {"NULL": true} у ключ GSI DynamoDB не дозволить.
    builder.Services.AddScoped<Amazon.DynamoDBv2.DataModel.IDynamoDBContext>(sp =>
        new Amazon.DynamoDBv2.DataModel.DynamoDBContext(
            sp.GetRequiredService<Amazon.DynamoDBv2.IAmazonDynamoDB>(),
            new Amazon.DynamoDBv2.DataModel.DynamoDBContextConfig { IgnoreNullValues = true }));

    builder.Services.AddScoped<IGroupPreOrderRepository, GroupPreOrderRepository>();
    builder.Services.AddScoped<IGroupPreOrderService, GroupPreOrderService>();

    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddProblemDetails();
    ...
    app.UseExceptionHandler();

    Якщо IDynamoDBContext у проєкті вже зареєстрований — другий раз не реєструйте.
*/
