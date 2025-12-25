using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Economy;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Contract system for generating and tracking trade missions.
    /// </summary>
    public class ContractSystem
    {
        private readonly MarketManager _marketManager;
        private readonly List<Contract> _activeContracts = new();
        private readonly Random _random = new();
        private int _nextContractId = 1;

        public ContractSystem(MarketManager marketManager)
        {
            _marketManager = marketManager;
        }

        public List<Contract> GetAvailableContracts() => _activeContracts.Where(c => c.Status == ContractStatus.Available).ToList();
        public List<Contract> GetActiveContracts() => _activeContracts.Where(c => c.Status == ContractStatus.Active).ToList();

        /// <summary>
        /// Generate a delivery contract between two stations.
        /// </summary>
        public Contract? GenerateDeliveryContract(int sourceStationId, int destStationId)
        {
            var sourceMarket = _marketManager.GetMarket(sourceStationId);
            var destMarket = _marketManager.GetMarket(destStationId);
            
            if (sourceMarket == null || destMarket == null)
                return null;

            // Find wares available at source that destination wants
            var eligibleWares = sourceMarket.Goods
                .Where(g => g.Value.StockLevel > 10 && destMarket.Goods.ContainsKey(g.Key))
                .Select(g => g.Key)
                .ToList();

            if (eligibleWares.Count == 0)
                return null;

            string wareId = eligibleWares[_random.Next(eligibleWares.Count)];
            var ware = _marketManager.GetWareTemplate(wareId);
            if (ware == null) return null;

            int quantity = _random.Next(5, 20);
            float basePrice = sourceMarket.GetPrice(wareId);
            float destPrice = destMarket.GetPrice(wareId);
            float priceDiff = destPrice - basePrice;
            float reward = Math.Max(100f, priceDiff * quantity * 1.5f);

            var contract = new Contract
            {
                Id = _nextContractId++,
                Type = ContractType.Delivery,
                WareId = wareId,
                WareName = ware.Name,
                Quantity = quantity,
                SourceStationId = sourceStationId,
                DestinationStationId = destStationId,
                Reward = reward,
                TimeLimit = 600f, // 10 minutes
                Status = ContractStatus.Available
            };

            _activeContracts.Add(contract);
            return contract;
        }

        /// <summary>
        /// Accept a contract for an entity (player/NPC).
        /// </summary>
        public bool AcceptContract(int contractId, int entityId)
        {
            var contract = _activeContracts.FirstOrDefault(c => c.Id == contractId);
            if (contract == null || contract.Status != ContractStatus.Available)
                return false;

            contract.Status = ContractStatus.Active;
            contract.AssignedEntityId = entityId;
            contract.StartTime = DateTime.Now;
            return true;
        }

        /// <summary>
        /// Complete a contract delivery.
        /// </summary>
        public bool CompleteContract(int contractId, Entity entity)
        {
            var contract = _activeContracts.FirstOrDefault(c => c.Id == contractId);
            if (contract == null || contract.Status != ContractStatus.Active || contract.AssignedEntityId != entity.Id)
                return false;

            var cargo = entity.GetComponent<CargoComponent>();
            if (cargo == null || cargo.GetQuantity(contract.WareId) < contract.Quantity)
                return false;

            var ware = _marketManager.GetWareTemplate(contract.WareId);
            if (ware == null) return false;

            // Remove cargo
            cargo.Remove(contract.WareId, contract.Quantity, ware.Volume);

            // Pay reward (add credits component if needed)
            // TODO: Add CreditsComponent and pay here

            contract.Status = ContractStatus.Completed;
            contract.CompletionTime = DateTime.Now;
            return true;
        }

        /// <summary>
        /// Update contracts (check timeouts, etc).
        /// </summary>
        public void Update(float deltaSeconds)
        {
            foreach (var contract in _activeContracts.Where(c => c.Status == ContractStatus.Active))
            {
                if (contract.StartTime.HasValue)
                {
                    var elapsed = (DateTime.Now - contract.StartTime.Value).TotalSeconds;
                    if (elapsed > contract.TimeLimit)
                    {
                        contract.Status = ContractStatus.Failed;
                    }
                }
            }

            // Cleanup old contracts
            _activeContracts.RemoveAll(c =>
                (c.Status == ContractStatus.Completed || c.Status == ContractStatus.Failed) &&
                c.CompletionTime.HasValue &&
                (DateTime.Now - c.CompletionTime.Value).TotalMinutes > 5);
        }

        /// <summary>
        /// Generate random contracts periodically.
        /// </summary>
        public void GenerateRandomContracts(int count)
        {
            var stationIds = _marketManager.Markets.Keys.ToList();
            if (stationIds.Count < 2) return;

            for (int i = 0; i < count; i++)
            {
                int source = stationIds[_random.Next(stationIds.Count)];
                int dest = stationIds[_random.Next(stationIds.Count)];
                
                if (source != dest)
                {
                    GenerateDeliveryContract(source, dest);
                }
            }
        }
    }

    public enum ContractType
    {
        Delivery,
        BuyGoods,
        SellGoods,
        Escort
    }

    public enum ContractStatus
    {
        Available,
        Active,
        Completed,
        Failed
    }

    public class Contract
    {
        public int Id { get; set; }
        public ContractType Type { get; set; }
        public string WareId { get; set; } = string.Empty;
        public string WareName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int SourceStationId { get; set; }
        public int DestinationStationId { get; set; }
        public float Reward { get; set; }
        public float TimeLimit { get; set; } // seconds
        public ContractStatus Status { get; set; }
        public int? AssignedEntityId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }

        public string GetDescription()
        {
            return Type switch
            {
                ContractType.Delivery => $"Deliver {Quantity}x {WareName} from Station {SourceStationId} to Station {DestinationStationId}",
                _ => "Unknown contract"
            };
        }

        public float GetRemainingTime()
        {
            if (!StartTime.HasValue) return TimeLimit;
            var elapsed = (DateTime.Now - StartTime.Value).TotalSeconds;
            return Math.Max(0f, TimeLimit - (float)elapsed);
        }
    }
}
