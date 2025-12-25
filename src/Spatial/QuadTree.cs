using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Core;

#nullable enable

namespace SpaceTradeEngine.Spatial
{
    /// <summary>
    /// Represents an object in the spatial partition with position and bounds
    /// </summary>
    public class SpatialObject
    {
        public Entity Entity { get; set; }
        public Vector2 Position { get; set; }
        public Rectangle Bounds { get; set; }
        
        public SpatialObject() { }
        
        public void Set(Entity entity, Vector2 position, Rectangle bounds)
        {
            Entity = entity;
            Position = position;
            Bounds = bounds;
        }
    }

    /// <summary>
    /// QuadTree node for efficient spatial partitioning
    /// </summary>
    public class QuadTreeNode
    {
        private const int MAX_OBJECTS = 8;
        private const int MAX_LEVELS = 8;

        private int _level;
        private List<SpatialObject> _objects;
        private Rectangle _bounds;
        private QuadTreeNode?[] _nodes;

        public QuadTreeNode(int level, Rectangle bounds)
        {
            _level = level;
            _bounds = bounds;
            _objects = new List<SpatialObject>();
            _nodes = new QuadTreeNode[4];

            // Track approx memory per node (list + array + bounds)
            GlobalMemoryArena.Instance.Allocate($"QuadTreeNode_L{level}", 512);
        }

        /// <summary>
        /// Clear the quadtree
        /// </summary>
        public void Clear()
        {
            _objects.Clear();

            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (node != null)
                {
                    node.Clear();
                    _nodes[i] = null;
                }
            }
        }

        /// <summary>
        /// Split the node into 4 subnodes
        /// </summary>
        private void Split()
        {
            int subWidth = _bounds.Width / 2;
            int subHeight = _bounds.Height / 2;
            int x = _bounds.X;
            int y = _bounds.Y;

            _nodes[0] = new QuadTreeNode(_level + 1, new Rectangle(x + subWidth, y, subWidth, subHeight));
            _nodes[1] = new QuadTreeNode(_level + 1, new Rectangle(x, y, subWidth, subHeight));
            _nodes[2] = new QuadTreeNode(_level + 1, new Rectangle(x, y + subHeight, subWidth, subHeight));
            _nodes[3] = new QuadTreeNode(_level + 1, new Rectangle(x + subWidth, y + subHeight, subWidth, subHeight));
        }

        /// <summary>
        /// Determine which node the object belongs to
        /// </summary>
        private int GetIndex(Rectangle bounds)
        {
            int index = -1;
            double verticalMidpoint = _bounds.X + (_bounds.Width / 2.0);
            double horizontalMidpoint = _bounds.Y + (_bounds.Height / 2.0);

            // Object can completely fit within the top quadrants
            bool topQuadrant = (bounds.Y < horizontalMidpoint && bounds.Y + bounds.Height < horizontalMidpoint);
            // Object can completely fit within the bottom quadrants
            bool bottomQuadrant = (bounds.Y > horizontalMidpoint);

            // Object can completely fit within the left quadrants
            if (bounds.X < verticalMidpoint && bounds.X + bounds.Width < verticalMidpoint)
            {
                if (topQuadrant)
                    index = 1;
                else if (bottomQuadrant)
                    index = 2;
            }
            // Object can completely fit within the right quadrants
            else if (bounds.X > verticalMidpoint)
            {
                if (topQuadrant)
                    index = 0;
                else if (bottomQuadrant)
                    index = 3;
            }

            return index;
        }

