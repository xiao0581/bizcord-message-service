using EasyNetQ;
using MessageMicroservice.Application.Interfaces;
using MessageMicroservice.Application.Services;
using MessageMicroservice.Domain.Repositories;
using MessageMicroservice.Infrastructure.Repositories;
using MessageMicroservice.Messaging;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IBus>(
    RabbitHutch.CreateBus("host=localhost")
);

builder.Services.AddSingleton<IMessageClient, EasyNetQMessageClient>();
builder.Services.AddHostedService<MessageSubscriber>();
builder.Services.AddSingleton<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IMessageService, MessageService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();