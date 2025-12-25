using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Events;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages factories/production: manufacturing chains, resource conversion, production queues.
    /// </summary>
    public class ProductionSystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly EventSystem _eventSystem;

        public ProductionSystem(EntityManager entityManager, EventSystem eventSystem)
        {
            _entityManager = entityManager;
            _eventSystem = eventSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<FactoryComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var factory = entity.GetComponent<FactoryComponent>();
                if (factory == null) continue;

                UpdateFactory(entity, factory, deltaTime);
            }
        }

        private void UpdateFactory(Entity entity, FactoryComponent factory, float deltaTime)
        {
            if (!factory.IsActive || factory.ProductionQueue.Count == 0)
                return;

            var currentJob = factory.ProductionQueue[0];
            
            // Check if we have resources
            if (!HasRequiredResources(entity, currentJob))
            {
                factory.IdleTime += deltaTime;
                if (factory.IdleTime > 10f) // Idle too long, cancel job
                {
                    factory.ProductionQueue.RemoveAt(0);
                    factory.IdleTime = 0f;
                    _eventSystem.Publish(new ProductionCancelledEvent(entity.Id, currentJob.RecipeId, "Insufficient resources", DateTime.UtcNow));
                }
                return;
            }

            factory.IdleTime = 0f;

            // Consume resources (once at start)
            if (currentJob.Progress == 0f)
            {
                ConsumeResources(entity, currentJob);
                _eventSystem.Publish(new ProductionStartedEvent(entity.Id, currentJob.RecipeId, DateTime.UtcNow));
            }

            // Progress production
            float productionRate = factory.ProductionRate * factory.Efficiency;
            currentJob.Progress += deltaTime * productionRate;

            if (currentJob.Progress >= currentJob.ProductionTime)
            {
                // Job complete
                ProduceOutput(entity, currentJob);
                factory.ProductionQueue.RemoveAt(0);
                factory.CompletedJobs++;
                _eventSystem.Publish(new ProductionCompletedEvent(entity.Id, currentJob.RecipeId, currentJob.OutputWareId, currentJob.OutputQuantity, DateTime.UtcNow));
            }
        }

        private bool HasRequiredResources(Entity entity, ProductionJob job)
        {
            var cargo = entity.GetComponent<CargoComponent>();
            if (cargo == null) return false;

            foreach (var input in job.InputWares)
            {
                if (!cargo.Contains(input.Key, input.Value))
                    return false;
            }

            return true;
        }

        private void ConsumeResources(Entity entity, ProductionJob job)
        {
            var cargo = entity.GetComponent<CargoComponent>();
            if (cargo == null) return;

            foreach (var input in job.InputWares)
            {
                cargo.Remove(input.Key, input.Value, 1.0f);
            }
        }

        private void ProduceOutput(Entity entity, ProductionJob job)
        {
            var cargo = entity.GetComponent<CargoComponent>();
            if (cargo == null) return;

            if (cargo.CurrentVolume + job.OutputQuantity <= cargo.MaxVolume)
            {
                cargo.Add(job.OutputWareId, job.OutputQuantity, 1.0f);
            }
            else
            {
                // Storage full, output lost or stored elsewhere
                // Could add overflow storage logic here
            }
        }

        public bool QueueProduction(int factoryEntityId, ProductionRecipe recipe, int quantity = 1)
        {
            var factory = _entityManager.GetEntity(factoryEntityId)?.GetComponent<FactoryComponent>();
            if (factory == null) return false;

            if (!factory.AvailableRecipes.Contains(recipe.Id))
                return false;

            for (int i = 0; i < quantity; i++)
            {
                var job = new ProductionJob
                {
                    RecipeId = recipe.Id,
                    ProductionTime = recipe.ProductionTime,
                    InputWares = new Dictionary<string, int>(recipe.InputWares),
                    OutputWareId = recipe.OutputWareId,
                    OutputQuantity = recipe.OutputQuantity,
                    Progress = 0f
                };
                factory.ProductionQueue.Add(job);
            }

            return true;
        }

        public void CancelProduction(int factoryEntityId, int jobIndex)
        {
            var factory = _entityManager.GetEntity(factoryEntityId)?.GetComponent<FactoryComponent>();
            if (factory == null || jobIndex < 0 || jobIndex >= factory.ProductionQueue.Count)
                return;

            var job = factory.ProductionQueue[jobIndex];
            factory.ProductionQueue.RemoveAt(jobIndex);
            
            _eventSystem.Publish(new ProductionCancelledEvent(factoryEntityId, job.RecipeId, "Cancelled by user", DateTime.UtcNow));
        }

        public void SetFactoryActive(int factoryEntityId, bool active)
        {
            var factory = _entityManager.GetEntity(factoryEntityId)?.GetComponent<FactoryComponent>();
            if (factory == null) return;
            factory.IsActive = active;
        }
    }

    /// <summary>
    /// Component for factory/production facilities.
    /// </summary>
    public class FactoryComponent : Component
    {
        public bool IsActive { get; set; } = true;
        public float ProductionRate { get; set; } = 1.0f; // Speed multiplier
        public float Efficiency { get; set; } = 1.0f; // Quality/waste multiplier
        public List<string> AvailableRecipes { get; } = new();
        public List<ProductionJob> ProductionQueue { get; } = new();
        public int MaxQueueSize { get; set; } = 10;
        public int CompletedJobs { get; set; } = 0;
        public float IdleTime { get; set; } = 0f;
    }

    /// <summary>
    /// A single production job in the queue.
    /// </summary>
    public class ProductionJob
    {
        public string RecipeId { get; set; } = string.Empty;
        public float ProductionTime { get; set; } = 10f;
        public float Progress { get; set; } = 0f;
        public Dictionary<string, int> InputWares { get; set; } = new();
        public string OutputWareId { get; set; } = string.Empty;
        public int OutputQuantity { get; set; } = 1;
    }

    /// <summary>
    /// Production recipe template.
    /// </summary>
    public class ProductionRecipe
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, int> InputWares { get; } = new(); // wareId -> quantity
        public string OutputWareId { get; set; } = string.Empty;
        public int OutputQuantity { get; set; } = 1;
        public float ProductionTime { get; set; } = 10f; // seconds
        public string RequiredFactoryType { get; set; } = "Generic"; // e.g. "Shipyard", "Refinery"
    }

    /// <summary>
    /// Registry for all production recipes in the game.
    /// </summary>
    public class RecipeManager
    {
        private readonly Dictionary<string, ProductionRecipe> _recipes = new();

        public void RegisterRecipe(ProductionRecipe recipe)
        {
            _recipes[recipe.Id] = recipe;
        }

        public ProductionRecipe? GetRecipe(string recipeId)
        {
            return _recipes.TryGetValue(recipeId, out var recipe) ? recipe : null;
        }

        public List<ProductionRecipe> GetRecipesForFactory(string factoryType)
        {
            return _recipes.Values.Where(r => r.RequiredFactoryType == factoryType || r.RequiredFactoryType == "Generic").ToList();
        }

        public List<ProductionRecipe> GetRecipesByInput(string wareId)
        {
            return _recipes.Values.Where(r => r.InputWares.ContainsKey(wareId)).ToList();
        }

        public List<ProductionRecipe> GetRecipesByOutput(string wareId)
        {
            return _recipes.Values.Where(r => r.OutputWareId == wareId).ToList();
        }

        public void LoadRecipesFromJson(string jsonPath)
        {
            // Stub: would load from JSON file
            // Example: {"recipes": [{"id": "steel_production", "inputWares": {"ore": 10}, "outputWareId": "steel", ...}]}
        }
    }

    // Production Events
    public record ProductionStartedEvent(int FactoryEntityId, string RecipeId, DateTime Timestamp) : BaseEvent(Timestamp);
    public record ProductionCompletedEvent(int FactoryEntityId, string RecipeId, string OutputWareId, int Quantity, DateTime Timestamp) : BaseEvent(Timestamp);
    public record ProductionCancelledEvent(int FactoryEntityId, string RecipeId, string Reason, DateTime Timestamp) : BaseEvent(Timestamp);
}
