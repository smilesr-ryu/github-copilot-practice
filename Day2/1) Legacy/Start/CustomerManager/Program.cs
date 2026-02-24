using CustomerManager.Services;
using CustomerManager.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;

AIAgent? aiAgent = null;
ICustomerService? customerService = null;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.Agents", LogLevel.Trace);
builder.Logging.AddFilter("Microsoft.Agents.AI.Hosting.AGUI", LogLevel.Trace);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Legacy API",
        Version = "1.0.0",
        Description = "레거시 .NET API - Step 1: 기본 기동 테스트"
    });
});

builder.Services.AddAGUI();
builder.Services.AddScoped<ICustomerService, CustomerService>();

// GitHub Models Agent setup
var githubToken = builder.Configuration["GitHubModels:ApiKey"];
Console.WriteLine($"[DEBUG] GitHubModels:ApiKey loaded: {(string.IsNullOrWhiteSpace(githubToken) ? "NO" : "YES (masked)")}");

if (!string.IsNullOrWhiteSpace(githubToken))
{
    Console.WriteLine("[DEBUG] Creating OpenAI client for GitHub Models...");
    var client = new OpenAIClient(new ApiKeyCredential(githubToken), new OpenAIClientOptions
    {
        Endpoint = new Uri("https://models.github.ai/inference")
    });

    var chatClient = client.GetChatClient("openai/gpt-5-mini");
    Console.WriteLine("[DEBUG] Creating AI Agent with customer management tools...");
    
    // Create customer service instance for tools
    customerService = new CustomerService();
    
    aiAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant for a customer management system. Help users manage customers by using the available tools.",
        name: "CustomerAssistant",
        tools: 
        [
            AIFunctionFactory.Create(GetAllCustomersAsync),
            AIFunctionFactory.Create(GetCustomerAsync),
            AIFunctionFactory.Create(SearchCustomerAsync),
            AIFunctionFactory.Create(CreateCustomerAsync),
            AIFunctionFactory.Create(UpdateCustomerAsync),
            AIFunctionFactory.Create(DeleteCustomerAsync)
        ]);

    Console.WriteLine("[DEBUG] ✓ Agent created successfully with 6 customer tools!");
}
else
{
    Console.WriteLine("[DEBUG] ✗ Agent NOT initialized - API key is missing or empty");
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var isAguiRequest = context.Request.Path.StartsWithSegments("/agui");

    try
    {
        await next();

        if (isAguiRequest)
        {
            Console.WriteLine($"[AGUI] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} (TraceId: {context.TraceIdentifier})");
        }
    }
    catch (Exception ex)
    {
        if (isAguiRequest)
        {
            Console.WriteLine($"[AGUI] Unhandled exception for {context.Request.Method} {context.Request.Path} (TraceId: {context.TraceIdentifier}): {ex}");
        }

        throw;
    }
});

app.MapGet("/health", () => Results.Ok(new HealthResponse
{
    Status = "Healthy",
    Message = "Legacy API is running",
    Timestamp = DateTime.UtcNow
}));

app.MapPost("/api/chat", async (ChatRequest request) =>
{
    if (aiAgent is null)
    {
        return Results.BadRequest("Agent is not configured. Set GitHubModels:ApiKey in appsettings.");
    }

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message is required");
    }

    try
    {
        Console.WriteLine($"[DEBUG] Calling agent.RunAsync with message: {request.Message}");
        var response = await aiAgent.RunAsync(request.Message);
        Console.WriteLine($"[DEBUG] Agent response: {response}");
        return Results.Ok(new { response });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG] Agent error: {ex.Message}");
        return Results.Problem($"Agent error: {ex.Message}");
    }
});

var customers = app.MapGroup("/api/customers");

customers.MapGet("", (ICustomerService service) => Results.Ok(service.GetAllCustomers()));

customers.MapGet("/search", (string name, ICustomerService service) =>
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Customer name is required");
    }

    var customer = service.SearchCustomer(name);
    return customer is null ? Results.NotFound($"Customer '{name}' not found") : Results.Ok(customer);
});

