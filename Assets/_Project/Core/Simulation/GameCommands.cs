using Game.Core.Commands;
using Game.Core.Farm;
using Game.Core.Store;

namespace Game.Core.Simulation
{
    /// <summary>Registra todos os handlers do jogo num executor. Ponto único para a apresentação e o simulador.</summary>
    public static class GameCommands
    {
        public static CommandExecutor CreateExecutor(GameContext ctx) => RegisterAll(new CommandExecutor(ctx));

        public static CommandExecutor RegisterAll(CommandExecutor executor)
        {
            executor.Register(new PlantHandler());
            executor.Register(new WaterHandler());
            executor.Register(new HarvestHandler());
            executor.Register(new BuyPlotHandler());
            executor.Register(new RestockHandler());
            executor.Register(new SetPriceHandler());
            executor.Register(new ScanItemHandler());
            executor.Register(new BuyFurnitureHandler());
            return executor;
        }
    }
}
