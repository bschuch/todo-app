using Microsoft.EntityFrameworkCore;
using TodoBackend.Data;
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

// 2. Add MongoDB EF Core Context
var mongoClient = new MongoDB.Driver.MongoClient("mongodb://localhost:27017");
builder.Services.AddDbContext<TodoDbContext>(options =>
    options.UseMongoDB(mongoClient, "TodoDatabase"));

// 3. Add Hotchocolate GraphQL Server
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();

app.UseCors();

// 3. Add GraphQL endpoint
app.MapGraphQL();

app.Run();
