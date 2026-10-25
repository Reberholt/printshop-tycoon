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
            if (asset == null || asset.data == null || asset.data.orders == null || asset.data.orders.Count == 0 ||
                asset.data.logos == null || asset.data.logos.Count == 0 || asset.data.machines == null || asset.data.machines.Count == 0)
                return BalanceData.CreateDefault();
            return asset.data;
        }
    }
}
