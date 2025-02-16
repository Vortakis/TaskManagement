using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TaskManagement.Api.Caching;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Common.Converters;
using TaskManagement.Api.Common.DataAnnotations.Validations;
using TaskManagement.Api.Common.Processing;
using TaskManagement.Api.Common.Swagger;
using TaskManagement.Api.Data;
using TaskManagement.Api.Services;
using TaskManagement.Api.Services.Handlers;
using TaskManagement.Api.Services.Helpers;

var builder = WebApplication.CreateBuilder(args);
//------------------------------------------------------------------------------------------
// DB Settings.
builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// Application Settings.
builder.Services.Configure<TaskPrioritySection>(
    builder.Configuration.GetSection("TaskManagement").GetSection("TaskPrioritySection"));
builder.Services.Configure<TaskCompletionSection>(
    builder.Configuration.GetSection("TaskManagement").GetSection("TaskCompletionSection"));
builder.Services.Configure<ConcurrentProcessingSection>(
    builder.Configuration.GetSection("TaskManagement").GetSection("ConcurrentProcessingSection"));
//------------------------------------------------------------------------------------------
// Configuration.
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.Formatting = Formatting.Indented;
    options.SerializerSettings.Converters.Add(new StringEnumConverter());
    options.SerializerSettings.Converters.Add(new SingleLineListConverter<int>());
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SchemaFilter<EnumSchemaFilter>();
});

builder.Services.AddMemoryCache();
//------------------------------------------------------------------------------------------
// App Dependencies.
builder.Services.AddSingleton<ICacheRepository, MemoryCacheRepository>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddTransient<ITaskPriorityHelper, TaskPriorityHelper>();
builder.Services.AddTransient<ICompletionStatusHelper, CompletionStatusHelper>();
builder.Services.AddTransient(typeof(IParallelProcessor<>), typeof(ParallelProcessor<>));
builder.Services.AddTransient<IBulkUpdateHandler, BulkUpdateHandler>();


//------------------------------------------------------------------------------------------
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