        /// <summary>
        /// Insert an object into the quadtree
        /// </summary>
        public void Insert(SpatialObject spatialObj)
        {
            var firstNode = _nodes[0];
            if (firstNode != null)
            {
                int index = GetIndex(spatialObj.Bounds);

                if (index != -1)
                {
                    _nodes[index]!.Insert(spatialObj);
                    return;
                }
            }

            _objects.Add(spatialObj);

            if (_objects.Count > MAX_OBJECTS && _level < MAX_LEVELS)
            {
                if (_nodes[0] == null)
                    Split();

                int i = 0;
                while (i < _objects.Count)
                {
                    int index = GetIndex(_objects[i].Bounds);
                    if (index != -1)
                    {
                        _nodes[index]!.Insert(_objects[i]);
                        _objects.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve all objects that could collide with the given bounds
        /// </summary>
        public List<SpatialObject> Retrieve(List<SpatialObject> returnObjects, Rectangle bounds)
        {
            // Descend into all child nodes whose bounds intersect the search area.
            // This avoids missing candidates when the query spans multiple quadrants.
            var firstNode = _nodes[0];
            if (firstNode != null)
            {
                for (int i = 0; i < _nodes.Length; i++)
                {
                    var node = _nodes[i];
                    if (node != null && node._bounds.Intersects(bounds))
                    {
                        node.Retrieve(returnObjects, bounds);
                    }
                }
            }

            // Include objects stored at this node
            returnObjects.AddRange(_objects);

            return returnObjects;
        }

        /// <summary>
        /// Get all objects within a circular radius
        /// </summary>
        public List<SpatialObject> RetrieveInRadius(List<SpatialObject> returnObjects, Vector2 center, float radius)
        {
            Rectangle searchBounds = new Rectangle(
                (int)(center.X - radius),
                (int)(center.Y - radius),
                (int)(radius * 2),
                (int)(radius * 2)
            );

            Retrieve(returnObjects, searchBounds);

            // Filter by actual circular distance
            returnObjects.RemoveAll(obj =>
                Vector2.Distance(obj.Position, center) > radius
            );

            return returnObjects;
        }

        /// <summary>
        /// Get the total number of objects in this node and all children
        /// </summary>
        public int GetTotalObjects()
        {
            int count = _objects.Count;
            
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (node != null)
                    count += node.GetTotalObjects();
            }
            
            return count;
        }

        /// <summary>
        /// Get debug visualization data
        /// </summary>
        public void GetDebugBounds(List<Rectangle> bounds)
        {
            bounds.Add(_bounds);
            
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (node != null)
                    node.GetDebugBounds(bounds);
            }
        }
    }

    /// <summary>
    /// Spatial partitioning system using QuadTree for efficient spatial queries
    /// </summary>
    public class QuadTree
    {
        private QuadTreeNode _root;
        private Rectangle _worldBounds;
        private readonly ObjectPool<SpatialObject> _spatialObjectPool;

        public QuadTree(Rectangle worldBounds)
        {
            _worldBounds = worldBounds;
            _root = new QuadTreeNode(0, worldBounds);
            
            // Pool pre-calentado con 256 objetos para evitar allocaciones
            _spatialObjectPool = new ObjectPool<SpatialObject>(
                obj => { obj.Entity = null; obj.Position = Vector2.Zero; obj.Bounds = Rectangle.Empty; },
                prewarmCount: 256
            );
        }

        /// <summary>
        /// Clear and rebuild the tree
        /// </summary>
        public void Clear()
        {
            _root.Clear();
        }

        /// <summary>
        /// Insert an entity into the spatial partition
        /// </summary>
        public void Insert(Entity entity)
        {
            var transform = entity.GetComponent<TransformComponent>();
            var collision = entity.GetComponent<CollisionComponent>();

            if (transform == null)
                return;

            Rectangle bounds;
            if (collision != null)
            {
                bounds = collision.GetBounds();
            }
            else
            {
                // Default bounds if no collision component
                bounds = new Rectangle((int)transform.Position.X - 5, (int)transform.Position.Y - 5, 10, 10);
            }

            var spatialObj = _spatialObjectPool.Get();
            spatialObj.Set(entity, transform.Position, bounds);
            _root.Insert(spatialObj);
        }

        /// <summary>
        /// Retrieve all entities that could collide with the given bounds
        /// </summary>
        public List<Entity> Query(Rectangle bounds)
        {
            var spatialObjects = new List<SpatialObject>();
            _root.Retrieve(spatialObjects, bounds);

            var entities = new List<Entity>();
            foreach (var obj in spatialObjects)
            {
                entities.Add(obj.Entity);
            }
            return entities;
        }

        /// <summary>
        /// Find all entities within a circular radius of a point
        /// </summary>
        public List<Entity> QueryRadius(Vector2 center, float radius)
        {
            var spatialObjects = new List<SpatialObject>();
            _root.RetrieveInRadius(spatialObjects, center, radius);

            var entities = new List<Entity>();
            foreach (var obj in spatialObjects)
            {
                entities.Add(obj.Entity);
            }
            return entities;
        }

        /// <summary>
        /// Find the nearest entity to a point within a maximum radius
        /// </summary>
        public Entity? FindNearest(Vector2 point, float maxRadius = float.MaxValue)
        {
            var candidates = QueryRadius(point, maxRadius);
            
            Entity? nearest = null;
            float nearestDistance = maxRadius;

            foreach (var entity in candidates)
            {
                var transform = entity.GetComponent<TransformComponent>();
                if (transform == null)
                    continue;

                float distance = Vector2.Distance(point, transform.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = entity;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find all entities along a ray (for weapon targeting, line of sight)
        /// </summary>
        public List<Entity> Raycast(Vector2 origin, Vector2 direction, float maxDistance)
        {
            var results = new List<Entity>();
            direction.Normalize();

            // Sample points along the ray
            int samples = (int)(maxDistance / 10f); // Sample every 10 units
            for (int i = 0; i < samples; i++)
            {
                Vector2 point = origin + direction * (i * 10f);
                var entities = QueryRadius(point, 10f);
                
                foreach (var entity in entities)
                {
                    if (!results.Contains(entity))
                        results.Add(entity);
                }
            }

            return results;
        }

        /// <summary>
        /// Get total object count (for debugging)
        /// </summary>
        public int GetTotalObjects()
        {
            return _root.GetTotalObjects();
        }

        /// <summary>
        /// Get debug visualization bounds
        /// </summary>
        public List<Rectangle> GetDebugBounds()
        {
            var bounds = new List<Rectangle>();
            _root.GetDebugBounds(bounds);
            return bounds;
        }

        /// <summary>
        /// Update world bounds (call if your game world changes size)
        /// </summary>
        public void UpdateWorldBounds(Rectangle newBounds)
        {
            _worldBounds = newBounds;
            _root = new QuadTreeNode(0, newBounds);
        }
    }
}
