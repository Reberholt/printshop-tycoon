using System.IO;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrintshopTycoon
{
    /// <summary>Designer-editable balance values (Assets/PrintshopTycoon/Resources/PrintshopTycoon/GameBalance.asset).</summary>
    [CreateAssetMenu(menuName = "Printshop Tycoon/Game Balance", fileName = "GameBalance")]
    public class GameBalanceConfig : ScriptableObject
    {
        public const string ResourcePath = "PrintshopTycoon/GameBalance";

        public BalanceData data = BalanceData.CreateDefault();

        public static BalanceData LoadOrDefault()
        {
            var asset = Resources.Load<GameBalanceConfig>(ResourcePath);
            if (asset != null && asset.data != null && asset.data.dataVersion < BalanceData.CurrentDataVersion)
            {
                Debug.LogWarning("[PrintshopTycoon] GameBalance.asset is from an older version - using the new defaults. Run 'Printshop Tycoon > Setup Project' to update it.");
                return BalanceData.CreateDefault();
            }
            if (asset == null || asset.data == null || asset.data.orders == null || asset.data.orders.Count == 0 ||
                asset.data.logos == null || asset.data.logos.Count == 0 || asset.data.machines == null || asset.data.machines.Count == 0 ||
                asset.data.products == null || asset.data.products.Count == 0 || asset.data.upgrades == null)
                return BalanceData.CreateDefault();
            return asset.data;
        }
    }
}
