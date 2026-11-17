using Application.Abstractions.Messaging;

namespace Application.Todos.Copy;

public sealed record CopyTodoCommand(Guid TodoItemId) : ICommand<Guid>;
