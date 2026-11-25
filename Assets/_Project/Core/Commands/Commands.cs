using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Simulation;

namespace Game.Core.Commands
{
    /// <summary>Ação do jogador. Só dados; a regra fica no handler.</summary>
    public interface ICommand { }

    public readonly struct CommandResult
    {
        private CommandResult(bool ok, string error) { IsSuccess = ok; Error = error; }

        public bool IsSuccess { get; }

        /// <summary>Código de erro estável (ver <see cref="CommandErrors"/>); null em caso de sucesso.</summary>
        public string Error { get; }

        public static CommandResult Ok() => new CommandResult(true, null);
        public static CommandResult Fail(string error) => new CommandResult(false, error ?? CommandErrors.Unknown);

        public override string ToString() => IsSuccess ? "Ok" : "Fail(" + Error + ")";
    }

    public static class CommandErrors
    {
        public const string Unknown = "unknown";
        public const string NoHandler = "no_handler";
        public const string InvalidArgument = "invalid_argument";
        public const string UnknownProduct = "unknown_product";
        public const string UnknownCrop = "unknown_crop";
        public const string InsufficientCoins = "insufficient_coins";
        public const string InsufficientGems = "insufficient_gems";
        public const string InsufficientStock = "insufficient_stock";
        public const string Locked = "locked";
        public const string InvalidState = "invalid_state";
        public const string UnknownPlot = "unknown_plot";
        public const string PlotOccupied = "plot_occupied";
        public const string PlotEmpty = "plot_empty";
        public const string NotReady = "not_ready";
        public const string AlreadyReady = "already_ready";
        public const string AlreadyWatered = "already_watered";
        public const string MaxReached = "max_reached";
        public const string UnknownShelf = "unknown_shelf";
        public const string UnknownFurniture = "unknown_furniture";
        public const string WrongCategory = "wrong_category";
        public const string ShelfHasOtherProduct = "shelf_other_product";
        public const string ShelfFull = "shelf_full";
        public const string NoCustomer = "no_customer";
        public const string NoSeeds = "no_seeds";
        public const string NoFeed = "no_feed";
        public const string UnknownAnimal = "unknown_animal";
        public const string AlreadyFed = "already_fed";
    }

    /// <summary>
    /// Regra de um comando. <see cref="Validate"/> NÃO pode alterar estado; <see cref="Apply"/> só é
    /// chamado se a validação passou e deve sempre conseguir aplicar.
    /// </summary>
    public interface ICommandHandler<in T> where T : ICommand
    {
        CommandResult Validate(GameContext ctx, T command);
        void Apply(GameContext ctx, T command);
    }

    /// <summary>
    /// Único caminho para alterar o estado a partir da UI: avança a simulação até agora,
    /// valida e aplica o comando e publica o resultado no barramento.
    /// </summary>
    public sealed class CommandExecutor
    {
        private readonly GameContext _ctx;
        private readonly Dictionary<Type, Func<ICommand, CommandResult>> _routes = new Dictionary<Type, Func<ICommand, CommandResult>>();

        public CommandExecutor(GameContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public CommandExecutor Register<T>(ICommandHandler<T> handler) where T : ICommand
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_routes.ContainsKey(typeof(T))) throw new InvalidOperationException($"Handler para {typeof(T).Name} já registrado.");
            _routes[typeof(T)] = cmd =>
            {
                var typed = (T)cmd;
                var validation = handler.Validate(_ctx, typed);
                if (!validation.IsSuccess) return validation;
                handler.Apply(_ctx, typed);
                return CommandResult.Ok();
            };
            return this;
        }

        public bool CanHandle(Type commandType) => _routes.ContainsKey(commandType);

        public CommandResult Execute(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            GameSimulation.Tick(_ctx);

            CommandResult result = _routes.TryGetValue(command.GetType(), out var route)
                ? route(command)
                : CommandResult.Fail(CommandErrors.NoHandler);

            if (result.IsSuccess) _ctx.Events.Publish(new CommandExecuted(command));
            else _ctx.Events.Publish(new CommandRejected(command, result.Error));
            return result;
        }
    }
}
