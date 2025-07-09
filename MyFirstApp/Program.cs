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

RouteGroupBuilder fruitGroup = app.MapGroup("/fruit").AddEndpointFilter<IdValidationFilter>();

fruitGroup.MapGet("/", () => _fruit);

//RouteGroupBuilder fruitApiWithValidation = fruitGroup.MapGroup("/").AddEndpointFilter<IdValidationFilter>();

//app.MapGet("/fruit", () => _fruit);

fruitGroup.MapGet("/fruit/{id}", (string id) =>

     _fruit.TryGetValue(id, out var fruit)
         ? TypedResults.Ok(fruit)
         : Results.Problem(statusCode: 404));

fruitGroup.MapPost("/fruit/{id}", (string id, Fruit fruit) =>
    _fruit.TryAdd(id, fruit)
        ? TypedResults.Created($"/fruit/{id}", fruit)
        : Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { "id", new[] { "Fruit with this ID already exists." } }
        }));

fruitGroup.MapPut("/fruit/{id}", (string id, Fruit fruit) =>
{
    _fruit[id] = fruit;
    return Results.NoContent();
});

fruitGroup.MapDelete("/fruit/{id}", (string id) =>
{
    _fruit.TryRemove(id, out var removedFruit);
    return Results.NoContent();
});

app.Run();

record Fruit(string Name, int Stock)
{
    public static readonly Dictionary<string, Fruit> All = new();
};

class IdValidationFilter: IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var id = context.GetArgument<string>(0);
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith('f'))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "id", new[] { "Invalid format, ID must start with 'f' " } }
            });
        }
        return await next(context);
    }
}
