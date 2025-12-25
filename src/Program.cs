using System;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.Examples;

namespace SpaceTradeEngine
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Quick entry point: run event system demo if requested
            var demoEnv = Environment.GetEnvironmentVariable("STE_DEMO");
            var runEventsDemo = string.Equals(demoEnv, "events", StringComparison.OrdinalIgnoreCase)
                               || (args != null && args.Length >= 2 && args[0] == "--demo" && args[1] == "events");
            var runCollisionDemo = string.Equals(demoEnv, "collision", StringComparison.OrdinalIgnoreCase)
                                   || (args != null && args.Length >= 2 && args[0] == "--demo" && args[1] == "collision");
            var runDamageDemo = string.Equals(demoEnv, "damage", StringComparison.OrdinalIgnoreCase)
                                   || (args != null && args.Length >= 2 && args[0] == "--demo" && args[1] == "damage");
            var runAiDemo = string.Equals(demoEnv, "ai", StringComparison.OrdinalIgnoreCase)
                               || (args != null && args.Length >= 2 && args[0] == "--demo" && args[1] == "ai");
            var runSpatialDemo = string.Equals(demoEnv, "spatial", StringComparison.OrdinalIgnoreCase)
                               || (args != null && args.Length >= 2 && args[0] == "--demo" && args[1] == "spatial");

            if (runEventsDemo)
            {
                try
                {
                    EventSystemExample.RunDemo().GetAwaiter().GetResult();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Event demo failed: {ex.Message}");
                    return;
                }
            }

            if (runCollisionDemo)
            {
                try
                {
                    EventSystemExample.RunCollisionDemo();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Collision demo failed: {ex.Message}");
                    return;
                }
            }

            if (runDamageDemo)
            {
                try
                {
                    EventSystemExample.RunDamageDemo();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Damage demo failed: {ex.Message}");
                    return;
                }
            }

            if (runAiDemo)
            {
                try
                {
                    BehaviorTreeDemo.RunDemo();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AI demo failed: {ex.Message}");
                    return;
                }
            }

            if (runSpatialDemo)
            {
                try
                {
                    SpatialPartitioningDemo.RunDemo();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Spatial demo failed: {ex.Message}");
                    return;
                }
            }

            using (var game = new GameEngine())
                game.Run();
        }
    }
}
