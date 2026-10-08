using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLogging();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
var MyAllowOrigins = "MyAllowOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(MyAllowOrigins, policy =>
    {
        policy
              // .AllowAnyOrigin()
              //   .AllowAnyMethod()
              //   .AllowAnyHeader()
              .WithOrigins("http://localhost:3000", "https://localhost:3000");
    });
});
var app = builder.Build();
app.UseCors(MyAllowOrigins);
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
//Custom Middleware
// app.Use(async (context, next) =>
// {
//     var path = context.Request.Path;
//     Console.WriteLine($"Request Path: {path}");
//     // Do work that can write to the Response.
//     await next.Invoke();
//     // Do logging or other work that doesn't write to the Response.
//     Console.WriteLine($"Response Status Code: {context.Response.StatusCode}");
// });
app.UseRequestLogging();
app.MapControllers();

app.Run();
