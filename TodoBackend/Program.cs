using TodoBackend.GraphQL;
var builder = WebApplication.CreateBuilder(args);

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173") // Default Vite Port
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Add Hotchocolate GraphQL Server
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();

app.UseCors();

// 3. Add GraphQL endpoint
app.MapGraphQL();

app.Run();
