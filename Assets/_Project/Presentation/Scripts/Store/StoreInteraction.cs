using Game.Core.Store;
using Game.Presentation.UI;

namespace Game.Presentation.Store
{
    /// <summary>Ações do caixa (chamadas pelo botão de ação perto do balcão).</summary>
    public static class StoreInteraction
    {
        public static void Scan()
        {
            var root = GameRoot.Instance;
            var front = StoreRules.FrontOfQueue(root.State.Store);
            if (front == null)
            {
                Toast.Show("Ninguém na fila");
                return;
            }
            root.Execute(new ScanItemCommand());
        }
    }
}
