using System.Collections.Concurrent;
using System.Reflection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePages();

var _fruit = new ConcurrentDictionary<string, Fruit>();

app.MapGet("/fruit", () => _fruit);

app.MapGet("/fruit/{id}", (string id) =>

     _fruit.TryGetValue(id, out var fruit)
         ? TypedResults.Ok(fruit)
         : Results.Problem(statusCode: 404))
         .AddEndpointFilterFactory(ValidationHelper.ValidateIdFactory);

app.MapPost("/fruit/{id}", (string id, Fruit fruit) =>
    _fruit.TryAdd(id, fruit)
        ? TypedResults.Created($"/fruit/{id}", fruit)
        : Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { "id", new[] { "Fruit with this ID already exists." } }
        })).AddEndpointFilterFactory(ValidationHelper.ValidateIdFactory);

app.MapPut("/fruit/{id}", (string id, Fruit fruit) =>
{
    _fruit[id] = fruit;
    return Results.NoContent();
});

app.MapDelete("/fruit/{id}", (string id) =>
{
    _fruit.TryRemove(id, out var removedFruit);
    return Results.NoContent();
});

app.Run();

record Fruit(string Name, int Stock)
{
    public static readonly Dictionary<string, Fruit> All = new();
};

class ValidationHelper
{
    internal static EndpointFilterDelegate ValidateIdFactory(
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next)
    {
        ParameterInfo[] parameters = context.MethodInfo.GetParameters();
        int? idPosition = null;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == "id" && parameters[i].ParameterType == typeof(string))
            {
                idPosition = i;
                break;
            }

        }
        if (!idPosition.HasValue)
        {
            return next;
        }

        return async (invocationContext) =>
        {
            var id = invocationContext.GetArgument<string>(idPosition.Value);
            if (string.IsNullOrEmpty(id) || !id.StartsWith("f"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                                { "id", new[] { "ID must start with 'f' and cannot be empty." } }
                });
            }
            return await next(invocationContext);
        };

    }
}