customers.MapGet("/{id:int}", (int id, ICustomerService service) =>
{
    if (id <= 0)
    {
        return Results.BadRequest("Invalid customer ID");
    }

    var customer = service.GetCustomer(id);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

customers.MapPost("", (Customer customer, ICustomerService service) =>
{
    if (HasInvalidCustomerInput(customer))
    {
        return Results.BadRequest("Customer name and email are required");
    }

    var createdCustomer = service.CreateCustomer(customer);
    return Results.Created($"/api/customers/{createdCustomer.Id}", createdCustomer);
});

customers.MapPut("/{id:int}", (int id, Customer customer, ICustomerService service) =>
{
    if (id <= 0)
    {
        return Results.BadRequest("Invalid customer ID");
    }

    if (HasInvalidCustomerInput(customer))
    {
        return Results.BadRequest("Customer name and email are required");
    }

    var updatedCustomer = service.UpdateCustomer(id, customer);
    return updatedCustomer is null ? Results.NotFound() : Results.Ok(updatedCustomer);
});

customers.MapDelete("/{id:int}", (int id, ICustomerService service) =>
{
    if (id <= 0)
    {
        return Results.BadRequest("Invalid customer ID");
    }

    return service.DeleteCustomer(id) ? Results.NoContent() : Results.NotFound();
});

// Map AGUI if agent is configured
if (aiAgent != null)
{
    Console.WriteLine("[DEBUG] Mapping AGUI endpoint at /agui");
    app.MapAGUI("/agui", aiAgent);
}

app.Run();

// Tool functions for AI Agent
[Description("Get a list of all customers in the system")]
string GetAllCustomersAsync()
{
    var customers = customerService?.GetAllCustomers() ?? [];
    return JsonSerializer.Serialize(customers);
}

[Description("Get a specific customer by their ID")]
string GetCustomerAsync([Description("The ID of the customer to retrieve")] int id)
{
    var customer = customerService?.GetCustomer(id);
    return customer != null 
        ? JsonSerializer.Serialize(customer)
        : JsonSerializer.Serialize(new { error = $"Customer with ID {id} not found" });
}

[Description("Search for a customer by name")]
string SearchCustomerAsync([Description("The name of the customer to search for")] string name)
{
    var customer = customerService?.SearchCustomer(name);
    return customer != null
        ? JsonSerializer.Serialize(customer)
        : JsonSerializer.Serialize(new { error = $"Customer '{name}' not found" });
}

[Description("Create a new customer")]
string CreateCustomerAsync(
    [Description("The name of the new customer")] string name,
    [Description("The email address of the new customer")] string email)
{
    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || customerService == null)
    {
        return JsonSerializer.Serialize(new { error = "Name and email are required" });
    }
    
    var customer = new Customer { Name = name, Email = email };
    var created = customerService.CreateCustomer(customer);
    return JsonSerializer.Serialize(created);
}

[Description("Update an existing customer's information")]
string UpdateCustomerAsync(
    [Description("The ID of the customer to update")] int id,
    [Description("The new name for the customer")] string name,
    [Description("The new email address for the customer")] string email)
{
    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || customerService == null)
    {
        return JsonSerializer.Serialize(new { error = "Name and email are required" });
    }
    
    var customer = new Customer { Name = name, Email = email };
    var updated = customerService.UpdateCustomer(id, customer);
    return updated != null
        ? JsonSerializer.Serialize(updated)
        : JsonSerializer.Serialize(new { error = $"Customer with ID {id} not found" });
}

[Description("Delete a customer from the system")]
string DeleteCustomerAsync([Description("The ID of the customer to delete")] int id)
{
    var deleted = customerService?.DeleteCustomer(id) ?? false;
    return JsonSerializer.Serialize(new { success = deleted, message = deleted ? "Customer deleted successfully" : "Customer not found" });
}

static bool HasInvalidCustomerInput(Customer customer)
{
    return customer == null
        || string.IsNullOrWhiteSpace(customer.Name)
        || string.IsNullOrWhiteSpace(customer.Email);
}

record ChatRequest(string Message);
