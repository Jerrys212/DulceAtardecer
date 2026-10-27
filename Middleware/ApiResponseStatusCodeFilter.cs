using DulceAtardecer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DulceAtardecer.Middleware;

public class ApiResponseStatusCodeFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IApiResponse response } result)
        {
            response.StatusCode = result.StatusCode ?? StatusCodes.Status200OK;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
